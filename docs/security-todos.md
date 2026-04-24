# Security TODOs

Findings from the 2026-04-23 audit of `src/FileCrypter.Core/`, ranked by severity. Items are actionable and map to specific files/lines.

## H1 — Decryption trusts attacker-controlled Argon2 cost parameters (DoS)

`Format/FileCrypterHeaderParser.cs:93-101` only rejects 0 for `argon2MemoryKiB` / `Iterations` / `Parallelism`. `FileCrypter.cs:1650-1657` (`ValidateSupportedPayload`) only rejects values > `int.MaxValue`. A maliciously crafted header can declare e.g. `MemorySize = 2,147,483,647` KiB (~2 TiB) or extremely high iterations/parallelism, and `FileCrypterKeyDeriver.DeriveKey` runs Argon2 with those values before AEAD ever gets a chance to reject the file. Allocation will eventually throw, but realistic values like 4 GiB memory or millions of iterations will exhaust RAM / burn CPU first.

**Fix:** clamp on decrypt — e.g. `MemoryKiB ≤ 1_048_576` (1 GiB), `Iterations ≤ 64`, `Parallelism ≤ 16`. Reject larger values with `InvalidArgon2Parameters` before calling `Argon2id`.

## M1 — `ValidateEncryptionOptions` allows trivially weak Argon2 parameters

`FileCrypter.cs:1436-1439` only checks `> 0`. Callers can encrypt with `Argon2MemoryKiB=1, Iterations=1, Parallelism=1` and produce files with essentially no password-stretching. No floor is enforced in the core library.

**Fix:** enforce minimums — e.g. `MemoryKiB ≥ 19_456` (OWASP 2023 Argon2id floor), `Iterations ≥ 2`, `Parallelism ≥ 1`. Mirror these bounds in H1's decrypt-side clamp.

## M2 — TOCTOU on symlink / path checks

`ValidateInputPath`, `ValidateKeyFilePath`, `ValidateOutputPath` call `IsSymbolicLink` (`FileCrypter.cs:1415-1425`) and `Directory.Exists` before the subsequent `FileStream` open. Between check and open, an attacker with write access to any parent directory can swap the target. Impact is bounded (input is read-only; output staging uses randomized `.<name>.<guid>.tmp` + `CreateNew`) but the pattern is worth hardening.

**Fix:** open with a pre-stat'd `FileInfo`, or use `File.OpenHandle` + `FileStream(SafeFileHandle,…)` and re-verify link metadata via the open handle. At minimum, document that input/key-file paths must live in a directory the user controls.

## M3 — Windows output files have no ACL tightening

`CreateOutputFileStreamOptions` (`FileCrypter.cs:1347-1364`) sets `UnixCreateMode = 0600` on Unix but does nothing on Windows, so staged ciphertext / key-file inherit the parent directory's ACL (often readable by "Authenticated Users" in a default profile).

**Fix:** on Windows, set an explicit `FileSecurity` granting only the current user, at minimum for the `GenerateKeyFileAsync` output path.

## L1 — Password bytes: only the UTF-8 copy is zeroed

`FileCrypterKeyDeriver.cs:24,42` zeroes `passwordBytes` but the `string password` remains in the managed heap. Known .NET limitation.

**Fix (optional):** expose `byte[]` / `ReadOnlySpan<byte>` password overloads so callers can provide pre-zero-able buffers and skip the string allocation.

## L2 — Settings store writes without symlink protection

`Settings/FileCrypterSettingsStore.cs:83` does `File.Move(stagingPath, settingsPath, overwrite: true)` unconditionally. If an attacker replaces `settings.json` with a symlink, a subsequent save overwrites the link target. Low impact (settings are non-secret) but inconsistent with the ciphertext path, which blocks symlinked output when `overwrite=true`.

**Fix:** reject the move when the destination is a symlink, matching the ciphertext path behavior.

## L3 — Non-final chunks with plaintext length 0 are accepted on decrypt

`FileCrypter.cs:2278-2283` only rejects `length > ChunkSize` or `(isFinal && length == ChunkSize)`. An authenticated but malicious file can contain many zero-length non-final chunks, each adding 8+16 bytes of overhead. Not exploitable without the key (AEAD blocks unauthenticated input), so informational.

**Fix (optional):** require non-final chunks to equal `ChunkSize`, since the encryptor only emits full chunks except the final one.

## L4 — Nonce-prefix width (informational, no action)

The 8-byte random prefix gives a birthday bound of ~2³² files before prefix collision. Because each file derives an independent key from a fresh 16-byte salt, a prefix collision does not cause AES-GCM key+nonce reuse. Safe as designed.

## Recommended priority

1. **H1** — only finding an unauthenticated attacker can trigger with just a crafted `.encrypted` file.
2. **M1** — fix alongside H1 so encrypt/decrypt share the same min/max.
3. **M2 / M3** — address if FileCrypter is used in multi-user environments.

## What's working (keep as-is)

- AAD = full header ‖ chunk prefix binds every format field (algorithm IDs, flags, chunk size, Argon2 params, salt, nonce prefix, payload kind) to the ciphertext — prevents downgrade / parameter-swap.
- Final-chunk flag + `EnsureNoTrailingData` closes truncation and append attacks.
- Credential preimage uses a fixed domain string and length-prefixed fields; password + key-file can't be confused across variants.
- Tar extraction path validation (`CreateArchiveExtractionOutputPath`) rejects separators, `.`/`..`, absolute paths, and re-verifies the resolved absolute path stays under the output directory with a trailing separator.
- Staged writes + `CreateNew` + `FileShare.None` + randomized temp names make pre-existing-symlink attacks on the output path very hard.
- `CryptographicOperations.ZeroMemory` is consistently applied to derived keys, key-file bytes, and preimage buffers.

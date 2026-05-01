# Security Audit Follow-ups

Findings from the 2026-04-23 audit of `src/FileCrypter.Core/`, ranked by the original severity labels. Current implementation notes live in `docs/security.md`; format-level constraints live in `docs/file-format.md`.

## Open Residual Risks

### M2 — TOCTOU on symlink / path checks

`ValidateInputPath`, `ValidateKeyFilePath`, and `ValidateOutputPath` reject BCL-detectable symbolic links and reparse points before later `FileStream` opens, and staged output files still use randomized `CreateNew` temp names plus final moves. A bounded race remains if an attacker can mutate a parent directory between validation and use.

**Current stance:** accepted residual risk. Keep input, key-file, output, and settings paths inside directories the user controls. Revisit only if the project adopts platform-specific open-time no-follow or open-by-handle primitives.

### L1 — Password bytes: only the UTF-8 copy is zeroed

`FileCrypterKeyDeriver` zeroes UTF-8 password byte copies, credential preimages, derived keys, and key-file buffers. The original `string password` can remain in the managed heap, which is a known .NET limitation.

**Current stance:** accepted residual risk. Consider byte-buffer or span-based password overloads if callers need to provide pre-zeroable password material.

## Resolved Archive

### 2026-04 — H1, M1: Argon2 bounds

Decryption now rejects Argon2 parameters outside the v1 valid ranges before key derivation, and encryption enforces matching minimums and maximums. Current ranges are `MemoryKiB = 19456..1048576`, `Iterations = 2..64`, and `Parallelism = 1..16`.

### 2026-04 — M3: Windows output file ACLs

Staged output files now use private permissions on supported platforms: Unix creates files with `0600`, and Windows creates files with an explicit ACL for the current user.

### 2026-04 — L2: Settings store symlink protection

Settings saves now align with ciphertext output behavior by rejecting symlinked or reparse-point destinations before replacing `settings.json`.

### 2026-04 — L3: Non-final chunk length validation

Decrypt now requires non-final chunks to have plaintext length exactly equal to the header chunk size. Final chunks must remain shorter than the chunk size, with a zero-length final chunk allowed for empty files and exact-boundary files.

### L4 — Nonce-prefix width

The 8-byte random nonce prefix remains informational only. Because each file derives an independent key from a fresh 16-byte salt, a prefix collision does not cause AES-GCM key+nonce reuse.

## What's Working

- AAD = full header and chunk prefix, binding every format field to ciphertext.
- Final-chunk validation plus `EnsureNoTrailingData` closes truncation and append attacks.
- Credential preimages use a fixed domain string and length-prefixed fields, so password and key-file credentials cannot be confused across variants.
- Tar extraction path validation rejects separators, `.`/`..`, absolute paths, and paths resolving outside the output directory.
- Staged writes, `CreateNew`, `FileShare.None`, and randomized temp names reduce pre-existing symlink attacks on output paths.
- Sensitive buffers are zeroed on ordinary and failure paths documented in `docs/security.md`.

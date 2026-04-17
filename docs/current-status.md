# Current Status

This document is a handoff note for continuing FileCrypter development in a fresh session.

Last updated: 2026-04-17.

## Branch

Current working branch:

```text
codex/phase-1-file-format
```

## Completed

- Phase 0 repository scaffold is complete and committed on `main`.
- Phase 1 file-format draft and internal header parser work is implemented on the current branch.
- Phase 2 password-only encryption/decryption is started on the current branch.
- The v1 format draft lives in `docs/file-format.md`.
- Internal format parser scaffolding lives under `src/FileCrypter.Core/Format`.
- Parser validation tests live under `tests/FileCrypter.Core.Tests/Format`.
- Password-only core encryption/decryption lives in `src/FileCrypter.Core/FileCrypter.cs`.
- Password-only key derivation lives in `src/FileCrypter.Core/Cryptography`.
- Public file-path convenience APIs with same-directory staged output writes and safe auto-renamed outputs are implemented in `src/FileCrypter.Core/FileCrypter.cs`.
- Minimal CLI encrypt/decrypt commands are wired through the staged file-path APIs in `src/FileCrypter.Cli/Program.cs`.
- The core test suite includes a fixed expected-byte deterministic password-only vector using injected randomness.
- Staged file-path API tests cover cleanup when encrypt/decrypt operations are canceled before writing the final output.
- CLI command tests cover password-only encrypt/decrypt success, missing password, missing input file, default output naming, overwrite protection, explicit overwrite, wrong-password failure, and interactive password prompting.
- The CLI prompts for a password without echoing input when neither `--password` nor `--password-stdin` is supplied and stdin is interactive.

## Current Format Decisions

- Default encrypted suffix: `.encrypted`.
- Future archive suffix: `.tar.zst.encrypted`.
- Header: fixed 64-byte little-endian binary layout.
- Magic bytes: ASCII `FCRYPT\r\n`.
- Version: `1`.
- AEAD: AES-256-GCM, 12-byte nonce, 16-byte tag.
- KDF: Argon2id, version `0x13`, 32-byte derived key.
- Default Argon2id parameters: 65536 KiB memory, 3 iterations, 4 lanes.
- Default chunk size: 1048576 bytes.
- Existing key-file size cap: 16 MiB.
- Header parser/model types are internal and exposed to tests with `InternalsVisibleTo`.

## Verified

These commands passed after the latest Phase 2 CLI hardening pass:

```bash
dotnet test --no-restore
```

Latest test count at handoff: 52 passed, 0 failed.

CLI smoke test also passed:

```bash
dotnet run --no-build --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- encrypt /tmp/filecrypter-smoke.txt /tmp/filecrypter-smoke.txt.encrypted --password smoke --overwrite
dotnet run --no-build --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- decrypt /tmp/filecrypter-smoke.txt.encrypted /tmp/filecrypter-smoke.out --password smoke --overwrite
cmp /tmp/filecrypter-smoke.txt /tmp/filecrypter-smoke.out
```

## Not Started

- Key-file encryption/decryption.
- Compression.
- Archive mode.
- Batch workflows.
- Settings, help, update flow, and graphical UI.
- Rich CLI UX, progress, troubleshooting messages, and exit-code coverage.
- Broader compatibility test-vector coverage.

## Product Alignment

The current implementation is aligned with the product outline for the foundation phase:

- local-only operation with no cloud, account, or remote processing dependency
- reusable core library consumed by a CLI host
- password-only single-file encrypt/decrypt path
- versioned documented encrypted file format
- AES-256-GCM, Argon2id, unique per-file salt, unique per-file nonce prefix, and chunk-level authentication
- bounded-memory streaming for large-file readiness
- non-destructive input handling
- staged writes that do not leave partial output at the final destination
- overwrite protection that auto-renames by default and allows explicit replacement

Remaining product-level gaps are expected for later phases:

- key-file protection
- compression and automatic decompression
- batch individual-file workflows
- archive creation/extraction
- richer CLI progress, troubleshooting messages, exit-code coverage, and command polish
- settings persistence, help/troubleshooting content, update flow, and UI
- symlink/permission hardening and broader compatibility test vectors

## Recommended Next Step

Continue Phase 2 hardening for password-only streaming encryption and decryption.

Immediate next slice:

- Add more filesystem edge-case tests for staged path APIs, including missing output directories, output paths that point to directories, input/output path collisions, and cleanup after failures.
- Harden symlink and permission behavior for filesystem safety.
- Add broader compatibility vectors after the CLI and filesystem hardening pass.

Recommended follow-up work:

- Add richer CLI UX, including progress reporting, clearer troubleshooting messages, and command exit-code coverage.
- Start key-file encryption/decryption once password-only filesystem behavior is hardened.

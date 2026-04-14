# Current Status

This document is a handoff note for continuing FileCrypter development in a fresh session.

## Branch

Current working branch:

```text
codex/phase-1-file-format
```

## Completed

- Phase 0 repository scaffold is complete and committed on `main`.
- Phase 1 file-format draft and internal header parser work is implemented on the current branch.
- The v1 format draft lives in `docs/file-format.md`.
- Internal format parser scaffolding lives under `src/FileCrypter.Core/Format`.
- Parser validation tests live under `tests/FileCrypter.Core.Tests/Format`.

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

These commands passed after Phase 1 implementation:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Latest test count at handoff: 23 passed, 0 failed.

## Not Started

- Encryption and decryption implementation.
- Header writer.
- Password/KDF package selection and integration.
- CLI commands.
- Compression.
- Archive mode.
- Output staging and overwrite protection.

## Recommended Next Step

Plan Phase 2: password-only streaming encryption and decryption.

Start by deciding the Argon2id NuGet package and the first public core API shape, then add:

- header writer
- password-only key derivation
- streaming AES-GCM chunk encryption/decryption
- progress and cancellation models
- minimal CLI encrypt/decrypt commands
- round-trip, empty-file, small-file, and larger streamed-file tests

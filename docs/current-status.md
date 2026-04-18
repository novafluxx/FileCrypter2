# Current Status

This document is a handoff note for continuing FileCrypter development in a fresh session.

Last updated: 2026-04-18.

## Branch

Current working branch:

```text
codex/avalonia-gui-shell
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
- Phase 2 filesystem hardening now rejects missing output directories, directory output paths, input/output path collisions, symlink inputs, and overwrite attempts against symlink outputs before writing.
- Input/output collision checks resolve symlinked directories, so common platform paths such as `/tmp` keep working while still preventing accidental self-overwrites through linked directories.
- Staged path API coverage now verifies cleanup after final-move failures and user-only Unix permissions for encrypted outputs where supported.
- Broader password-only compatibility vectors now cover empty payloads, small text, exact chunk-boundary input, and multi-chunk input with fixed encrypted digests, headers, chunk prefixes, and tags.
- CLI failure coverage now includes filesystem validation failures and malformed encrypted-file failures.
- CLI troubleshooting messages now add short hints for wrong passwords, malformed files, truncated files, unsupported payloads, and path-access problems.
- CLI file encrypt/decrypt commands now report progress to stderr while keeping stdout reserved for the final output path.
- CLI progress reporting preserves an injected host progress callback when one is supplied through `FileCrypterOptions`.
- Existing key-file encryption/decryption is implemented for stream and path APIs.
- Key-file credential binding hashes supplied key-file bytes with SHA-256 and includes that digest in the Argon2id credential preimage.
- Key-file outputs set the v1 key-file-required header flag and SHA-256 key-file hash algorithm marker.
- Path APIs enforce the 16 MiB existing key-file cap and avoid overwriting the selected key file as an output.
- CLI encrypt/decrypt commands accept `--key-file` and report a clear missing-key-file hint for protected payloads.
- Tests cover key-file round trips, wrong key files, missing key files, oversized key files, and password-only compatibility when an extra key file is supplied during decrypt.
- Phase 2 CLI command-polish boundary is settled: keep a single nonzero failure exit code for now and defer stable exit-code categories until the CLI contract hardens.
- CLI help now documents key-file recovery limitations and the 16 MiB existing-key-file cap.
- Core single-file compression is implemented with Zstandard before encryption and automatic decompression after authenticated decrypt.
- `FileCrypterOptions.EnableCompression` writes the v1 Zstd compression marker while preserving uncompressed output compatibility by default.
- Compression coverage includes password-only round trips, password plus key-file round trips, wrong-password failure, and authenticated malformed compressed payload failure.
- CLI encryption now accepts `--compress`, passes `EnableCompression = true` into the core options, and keeps decryption automatic through the header compression marker.
- CLI compression coverage includes compressed password-only round trips, progress reporting, wrong-password failure, password plus key-file round trips, and help text.
- Core key-file generation is implemented with a 32-byte default, cryptographic randomness, staged writes, private Unix permissions where supported, and safe auto-renamed outputs by default.
- CLI encryption now accepts `--generate-key-file <path>` to create a new key file before encryption, then uses it for password plus key-file protection while keeping stdout reserved for the encrypted output path.
- Generated key-file CLI behavior preserves existing key files by auto-renaming, rejects generated key-file paths that match the input or encrypted output, and keeps recovery warnings visible.
- Tests cover generated key-file bytes, generated-key-file round trips, overwrite protection, path validation, CLI mutual-exclusion behavior, and generated-key-file help/recovery messaging.
- Core batch individual-file orchestration is implemented with per-file results, a 1000-file limit, safe output naming through the existing staged path APIs, and automatic compression for batch encryption.
- CLI batch commands are wired as `batch-encrypt <output-directory> <input>...` and `batch-decrypt <output-directory> <input>...`, with successful output paths on stdout and per-file failures plus summary output on stderr.
- Batch coverage includes round trips, automatic compression, key-file use, duplicate output auto-renaming, mixed success/failure continuation, and batch help text.
- CLI-host settings persistence is implemented with a local JSON settings file and a `settings show` / `settings set compression-default <on|off>` command.
- Single-file encryption applies the persisted compression default when `--compress` is omitted, while explicit `--compress` still enables compression for that run.
- Settings coverage includes missing settings defaults, save/load behavior, compression-default application, corrupt settings troubleshooting, and decrypt ignoring corrupt settings.
- Core archive encryption/extraction is implemented for file-list archives using tar payloads, Zstandard compression, the v1 archive payload-kind marker, staged temporary files, key-file support, duplicate input-name auto-renaming, and safe extraction path validation.
- CLI archive commands are wired as `archive-encrypt <output-archive-or-directory> <input>...` and `archive-decrypt <input-archive> <output-directory>`, with encrypted or extracted output paths written to stdout.
- Archive coverage includes password-only and key-file round trips, archive header markers, duplicate entry renaming, extraction overwrite protection, malformed archive handling, CLI archive round trips, and archive help text.
- CLI archive encryption can now accept an output directory, generate a timestamped `.tar.zst.encrypted` archive name, and accept a validated `--archive-name` basename for directory outputs.
- Archive progress reporting now covers tar creation, archive encryption, archive decryption, and extraction phases through core progress callbacks and CLI stderr output.
- CLI archive troubleshooting now points users to `archive-decrypt` when they try to use single-file `decrypt` on an archive payload, and points users back to `decrypt` when they try `archive-decrypt` on a single-file payload.
- CLI batch item troubleshooting now includes format hints, including a specific `archive-decrypt` hint when `batch-decrypt` receives an archive payload.
- `README.md` now reflects the current CLI surface, including single-file, compression, key-file generation, batch, archive, settings, smoke-test commands, and safety notes.
- Avalonia 12.0.1 GUI work is started in `src/FileCrypter.App` with a desktop MVVM app shell, persistent sidebar navigation, placeholder Decrypt/Batch/Help/Settings pages, and an implemented single-file Encrypt view model wired to `FileCrypter.Core`.
- GUI view model coverage lives in `tests/FileCrypter.App.Tests` and covers sidebar navigation, encrypt command validation, compression propagation, running-state behavior, success, and failure messaging.
- The Encrypt page now keeps existing key-file selection and generated key-file output mutually exclusive, adds clearer user-facing guidance plus explicit clear actions, and labels generated key-file results in the success state.
- App test coverage now includes the Encrypt page key-file choice behavior plus picker-service option forwarding for open/save dialogs; full native file-picker interaction remains a manual smoke-test concern.

## Current Format Decisions

- Default encrypted suffix: `.encrypted`.
- Archive suffix: `.tar.zst.encrypted`.
- Header: fixed 64-byte little-endian binary layout.
- Magic bytes: ASCII `FCRYPT\r\n`.
- Version: `1`.
- AEAD: AES-256-GCM, 12-byte nonce, 16-byte tag.
- KDF: Argon2id, version `0x13`, 32-byte derived key.
- Compression: none by default, optional Zstandard for single-file payloads, required Zstandard for archive encryption.
- Key-file binding: SHA-256 digest of raw key-file bytes in the credential preimage when key-file protection is enabled.
- Default Argon2id parameters: 65536 KiB memory, 3 iterations, 4 lanes.
- Default chunk size: 1048576 bytes.
- Existing key-file size cap: 16 MiB.
- Header parser/model types are internal and exposed to tests with `InternalsVisibleTo`.

## Verified

These commands passed after the latest Encrypt-page UX pass:

```bash
dotnet build
dotnet test
dotnet test tests/FileCrypter.App.Tests/FileCrypter.App.Tests.csproj --no-build
```

Latest test count at handoff: 139 passed, 0 failed.

GUI launch smoke coverage at handoff:

```bash
dotnet run --project src/FileCrypter.App/FileCrypter.App.csproj
```

The desktop app launch command stayed running until manually interrupted, which is a basic launch smoke signal. Native file-picker dialogs were not visually exercised end-to-end in automation and should still get a quick manual click-through on the target desktop.

Focused CLI coverage also passed:

```bash
dotnet test tests/FileCrypter.Cli.Tests/FileCrypter.Cli.Tests.csproj
```

CLI smoke test also passed:

```bash
dotnet run --no-build --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- encrypt /tmp/filecrypter-smoke.txt /tmp/filecrypter-smoke.txt.encrypted --password smoke --overwrite
dotnet run --no-build --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- decrypt /tmp/filecrypter-smoke.txt.encrypted /tmp/filecrypter-smoke.out --password smoke --overwrite
cmp /tmp/filecrypter-smoke.txt /tmp/filecrypter-smoke.out
```

Key-file CLI smoke test also passed:

```bash
dotnet run --no-build --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- encrypt /tmp/filecrypter-key-smoke.txt /tmp/filecrypter-key-smoke.txt.encrypted --password smoke --key-file /tmp/filecrypter-key-smoke.key --overwrite
dotnet run --no-build --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- decrypt /tmp/filecrypter-key-smoke.txt.encrypted /tmp/filecrypter-key-smoke.out --password smoke --key-file /tmp/filecrypter-key-smoke.key --overwrite
cmp /tmp/filecrypter-key-smoke.txt /tmp/filecrypter-key-smoke.out
```

## Not Started

- Broader settings, help, update flow, and graphical UI.
- Broader CLI command polish.

## Product Alignment

The current implementation is aligned with the product outline for the foundation phase:

- local-only operation with no cloud, account, or remote processing dependency
- reusable core library consumed by a CLI host
- password-only single-file encrypt/decrypt path
- password plus existing-key-file single-file encrypt/decrypt path
- generated key-file single-file encrypt path
- batch individual-file encrypt/decrypt path
- compressed tar archive encrypt/decrypt path for file-list archives
- persisted compression default for single-file encryption
- versioned documented encrypted file format
- AES-256-GCM, Argon2id, unique per-file salt, unique per-file nonce prefix, and chunk-level authentication
- bounded-memory streaming for large-file readiness
- non-destructive input handling
- staged writes that do not leave partial output at the final destination
- overwrite protection that auto-renames by default and allows explicit replacement

Remaining product-level gaps are expected for later phases:

- broader key-file UX/help
- broader batch UX and aggregate progress
- broader settings and help UX
- broader CLI command polish
- update flow and broader graphical UI

## Recommended Next Step

Continue the Avalonia GUI now that the shell and first encrypt workflow are scaffolded.

Recommended next slices:

- Add the single-file Decrypt page using the same app-service pattern.

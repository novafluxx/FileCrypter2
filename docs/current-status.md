# Current Status

This document is a handoff note for continuing FileCrypter development in a fresh session.

Last updated: 2026-04-19.

## Branch

Current working branch:

```text
main
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
- Avalonia GUI single-file decryption is now implemented in `src/FileCrypter.App` with source/output pickers, safe default output naming, optional existing key-file input, progress reporting, and result/error messaging wired through `FileCrypter.Core`.
- The desktop shell footer/status area now reflects the active workflow page instead of always showing Encrypt-page state.
- App test coverage now includes the Decrypt page command validation, running-state behavior, success, authentication failure guidance, key-file picker flow, output-name suggestions, and decrypt navigation shell wiring.
- Shared local settings persistence now lives in `src/FileCrypter.Core/Settings`, so the CLI host and Avalonia app use the same `settings.json` path, JSON shape, and staged-write save behavior.
- Avalonia GUI Settings is now a real page in `src/FileCrypter.App` with local load/save/reload actions for the persisted compression default plus settings-file visibility and success/error feedback.
- The Encrypt page now starts from the persisted compression default and updates immediately after Settings saves or reloads, keeping GUI single-file encryption aligned with the CLI host behavior.
- App test coverage now includes Settings page save/reload/error handling plus MainWindow integration that propagates saved compression defaults back into Encrypt.
- The Settings page save/reload workflow now stays on the captured UI synchronization context after async settings I/O, preventing Avalonia cross-thread crashes when command state changes after a save.
- Avalonia GUI Batch is now a real page in `src/FileCrypter.App` with batch-encrypt and batch-decrypt mode switching, multi-file selection, output-directory selection, shared password and optional key-file input, progress feedback, and per-file success/failure results backed by the existing core batch APIs.
- The Batch page now also exposes archive-encrypt and archive-decrypt workflows in `src/FileCrypter.App`, including archive-vs-individual mode switching, single-archive extraction, optional archive naming, archive-phase progress feedback, and archive result summaries through the same app-service/MVVM pattern as the other pages.
- The app picker abstraction now supports multi-file and folder selection so batch workflows can use native Avalonia pickers without breaking the existing single-file pages.
- App test coverage now includes Batch page command validation, picker flows, deduplicated file selection, archive-mode behavior, running-state behavior, mixed batch results, and shell navigation wiring.
- Batch workflow polish now pre-validates missing or nonexistent output folders, missing passwords, archive-extract single-selection requirements, individual-batch file-count limits, and unsafe custom archive names before the user starts a run.
- Batch picker-driven flows now auto-fill the output directory from the first selected source file when the user has not chosen one yet, which reduces friction across both individual-file and archive workflows.
- Batch results now clear when the selected source-file list changes after a completed run, which avoids stale summaries/results lingering while the user prepares the next batch.
- App test coverage now also includes Batch preflight validation copy, auto-filled output-directory behavior from picker selections, stale-result clearing after source changes, and invalid archive-name blocking.
- Avalonia GUI Help is now a real page in `src/FileCrypter.App` with workflow guidance, recovery warnings, troubleshooting copy, local-only safety notes, visible app/format version details, and a practical update-status panel instead of the old placeholder card.
- The desktop shell version label now comes from an app metadata service rather than a hardcoded string, which keeps the sidebar and Help page aligned on the same displayed version.
- The first Help/update-flow slice uses the existing app-service and MVVM pattern with a small update-status abstraction that currently reports the local development-build state while leaving fuller automatic install/relaunch work for later.
- App test coverage now includes Help page metadata/update-state behavior, update-check failure handling, and main-window Help navigation/footer wiring.
- A repo-native macOS packaging script now creates a true `artifacts/macos/FileCrypter.app` bundle for local use by publishing `src/FileCrypter.App` as a framework-dependent Release macOS apphost and wrapping it in the standard `Contents/MacOS`, `Contents/Resources`, and `Info.plist` structure.
- The macOS packaging script now also applies an ad-hoc code signature to the completed `.app` bundle so Launch Services and `codesign --verify` see a coherent signed bundle after the manual wrapper step.
- The macOS bundle metadata now lives in `src/FileCrypter.App/FileCrypter.App.csproj`, which keeps the bundle name, identifier, version, executable name, and minimum macOS version deterministic for the packaging script.
- App test coverage now also includes a focused macOS packaging test that runs the repo bundle script on macOS and verifies the resulting `.app` contents, `Info.plist`, executable permissions, absence of CLI payloads, and successful `codesign --verify`.
- A real macOS `.app` smoke pass is now complete for the Batch page using the bundled desktop app, native file/folder pickers, and Computer Use against `artifacts/macos/FileCrypter.app`.
- That desktop Batch smoke covered individual-file encrypt picker flow, output-folder autofill from selected sources, folder override via the native picker, archive-encrypt multi-file selection, archive-decrypt single-selection validation, single-archive native selection, extraction-folder picking, and a full archive encrypt/decrypt round trip with extracted files landing in the chosen folder.
- Shared local settings now also cover default overwrite protection, default output directory, and reset-to-defaults behavior through the same `src/FileCrypter.Core/Settings` persistence used by the CLI host.
- The Avalonia Settings page now exposes those shared defaults with local validation plus native folder picking for the default output directory.
- Encrypt, Decrypt, and Batch now honor the shared overwrite/output defaults on startup, while Batch still falls back to first-selected-source autofill when no saved default output directory is active.
- App test coverage now includes shared-settings save/reload/reset behavior plus focused startup-default coverage for Encrypt, Decrypt, Batch, and main-window settings propagation.
- A focused bundled macOS app smoke is now also complete for the shared-settings slice: saved overwrite/output defaults were verified across Encrypt, Decrypt, and Batch, reset-to-defaults restored the expected startup state, and Batch still fell back to first-source autofill when no default output directory was saved.
- A packaging follow-up fixed a macOS bundle-signing issue that could surface as early startup aborts or Launch Services relaunch trouble. The bundle script now re-signs the completed `.app`, and the packaging test now verifies `codesign --verify` on macOS.
- Desktop file-drop support is now implemented in `src/FileCrypter.App` for the Encrypt and Decrypt source-file cards plus the Batch source list, using local-file drag/drop handling that keeps the existing shell structure and visual language intact.
- Encrypt and Decrypt now accept dropped source files through the same source-path/output-suggestion flow as picker selections, while Batch drop handling preserves the existing dedupe behavior, archive-extract replacement behavior, and first-source output-directory autofill when no saved default output directory is active.
- App test coverage now also includes focused dropped-file behavior for Encrypt, Decrypt, and Batch, including Batch dedupe/autofill preservation and archive-extract replacement behavior.
- A focused bundled macOS app smoke is now also complete for the desktop file-drop slice: Encrypt drop updated the source and suggested output, Decrypt drop updated the source and suggested output, Batch multi-file drop preserved dedupe plus first-source output-folder autofill when no saved default output directory was set, and Batch archive-decrypt drop replaced the selection with exactly one archive source.
- Theme preference support is now implemented across the shared settings model and Avalonia app, with persisted `Light`, `Dark`, and `System` choices stored locally beside the other shared defaults.
- The Avalonia Settings page now exposes the theme preference alongside the existing shared defaults, and those settings now autosave live while `Reload` and `Reset to defaults` remain as explicit recovery actions.
- The running desktop app now applies the selected theme through Avalonia theme variants, and the shell's shared brushes now have light/dark variants so the existing visual language carries across theme switches without a redesign.
- App test coverage now also includes focused settings-store and theme-application coverage for the theme slice, including autosave, debounced directory persistence, reload/reset, failure reversion, and main-window/app-theme propagation.
- A focused bundled macOS app smoke is now also complete for the settings-autosave slice: compression-default changes took effect immediately without a save step, typed default-output-directory changes autosaved after the debounce window and updated Encrypt output suggestions, and `Reset to defaults` restored the expected startup behavior afterward.
- Avalonia workflow feedback cards now expose copy actions for visible issue text plus single-file and batch result details, using a small clipboard service that stays within the existing app-service/MVVM pattern.
- App test coverage now also includes focused clipboard-copy behavior for Encrypt success details, Decrypt troubleshooting copy, and Batch result-summary/detail copy.
- The global shell-header Changelog label is now removed from page chrome, and Settings now includes an About/Version card with a toggleable local changelog entry tied to the app version metadata.
- App test coverage now also includes focused Settings/MainWindow metadata wiring for the relocated changelog surface.
- The Batch page workflow chooser now presents workflow type and action as one combined decision card with a visual divider, richer per-option captions, and combination-specific helper copy for all four supported batch/archive modes.
- App test coverage now also includes focused Batch workflow-description coverage so archive extraction and the other mode combinations keep their intended copy.

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

These commands passed after the latest desktop theme-support pass:

```bash
dotnet test tests/FileCrypter.App.Tests/FileCrypter.App.Tests.csproj
dotnet test
```

These commands also passed after the macOS bundle workflow was added:

```bash
./scripts/package-macos-app.sh
```

These bundled-app launch and manual desktop smoke steps also passed on macOS:

```bash
./scripts/package-macos-app.sh
open artifacts/macos/FileCrypter.app
```

Important testing note: Computer Use needs the app to be rebuilt and launched as the real macOS bundle. For desktop automation or picker smoke tests, rerun `./scripts/package-macos-app.sh` after app changes and then launch with `open artifacts/macos/FileCrypter.app`. Do not rely on `dotnet run` for Computer Use attachment, because the tool sees the bundled `.app` reliably but does not reliably attach to the raw `dotnet`-hosted process.

The packaged app was launched as a real macOS bundle and exercised through native picker dialogs. The Batch page successfully completed:

- individual-file encrypt selection through the native multi-file picker
- output-directory autofill from the first selected source file
- output-directory override through the native folder picker
- archive-encrypt multi-file selection and archive creation
- archive-decrypt validation for invalid multi-selection
- archive-decrypt single encrypted-archive selection through the native picker
- extraction-directory selection through the native folder picker
- archive extraction into `/tmp/filecrypter-batch-smoke/extract-out/`, with `alpha.txt`, `beta.txt`, and `gamma.txt` confirmed on disk after the run

The packaged app was also exercised through a focused shared-settings smoke. That pass verified:

- saved `Default overwrite protection` propagation across Encrypt, Decrypt, and Batch
- saved `Default output directory` propagation across Encrypt, Decrypt, and Batch
- `Reset settings to defaults` restoring startup defaults after relaunch
- Batch first-source autofill still taking over when no default output directory is saved

The packaged app was also exercised through a focused desktop file-drop smoke. That pass verified:

- Encrypt single-file drop updating the source field and suggested output path
- Decrypt single-file `.encrypted` drop updating the source field and suggested output path
- Batch multi-file drop preserving dedupe behavior and first-source output-folder autofill when no saved default output directory was active
- Batch archive-decrypt drop replacing the selection with exactly one archive source

The macOS bundle workflow now includes a post-assembly ad-hoc re-sign, which resolved a previously observed invalid-bundle-signature state and stabilized normal `open artifacts/macos/FileCrypter.app` launches in local verification.

The latest workflow-feedback copy slice was verified with automated coverage only in this session. The preferred packaged-app flow for any follow-up manual desktop smoke remains:

```bash
./scripts/package-macos-app.sh
open artifacts/macos/FileCrypter.app
```

The changelog-relocation slice was verified with focused app view-model coverage:

```bash
dotnet test tests/FileCrypter.App.Tests/FileCrypter.App.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~MainWindowViewModelTests"
```

Note for the current Windows sandbox environment: a broader `dotnet test tests/FileCrypter.App.Tests/FileCrypter.App.Tests.csproj` rerun still reports pre-existing path-format assertion failures in unrelated Encrypt/Decrypt/Batch picker-drop tests that expect `/tmp/...`-style paths.

The Batch workflow-card grouping slice was verified with focused app view-model coverage:

```bash
dotnet test tests/FileCrypter.App.Tests/FileCrypter.App.Tests.csproj --filter "FullyQualifiedName~BatchViewModelTests.WorkflowKindDescription_FollowsSelectedCombination|FullyQualifiedName~BatchViewModelTests.WorkflowChooserDescription_ExplainsThatAllFourCombinationsAreAvailable|FullyQualifiedName~BatchViewModelTests.SwitchingToArchiveMode_UpdatesTitlesAndShowsArchiveNameInput|FullyQualifiedName~BatchViewModelTests.StartBatchCommand_InArchiveDecryptMode_RequiresExactlyOneArchive"
```

Latest test count at handoff: 192 passed, 0 failed.

Previous GUI launch smoke coverage:

```bash
dotnet run --project src/FileCrypter.App/FileCrypter.App.csproj
```

The desktop app launch command stayed running until manually interrupted during the previous GUI pass, which is a basic launch smoke signal. A fresh rerun in this session hit `Avalonia.Native` render-timer startup error `-6661` before window creation in the current environment, so native picker dialogs and the new Help page still need a real desktop click-through on the target machine.

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

- Automatic update source detection, install, defer, and relaunch flow.
- Broader CLI command polish.
- macOS signing, notarization, icon conversion, and DMG packaging.

## Product Alignment

The current implementation is aligned with the product outline for the foundation phase:

- local-only operation with no cloud, account, or remote processing dependency
- reusable core library consumed by a CLI host
- password-only single-file encrypt/decrypt path
- password plus existing-key-file single-file encrypt/decrypt path
- generated key-file single-file encrypt path
- batch individual-file encrypt/decrypt path
- compressed tar archive encrypt/decrypt path for file-list archives
- persisted shared defaults for theme preference, single-file compression, overwrite protection, and default output directory
- versioned documented encrypted file format
- AES-256-GCM, Argon2id, unique per-file salt, unique per-file nonce prefix, and chunk-level authentication
- bounded-memory streaming for large-file readiness
- non-destructive input handling
- staged writes that do not leave partial output at the final destination
- overwrite protection that auto-renames by default and allows explicit replacement

Remaining product-level gaps are expected for later phases:

- fuller update source/install flow
- broader CLI command polish
- richer macOS distribution work beyond the local framework-dependent `.app` bundle

## Recommended Next Step

Continue the Avalonia GUI now that single-file encrypt, decrypt, settings, and both batch individual-file plus archive workflows are implemented.

Recommended next slices:

- Favor another small workflow-polish slice that stays within the current shell and local-first scope, with checkbox visibility polish plus sidebar hover feedback as the strongest next candidate now that the Batch workflow chooser reads more clearly.
- If a thinner UI-only slice feels better, clearer drag/drop affordances or deeper path/open-folder conveniences would also fit the current boundary well.
- After that, keep choosing practical GUI slices that preserve the existing shell structure and avoid drifting into installer, updater, or broader distribution infrastructure.
- Keep automatic install/relaunch updater work deferred until there is real release/distribution infrastructure to attach it to.

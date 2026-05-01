# Cleanup TODOs

This checklist captures the security, quality, documentation, and dependency follow-ups from the April 2026 codebase audit. Prioritize the security items first, especially anything that reduces password/key material lifetime or prevents sensitive details from leaking into UI/CLI output.

## Critical Security

### 1. De-emphasize process-visible CLI passwords

- Files:
  - `src/FileCrypter.Cli/FileCrypterCommand.cs:88`
  - `README.md:83`
  - `README.md:95`
- Problem: `--password <value>` is treated as a first-class path and README examples model it repeatedly. Command-line arguments can be exposed through shell history, process listings, logs, terminal scrollback, and crash reports.
- Fix:
  - Prefer interactive hidden prompting and `--password-stdin` in help and README examples.
  - Add a warning when `--password` is used, or consider moving it behind an explicit unsafe/compatibility note.
  - Keep `--password-stdin` as the recommended automation path.

### 2. Clear UI passwords after failures

- Files:
  - `src/FileCrypter.Desktop/ViewModels/EncryptViewModel.cs:544`
  - `src/FileCrypter.Desktop/ViewModels/DecryptViewModel.cs:394`
  - `src/FileCrypter.Desktop/ViewModels/BatchViewModel.cs:608`
  - `tests/FileCrypter.Desktop.Tests/ViewModels/EncryptViewModelTests.cs:98`
  - `tests/FileCrypter.Desktop.Tests/ViewModels/DecryptViewModelTests.cs:89`
  - `tests/FileCrypter.Desktop.Tests/ViewModels/BatchViewModelTests.cs:109`
- Problem: `Password` is cleared on successful operations, but failed encryption/decryption/batch operations leave it in bindable view-model properties and UI controls.
- Fix:
  - Clear `Password` in `finally` or immediately after constructing request objects.
  - Update tests that currently assert password retention after failure.
  - Verify failure messages still remain useful after the password is cleared.

### 3. Avoid duplicate hidden plaintext password controls

- Files:
  - `src/FileCrypter.Desktop/Views/EncryptView.axaml:201`
  - `src/FileCrypter.Desktop/Views/DecryptView.axaml:191`
  - `src/FileCrypter.Desktop/Views/BatchView.axaml:393`
- Problem: each password editor binds both a masked and unmasked `TextBox` to `Password`. Even when hidden, the plaintext field can still hold the secret in a UI control property.
- Fix:
  - Replace the paired controls with a single password editor that toggles masking/reveal behavior.
  - Review accessibility/UI automation exposure for the reveal state.
  - Add UI/view-model tests to confirm password text is not duplicated into hidden controls.

### 4. Sanitize unknown workflow and CLI errors

- Files:
  - `src/FileCrypter.Desktop/Services/WorkflowErrorMessageFormatter.cs:27`
  - `src/FileCrypter.Cli/FileCrypterCommand.cs:58`
  - `src/FileCrypter.Cli/FileCrypterCommand.cs:878`
  - `src/FileCrypter.Cli/FileCrypterCommand.cs:892`
  - `src/FileCrypter.Desktop/ViewModels/SettingsViewModel.cs:435`
  - `src/FileCrypter.Desktop/ViewModels/HelpViewModel.cs:159`
- Problem: known format failures are mapped to friendly guidance, but unknown exceptions and path/settings failures often surface raw `exception.Message` text. These messages may include absolute paths, usernames, OS-specific details, or low-level library text.
- Fix:
  - Map common `IOException`, `UnauthorizedAccessException`, `ArgumentException`, compression, and platform failures to sanitized messages.
  - Add an explicit verbose/debug path if raw details are needed.
  - Ensure copy-to-clipboard error actions do not include raw sensitive details by default.

### 5. Zero chunk stream buffers on dispose

- Files:
  - `src/FileCrypter.Core/FileCrypter.cs:1933`
  - `src/FileCrypter.Core/FileCrypter.cs:2127`
- Problem: `ChunkEncryptingStream` and `ChunkDecryptingStream` keep plaintext, ciphertext, tag, nonce, and associated-data buffers, but neither stream clears these arrays on disposal.
- Fix:
  - Override `Dispose(bool)` in both chunk stream classes.
  - Use `CryptographicOperations.ZeroMemory` for all sensitive buffers.
  - Keep existing `using var` call sites so disposal runs reliably.

### 6. Zero key-file bytes on failed reads

- Files:
  - `src/FileCrypter.Core/FileCrypter.cs:1427`
  - `src/FileCrypter.Core/FileCrypter.cs:1429`
  - `src/FileCrypter.Core/FileCrypter.cs:1438`
- Problem: `ReadKeyFileAsync` zeroes `keyFileBytes` for the trailing-byte oversize path, but short reads or cancellation after partial reads can leave key-file material in memory.
- Fix:
  - Wrap key-file read logic so any failure after allocation zeros `keyFileBytes` before rethrowing.
  - Add a focused test using a faulting/short-read stream if the helper is refactored for testability.

### 7. Revisit symlink/path TOCTOU residual risk

- Files:
  - `src/FileCrypter.Core/FileCrypter.cs:539`
  - `src/FileCrypter.Core/FileCrypter.cs:573`
  - `src/FileCrypter.Core/FileCrypter.cs:1305`
  - `src/FileCrypter.Core/FileCrypter.cs:1318`
  - `docs/security.md:13`
- Problem: input/key/output symlink checks happen before later opens or moves, so writable shared directories can still be raced.
- Fix:
  - Prefer open-time no-follow primitives where available.
  - Keep staging-file behavior, but document any remaining platform limitations clearly.
  - Consider additional tests for hostile symlink replacement in writable directories.

## Major Quality And Architecture

### 8. Observe async picker command failures

- Files:
  - `src/FileCrypter.Desktop/Views/EncryptView.axaml.cs:46`
  - `src/FileCrypter.Desktop/Views/DecryptView.axaml.cs:46`
  - `src/FileCrypter.Desktop/Views/BatchView.axaml.cs:46`
- Problem: drop-zone click handlers discard `ExecuteAsync` tasks. Picker exceptions may become unobserved and bypass the view-model error surface.
- Fix:
  - Await the command task from an async event handler, or route picker clicks through view-model methods that catch and display sanitized failures.
  - Add tests with picker services that throw.

### 9. Report path reveal failures

- File: `src/FileCrypter.Desktop/Services/DesktopPathRevealService.cs:21`
- Problem: reveal failures are intentionally swallowed, but the UI gives no status or toast when reveal fails.
- Fix:
  - Return a result from `TryRevealPathAsync`, or expose a callback/status path.
  - Show a non-sensitive warning toast such as "Could not reveal that path."

### 10. Add direct `--password-stdin` tests

- Files:
  - `src/FileCrypter.Cli/FileCrypterCommand.cs:97`
  - `src/FileCrypter.Cli/FileCrypterCommand.cs:272`
  - `src/FileCrypter.Cli/FileCrypterCommand.cs:400`
  - `src/FileCrypter.Cli/FileCrypterCommand.cs:522`
  - `tests/FileCrypter.Cli.Tests/FileCrypterCommandTests.cs`
- Problem: the safer automation path is implemented, but not directly covered by success and EOF/empty-input tests.
- Fix:
  - Add single-file encrypt/decrypt tests using `--password-stdin`.
  - Add at least one batch or archive stdin-password test.
  - Add empty stdin coverage.

## Minor Documentation And Style

### 11. Add XML docs for public Core API

- Files:
  - `src/FileCrypter.Core/FileCrypter.cs:13`
  - `src/FileCrypter.Core/FileCrypterOptions.cs:3`
  - `src/FileCrypter.Core/FileCrypterProgress.cs:3`
  - `src/FileCrypter.Core/Format/FileCrypterFormatException.cs:3`
  - `src/FileCrypter.Core/Format/FileCrypterFormatErrorCode.cs:3`
- Status: completed 2026-05-01. Public Core APIs now include XML documentation for behavior, password/key-file handling, overwrite and compression behavior, progress, exceptions, and security caveats.
- Problem: public crypto APIs lack XML summaries for behavior, parameter expectations, and security caveats.
- Fix:
  - Document API purpose, password/key-file handling, overwrite behavior, compression behavior, and exception shapes.
  - Consider enabling XML doc generation or analyzer enforcement for `FileCrypter.Core`.

### 12. Refresh stale security TODOs

- File: `docs/security-todos.md:7`
- Status: completed 2026-05-01. Resolved audit findings were moved into a dated archive, and the only current entries are accepted residual risks with present-tense guidance.
- Problem: resolved historical issues still read like active vulnerable behavior.
- Fix:
  - Convert resolved items into a dated changelog/archive section, or remove them.
  - Keep only open security work in present-tense TODO form.

### 13. Align help text with generated-password behavior

- Files:
  - `src/FileCrypter.Desktop/ViewModels/HelpViewModel.cs:49`
  - `src/FileCrypter.Desktop/ViewModels/HelpViewModel.cs:89`
  - `src/FileCrypter.Desktop/ViewModels/EncryptViewModel.cs:467`
  - `src/FileCrypter.Desktop/ViewModels/BatchViewModel.cs:469`
- Problem: help says generated values are "shown once" and not reusable unless recorded, but generated values are assigned to bindable `Password` strings, shown by default, and currently retained after failures.
- Fix:
  - First fix password clearing behavior.
  - Then update help text to accurately describe generated password lifetime.

### 14. Review doc-only TODO markers

- Files:
  - `docs/UI-plan.md:144`
  - `docs/UI-plan.md:163`
  - `docs/security-todos.md:1`
- Status: completed 2026-05-01. `docs/UI-plan.md` is now an archive, active UI work was moved below, and `docs/security-todos.md` no longer presents resolved audit findings as live work.
- Problem: no source-code `TODO`, `FIXME`, or `HACK` markers were found, but docs contain open planning/TODO markers.
- Fix:
  - Decide whether these are still actionable.
  - Move active work into this file or issue tracker, and archive stale planning notes.

## UI Backlog Moved From UI Plan

These items remain active but are no longer tracked as planning markers in `docs/UI-plan.md`.

### Sidebar recent files or drop target

- Former source: `docs/UI-plan.md` Slice 8.
- Problem: the sidebar still has a large unused vertical gap between Batch and Help.
- Fix:
  - Add an opt-in recent encrypted/output files list, or a sidebar-wide drop target that defaults to the Encrypt flow.
  - Avoid storing plaintext paths by default; prefer encrypted output paths or explicit opt-in.
  - Persist only minimal local metadata needed for the chosen behavior.

### Checkbox visibility polish

- Former source: `docs/UI-plan.md` Slice 9.
- Problem: unchecked checkboxes can be hard to see against the dark background.
- Fix:
  - Increase unchecked border contrast and thickness while preserving the checked state.
  - Verify the unchecked border reaches WCAG AA 3:1 contrast for UI components.

## Dependency Cleanup

### 15. Patch `ZstdSharp.Port`

- File: `Directory.Packages.props:15`
- Status: completed 2026-05-01. `ZstdSharp.Port` is centrally pinned and locked at `0.8.8`.
- Current: `ZstdSharp.Port 0.8.8`
- Latest reported by NuGet: `0.8.8`
- Fix:
  - Update the package.
  - Run core archive/compression tests and CLI archive smoke tests.

### 16. Review transitive Desktop and test updates

- Files:
  - `src/FileCrypter.Desktop/FileCrypter.Desktop.csproj:29`
  - `tests/FileCrypter.*.Tests/*.csproj`
- Outdated transitives reported by NuGet:
  - `Avalonia.Angle.Windows.Natives`
  - `MicroCom.Runtime`
  - `Microsoft.Extensions.DependencyInjection.Abstractions`
  - `Microsoft.Extensions.Logging.Abstractions`
  - `Microsoft.ApplicationInsights`
  - `Microsoft.Testing.Platform*`
  - `Newtonsoft.Json`
- Status: completed 2026-05-01. Direct package updates were reviewed; no additional direct pins or risky upgrades were added because only transitive packages remained outdated after the Zstd patch update.
- Fix:
  - Check whether updating direct Avalonia and test packages moves these transitives.
  - Validate desktop launch and tests after any package updates.

### 17. Consider central package management and lock files

- Files:
  - `Directory.Packages.props`
  - `src/*/packages.lock.json`
  - `tests/*/packages.lock.json`
- Status: completed 2026-05-01. Central package management is enabled and all projects have NuGet lock files.
- Problem: package versions are pinned directly in project files, but restores are not locked for reproducible crypto-app builds.
- Fix:
  - Add `Directory.Packages.props` if central management fits the repo.
  - Enable NuGet lock files and consider locked-mode restore in CI.

### 18. Keep dependency notices aligned

- File: `THIRD-PARTY-NOTICES.md:1`
- Status: completed 2026-05-01. The root notices file remains canonical, and `docs/THIRD-PARTY-NOTICES.md` now points to it.
- Problem: the audit request referenced `docs/THIRD-PARTY-NOTICES`, but the repo stores notices at the root. The root file matches production dependencies reasonably well.
- Fix:
  - Decide whether to keep notices at the root or mirror/link them under `docs/`.
  - Re-check notices after dependency updates.

## Clean / Positive Observations

- No weak crypto hits found for MD5, SHA1 integrity, DES, 3DES, RC2, RC4, or ECB.
- Core uses AES-256-GCM and Argon2id.
- Salt and nonce prefix use `RandomNumberGenerator`.
- Generated passwords and passphrases use `RandomNumberGenerator.GetInt32`.
- Header and chunk prefix are authenticated as AAD.
- Chunk nonces use an 8-byte random prefix plus a 32-bit chunk counter with overflow checks.
- `AesGcm`, compression, tar, and file streams are disposed through `using` / `await using`.
- Derived keys, password UTF-8 bytes, credential preimages, generated key-file bytes, and normal key-file buffers are zeroed on ordinary paths.
- CLI, Desktop, and Core project boundaries are clean: crypto remains in Core; host/UI layers call services.
- All projects target `net10.0`.
- NuGet reported no vulnerable or deprecated packages with the current configured sources.
- Tests passed during audit:
  - Core: 94 passed
  - CLI: 45 passed
  - Desktop: 109 passed

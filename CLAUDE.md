# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

FileCrypter is a local-first .NET 10 file encryption product: a reusable crypto core, a CLI host, and an Avalonia desktop UI. There is no cloud service, telemetry, or network dependency by design — do not introduce one without explicit design review.

## Toolchain

- SDK is pinned to `10.0.300` with `rollForward: latestFeature` in `global.json`; a matching SDK must be installed or builds fail. Licensed Apache-2.0 (`LICENSE`, `NOTICE`); third-party attribution lives in `THIRD-PARTY-NOTICES.md`, which the desktop project embeds as a resource.
- Solution is `FileCrypterDotNet.slnx` (XML `.slnx`, not `.sln`); root-level `dotnet build` / `dotnet test` resolve it automatically.
- Central Package Management: all versions live in `Directory.Packages.props` — never pin versions in a `.csproj`. `RestorePackagesWithLockFile=true`, and per-project `packages.lock.json` files are committed; regenerate and commit them whenever a version changes.
- `Directory.Build.props` sets `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `Deterministic=true`. A new analyzer warning fails the build — fix the cause rather than suppressing.

## Commands

```bash
dotnet restore                       # add --locked-mode for reproducible restore
dotnet build --no-restore            # warnings are errors
dotnet test --no-build               # full suite (build first)

# single project / single test:
dotnet test tests/FileCrypter.Core.Tests --no-build
dotnet test tests/FileCrypter.Core.Tests --no-build --filter "FullyQualifiedName~EncryptFileAsyncDecryptFileAsync_RoundTrips"

dotnet run --project src/FileCrypter.Cli -- --help
dotnet run --project src/FileCrypter.Desktop
./scripts/package-macos-app.sh       # -> artifacts/macos/FileCrypter.app (gitignored)
```

Order matters: `restore -> build -> test`; `--no-build` assumes a prior build.

## Architecture

- `src/FileCrypter.Core/` — crypto + file format, no host dependencies. All host-facing entry points are the static `FileCrypter` class in `FileCrypter.cs` (`EncryptFileAsync`, `DecryptFileAsync`, `EncryptFilesAsync`/`DecryptFilesAsync` for batch, `EncryptArchiveAsync`/`DecryptArchiveAsync`, `GenerateKeyFileAsync`, `InspectAsync`, `VerifyFileAsync`). Subfolders: `Format/` (header constants, parser, writer, error codes), `Cryptography/` (key derivation), `Settings/` (persisted local settings shared by both hosts). `IFileCrypterRandomSource` is internal and injectable for deterministic tests.
- The `Format/` types are all `internal`. Hosts read header metadata through the public projection `FileCrypterFileInfo` (returned by `InspectAsync`), which deliberately omits the salt and nonce prefix. Header metadata is unauthenticated — it is what the file claims. Only `VerifyFileAsync` or a full decryption authenticates the payload, so never phrase inspected values as proof a file is intact.
- `src/FileCrypter.Cli/` — `Program.cs` delegates to `FileCrypterCommand` (most CLI logic lives there). `IFileCrypterConsole` abstracts stdio for tests.
- `src/FileCrypter.Desktop/` — Avalonia 12 + `CommunityToolkit.Mvvm`. `Views/` (`.axaml` + code-behind), `ViewModels/` (Encrypt, Decrypt, Batch, Settings, Help, MainWindow), and `Services/` — the seam between UI and core. Every service is interface-first (`IFileCrypterWorkflowService`, `IFilePickerService`, `IClipboardService`, `IAppThemeService`, `IFileCrypterSettingsService`, `IAppUpdateService`, `IPathRevealService`, `IPasswordGeneratorService`) with request/result record types per workflow, so view models are testable headlessly; add new UI capabilities behind an interface with a No-Op/Development implementation rather than calling platform APIs from a view model. `ViewLocator.cs` maps view models to views. Compiled bindings are on by default. `RuntimeIdentifiers` are `osx-arm64;win-x64`; `UseAppHost=false` in Debug.
- Tests mirror each project under `tests/*.Tests/`, including subfolders (`Format/`, `Services/`, `ViewModels/`, `Packaging/`). Name test files after the type or workflow under test (`FileCrypterCommandTests.cs`, `EncryptViewModelTests.cs`, `MacAppBundleTests.cs`).
- `docs/file-format.md` is the source of truth for the v1 encrypted file format (64-byte header, AES-256-GCM chunked payload, Argon2id). Any change to header constants, algorithm ids, Argon2 bounds, or chunk framing requires updating that doc and its deterministic test vectors.

## Style

Four-space indentation, file-scoped namespaces where already used, PascalCase for types and public members, camelCase for locals and parameters, `I`-prefixed interfaces. Keep compiled Avalonia bindings valid and pair every `.axaml` view with its code-behind. Fix analyzer findings rather than suppressing them.

## Testing quirks

- Production Argon2id defaults (65536 KiB / 3 iterations / parallelism 4) are slow. Core tests use a `CreateFastOptions()` helper with the minimums (`19456` KiB / `2` iterations / parallelism `1`) and minimum chunk size (`64` KiB). Use it in new core tests or the suite becomes unusably slow.
- Deterministic vectors in `docs/file-format.md` depend on `FixedRandomSource` injecting sequential bytes (`RandomStart`) for salt and nonce prefix. Keep vectors in sync if crypto output changes.
- Test projects are `OutputType=Exe` (xUnit v3 auto-generates the entrypoint — no `Program.cs`) and suppress analyzer `xUnit1051`.
- For file operations, use temporary paths and assert overwrite, auto-rename, symlink rejection, and failure behavior explicitly.

## CLI conventions

- Final output paths go to stdout; progress, warnings, and errors go to stderr. Preserve this separation when adding output.
- Prefer `--password-stdin` or the hidden interactive prompt over `--password <value>` (the latter warns). Never commit passwords, generated key files, or encrypted personal samples.
- `archive-decrypt` is for `.tar.zst.encrypted` archives; `decrypt` is for single-file `.encrypted`. `inspect` reports which one a file actually is without needing a password, so prefer it over guessing from the suffix. Existing key files are capped at 16 MiB.
- `inspect` prints header metadata only and needs no password; `verify` decrypts to nothing to authenticate every chunk and writes no files. For archive payloads `verify` authenticates the archive bytes but does not walk tar entry structure — do not let user-facing text imply otherwise.

## Security constraints

- Core rejects symlinks/reparse points on input, key-file, output, and settings paths before use. Staged outputs are created `0600` on Unix (explicit ACL on Windows). Preserve these checks when touching path handling.
- Argon2id parameters are bounded on both encrypt and decrypt; out-of-range headers are rejected before key derivation. Never relax the bounds in `Format/FileCrypterFormatConstants.cs` without review.
- `docs/security.md` records accepted residual risks (bounded TOCTOU window; managed `string` passwords cannot be reliably zeroed).

## Commit & PR

- Short, specific, imperative subjects, one line (`Add desktop workflow cancellation`, `Harden settings storage and path reveal process launches`).
- There is no CI and no `.github/` directory — verification is local: restore, build, then test.
- PRs should summarize behavior and security impact, list the build/test commands run, link relevant docs/issues, and include screenshots for visible Avalonia changes.
- `bin/`, `obj/`, and `artifacts/` are gitignored; `packages.lock.json` files are tracked.

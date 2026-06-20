# FileCrypter Agent Guide

Local-first .NET 10 file encryption product: a reusable crypto core, a CLI host, and an Avalonia desktop UI. No cloud, telemetry, or network dependency by design — do not introduce any without explicit design review.

## Toolchain

- .NET SDK is pinned to `10.0.300` with `rollForward: latestFeature` in `global.json`; a matching SDK must be installed or builds fail.
- Solution is `FileCrypterDotNet.slnx` (XML `.slnx`, not `.sln`); `dotnet build` / `dotnet test` from the repo root resolve it automatically.
- Central Package Management: all package versions live in `Directory.Packages.props`. Do not pin versions in individual `.csproj` files. `RestorePackagesWithLockFile=true` with per-project `packages.lock.json` files that are committed; use `dotnet restore --locked-mode` for reproducible restores, and regenerate + commit the lockfiles whenever you change a version.
- `Directory.Build.props` sets `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `Deterministic=true`. Any new analyzer warning fails the build — fix the cause rather than suppressing.

## Commands

```bash
dotnet restore                       # add --locked-mode for reproducible restore
dotnet build --no-restore            # warnings are errors
dotnet test --no-build               # full suite (run a build first)

# single test / project / filter:
dotnet test tests/FileCrypter.Core.Tests --no-build --filter "FullyQualifiedName~EncryptFileAsyncDecryptFileAsync_RoundTrips"
dotnet test tests/FileCrypter.Cli.Tests --no-build

dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- --help
dotnet run --project src/FileCrypter.Desktop/FileCrypter.Desktop.csproj
./scripts/package-macos-app.sh       # -> artifacts/macos/FileCrypter.app (gitignored)
```

Order matters: `restore -> build -> test`; `--no-build` assumes a prior build.

## Architecture

- `src/FileCrypter.Core/` — crypto + file format, no host dependencies. Entry point is the static `FileCrypter` class in `FileCrypter.cs`. Subfolders: `Format/` (header constants, parser, writer, error codes), `Cryptography/` (key derivation), `Settings/` (persisted local settings). `IFileCrypterRandomSource` is internal and injectable for deterministic tests.
- `src/FileCrypter.Cli/` — `Program.cs` delegates to `FileCrypterCommand` in `FileCrypterCommand.cs` (most CLI logic). `IFileCrypterConsole` abstracts stdio for tests.
- `src/FileCrypter.Desktop/` — Avalonia 12 + `CommunityToolkit.Mvvm`. `Views/` (`.axaml` + `.axaml.cs`), `ViewModels/`, app-facing `Services/` (workflow, file pickers, clipboard, theme, settings, app update, path reveal). `ViewLocator.cs` maps view models to views. Compiled bindings are on (`AvaloniaUseCompiledBindingsByDefault=true`). `RuntimeIdentifiers` are `osx-arm64;win-x64`; `UseAppHost=false` in Debug.
- Tests mirror each project under `tests/*.Tests/`, including subfolders (`Format/`, `Services/`, `ViewModels/`, `Packaging/`). Name test files after the type or workflow under test (`FileCrypterCommandTests.cs`, `EncryptViewModelTests.cs`, `MacAppBundleTests.cs`).
- `docs/file-format.md` is the source of truth for the v1 encrypted file format (64-byte header, AES-256-GCM chunked payload, Argon2id). Any change to header constants, algorithm ids, Argon2 bounds, or chunk framing requires updating that doc and its deterministic test vectors.

## Testing quirks

- Production Argon2id defaults (65536 KiB / 3 iterations / parallelism 4) are slow. Core tests use a `CreateFastOptions()` helper that sets the minimums (`19456` KiB / `2` iterations / `1` parallelism) and minimum chunk size (`64` KiB). Use the same fast options in new core tests or the suite becomes unusably slow.
- Deterministic test vectors in `docs/file-format.md` rely on `FixedRandomSource` injecting sequential bytes (`RandomStart`) for salt and nonce prefix. Keep vectors in sync if crypto output changes.
- Test projects are `OutputType=Exe` (xUnit v3 auto-generates the entrypoint — no `Program.cs` needed) and suppress analyzer `xUnit1051`.
- For file operations, use temporary paths and assert overwrite, auto-rename, symlink rejection, and failure behavior explicitly.

## CLI conventions

- Final output paths go to stdout; progress, warnings, and errors go to stderr. Preserve this separation when adding output.
- Prefer `--password-stdin` or the hidden interactive prompt over `--password <value>` (the latter warns). Never commit passwords, generated key files, or encrypted personal samples.
- `archive-decrypt` is for `.tar.zst.encrypted` archives; `decrypt` is for single-file `.encrypted`.

## Security constraints

- Core rejects symlinks/reparse points on input, key-file, output, and settings paths before use. Staged outputs are created `0600` on Unix (explicit ACL on Windows). Preserve these checks when touching path handling.
- Argon2id parameters are bounded on both encrypt and decrypt; out-of-range headers are rejected before key derivation. Never relax the bounds in `Format/FileCrypterFormatConstants.cs` without review.
- See `docs/security.md` for accepted residual risks (bounded TOCTOU window; managed `string` passwords cannot be reliably zeroed).

## Commit & PR

- Short, specific, imperative subjects under one line (`Add workflow service integration tests`, `Harden Argon2 bounds and output path security`). Some security fixes use `fix(security):` prefixes; Aikido autofix PRs use `fix/aikido-security-sast-...` branches.
- There is no CI (`.github/workflows/` is absent) — verification is local: build, then test.
- PRs should list tests run, link related docs/issues, include screenshots for visible Avalonia changes, and call out any encryption format, password, key-file, or output-path security implications.
- `.github/instructions/aikido_rules.instructions.md` directs running `aikido_full_scan` (via the Aikido MCP server) on generated/modified first-party code and fix-and-rescan until clean. Run it when the MCP server is available; otherwise tell the user to install it per the Aikido MCP setup guide.

## VCS

`bin/`, `obj/`, and `artifacts/` are gitignored. `packages.lock.json` files are tracked — keep them updated. Don't commit `.DS_Store`, passwords, key files, encrypted personal samples, or local settings.

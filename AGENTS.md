# Repository Guidelines

## Project Structure & Module Organization

FileCrypterDotNet is a local-first .NET 10 solution (`FileCrypterDotNet.slnx`). Production code lives under `src/`: `FileCrypter.Core` contains encryption, file-format, cryptography, and settings logic; `FileCrypter.Cli` provides the command-line host; and `FileCrypter.Desktop` contains the Avalonia UI, view models, and desktop services. Tests mirror those projects under `tests/*.Tests`, with feature folders such as `Format/`, `Services/`, and `ViewModels/`. File-format and security contracts are documented in `docs/`; macOS packaging lives in `scripts/package-macos-app.sh`.

## Build, Test, and Development Commands

Use the SDK selected by `global.json`, then run commands from the repository root:

```bash
dotnet restore --locked-mode   # reproduce committed package resolution
dotnet build --no-restore      # compile; warnings fail the build
dotnet test --no-build         # run the full xUnit v3 suite
dotnet run --project src/FileCrypter.Cli -- --help
dotnet run --project src/FileCrypter.Desktop
./scripts/package-macos-app.sh # create artifacts/macos/FileCrypter.app
```

When changing a version in `Directory.Packages.props`, regenerate and commit the affected `packages.lock.json` files. Package versions do not belong in individual project files.

## Coding Style & Naming Conventions

Follow existing C# conventions: four-space indentation, file-scoped namespaces where already used, PascalCase for types and public members, camelCase for locals and parameters, and `I` prefixes for interfaces. Nullable reference types, implicit usings, latest analyzers, and deterministic builds are enabled in `Directory.Build.props`. Fix analyzer findings instead of suppressing them. Keep compiled Avalonia bindings valid and pair `.axaml` views with their code-behind files.

## Testing Guidelines

Name test files after the subject, for example `FileCrypterCommandTests.cs`, and use behavior-focused method names. Core crypto tests should use minimum-cost test options rather than production Argon2 settings. Cover success and failure paths, including overwrite behavior, auto-renaming, cancellation, and symlink rejection. File-format changes must update `docs/file-format.md` and deterministic vectors.

## Security, Commits & Pull Requests

Do not introduce networking, telemetry, secrets, personal encrypted samples, or generated key files. Preserve path validation, restrictive staged-output permissions, and Argon2 bounds.

Write short imperative commits such as `Add desktop workflow cancellation`. Pull requests should summarize behavior and security impact, list local build/test commands, link relevant issues or docs, and include screenshots for visible Avalonia changes.

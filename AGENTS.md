# Repository Guidelines

## Project Structure & Module Organization

FileCrypter is a .NET 10 solution pinned by `global.json` and organized by host boundary. Core encryption and file-format logic lives in `src/FileCrypter.Core/`. Command-line behavior lives in `src/FileCrypter.Cli/`. The Avalonia desktop UI lives in `src/FileCrypter.Desktop/`, with `Views/`, `ViewModels/`, and app-facing `Services/`. Tests mirror these projects under `tests/FileCrypter.Core.Tests/`, `tests/FileCrypter.Cli.Tests/`, and `tests/FileCrypter.Desktop.Tests/`. Product and implementation notes are in `docs/`, with `docs/file-format.md` especially important for format changes.

## Build, Test, and Development Commands

- `dotnet restore`: restore NuGet packages for the solution.
- `dotnet build --no-restore`: compile all projects with warnings treated as errors.
- `dotnet test --no-build`: run the xUnit v3 test suite after a build.
- `dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- --help`: run the CLI locally.
- `dotnet run --project src/FileCrypter.Desktop/FileCrypter.Desktop.csproj`: launch the Avalonia app.
- `./scripts/package-macos-app.sh`: create the macOS `.app` bundle on macOS-capable environments.

## Coding Style & Naming Conventions

Use C# conventions: four-space indentation, PascalCase for public types and members, camelCase for locals and parameters, and `I` prefixes for interfaces. Keep nullable annotations clean; `Directory.Build.props` enables nullable reference types, implicit usings, latest analysis, deterministic builds, and `TreatWarningsAsErrors`. Prefer focused services and view models over broad utility classes. Keep generated `bin/` and `obj/` output out of commits.

## Testing Guidelines

Tests use xUnit v3 with `Microsoft.NET.Test.Sdk` and `coverlet.collector`. Name test files after the type or workflow under test, such as `FileCrypterCommandTests.cs` or `EncryptViewModelTests.cs`. Add tests in the matching project when changing core crypto behavior, CLI parsing/output, workflow services, or UI view-model state. For file operations, use temporary paths and assert overwrite, auto-rename, and failure behavior explicitly.

## Commit & Pull Request Guidelines

Recent history uses short imperative commit subjects, for example `Add workflow service integration tests` and `Harden Argon2 bounds and output path security`. Keep subjects specific and under one line. Pull requests should describe the behavior change, list tests run, link related issues or docs, and include screenshots for visible Avalonia UI changes. Call out any encryption format, password, key-file, or output-path security implications.

## Security & Configuration Tips

Do not commit passwords, generated key files, encrypted personal samples, or local settings. Preserve local-first behavior: no cloud processing, telemetry, or network dependency should be introduced without explicit design review.

# FileCrypter

FileCrypter is a local-first file encryption product for protecting files before storage, transfer, backup, or sharing. The implementation is starting with a reusable .NET crypto core, a thin command-line host, and tests before any graphical UI is added.

## Current Phase

Phase 0: repository and solution setup.

This phase establishes the .NET solution, project layout, shared build defaults, and baseline documentation. It intentionally does not define the encrypted file format, crypto APIs, CLI commands, compression behavior, or archive workflows yet.

## Solution Layout

```text
FileCrypterDotNet/
  FileCrypterDotNet.slnx
  src/
    FileCrypter.Core/
    FileCrypter.Cli/
  tests/
    FileCrypter.Core.Tests/
  docs/
    planning-brief.md
    file-format.md
    security.md
  product.md
```

## Build And Test

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

The repository targets .NET 10 and pins the SDK through `global.json`.

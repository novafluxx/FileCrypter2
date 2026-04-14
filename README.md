# FileCrypter

FileCrypter is a local-first file encryption product for protecting files before storage, transfer, backup, or sharing. The implementation is starting with a reusable .NET crypto core, a thin command-line host, and tests before any graphical UI is added.

## Current Phase

Phase 1: file format draft and header parser.

The repository now has the initial .NET solution scaffold plus a draft v1 encrypted file format and internal header parser validation tests. It intentionally does not implement encryption, decryption, CLI commands, compression behavior, or archive workflows yet.

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
    current-status.md
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

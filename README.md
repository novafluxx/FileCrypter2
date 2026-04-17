# FileCrypter

FileCrypter is a local-first file encryption product for protecting files before storage, transfer, backup, or sharing. The implementation is starting with a reusable .NET crypto core, a thin command-line host, and tests before any graphical UI is added.

## Current Phase

Phase 2: single-file encryption/decryption foundation.

The repository has a reusable .NET core library, a minimal CLI host, a documented v1 encrypted file format, and tests for password-only and password plus key-file single-file workflows. The CLI supports safe staged encrypt/decrypt operations, overwrite protection, progress reporting, existing key files, and optional Zstandard compression with `encrypt --compress`.

Archive mode, batch workflows, key-file generation, settings, and graphical UI work are still future phases.

## Solution Layout

```text
FileCrypterDotNet/
  FileCrypterDotNet.slnx
  src/
    FileCrypter.Core/
    FileCrypter.Cli/
  tests/
    FileCrypter.Core.Tests/
    FileCrypter.Cli.Tests/
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

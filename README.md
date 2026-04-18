# FileCrypter

FileCrypter is a local-first file encryption product for protecting files before storage, transfer, backup, or sharing. Sensitive operations happen on the local machine; there is no cloud service, account, or remote processing dependency.

The current implementation is a reusable .NET crypto core, a command-line host, and the start of an Avalonia desktop UI.

## Current Phase

Phase 2: CLI and core encryption workflows.

The repository currently supports:

- single-file encrypt and decrypt
- optional Zstandard compression for single-file encryption
- password plus existing key-file protection
- generated key files for encryption
- batch individual-file encrypt and decrypt
- encrypted compressed tar archives with archive extraction
- a persisted single-file compression default setting
- safe staged writes, overwrite protection, auto-renamed outputs, and CLI progress reporting
- a documented v1 encrypted file format in `docs/file-format.md`
- an Avalonia 12 desktop app shell with sidebar navigation and a first single-file encrypt workflow

The CLI writes final output paths to stdout and progress, warnings, and errors to stderr.

## Solution Layout

```text
FileCrypterDotNet/
  FileCrypterDotNet.slnx
  src/
    FileCrypter.Core/
    FileCrypter.Cli/
    FileCrypter.App/
  tests/
    FileCrypter.Core.Tests/
    FileCrypter.Cli.Tests/
    FileCrypter.App.Tests/
  docs/
    planning-brief.md
    file-format.md
    current-status.md
    security.md
  product.md
```

## Requirements

The repository targets .NET 10 and pins the SDK through `global.json`.

## Build And Test

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

## Run The CLI

From the repository root:

```bash
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- --help
```

General command shape:

```text
filecrypter encrypt <input> [output] [--password <password> | --password-stdin] [--key-file <path> | --generate-key-file <path>] [--compress] [--overwrite]
filecrypter decrypt <input> [output] [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
filecrypter batch-encrypt <output-directory> <input>... [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
filecrypter batch-decrypt <output-directory> <input>... [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
filecrypter archive-encrypt <output-archive-or-directory> <input>... [--archive-name <name>] [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
filecrypter archive-decrypt <input-archive> <output-directory> [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
filecrypter settings show
filecrypter settings set compression-default <on|off>
```

When using `dotnet run`, put the FileCrypter arguments after `--`:

```bash
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- encrypt <input> [output] --password <password>
```

## Quick Smoke Tests

Single-file round trip:

```bash
printf "hello from FileCrypter\n" > /tmp/filecrypter-demo.txt

dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  encrypt /tmp/filecrypter-demo.txt /tmp/filecrypter-demo.txt.encrypted \
  --password demo --overwrite

dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  decrypt /tmp/filecrypter-demo.txt.encrypted /tmp/filecrypter-demo-restored.txt \
  --password demo --overwrite

cmp /tmp/filecrypter-demo.txt /tmp/filecrypter-demo-restored.txt
```

Compressed single-file encryption:

```bash
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  encrypt /tmp/filecrypter-demo.txt /tmp/filecrypter-demo.compressed.encrypted \
  --password demo --compress --overwrite
```

Generated key-file round trip:

```bash
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  encrypt /tmp/filecrypter-demo.txt /tmp/filecrypter-demo.keyed.encrypted \
  --password demo --generate-key-file /tmp/filecrypter-demo.key --overwrite

dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  decrypt /tmp/filecrypter-demo.keyed.encrypted /tmp/filecrypter-demo-keyed-restored.txt \
  --password demo --key-file /tmp/filecrypter-demo.key --overwrite

cmp /tmp/filecrypter-demo.txt /tmp/filecrypter-demo-keyed-restored.txt
```

Batch individual-file round trip:

```bash
mkdir -p /tmp/filecrypter-batch/input /tmp/filecrypter-batch/encrypted /tmp/filecrypter-batch/restored
printf "first\n" > /tmp/filecrypter-batch/input/first.txt
printf "second\n" > /tmp/filecrypter-batch/input/second.txt

dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  batch-encrypt /tmp/filecrypter-batch/encrypted \
  /tmp/filecrypter-batch/input/first.txt \
  /tmp/filecrypter-batch/input/second.txt \
  --password demo

dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  batch-decrypt /tmp/filecrypter-batch/restored \
  /tmp/filecrypter-batch/encrypted/first.txt.encrypted \
  /tmp/filecrypter-batch/encrypted/second.txt.encrypted \
  --password demo

cmp /tmp/filecrypter-batch/input/first.txt /tmp/filecrypter-batch/restored/first.txt
cmp /tmp/filecrypter-batch/input/second.txt /tmp/filecrypter-batch/restored/second.txt
```

Archive round trip:

```bash
mkdir -p /tmp/filecrypter-archive/input /tmp/filecrypter-archive/archives /tmp/filecrypter-archive/restored
printf "first\n" > /tmp/filecrypter-archive/input/first.txt
printf "second\n" > /tmp/filecrypter-archive/input/second.txt

dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  archive-encrypt /tmp/filecrypter-archive/archives \
  /tmp/filecrypter-archive/input/first.txt \
  /tmp/filecrypter-archive/input/second.txt \
  --archive-name demo --password demo

dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  archive-decrypt /tmp/filecrypter-archive/archives/demo.tar.zst.encrypted \
  /tmp/filecrypter-archive/restored --password demo

cmp /tmp/filecrypter-archive/input/first.txt /tmp/filecrypter-archive/restored/first.txt
cmp /tmp/filecrypter-archive/input/second.txt /tmp/filecrypter-archive/restored/second.txt
```

Settings:

```bash
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- settings show
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- settings set compression-default on
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- settings set compression-default off
```

## Safety Notes

- Keep passwords and key files. FileCrypter cannot recover forgotten passwords or lost or changed key files.
- Existing key files are capped at 16 MiB to avoid accidental large-file selection.
- Original input files are left unchanged.
- Existing output files are not overwritten unless `--overwrite` is supplied; otherwise FileCrypter auto-renames the new output.
- Use `archive-decrypt` for `.tar.zst.encrypted` archives and `decrypt` for single-file `.encrypted` files.

## Run The GUI

The Avalonia desktop app is an early shell around the same core library:

```bash
dotnet run --project src/FileCrypter.App/FileCrypter.App.csproj
```

Current GUI scope:

- windowed Avalonia 12 app with persistent sidebar navigation
- implemented Encrypt page for single-file encryption
- placeholder Decrypt, Batch, Help, and Settings pages
- view model tests for navigation and the first encrypt workflow

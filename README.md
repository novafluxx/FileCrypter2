# FileCrypter

FileCrypter is a local-first file encryption product for protecting files before storage, transfer, backup, or sharing. Sensitive operations happen on the local machine; there is no cloud service, account, or remote processing dependency.

The current implementation is a reusable .NET crypto core, a command-line host, and an Avalonia desktop UI for the same local workflows.

## Current Capabilities

FileCrypter currently supports:

- single-file encrypt and decrypt
- optional Zstandard compression for single-file encryption
- password plus existing key-file protection
- generated key files for encryption
- batch individual-file encrypt and decrypt
- encrypted compressed tar archives with archive extraction
- header inspection without a password, reporting payload kind, compression, and key-file requirement
- integrity verification that authenticates every chunk without writing any output
- shared local settings for compression, overwrite behavior, output directory, and desktop theme preference
- safe staged writes, overwrite protection, auto-renamed outputs, and CLI progress reporting
- a documented v1 encrypted file format in `docs/file-format.md`
- an Avalonia 12 desktop app with Encrypt, Decrypt, Batch, Settings, and Help workflows

The CLI writes final output paths to stdout and progress, warnings, and errors to stderr.

## Solution Layout

```text
FileCrypterDotNet/
  FileCrypterDotNet.slnx
  LICENSE
  NOTICE
  THIRD-PARTY-NOTICES.md
  src/
    FileCrypter.Core/
    FileCrypter.Cli/
    FileCrypter.Desktop/
  tests/
    FileCrypter.Core.Tests/
    FileCrypter.Cli.Tests/
    FileCrypter.Desktop.Tests/
  docs/
    file-format.md
    product.md
    security.md
    security-todos.md
```

## Documentation

- [Product outline](docs/product.md)
- [Encrypted file format](docs/file-format.md)
- [Security implementation notes](docs/security.md)
- [Accepted residual security risks](docs/security-todos.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

## Requirements

The repository targets .NET 10 and pins the SDK through `global.json`.

## Build And Test

```bash
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
```

Tests use Microsoft Testing Platform (MTP), selected in `global.json`. To run
one project or collect coverage:

```bash
dotnet test --project tests/FileCrypter.Core.Tests --no-build
dotnet test --no-build --coverlet --coverlet-output-format cobertura
```

When run from the repository root, coverage reports are written to `TestResults/`
with a separate timestamped report for each test project.
`tests/Directory.Build.targets` disables registration of the MTP telemetry
extension and selects the MTP runner for direct test executable launches too.
The VSTest adapter packages remain available for editor compatibility.

## Run The CLI

From the repository root:

```bash
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- --help
```

General command shape:

```text
filecrypter encrypt <input> [output] [--password-stdin | --password <password>] [--key-file <path> | --generate-key-file <path>] [--compress] [--overwrite]
filecrypter decrypt <input> [output] [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
filecrypter inspect <input>
filecrypter verify <input> [--password-stdin | --password <password>] [--key-file <path>]
filecrypter batch-encrypt <output-directory> <input>... [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
filecrypter batch-decrypt <output-directory> <input>... [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
filecrypter archive-encrypt <output-archive-or-directory> <input>... [--archive-name <name>] [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
filecrypter archive-decrypt <input-archive> <output-directory> [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
filecrypter settings show
filecrypter settings set compression-default <on|off>
filecrypter settings set overwrite-default <on|off>
filecrypter settings set output-directory <path|clear>
```

If no password option is supplied, the CLI prompts interactively without echoing the password. For automation, pipe one password line to `--password-stdin`. Avoid `--password <password>` when possible because command-line arguments can be exposed through shell history, process listings, logs, terminal scrollback, and crash reports; FileCrypter prints a warning when it is used.

When using `dotnet run`, put the FileCrypter arguments after `--`:

```bash
printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- encrypt <input> [output] --password-stdin
```

## Quick Smoke Tests

Single-file round trip:

```bash
printf "hello from FileCrypter\n" > /tmp/filecrypter-demo.txt

printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  encrypt /tmp/filecrypter-demo.txt /tmp/filecrypter-demo.txt.encrypted \
  --password-stdin --overwrite

printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  decrypt /tmp/filecrypter-demo.txt.encrypted /tmp/filecrypter-demo-restored.txt \
  --password-stdin --overwrite

cmp /tmp/filecrypter-demo.txt /tmp/filecrypter-demo-restored.txt
```

Inspect an encrypted file without a password:

```bash
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  inspect /tmp/filecrypter-demo.txt.encrypted
```

This reads only the 64-byte header and reports the format version, payload kind, compression, key-file requirement, chunk size, and Argon2id parameters. Those values are what the file claims; they are not authenticated. Use it to tell a single-file `.encrypted` apart from a `.tar.zst.encrypted` archive, or to check whether a key file is required, before committing to a decrypt.

Verify integrity without writing output:

```bash
printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  verify /tmp/filecrypter-demo.txt.encrypted --password-stdin
```

Verification decrypts to nothing and authenticates every chunk, so it detects a wrong password, a wrong key file, corruption, truncation, and tampering. It creates no files. For archive payloads it authenticates the archive bytes but does not check the archive entry structure.

Compressed single-file encryption:

```bash
printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  encrypt /tmp/filecrypter-demo.txt /tmp/filecrypter-demo.compressed.encrypted \
  --password-stdin --compress --overwrite
```

Generated key-file round trip:

```bash
printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  encrypt /tmp/filecrypter-demo.txt /tmp/filecrypter-demo.keyed.encrypted \
  --password-stdin --generate-key-file /tmp/filecrypter-demo.key --overwrite

printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  decrypt /tmp/filecrypter-demo.keyed.encrypted /tmp/filecrypter-demo-keyed-restored.txt \
  --password-stdin --key-file /tmp/filecrypter-demo.key --overwrite

cmp /tmp/filecrypter-demo.txt /tmp/filecrypter-demo-keyed-restored.txt
```

Batch individual-file round trip:

```bash
mkdir -p /tmp/filecrypter-batch/input /tmp/filecrypter-batch/encrypted /tmp/filecrypter-batch/restored
printf "first\n" > /tmp/filecrypter-batch/input/first.txt
printf "second\n" > /tmp/filecrypter-batch/input/second.txt

printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  batch-encrypt /tmp/filecrypter-batch/encrypted \
  /tmp/filecrypter-batch/input/first.txt \
  /tmp/filecrypter-batch/input/second.txt \
  --password-stdin

printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  batch-decrypt /tmp/filecrypter-batch/restored \
  /tmp/filecrypter-batch/encrypted/first.txt.encrypted \
  /tmp/filecrypter-batch/encrypted/second.txt.encrypted \
  --password-stdin

cmp /tmp/filecrypter-batch/input/first.txt /tmp/filecrypter-batch/restored/first.txt
cmp /tmp/filecrypter-batch/input/second.txt /tmp/filecrypter-batch/restored/second.txt
```

Archive round trip:

```bash
mkdir -p /tmp/filecrypter-archive/input /tmp/filecrypter-archive/archives /tmp/filecrypter-archive/restored
printf "first\n" > /tmp/filecrypter-archive/input/first.txt
printf "second\n" > /tmp/filecrypter-archive/input/second.txt

printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  archive-encrypt /tmp/filecrypter-archive/archives \
  /tmp/filecrypter-archive/input/first.txt \
  /tmp/filecrypter-archive/input/second.txt \
  --archive-name demo --password-stdin

printf "demo\n" | dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- \
  archive-decrypt /tmp/filecrypter-archive/archives/demo.tar.zst.encrypted \
  /tmp/filecrypter-archive/restored --password-stdin

cmp /tmp/filecrypter-archive/input/first.txt /tmp/filecrypter-archive/restored/first.txt
cmp /tmp/filecrypter-archive/input/second.txt /tmp/filecrypter-archive/restored/second.txt
```

Settings:

```bash
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- settings show
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- settings set compression-default on
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- settings set compression-default off
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- settings set overwrite-default off
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- settings set output-directory /tmp/filecrypter-out
dotnet run --project src/FileCrypter.Cli/FileCrypter.Cli.csproj -- settings set output-directory clear
```

Settings are shared with the desktop app. `overwrite-default` and `output-directory` are stored for the desktop app and do not change what an explicit CLI option does; `overwrite-default on` means outputs replace existing files, and `off` keeps them and auto-renames instead.

## Safety Notes

- Keep passwords and key files. FileCrypter cannot recover forgotten passwords or lost or changed key files.
- Prefer the hidden interactive prompt or `--password-stdin`; `--password <password>` is supported for compatibility but can expose secrets outside FileCrypter.
- Existing key files are capped at 16 MiB to avoid accidental large-file selection.
- Original input files are left unchanged.
- Existing output files are not overwritten unless `--overwrite` is supplied; otherwise FileCrypter auto-renames the new output.
- Use `archive-decrypt` for `.tar.zst.encrypted` archives and `decrypt` for single-file `.encrypted` files. Run `inspect` if you are unsure which one a file is; it needs no password.
- `inspect` reports unauthenticated header metadata and never proves a file is intact. Use `verify` for that.

## Run The GUI

The Avalonia desktop app wraps the same core library:

```bash
dotnet run --project src/FileCrypter.Desktop/FileCrypter.Desktop.csproj
```

Current GUI scope:

- windowed Avalonia 12 app with persistent sidebar navigation
- implemented Encrypt and Decrypt pages for single-file workflows
- automatic header inspection on the Decrypt page, reporting payload kind, compression, and key-file requirement as soon as a file is selected
- implemented Batch page for individual-file and encrypted-archive workflows
- implemented Settings page for compression, overwrite, output-directory, and theme preferences
- implemented Help page with recovery guidance, troubleshooting, version details, and update-status messaging
- drag/drop source-file support for Encrypt, Decrypt, and Batch workflows
- copy actions for visible issue text and result details
- view model tests for navigation, workflow validation, settings propagation, clipboard behavior, and help/update state

## Package The macOS App

For local macOS testing, the repository now includes a bundle script that creates a true `.app` wrapper around the published GUI output:

```bash
./scripts/package-macos-app.sh
```

This produces:

```text
artifacts/macos/FileCrypter.app
```

Launch it with:

```bash
open artifacts/macos/FileCrypter.app
```

Notes:

- this first macOS bundle flow is framework-dependent, so `.NET 10` must be installed on the launch machine
- the bundle includes the GUI app and `FileCrypter.Core`, but not the CLI host
- the script applies an ad-hoc code signature for local launch and verification
- notarization, icon conversion, and DMG packaging are intentionally deferred for a later slice
- Avalonia Parcel remains an optional later path if the project moves to richer macOS distribution automation

## License

FileCrypter is licensed under the [Apache License 2.0](LICENSE). Third-party
libraries and assets retain their respective licenses as documented in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

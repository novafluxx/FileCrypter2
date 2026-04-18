# FileCrypter GUI Plan

## Direction

The first graphical FileCrypter host is a desktop-first Avalonia app using:

- Avalonia 12.0.1
- .NET 10
- CommunityToolkit.Mvvm
- MVVM view models that depend on app services and `FileCrypter.Core`

The app is a normal desktop window with persistent left sidebar navigation and a main content area. The sidebar carries FileCrypter branding and version text at the top, primary workflow navigation in the middle, and Help/Settings near the bottom.

## Project Shape

The initial app lives in:

```text
src/FileCrypter.App/
```

The GUI references `FileCrypter.Core` directly and does not reference `FileCrypter.Cli`. The CLI and GUI are peer hosts over the same core.

View model tests live in:

```text
tests/FileCrypter.App.Tests/
```

## First Workflow

The first implemented page is single-file encryption. It supports:

- source file path
- output file path, with core auto-renaming when overwrite protection is enabled
- password entry
- optional Zstandard compression
- optional existing key file
- optional generated key-file path
- progress display
- final encrypted output path
- user-facing errors

Decrypt, Batch, Help, and Settings are navigation placeholders in the first slice.

## App Services

The app layer owns host concerns that should not leak into the core:

- `IFilePickerService` adapts Avalonia storage pickers to local paths.
- `IFileCrypterWorkflowService` adapts GUI workflow requests to `FileCrypter.Core`.

The first desktop slice uses existing core path APIs. Future mobile/browser hosts can introduce storage abstractions once the desktop workflow is proven.

## Safety

The GUI must not store passwords, key-file bytes, or plaintext contents. It should keep the product warning visible: forgotten passwords and lost or changed key files cannot be recovered.

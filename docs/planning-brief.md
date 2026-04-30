# FileCrypter Planning Brief

## Purpose

This document captures the initial implementation direction for FileCrypter after reviewing the product outline and discussing the stack. It is intended to feed a more detailed planning pass.

FileCrypter should be built as a local-first file encryption product with a reusable crypto core, an early command-line interface, and a later Avalonia-based cross-platform UI.

The primary architectural decision is to implement and test the core encryption engine before building the graphical app.

## Product Direction

FileCrypter is a local file encryption app for protecting files before storage, transfer, backup, or sharing.

The app should:

- Encrypt and decrypt files locally.
- Support password-only protection.
- Support optional password plus key-file protection.
- Support single-file workflows.
- Support batch workflows.
- Support creating and extracting encrypted archives.
- Avoid destructive changes by default.
- Avoid overwriting existing files by default.
- Process large files with bounded memory usage.
- Provide clear progress, completion, and error feedback.
- Define a new versioned encrypted file format.
- Avoid cloud, account, telemetry, recovery, escrow, or remote processing assumptions.

## Recommended Stack

Use one git repository with one .NET solution containing multiple projects.

Initial stack:

- .NET 10
- C#
- xUnit for tests
- A command-line app as the first application host
- A UI-independent core library

Later UI stack:

- Avalonia 12 or newer
- MVVM-oriented app structure
- Desktop first if needed, but with mobile support considered from the beginning

Supporting libraries to evaluate:

- Argon2id library for password-based key derivation
- .NET `System.Security.Cryptography.AesGcm` for AES-256-GCM, or a well-maintained equivalent AEAD library
- ZSTD compression library
- `System.Formats.Tar` for archive mode
- Command-line parsing library for the CLI, unless the initial CLI is intentionally minimal

## Repository Shape

Recommended starting layout:

```text
FileCrypterDotNet/
  FileCrypterDotNet.slnx

  src/
    FileCrypter.Core/
      FileCrypter.Core.csproj

    FileCrypter.Cli/
      FileCrypter.Cli.csproj

  tests/
    FileCrypter.Core.Tests/
      FileCrypter.Core.Tests.csproj

  docs/
    planning-brief.md
    file-format.md
    security.md

  product.md
  README.md
  .gitignore
```

Later UI layout options:

```text
src/
  FileCrypter.Desktop/
    FileCrypter.Desktop.csproj
```

or, if platform-specific host projects become useful:

```text
src/
  FileCrypter.Desktop/
  FileCrypter.Android/
  FileCrypter.iOS/
```

The exact UI project shape should wait until the core and CLI establish the application model.

## Project Responsibilities

### FileCrypter.Core

This is the product engine. It must not reference Avalonia, console APIs, or platform-specific UI code.

Responsibilities:

- Encrypted file format reader and writer
- Streaming encryption
- Streaming decryption
- Password-based key derivation
- Optional key-file protection
- Compression integration
- Archive creation and extraction orchestration
- Output overwrite handling
- Temporary-file staging and final promotion
- Progress models
- Cancellation behavior
- Domain-specific errors
- Safety checks for malformed, unsupported, truncated, or tampered files

The core should be designed as a reusable library consumed by the CLI, tests, and later UI apps.

### FileCrypter.Cli

This is the first application host and the first real consumer of the core.

Responsibilities:

- Parse commands and options
- Accept filesystem paths
- Open input and output streams
- Display progress
- Print clear success and error messages
- Return useful process exit codes
- Exercise real file workflows on macOS and Windows

The CLI is not only a user-facing tool. It is also a practical development tool for validating the core before the UI exists.

### FileCrypter.Core.Tests

This project validates the core behavior without depending on any UI.

Responsibilities:

- Round-trip tests
- Wrong password tests
- Wrong key-file tests
- Missing key-file tests
- Tampered header tests
- Tampered chunk tests
- Truncated file tests
- Empty file tests
- Large-file streaming tests
- Overwrite protection tests
- Compression tests
- Future file-format compatibility tests

## Dependency Direction

Dependencies should point inward toward the core:

```text
FileCrypter.Core
  ^
  |
  +-- FileCrypter.Cli
  |
  +-- FileCrypter.Core.Tests
  |
  +-- FileCrypter.Desktop
```

Rules:

- Core must not depend on CLI.
- Core must not depend on Avalonia.
- Core must not depend on mobile or desktop UI concerns.
- UI and CLI should adapt their input/output models to the core.
- Platform-specific file picker, storage, permission, and sharing behavior should stay outside the core.

## Core API Direction

The core should expose request and result models rather than UI-shaped methods.

Candidate API shape:

```csharp
public sealed record EncryptRequest(
    Stream Plaintext,
    Stream Ciphertext,
    ReadOnlyMemory<char> Password,
    KeyFileMaterial? KeyFile,
    CompressionMode CompressionMode,
    bool LeaveOpen);

public sealed record DecryptRequest(
    Stream Ciphertext,
    Stream Plaintext,
    ReadOnlyMemory<char> Password,
    KeyFileMaterial? KeyFile,
    bool LeaveOpen);

public interface IFileCrypter
{
    Task<EncryptResult> EncryptAsync(
        EncryptRequest request,
        IProgress<CryptoProgress>? progress,
        CancellationToken cancellationToken);

    Task<DecryptResult> DecryptAsync(
        DecryptRequest request,
        IProgress<CryptoProgress>? progress,
        CancellationToken cancellationToken);
}
```

The exact API can change, but the design intent should remain:

- Stream-oriented
- Async
- Cancellation-aware
- Progress-aware
- Independent of UI controls
- Independent of command-line parsing
- Friendly to desktop and mobile storage adapters

## File Format Direction

The new implementation should define its own encrypted file format. Binary compatibility with the previous FileCrypter app is not required.

The v1 format should include:

- Magic bytes
- Format version
- Algorithm identifiers
- KDF parameters
- Salt
- Nonce or nonce-base material
- Chunk size
- Flags for compression and key-file requirement
- Authenticated metadata
- Chunk authentication tags
- A clear end-of-stream or final-chunk mechanism

Security requirements:

- Use authenticated encryption.
- Detect wrong passwords.
- Detect wrong key files.
- Detect tampering.
- Detect truncation.
- Reject unsupported versions safely.
- Reject malformed metadata safely.
- Avoid unauthenticated interpretation of sensitive metadata.

Documentation requirements:

- Create `docs/file-format.md`.
- Include field names, sizes, encodings, and validation rules.
- Document compatibility expectations for future versions.
- Include test-vector guidance.

## Crypto Direction

Recommended baseline:

- AES-256-GCM or security-equivalent modern AEAD
- Argon2id or security-equivalent memory-hard KDF
- 16-byte salt or stronger
- 32-byte derived key
- Unique nonce material per encrypted file
- Chunk-level authentication
- Header or metadata authentication
- Default chunk size around 1 MiB

Key-file behavior:

- Key files are optional.
- Encryption may use an existing key file.
- Encryption may generate a new key file.
- Generated key files should contain cryptographically random bytes.
- At least 32 random bytes should be generated.
- Files encrypted with a key file require the password and matching key file for decryption.
- The app must warn that lost or modified key files make recovery impossible.
- Existing key files should have a documented maximum size.

Sensitive data handling:

- Do not store passwords.
- Do not store key-file contents.
- Do not upload file contents.
- Do not upload passwords.
- Do not upload key material.
- Clear sensitive buffers where practical.
- Keep user-facing errors clear but sanitized.

## Compression And Archive Direction

Compression:

- Use ZSTD unless evaluation reveals a blocker.
- Single-file encryption should eventually allow compression to be optional.
- Batch encryption should enable compression automatically.
- Archive mode should produce compressed encrypted archives.
- Decryption should detect and reverse supported compression automatically.

Archive mode:

- Use tar as the archive container unless evaluation reveals a blocker.
- Compress the archive before or during encryption.
- Encrypt the compressed archive stream.
- Support extraction into a chosen output directory.
- Respect overwrite protection during extraction.

## Output Safety Direction

Overwrite protection is a core product safety feature, not just UI behavior.

Requirements:

- Original files remain unchanged by default.
- Existing output files are not overwritten by default.
- If overwrite protection is enabled and the target exists, generate a safe alternative name.
- Writes should go to temporary files first.
- Temporary files should be promoted only after successful completion.
- Failed or canceled operations should not leave partial files at the final destination path.
- Permission errors should be surfaced as clear domain errors.

## Mobile Readiness Principles

The core should be mobile-ready even before the mobile UI exists.

Design implications:

- Prefer streams and abstractions over raw filesystem assumptions.
- Keep path validation and path selection concerns separable.
- Do not assume every platform exposes normal writable paths.
- Do not assume drag-and-drop exists.
- Do not assume long-running operations can block a foreground thread.
- Keep progress and cancellation first-class.
- Keep user messages outside the crypto engine where possible.

The CLI may use normal filesystem paths. The core should remain usable by future Avalonia Android and iOS hosts that adapt platform storage APIs into streams or output handles.

## Suggested Implementation Phases

### Phase 0: Repository And Solution Setup

Goals:

- Create solution.
- Create core project.
- Create CLI project.
- Create test project.
- Add project references.
- Add baseline README.
- Add initial docs placeholders.
- Verify `dotnet build` and `dotnet test`.

Deliverables:

- `FileCrypterDotNet.slnx`
- `src/FileCrypter.Core`
- `src/FileCrypter.Cli`
- `tests/FileCrypter.Core.Tests`
- Passing empty test suite

### Phase 1: File Format Draft

Goals:

- Draft `docs/file-format.md`.
- Choose v1 magic bytes.
- Choose header fields.
- Choose chunk structure.
- Choose authenticated metadata approach.
- Define error behavior for unsupported and malformed files.

Deliverables:

- File format draft
- Header model types
- Parser validation tests

### Phase 2: Password-Only Streaming Encryption

Goals:

- Implement password-only encryption.
- Implement password-only decryption.
- Use bounded memory.
- Provide progress.
- Provide cancellation.
- Stage output safely.

Deliverables:

- Core encrypt/decrypt round trip
- CLI encrypt/decrypt commands
- Tests for empty files, small files, and larger streamed files

### Phase 3: Failure And Tamper Behavior

Goals:

- Validate wrong password behavior.
- Validate truncated file behavior.
- Validate tampered header behavior.
- Validate tampered chunk behavior.
- Convert low-level crypto failures into domain errors.

Deliverables:

- Negative-path tests
- Clear CLI error output
- Documented domain errors

### Phase 4: Key-File Protection

Goals:

- Add existing key-file support.
- Add key-file generation.
- Define key-file size limits.
- Validate wrong, missing, and modified key-file behavior.

Deliverables:

- Key-file encryption and decryption
- Key-file CLI options
- Tests for key-file success and failure paths

### Phase 5: Compression

Goals:

- Add ZSTD compression for encryption.
- Add automatic decompression for decryption.
- Ensure metadata identifies compression.
- Preserve streaming behavior.

Deliverables:

- Compressed round-trip tests
- CLI compression option
- Documentation of compression metadata

### Phase 6: Batch And Archive Foundation

Goals:

- Add batch orchestration.
- Add per-file result reporting.
- Add archive creation.
- Add archive extraction.
- Respect overwrite safety.

Deliverables:

- Batch encryption and decryption tests
- Archive round-trip tests
- CLI batch/archive commands

### Phase 7: Avalonia UI Planning

Goals:

- Review core API from UI perspective.
- Decide Avalonia project shape.
- Decide desktop-first or shared app-first structure.
- Define view models around core request/result types.
- Identify mobile storage adapter needs.

Deliverables:

- UI architecture plan
- Avalonia project scaffold
- First thin UI workflow using the tested core

## Initial CLI Shape

Candidate commands:

```text
filecrypter encrypt <input> [--output <path>] [--password <password>] [--key-file <path>] [--compress]
filecrypter decrypt <input> [--output <path>] [--password <password>] [--key-file <path>]
filecrypter keygen <output>
filecrypter inspect <input>
```

Notes:

- The CLI should support interactive password entry.
- Passing passwords as command-line arguments is useful for tests but risky for normal use.
- The CLI should warn or document when password arguments may be visible in shell history or process listings.
- `inspect` should only show non-sensitive metadata.

## Planning Questions

The detailed planning pass should answer:

- Should the solution use `.slnx` or classic `.sln`?
- Which Argon2id package should be used?
- Which ZSTD package should be used?
- Should v1 use AES-256-GCM exactly, or an equivalent AEAD with easier nonce ergonomics?
- How should key-file material be combined with password-derived material?
- What is the maximum supported key-file size?
- What file extension should v1 encrypted files use?
- Should archive outputs use `.tar.zst.encrypted`, `.fcrypt`, or both?
- What is the exact temporary-file naming strategy?
- What domain error model should the core expose?
- How large should default chunks be?
- Should test vectors be deterministic with injected randomness for tests?
- What is the minimum supported OS matrix for the CLI?
- When should the Avalonia app be scaffolded?

## Definition Of Done For The Core Foundation

The core foundation is ready for UI work when:

- Password-only encrypt/decrypt works through the CLI.
- Key-file encrypt/decrypt works through the CLI.
- Compressed encrypt/decrypt works through the CLI.
- Wrong password fails safely.
- Wrong key file fails safely.
- Tampered files fail safely.
- Truncated files fail safely.
- Empty files round-trip successfully.
- Large files process without memory scaling with file size.
- Output overwrite protection works.
- Temporary output behavior avoids partial final files.
- File format v1 is documented.
- Core tests cover success, failure, and format validation paths.

## Recommended Next Step

Use this document to create an implementation plan for Phase 0 through Phase 2.

The first concrete coding milestone should be:

1. Scaffold the solution, core, CLI, and test projects.
2. Add a tiny core API placeholder.
3. Add a CLI command placeholder that references the core.
4. Add one smoke test.
5. Confirm `dotnet build` and `dotnet test` pass.

After that, plan and implement the v1 file format draft before writing the real encryption pipeline.

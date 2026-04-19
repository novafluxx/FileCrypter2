# FileCrypter Product Outline

## 1. Purpose

FileCrypter is a local file encryption app for protecting files before storage, transfer, backup, or sharing. It should provide feature parity with the current FileCrypter user experience while staying independent of any specific implementation stack.

The product should make strong file encryption approachable without requiring users to understand cryptographic details. Sensitive operations should happen locally on the user's device, and the default behavior should avoid destructive file changes.

## 2. Product Goals

- Provide a simple, trustworthy workflow for encrypting and decrypting files.
- Support both single-file and multi-file workflows.
- Allow users to bundle multiple files into one encrypted archive.
- Protect users from accidental overwrites by default.
- Support large files with bounded memory usage.
- Offer optional compression to reduce encrypted output size.
- Offer optional key-file protection for users who want a second factor.
- Provide clear progress, completion, and error feedback.
- Define a stable, versioned encrypted file format for the new implementation.
- Avoid cloud dependency, account dependency, or remote processing.

## 3. Product Objectives

### Usability Objectives

- A new user should be able to encrypt a file without reading documentation.
- A returning user should be able to decrypt a file using the same password and optional key file.
- Common failure states should explain what the user can try next without exposing sensitive internals.
- The product should clearly warn users that forgotten passwords or lost key files cannot be recovered.

### Safety Objectives

- Original input files should remain unchanged unless the user explicitly chooses otherwise.
- Output files should not overwrite existing files by default.
- Failed operations should not leave partial files at the final destination path.
- The app should reject risky paths, unsupported formats, and malformed encrypted files safely.

### Security Objectives

- File contents, passwords, and key file material should remain local to the user's device.
- Passwords and key files should not be stored by the app.
- Encryption should provide confidentiality and tamper detection.
- File formats should include enough metadata to decrypt supported outputs later.

## 4. Non-Goals

- Cloud storage or cloud sync.
- User accounts.
- Server-side encryption or remote file processing.
- Password reset, recovery, escrow, or backdoor access.
- Built-in recipient identity management.
- Public-key encryption, unless added as a future feature.
- Secure deletion of original files, unless added as an advanced future feature.
- Enterprise key management.
- Binary compatibility with encrypted files produced by the current FileCrypter app.

## 5. Target Users

- Individuals protecting personal files before upload, backup, transfer, or storage.
- Small business users who need a simple local encryption utility.
- Technical users who want predictable local encryption behavior.
- Users who need to encrypt many files at once.
- Users who prefer one encrypted archive for a folder-like collection of files.

## 6. Core Product Areas

The product should provide these primary areas:

- Encrypt
- Decrypt
- Batch
- Settings
- Help

The navigation model, layout, and visual system may vary by implementation, but these areas should be discoverable and easy to move between.

## 7. Feature Requirements

### 7.1 Single File Encryption

The user should be able to:

- Select one input file.
- Optionally drag and drop the input file where the platform supports it.
- Choose an output location and filename.
- Let the app auto-generate an output filename when no destination is chosen.
- Enter a password.
- See password validity and strength feedback.
- Optionally enable compression before encryption.
- Optionally select or generate a key file.
- Start encryption.
- See progress while encryption runs.
- See the final output path when encryption succeeds.
- Receive clear feedback when encryption fails.
- Copy a share/recipient message after successful encryption.

Default behavior:

- The original file remains unchanged.
- The default encrypted filename appends `.encrypted`.
- The output location defaults to the input file's folder unless the user has configured another default.
- Existing output files are not overwritten by default.

### 7.2 Single File Decryption

The user should be able to:

- Select one encrypted input file.
- Optionally drag and drop the encrypted file where the platform supports it.
- Choose an output location and filename.
- Let the app auto-generate an output filename.
- Enter the password used during encryption.
- Provide the original key file if the encrypted file requires one.
- Start decryption.
- See progress while decryption runs.
- See the final output path when decryption succeeds.
- Receive clear feedback when decryption fails.

Expected behavior:

- Compressed encrypted files should be detected and decompressed automatically.
- If the file was encrypted with a key file, decryption must require the matching key file.
- Wrong passwords, wrong key files, corrupted files, and tampered files should fail safely.

### 7.3 Batch Individual Files

The user should be able to:

- Choose batch encryption or batch decryption.
- Select multiple files.
- Add files through drag and drop where supported.
- Remove individual files from the selection.
- Clear the full selection.
- Choose an output directory.
- Enter one password for the batch.
- Optionally provide one key file for the batch.
- Confirm before starting the batch operation.
- See aggregate progress.
- See per-file success and failure results.

Batch encryption requirements:

- Each input file should produce one encrypted output file.
- Each output filename should append `.encrypted`.
- Compression should be enabled automatically for batch encryption.

Batch decryption requirements:

- Each encrypted input file should produce one decrypted output file.
- If the source filename ends in `.encrypted`, the generated output name should remove that extension.
- If the source filename does not end in `.encrypted`, the generated output name should use a safe fallback such as `.decrypted`.

Batch limits:

- A single batch run should support up to 1000 files.
- Per-file failures should not stop the entire batch unless the failure prevents the batch from continuing safely.

### 7.4 Batch Archive Mode

The user should be able to create one encrypted archive from multiple files and later decrypt/extract that archive.

Archive encryption requirements:

- Accept multiple input files.
- Bundle the files into one archive.
- Compress the archive before or during encryption.
- Encrypt the resulting archive.
- Save one encrypted archive output file.
- Allow an optional custom archive name.
- Auto-generate a timestamp-based archive name when the user leaves the name blank.
- Validate archive names against cross-platform unsafe filename characters.
- Show progress across archiving and encryption phases.

Archive decryption requirements:

- Accept exactly one encrypted archive input.
- Decrypt the archive.
- Extract the archive contents into the selected output directory.
- Respect overwrite protection.
- Show progress across decryption and extraction phases.

Reference behavior:

- Archive outputs may use a `.tar.zst.encrypted`-style filename when the chosen archive/compression format matches that convention.

### 7.5 Compression

The product should support compression for encryption workflows.

Requirements:

- Single-file encryption should allow compression to be toggled by the user.
- A setting should allow compression to be enabled by default for single-file encryption.
- Batch individual-file encryption should use compression automatically.
- Archive mode should create a compressed archive before encrypting it or otherwise produce equivalent compressed encrypted output.
- Decryption should automatically identify and decompress supported compressed encrypted files.
- The UI should explain that compression helps most with text, documents, and similar files, and helps less with already-compressed media or archives.

Reference behavior:

- ZSTD is a strong default choice for compression because it is fast and effective for many document/text workloads.

### 7.6 Key File Protection

The product should support optional key-file protection.

Requirements:

- Users can select an existing key file during encryption.
- Users can generate a new key file during encryption.
- Generated key files should contain cryptographically random bytes.
- Files encrypted with a key file require both the password and the same key file for decryption.
- The app should warn that losing or modifying the key file makes recovery impossible.
- Key file generation should only appear in encryption workflows.
- The same key file can be used across all files in a batch.

Reference behavior:

- Generated key files should use at least 32 cryptographically random bytes.
- Existing key files should have a documented maximum size to prevent accidental large-file selection or memory pressure.

### 7.7 Output Safety and Overwrite Handling

The product should prevent accidental data loss by default.

Requirements:

- "Never overwrite existing files" should be enabled by default.
- When overwrite protection is enabled and the destination exists, the app should auto-rename the output.
- Auto-renamed files should use a clear suffix such as `name (1)`.
- Users can disable overwrite protection when they intentionally want to replace destination files.
- Writes should be staged through temporary files and promoted only after successful completion.
- Failed or canceled operations should not leave partial output at the final destination path.

### 7.8 Settings

The product should persist user preferences locally.

For low-risk desktop preferences such as defaults and appearance, the app should apply and persist changes automatically when practical rather than requiring an explicit save step.

Settings should include:

- Theme preference: Light, Dark, or System.
- Default compression for single-file encryption.
- Default overwrite protection.
- Default output directory.
- Reset settings to defaults.

Optional local-only setting/metric:

- Count how many times the user copies the share/recipient message.

### 7.9 Updates

The product should support an update flow appropriate to the target platform.

Desktop parity requirements:

- Check for updates after startup.
- Notify the user when an update is available.
- Allow the user to update now or defer.
- Relaunch after update when required by the platform.

Platform-specific stores or package managers may replace the in-app update flow where appropriate.

### 7.10 Help and Troubleshooting

The product should include help content for:

- Encrypting one file.
- Decrypting one file.
- Batch individual-file mode.
- Batch archive mode.
- Compression behavior.
- Key-file protection.
- Output overwrite protection and auto-renaming.
- Large-file behavior.
- Updates.
- Password and recovery warnings.

Troubleshooting should cover:

- Incorrect password.
- Wrong or missing key file.
- Corrupted or incomplete encrypted file.
- Permission denied.
- Too many selected files.
- File picker or drag-and-drop issues.
- Unsupported encrypted format.

## 8. Security and Compatibility Requirements

### 8.1 Recommended Cryptographic Profile

To maintain the intended security posture, the implementation should use:

- Authenticated encryption with AES-256-GCM or a security-equivalent modern AEAD.
- Password-based key derivation with Argon2id or a security-equivalent memory-hard KDF.
- Unique salt per encrypted file.
- Unique nonce material per encrypted file.
- Chunk-level authentication for streaming operation.
- Header or metadata authentication so tampering is detected.

Recommended baseline profile:

- AES-256-GCM.
- Argon2id.
- 16-byte salt.
- 32-byte derived key.
- Argon2id memory cost: 64 MiB.
- Argon2id time cost: 3 iterations.
- Argon2id parallelism: 4.
- Default chunk size: 1 MB.

### 8.2 Streaming and Large Files

The product should process files in chunks rather than reading entire files into memory.

Requirements:

- Memory use should remain bounded relative to chunk size.
- Progress should be based on bytes or files processed.
- Empty files should encrypt and decrypt correctly.
- Decryption should validate chunk sizes and metadata before allocating large buffers.
- Malformed files should fail safely.

### 8.3 File Format Design

The new implementation does not need to read encrypted files produced by the current FileCrypter app. It should define its own stable encrypted file format.

The file format should support:

- Non-compressed streaming files.
- Compressed streaming files.
- Key-file protected streaming files.
- Compressed and key-file protected streaming files.

Requirements:

- Include a format version.
- Include enough metadata to identify compression and key-file requirements.
- Authenticate metadata so tampering is detected.
- Allow future versions to remain backward-compatible where practical.
- Reject unsupported, malformed, truncated, or tampered files safely.
- Document the format well enough for future maintenance and test-vector generation.

### 8.4 Local Sensitive Data Handling

Requirements:

- Do not upload file contents.
- Do not upload passwords.
- Do not upload key file material.
- Do not store passwords.
- Do not store key file contents.
- Clear password fields after operation attempts.
- Sanitize user-facing error messages.
- Minimize how long sensitive material remains in memory.

### 8.5 Filesystem Safety

Requirements:

- Avoid following symlinks in ways that could read or write unexpected paths.
- Use restrictive permissions for temporary and output files where the platform supports it.
- Prefer atomic or transaction-like writes.
- Validate output paths and filenames.
- Respect platform permission errors and explain them clearly.

## 9. Performance Objectives

- Single-file operations should show progress.
- Batch operations should show aggregate and per-file progress.
- Archive operations should show phase-aware progress.
- Large files should not cause memory spikes proportional to file size.
- Compression should be presented as a size/speed tradeoff.
- Batch operations should continue through recoverable per-file errors.

## 10. Usability and Accessibility Objectives

- Primary flows should be usable with standard file selection controls.
- Drag-and-drop should enhance workflows where available but should not be required.
- Processing states should disable conflicting actions.
- Mode switches that discard selected files or typed input should require confirmation.
- Error text should be selectable or copyable where useful.
- The app should support light and dark visual modes.
- Controls should remain readable and usable at small desktop window sizes.
- The app should avoid implying that forgotten passwords or lost key files can be recovered.

## 11. Suggested Information Architecture

The exact UI may vary, but the product should expose these concepts clearly:

- A primary encryption workflow.
- A primary decryption workflow.
- A multi-file workflow with individual and archive modes.
- A settings area for defaults and appearance.
- A help area for recovery warnings, workflow instructions, and troubleshooting.
- A visible app version or build identifier.
- A clear update notification area when updates are available.

## 12. Data and Privacy Principles

- The app should function without an account.
- The app should function without internet access except for update checks or external links.
- Any product metrics should be local-only unless a future telemetry policy and consent model are explicitly added.
- Local settings should not include passwords, key file contents, or plaintext file contents.

## 13. Quality Objectives

Before release, validate:

- Single-file encrypt/decrypt round trips.
- Single-file compressed encrypt/decrypt round trips.
- Key-file encrypt/decrypt round trips.
- Batch individual encrypt/decrypt success and partial-failure behavior.
- Archive encrypt/decrypt/extract round trips.
- Overwrite protection and auto-rename behavior.
- Empty file handling.
- Large file handling.
- Wrong password behavior.
- Wrong or missing key file behavior.
- Corrupted/tampered file behavior.
- Permission-denied behavior.
- Settings persistence.
- Update notification behavior, where applicable.
- New-format compatibility across future versions.

## 14. Acceptance Criteria

The product reaches feature parity when:

- A user can encrypt and decrypt one file using only a password.
- A user can encrypt and decrypt one file using a password plus key file.
- A user can encrypt and decrypt many files in one batch.
- A user can create one encrypted archive from many files and later decrypt/extract it.
- Compression works in single-file, batch, and archive workflows according to the requirements above.
- Existing files are protected from accidental overwrite by default.
- Progress and final status are visible for long-running operations.
- User-facing help explains passwords, key files, compression, overwrite behavior, and common errors.
- Sensitive operations are local-only.
- The new encrypted file format is versioned, documented, and suitable for future backward compatibility.

## 15. Open Questions

- Should public-key recipient encryption be added later?
- Should secure deletion of original files become an advanced option?
- Should mobile platforms be first-class release targets?
- Should the share/recipient message remain plain text, or become a structured export?
- Should cryptographic parameters remain fixed for simplicity, or become configurable for advanced users?

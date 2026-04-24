# Security

This document summarizes security-relevant implementation notes for the current FileCrypter core.

FileCrypter v1 uses AES-256-GCM with a 16-byte Argon2id salt, an 8-byte per-file nonce prefix, and a 32-bit chunk counter. The full 64-byte header and each 8-byte chunk prefix are authenticated as AAD, which binds algorithm identifiers, payload kind, chunk size, salt, nonce prefix, and Argon2 parameters to the ciphertext.

Argon2id parameters are bounded on both encryption and decryption. Memory must be `19456..1048576` KiB, iterations must be `2..64`, and parallelism must be `1..16`. Headers outside those ranges are rejected before key derivation.

Staged output files are created with private permissions: Unix uses `0600`, and Windows creates files with an explicit ACL for the current user. Settings writes reject symlinked destinations before replacing `settings.json`.

## Accepted Residual Risks

- Path validation still has a bounded TOCTOU window between pre-open symlink/path checks and later `FileStream` opens. Users should choose input, key-file, output, and settings paths inside directories they control. Output staging still uses randomized `CreateNew` temp files and final moves to reduce pre-existing symlink attacks.
- Passwords are accepted as `string` values. The core zeroes UTF-8 password byte copies, credential preimages, derived keys, and key-file buffers, but the original managed `string` cannot be reliably zeroed on the .NET heap.

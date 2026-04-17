# FileCrypter Encrypted File Format

This document defines the draft v1 FileCrypter encrypted file format. The format is new for the .NET implementation; compatibility with earlier FileCrypter outputs is not required.

Phase 1 documents and validates the header format. Phase 2 starts password-only encryption/decryption for uncompressed single-file streams. Compression, archive handling, and richer CLI inspection are implemented in later phases.

## Naming

Single-file and batch encrypted outputs append `.encrypted` by default.

Future archive outputs may use `.tar.zst.encrypted` when the payload is a tar archive compressed with Zstandard before encryption.

## Encoding Rules

- All integer fields are unsigned and little-endian.
- All fixed byte arrays are stored exactly as listed.
- Reserved fields must be zero.
- Unknown versions, flags, algorithms, payload kinds, and malformed metadata must be rejected before decrypting payload data.
- The v1 header is exactly 64 bytes.

## Header Layout

| Offset | Size | Field | Value |
| ---: | ---: | --- | --- |
| 0 | 8 | `Magic` | ASCII `FCRYPT\r\n` |
| 8 | 2 | `FormatVersion` | `1` |
| 10 | 2 | `HeaderLength` | `64` |
| 12 | 2 | `HeaderFlags` | bit `0` = key file required; all other bits reserved |
| 14 | 1 | `AeadAlgorithmId` | `1` = AES-256-GCM |
| 15 | 1 | `KdfAlgorithmId` | `1` = Argon2id |
| 16 | 1 | `CompressionAlgorithmId` | `0` = none, `1` = Zstd reserved |
| 17 | 1 | `PayloadKind` | `1` = single-file stream, `2` = tar archive reserved |
| 18 | 4 | `ChunkSize` | default `1048576`; valid range `65536` to `16777216`, multiple of `1024` |
| 22 | 16 | `Salt` | Argon2id salt |
| 38 | 8 | `NoncePrefix` | first 8 bytes of each AES-GCM nonce |
| 46 | 4 | `Argon2MemoryKiB` | default `65536` |
| 50 | 4 | `Argon2Iterations` | default `3` |
| 54 | 4 | `Argon2Parallelism` | default `4` |
| 58 | 1 | `Argon2Version` | `0x13` |
| 59 | 1 | `KeyFileHashAlgorithmId` | `0` = none, `1` = SHA-256 |
| 60 | 4 | `Reserved` | zero |

## Algorithm Defaults

AES-256-GCM is the v1 AEAD. Each chunk uses a 12-byte nonce and a 16-byte authentication tag.

Argon2id is the v1 password-based key derivation function. The derived key length is 32 bytes. The default parameters are:

- memory: `65536` KiB
- iterations: `3`
- parallelism: `4`
- salt length: `16` bytes
- Argon2 version: `0x13`

## Key-File Credential Binding

Key files are optional. When the key-file-required flag is set, the key-file hash algorithm must be SHA-256. When the flag is not set, the key-file hash algorithm must be none.

Existing key files are capped at 16 MiB to avoid accidental large-file selection and memory pressure. Generated key files are future Phase 4 work.

The future key derivation input is:

1. Encode the password as UTF-8 without normalization inside the core.
2. If a key file is supplied, hash the raw key-file bytes with SHA-256.
3. Feed Argon2id a length-prefixed credential preimage containing a domain label, password bytes, and the optional key-file digest.

## Chunk Layout

Encrypted payload data is a sequence of chunk frames.

| Offset | Size | Field | Value |
| ---: | ---: | --- | --- |
| 0 | 4 | `PlaintextLength` | plaintext length represented by this chunk |
| 4 | 2 | `ChunkFlags` | bit `0` = final chunk; all other bits reserved |
| 6 | 2 | `Reserved` | zero |
| 8 | `PlaintextLength` | `Ciphertext` | AES-GCM ciphertext |
| 8 + `PlaintextLength` | 16 | `Tag` | AES-GCM tag |

The nonce for chunk index `n` is:

```text
NoncePrefix[8] || LE32(n)
```

The associated authenticated data for each chunk is:

```text
Header[64] || ChunkFramePrefix[8]
```

Empty files emit one final chunk with `PlaintextLength = 0`. Files ending exactly on a chunk boundary emit a zero-length final chunk. No bytes may appear after the final chunk.

## Validation Rules

Readers must reject:

- inputs shorter than 64 bytes
- incorrect magic bytes
- unsupported format versions
- header lengths other than 64
- unknown header flags
- unsupported AEAD or KDF ids
- compression ids other than none or reserved Zstd
- payload kinds other than single-file stream or reserved tar archive
- chunk sizes outside the valid range or not divisible by 1024
- Argon2 memory, iteration, or parallelism values of zero
- Argon2 versions other than `0x13`
- key-file flag/hash combinations that do not match
- nonzero reserved bytes

Parser errors should carry stable machine-readable codes so the CLI and UI can show clear, sanitized messages.

## Compatibility

Version `1` readers must reject unsupported versions safely. Future versions may add new header layouts or algorithm identifiers, but v1 reserved fields must remain zero and v1 readers must not infer behavior from unknown bits or ids.

## Test Vectors

Future encryption phases should add deterministic test vectors using injected randomness for salt and nonce-prefix generation. Vectors should include the header bytes, plaintext, password, optional key-file bytes, ciphertext chunks, tags, and expected failure cases for tampering and truncation.

## References

- RFC 9106: Argon2 Memory-Hard Function for Password Hashing and Proof-of-Work Applications
- Microsoft Learn: `System.Security.Cryptography.AesGcm`
- NIST SP 800-38D: Recommendation for Block Cipher Modes of Operation: Galois/Counter Mode (GCM) and GMAC

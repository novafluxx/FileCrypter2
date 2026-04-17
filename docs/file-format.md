# FileCrypter Encrypted File Format

This document defines the draft v1 FileCrypter encrypted file format. The format is new for the .NET implementation; compatibility with earlier FileCrypter outputs is not required.

Phase 1 documents and validates the header format. Phase 2 implements password-only and password plus key-file encryption/decryption for single-file streams. Compression is implemented for core single-file streams; archive handling and richer CLI inspection are implemented in later phases.

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
| 16 | 1 | `CompressionAlgorithmId` | `0` = none, `1` = Zstd |
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

Existing key-file encryption/decryption is implemented. Existing key files are capped at 16 MiB to avoid accidental large-file selection and memory pressure. Generated key files are future Phase 4 work.

The key derivation input is:

1. Encode the password as UTF-8 without normalization inside the core.
2. When the header requires a key file, hash the raw key-file bytes with SHA-256.
3. Feed Argon2id a length-prefixed credential preimage containing a domain label, password bytes, and the optional key-file digest.

Supplying a key file while decrypting a password-only payload does not change the derived key, because password-only payloads do not include a key-file digest in the credential preimage.

## Chunk Layout

Encrypted payload data is a sequence of chunk frames. When `CompressionAlgorithmId` is `1`, the chunk plaintext is the Zstandard-compressed byte stream, and decryptors decompress it after chunk authentication succeeds.

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
- compression ids other than none or Zstd
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

The core test suite includes deterministic password-only vectors using injected randomness for salt and nonce-prefix generation. These vectors use:

- password: `correct horse battery staple`
- chunk size: `65536`
- Argon2 memory: `1024` KiB
- Argon2 iterations: `1`
- Argon2 parallelism: `1`
- key file: none
- compression: none

The `RandomStart` value means the injected random stream is sequential bytes beginning at that decimal value. For example, `RandomStart = 0` supplies salt bytes `00..0F` and nonce-prefix bytes `10..17`.

| Name | Plaintext | RandomStart | Encrypted length | Encrypted SHA-256 |
| --- | --- | ---: | ---: | --- |
| `empty` | empty byte string | 0 | 88 | `EBFBAC483FFB75A805D456718D1C680119CA127ED289153FF1E5004935795084` |
| `small-text` | UTF-8 `deterministic` | 0 | 101 | `236C3BCFE0BC19917619E64396C64310D4E381233FC4DC890A072A7A4B42B9AA` |
| `chunk-boundary` | byte sequence `00..FF` repeated to 65536 bytes | 24 | 65648 | `126F2EB854EEDA5BC3F599FC1F4A504E6B0F05B584C5155C51334875970A9DA9` |
| `multi-chunk` | byte sequence `FF..00` repeated to 65553 bytes | 48 | 65665 | `E3E3FAC63488BF0585F427875DAA0D8A02582A1F56C0B501C2CFD3B9FFDE4B5B` |

Expected frame prefixes and tags:

| Name | Chunk | Prefix | Tag |
| --- | ---: | --- | --- |
| `empty` | 0 | `0000000001000000` | `5005EAF8395E79184BB3EFC2482D76F3` |
| `small-text` | 0 | `0D00000001000000` | `62A01FB62BE218125E3A13BC157843C6` |
| `chunk-boundary` | 0 | `0000010000000000` | `CB60E5DC2A51F5762477B7373CE669A9` |
| `chunk-boundary` | 1 | `0000000001000000` | `E935FB59E33E5AB1157DA6A004BF03F1` |
| `multi-chunk` | 0 | `0000010000000000` | `1006D8D39F494D822E815878CC82E855` |
| `multi-chunk` | 1 | `1100000001000000` | `DFC4382DC1018277617AB5B18C99D67F` |

## References

- RFC 9106: Argon2 Memory-Hard Function for Password Hashing and Proof-of-Work Applications
- Microsoft Learn: `System.Security.Cryptography.AesGcm`
- NIST SP 800-38D: Recommendation for Block Cipher Modes of Operation: Galois/Counter Mode (GCM) and GMAC

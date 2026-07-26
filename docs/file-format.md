# FileCrypter Encrypted File Format

This document defines the draft v1 FileCrypter encrypted file format. The format is new for the .NET implementation; compatibility with earlier FileCrypter outputs is not required.

Phase 1 documents and validates the header format. Phase 2 implements password-only and password plus key-file encryption/decryption for single-file streams. Compression is implemented for core single-file streams. Archive payload creation/extraction is implemented for file-list archive workflows; richer CLI inspection is implemented in later phases.

## Naming

Single-file and batch encrypted outputs append `.encrypted` by default.

Archive outputs may use `.tar.zst.encrypted` when the payload is a tar archive compressed with Zstandard before encryption.

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
| 17 | 1 | `PayloadKind` | `1` = single-file stream, `2` = tar archive |
| 18 | 4 | `ChunkSize` | default `1048576`; valid range `65536` to `16777216`, multiple of `1024` |
| 22 | 16 | `Salt` | Argon2id salt |
| 38 | 8 | `NoncePrefix` | first 8 bytes of each AES-GCM nonce |
| 46 | 4 | `Argon2MemoryKiB` | default `65536`; valid range `19456` to `1048576` |
| 50 | 4 | `Argon2Iterations` | default `3`; valid range `2` to `64` |
| 54 | 4 | `Argon2Parallelism` | default `4`; valid range `1` to `16` |
| 58 | 1 | `Argon2Version` | `0x13` |
| 59 | 1 | `KeyFileHashAlgorithmId` | `0` = none, `1` = SHA-256 |
| 60 | 4 | `Reserved` | zero |

## Header Readability

The header is deliberately readable without any credential. Every field above is plaintext, so a host can determine the format version, payload kind, compression algorithm, and whether a key file is required before asking the user for a password. This is a supported guarantee, not an implementation detail: `FileCrypter.InspectAsync` exposes it through the public `FileCrypterFileInfo` projection, which omits the salt and nonce prefix because they are key-derivation and framing inputs with no host use.

Header metadata is authenticated only as AAD during payload decryption. Reading it proves nothing about the payload — a header can be fabricated or edited, and tampering is detected when a chunk fails to authenticate, not when the header is parsed. Hosts must present inspected values as what the file claims. `FileCrypter.VerifyFileAsync` authenticates the payload itself by decrypting every chunk and discarding the plaintext; for tar-archive payloads it authenticates the archive bytes but does not walk the tar entry structure.

## Algorithm Defaults

AES-256-GCM is the v1 AEAD. Each chunk uses a 12-byte nonce and a 16-byte authentication tag.

Argon2id is the v1 password-based key derivation function. The derived key length is 32 bytes. The default parameters are:

- memory: `65536` KiB
- iterations: `3`
- parallelism: `4`
- salt length: `16` bytes
- Argon2 version: `0x13`

Decryptors must reject Argon2 parameters outside the v1 valid ranges before deriving a key, so crafted headers cannot force excessive memory or CPU use.

## Key-File Credential Binding

Key files are optional. When the key-file-required flag is set, the key-file hash algorithm must be SHA-256. When the flag is not set, the key-file hash algorithm must be none.

Existing key-file encryption/decryption is implemented. Existing key files are capped at 16 MiB to avoid accidental large-file selection and memory pressure. Generated key files contain at least 32 bytes of cryptographically random data.

The key derivation input is:

1. Encode the password as UTF-8 without normalization inside the core.
2. When the header requires a key file, hash the raw key-file bytes with SHA-256.
3. Feed Argon2id a length-prefixed credential preimage containing a domain label, password bytes, and the optional key-file digest.

Supplying a key file while decrypting a password-only payload does not change the derived key, because password-only payloads do not include a key-file digest in the credential preimage.

## Chunk Layout

Encrypted payload data is a sequence of chunk frames. When `CompressionAlgorithmId` is `1`, the chunk plaintext is the Zstandard-compressed byte stream, and decryptors decompress it after chunk authentication succeeds. Archive payloads are tar streams before compression and encryption.

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

Non-final chunks must have `PlaintextLength` exactly equal to the header `ChunkSize`. Final chunks must have `PlaintextLength` less than `ChunkSize`. Empty files emit one final chunk with `PlaintextLength = 0`. Files ending exactly on a chunk boundary emit a zero-length final chunk. No bytes may appear after the final chunk.

## Validation Rules

Readers must reject:

- inputs shorter than 64 bytes
- incorrect magic bytes
- unsupported format versions
- header lengths other than 64
- unknown header flags
- unsupported AEAD or KDF ids
- compression ids other than none or Zstd
- payload kinds other than single-file stream or tar archive
- chunk sizes outside the valid range or not divisible by 1024
- Argon2 memory, iteration, or parallelism values outside the valid ranges
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
- Argon2 memory: `19456` KiB
- Argon2 iterations: `2`
- Argon2 parallelism: `1`
- key file: none
- compression: none

The `RandomStart` value means the injected random stream is sequential bytes beginning at that decimal value. For example, `RandomStart = 0` supplies salt bytes `00..0F` and nonce-prefix bytes `10..17`.

| Name | Plaintext | RandomStart | Encrypted length | Encrypted SHA-256 |
| --- | --- | ---: | ---: | --- |
| `empty` | empty byte string | 0 | 88 | `6B4F40FBDA0B0C9725D60818288760CAD3404F1635786AC5453F7181972D8770` |
| `small-text` | UTF-8 `deterministic` | 0 | 101 | `68C46824F9C874EB45C3C3AABDE7863BB64DEBF4D5055824A60F04A6AD9FFB78` |
| `chunk-boundary` | byte sequence `00..FF` repeated to 65536 bytes | 24 | 65648 | `FDBDF7D814A80F5116F472E81337AD154A0A365F81B6F907BE29601AE8154540` |
| `multi-chunk` | byte sequence `FF..00` repeated to 65553 bytes | 48 | 65665 | `96EDD56D00C59F7AC0713CB5A2D49DE32FA25AC9206AA93677CF8AB40EBC1B7F` |

Expected frame prefixes and tags:

| Name | Chunk | Prefix | Tag |
| --- | ---: | --- | --- |
| `empty` | 0 | `0000000001000000` | `62743EA364CB61B90B09C56D4F2DF2CD` |
| `small-text` | 0 | `0D00000001000000` | `0C1A2FDB1DAD906AABD0551ECDC39A07` |
| `chunk-boundary` | 0 | `0000010000000000` | `3BFD480077B64BAC2DE4841456B32555` |
| `chunk-boundary` | 1 | `0000000001000000` | `266EE2CB63D9137A000C241D24144320` |
| `multi-chunk` | 0 | `0000010000000000` | `FAED6591B39742547CC62520C1E62BFC` |
| `multi-chunk` | 1 | `1100000001000000` | `1CA532EFD0A4CCCC62F2D29693D1884F` |

## References

- RFC 9106: Argon2 Memory-Hard Function for Password Hashing and Proof-of-Work Applications
- Microsoft Learn: `System.Security.Cryptography.AesGcm`
- NIST SP 800-38D: Recommendation for Block Cipher Modes of Operation: Galois/Counter Mode (GCM) and GMAC

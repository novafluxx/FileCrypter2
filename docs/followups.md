# Engineering Follow-ups

Items identified during the 2026-07-25 Tier 1 work (inspect, verify, CLI settings expansion) that were deliberately
left unaddressed. Nothing here blocks the shipped slice. Security-specific residual risks live in
`docs/security-todos.md`; this file is for design debt, coverage gaps, and deferred scope.

Ordered by how much they cost to fix if left alone, not by urgency today.

---

## F1 — Tar traversal rejection branches covered (completed 2026-07-26)

**Severity:** highest-value item here. **Effort:** small (test-only). **Blocks:** F5.

`CreateArchiveExtractionOutputPath` (`src/FileCrypter.Core/FileCrypter.cs:1745-1774`) is the only thing standing
between a hostile archive and arbitrary file write. It rejects entry names that contain a separator, are rooted, are
`.`/`..`, or resolve outside the output directory — and **not one of those branches is exercised by a test**. There is
no test anywhere in `tests/FileCrypter.Core.Tests/` that constructs a tar with a malicious entry name; every archive
under test is one FileCrypter itself wrote, and the writer never emits an unsafe name.

The guard is currently correct as far as I can tell by reading it. That is not the same as knowing it, and it is the
wrong place in the codebase to be relying on inspection.

**Completed coverage:** `FileCrypterTests` now builds authenticated tar payloads directly with
`System.Formats.Tar.TarWriter`, encrypts them through the test-only archive payload helper, and verifies
`DecryptArchiveAsync` returns `InvalidArchivePayload` for `../escape.txt`, `sub/dir/file.txt`, `/etc/passwd`,
`C:\Windows\x.txt`, `..`, `.`, an empty name, and separator-only names. The tests assert no extracted or staged files
remain (including no escaped `../escape.txt`) and include a `file.txt` positive control that verifies the returned path
and contents. The empty-name case serializes a valid Ustar entry, clears its name field, and recomputes the tar checksum.

This is the regression prerequisite for F5: any future path-validation relaxation must retain this hostile-input net.

---

## F2 — Short-file format classification centralized in Core (completed 2026-07-26)

**Severity:** low today, silent-drift risk later. **Effort:** small.

The short-file pre-check added late in the Tier 1 work needed to know the header length and magic bytes. The format
implementation is internal, so the CLI carried its own copies:

- `src/FileCrypter.Cli/FileCrypterCommand.cs:13` — `private const int FileCrypterHeaderLength = 64;`
- `src/FileCrypter.Cli/FileCrypterCommand.cs:1081` — `private static ReadOnlySpan<byte> FileCrypterMagic => "FCRYPT\r\n"u8;`

Both contradicted `CLAUDE.md` and `docs/file-format.md`, which designate
`Format/FileCrypterFormatConstants.cs` as the single source of truth for header constants.

**Completed resolution:** `FileCrypterHeaderParser` now checks the available magic prefix before requiring a complete
header. Empty inputs and prefixes that contradict the FileCrypter magic return `InvalidMagic`; non-empty matching
prefixes that end before the full header return `TruncatedHeader`. The CLI relies on those existing public error codes
and its normal troubleshooting-message path, so the duplicated header length, magic bytes, pre-read helper, and special
short-file error writer have been removed.

No public format constants or new APIs were added. Inspection, verification, and decryption now share the same Core
classification, and the desktop requires no host-specific change.

---

## F3 — `GetAvailableArchiveEntryName` collapses directory prefixes on collision

**Severity:** latent (unreachable today). **Effort:** small. **Becomes live with:** F5.

`src/FileCrypter.Core/FileCrypter.cs:1788` splits the entry name with `Path.GetFileNameWithoutExtension` /
`Path.GetExtension` to build a ` (1)` suffix. For `a/b/c.txt` that yields `c (1).txt` — the `a/b/` prefix is silently
dropped, so a collision would relocate the entry to the archive root.

Not reachable now: the only caller (`FileCrypter.cs:1607`) passes `Path.GetFileName(fullInputPath)`, so entry names are
always bare filenames and the prefix is always empty. Fix it when entry names can legitimately contain separators, not
before — a defensive fix now would be untestable.

---

## F4 — `docs/security-todos.md:46` will become false if folder support lands

**Severity:** documentation accuracy. **Effort:** trivial, but easy to forget.

The "What's Working" entry reads:

> Tar extraction path validation rejects separators, `.`/`..`, absolute paths, and paths resolving outside the output
> directory.

Relaxing separator rejection (which folder support requires by definition) invalidates the first clause. Whoever does
F5 must rewrite this line to describe per-segment validation plus the retained containment check — an audit doc that
overstates its guarantees is worse than one that omits them.

---

## F5 — Folder support (deferred by decision, not oversight)

**Severity:** feature scope. **Effort:** 2-3x the entire Tier 1 slice. **Decision date:** 2026-07-25.

Recursive directory encryption was proposed, costed, and explicitly deferred in favor of shipping inspect + verify +
CLI settings as a tight, low-risk slice. **Do not re-propose it as a quick win.** The enumeration side is easy; all the
cost is in the extraction path, where allowing separators in tar entry names reopens path-traversal surface that is
currently closed by blunt rejection.

Known blockers, all of which must be handled together:

- `CreateArchiveExtractionOutputPath` (`FileCrypter.cs:1745`) rejects any entry name with a directory component. Needs
  per-segment validation while keeping the final containment check.
- `GetAvailableArchiveEntryName` — see F3.
- `CreateStagingPath` / `ValidateOutputPath` assume the parent directory already exists.
- The blunt `path.Contains("..")` substring guards — see F8, which should land first.
- `IsRegularFileEntry` throws on tar `Directory` entries.
- `FileDropDataHelper` silently discards dropped folders, and is shared by all three views.
- `EncryptArchiveAsync_WithDuplicateInputFileNames_AutoRenamesArchiveEntries` asserts the current flattening behavior
  and would need rewriting by design.
- F1's completed regression coverage is a prerequisite; F4 remains outstanding.

This warrants a dedicated security review of its own, not a bolt-on to a feature PR.

---

## F6 — `VerifyFileAsync` is Core- and CLI-only

**Severity:** none. **Effort:** small if ever wanted. Recorded so it isn't mistaken for an oversight.

`FileCrypter.VerifyFileAsync` (both overloads) is public on Core and surfaced by the CLI `verify` command, but is
deliberately absent from `IFileCrypterWorkflowService`. Reason: there is no UI consumer, and the interface has numerous
test implementations across the test suite — adding an unused member would have meant no-op stubs for no behavior.

If a desktop "Verify" action is ever wanted, add the member to the interface and implement it in
`FileCrypterWorkflowService` alongside `InspectFileAsync` (`Services/FileCrypterWorkflowService.cs:204`); Core needs no
change.

---

## F7 — `overwrite-default` polarity differs between CLI and desktop

**Severity:** cosmetic; verified correct in both hosts. Recorded to stop a future reader "fixing" one of them.

The stored setting is `NeverOverwriteExistingFilesByDefault` (negative sense). The CLI intentionally inverts it so
`settings set overwrite-default on|off` reads the same direction as the existing `--overwrite` flag
(`FileCrypterCommand.cs:852`, displayed at `:939`). The desktop settings checkbox uses the stored negative framing
directly (`SettingsViewModel.cs:136`).

Both are self-consistent and both write the same underlying value. Changing either in isolation would silently invert
behavior for users of the other host.

---

## F8 — Substring `..` guards reject legitimate file names today

**Severity:** user-visible bug, live now (not gated on F5). **Effort:** small. **Found:** 2026-09-30.

Three `aikido-autofix[bot]` commits (`0073d72`, `747cd5a`, `91fb2cc`) added `path.Contains("..")` checks that throw
`ArgumentException("Invalid file path")`. They run on every full input/output path, so any path containing two
consecutive dots anywhere — `my..notes.txt`, a directory named `v1..2`, or a relative CLI argument like
`../file.txt` — fails. Reproduced: `encrypt my..notes.txt` reports *"Path error: FileCrypter could not access one of
the requested paths"*, which also misdirects the user toward permissions or a missing output directory.

Locations (nine path guards):

- `src/FileCrypter.Core/FileCrypter.cs` — `CreateOutputFileStream`, `CreateWindowsOutputFileStream`,
  `MoveStagedOutput` (both arguments), `IsSymbolicLinkOrReparsePoint`, `GetResolvedSymbolicLinkTarget`, `TryDeleteFile`.
- `src/FileCrypter.Core/Settings/FileCrypterSettingsStore.cs` — `IsSymbolicLink` (a profile path containing `..` would
  break settings saves).
- `src/FileCrypter.Cli/FileCrypterCommand.cs` — the encrypt/decrypt input-path check.

They add no protection: every guarded value is already a full path from `Path.GetFullPath`, and the real defenses —
symlink/reparse-point rejection and the per-segment checks plus containment check in
`CreateArchiveExtractionOutputPath` — are independent of them. Keep `IsSafeFileName` in the settings store: it
validates app-chosen settings and staging file *names* (no separators), not user paths, and carries a `nosec` rationale.

**Done looks like:** remove the nine guards; add Core and CLI tests that encrypt/decrypt `my..notes.txt` and a file in a
`dir..name` directory, plus a CLI test with a relative `..\` input path; confirm the symlink-rejection and F1 traversal
tests still pass. Expect Aikido to re-flag the sinks — answer with `nosec` comments stating the canonicalization
rationale, matching the existing one in `FileCrypterSettingsStore.TryDeleteStagingFile`, rather than restoring the
substring checks. Needs explicit sign-off since it removes code a security tool added.

---

## Related decisions already settled

- **Batch file cap stays at 1000** as a hard limit, surfaced earlier rather than raised. The desktop archive-mode gap
  that skipped the cap was closed as part of Tier 1.
- **No cloud, telemetry, or network dependency** — unchanged, and none of the above should introduce one.

---

## State at handoff

The Tier 1 slice, F1, and F2 are committed. As of 2026-09-30 the build is clean at zero warnings
(`TreatWarningsAsErrors` is on) and the full suite is 361 tests passing. Open items: F3 and F4 (both gated on F5),
F5 (deferred), F8 (live bug).

# Engineering Follow-ups

Items identified during the 2026-07-25 Tier 1 work (inspect, verify, CLI settings expansion) that were deliberately
left unaddressed. Nothing here blocks the shipped slice. Security-specific residual risks live in
`docs/security-todos.md`; this file is for design debt, coverage gaps, and deferred scope.

Ordered by how much they cost to fix if left alone, not by urgency today.

---

## F1 — Tar traversal rejection branches have no test coverage

**Severity:** highest-value item here. **Effort:** small (test-only). **Blocks:** F5.

`CreateArchiveExtractionOutputPath` (`src/FileCrypter.Core/FileCrypter.cs:1745-1774`) is the only thing standing
between a hostile archive and arbitrary file write. It rejects entry names that contain a separator, are rooted, are
`.`/`..`, or resolve outside the output directory — and **not one of those branches is exercised by a test**. There is
no test anywhere in `tests/FileCrypter.Core.Tests/` that constructs a tar with a malicious entry name; every archive
under test is one FileCrypter itself wrote, and the writer never emits an unsafe name.

The guard is currently correct as far as I can tell by reading it. That is not the same as knowing it, and it is the
wrong place in the codebase to be relying on inspection.

**Done looks like:** a test file that builds tars directly with `System.Formats.Tar.TarWriter`, encrypts them via the
archive path, and asserts `DecryptArchiveAsync` throws `FileCrypterFormatException` with
`FileCrypterFormatErrorCode.InvalidArchivePayload` for at least: `../escape.txt`, `sub/dir/file.txt`, `/etc/passwd`,
`C:\Windows\x.txt`, `..`, `.`, an empty name, and a name that is only a separator. Add a positive control (a plain
`file.txt` entry) so the test can't pass by rejecting everything.

Write these **before** touching F5. They are the regression net that makes relaxing the validation survivable.

---

## F2 — Format constants duplicated into the CLI

**Severity:** low today, silent-drift risk later. **Effort:** small.

The short-file pre-check added late in the Tier 1 work needed to know the header length and magic bytes, but
`FileCrypter.Core.Format` is entirely `internal` and `InternalsVisibleTo` names only `FileCrypter.Core.Tests`. The CLI
therefore carries its own copies:

- `src/FileCrypter.Cli/FileCrypterCommand.cs:13` — `private const int FileCrypterHeaderLength = 64;`
- `src/FileCrypter.Cli/FileCrypterCommand.cs:1081` — `private static ReadOnlySpan<byte> FileCrypterMagic => "FCRYPT\r\n"u8;`

Both contradict `CLAUDE.md` and `docs/file-format.md`, which designate `Format/FileCrypterFormatConstants.cs` as the
single source of truth for header constants.

Harmless while v1 is frozen — the parser hard-rejects any header length other than 64, so a mismatch cannot go
undetected at runtime. It becomes a real bug the day a v2 header changes length, and it will not announce itself:
the CLI would simply start misclassifying short v2 files as "not a FileCrypter file."

**Two ways out, in order of preference:**

1. Fix it in Core rather than working around it in a host. Give `InspectAsync` a sibling — or an error code on the
   existing `FileCrypterFormatException` — that distinguishes *"this is not a FileCrypter file"* from *"this is a
   truncated FileCrypter file."* That is genuinely Core's job: it owns the format, and today it cannot express the
   distinction for inputs under 64 bytes because the magic check is unreachable. Both hosts benefit and the CLI
   constants delete themselves.
2. Failing that, expose the header length and magic as public constants on Core and have the CLI reference them.
   Cheaper, but it widens the public surface for a host-convenience reason, which is the weaker trade.

The desktop side needs nothing here — it catches `FileCrypterFormatException` broadly and never inspects byte counts.

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
- Five separate blunt `path.Contains("..")` substring guards reject legitimate names such as `my..notes`.
- `IsRegularFileEntry` throws on tar `Directory` entries.
- `FileDropDataHelper` silently discards dropped folders, and is shared by all three views.
- `EncryptArchiveAsync_WithDuplicateInputFileNames_AutoRenamesArchiveEntries` asserts the current flattening behavior
  and would need rewriting by design.
- F1 and F4 above.

This warrants a dedicated security review of its own, not a bolt-on to a feature PR.

---

## F6 — `VerifyFileAsync` is Core- and CLI-only

**Severity:** none. **Effort:** small if ever wanted. Recorded so it isn't mistaken for an oversight.

`FileCrypter.VerifyFileAsync` (both overloads) is public on Core and surfaced by the CLI `verify` command, but is
deliberately absent from `IFileCrypterWorkflowService`. Reason: there is no UI consumer, and the interface has nine
implementations across the test suite — adding an unused member would have meant nine no-op stubs for no behavior.

If a desktop "Verify" action is ever wanted, add the member to the interface and implement it in
`FileCrypterWorkflowService` alongside `InspectFileAsync` (`Services/FileCrypterWorkflowService.cs:204`); Core needs no
change.

---

## F7 — `overwrite-default` polarity differs between CLI and desktop

**Severity:** cosmetic; verified correct in both hosts. Recorded to stop a future reader "fixing" one of them.

The stored setting is `NeverOverwriteExistingFilesByDefault` (negative sense). The CLI intentionally inverts it so
`settings set overwrite-default on|off` reads the same direction as the existing `--overwrite` flag
(`FileCrypterCommand.cs:863`, displayed at `:950`). The desktop settings checkbox uses the stored negative framing
directly (`SettingsViewModel.cs:136`).

Both are self-consistent and both write the same underlying value. Changing either in isolation would silently invert
behavior for users of the other host.

---

## Related decisions already settled

- **Batch file cap stays at 1000** as a hard limit, surfaced earlier rather than raised. The desktop archive-mode gap
  that skipped the cap was closed as part of Tier 1.
- **No cloud, telemetry, or network dependency** — unchanged, and none of the above should introduce one.

---

## State at handoff

The Tier 1 slice is complete and uncommitted: 21 files changed or added across Core, CLI, Desktop, plus `CLAUDE.md`,
`README.md`, and `docs/file-format.md`. Build is clean at zero warnings (`TreatWarningsAsErrors` is on); the full suite
is 344 tests passing (113 Core, 87 CLI, 144 Desktop), up from 301.

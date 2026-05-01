# FileCrypter UI Plan Archive

A set of discrete, independently shippable UI improvements from the first UI polish pass. Completed slices are retained here for context. Remaining active work from Slice 8 and Slice 9 is tracked in `docs/cleanup-todos.md` under "UI Backlog Moved From UI Plan."

---

## Slice 1 — Fix the empty-state banner

`[x] DONE:`

**Problem.** The red "No file selected" banner with a "Copy issue" button fires on first paint across Encrypt, Decrypt, and Batch. This makes the app feel broken before the user has done anything. Red + "Copy issue" is the visual language of a failure, but no failure has occurred — the user just hasn't picked a file yet.

**Change.** Remove the banner from the initial empty state entirely. The status bar already says "No file selected," and the empty input fields communicate the same thing. Reserve the red alert treatment for real failures: wrong password, corrupt header, I/O error, unsupported format, cancelled operation.

**Screens affected.** Encrypt, Decrypt, Batch.

**Done when.**
- Opening the app on any of the three screens shows no red banner.
- Triggering a real error (e.g. start decryption with a bad password) surfaces the same red banner treatment, with "Copy issue" intact.
- Error banner dismisses when the user changes relevant inputs.

---

## Slice 2 — Add icons to the sidebar nav

`[x] DONE:`

**Problem.** Text-only nav items (Encrypt, Decrypt, Batch, Help, Settings) feel undercooked for a desktop app and offer no path to collapsing the sidebar later.

**Change.** Add a single icon to the left of each nav label. Suggested glyphs:
- Encrypt → lock (closed)
- Decrypt → lock (open) or key
- Batch → stacked documents
- Help → question mark in circle
- Settings → gear

**Done when.**
- Every nav item has an icon + label.
- Icons inherit the correct color for default / hover / active states.
- Icon style is consistent (all outline or all filled, same stroke weight).

---

## Slice 3 — Make file inputs look like drop zones

`[x] DONE:`

**Problem.** The inputs advertise drag-and-drop ("Select or drag a file...") but visually read as plain text fields. The affordance is invisible.

**Change.** Restyle the file input area as a drop zone:
- Dashed border instead of solid.
- Taller minimum height (roughly 2× current).
- A document/upload glyph centered or leading.
- Distinct hover state and a clear "drag-over" state (brighter border, subtle fill).
- Keep the Browse button beside it.

**Screens affected.** Encrypt (source + output), Decrypt (encrypted file + output), Batch (files list).

**Done when.**
- Empty state clearly reads as droppable.
- Dragging a file over the window visibly highlights the target zone.
- Output fields that are auto-generated keep a lighter / less prominent treatment since the user usually won't drop there.

---

## Slice 4 — File preview after selection

`[x] DONE:`

**Problem.** Once a file is selected there's no preview — just a path string in the input. The user can't quickly verify they picked the right thing.

**Change.** When a file is loaded, replace the path-in-textbox with a compact preview row inside the drop zone:
- File-type icon
- Filename (truncated with ellipsis middle, full path on hover tooltip)
- File size (human-readable: KB / MB / GB)
- A small × button to clear the selection

**Screens affected.** Encrypt, Decrypt, Batch.

**Done when.**
- Selecting a file swaps the empty-state drop zone for the preview row.
- The clear button returns the zone to empty state.
- Batch mode shows a list of these rows, each with its own × button.

**Depends on.** Slice 3 (drop zone styling).

---

## Slice 5 — Enrich the status bar

`[x] DONE:`

**Problem.** "Ready" + "No file selected" is passive. The bar is prime ambient-state real estate that's currently underused.

**Change.** Expand what the status bar reports based on app state:
- **Idle, no file.** "Ready" / "No file selected"
- **File loaded.** "Ready" / `filename.ext — 4.2 MB`
- **Running.** `Encrypting…` / `chunk 14/87 — 62%` (or similar progress)
- **Complete.** `Done in 2.1s` / `Saved to ~/Documents/file.encrypted` (clickable to reveal in Finder/Explorer)
- **Error.** Matches the error in the main area; bar shows `Failed` + brief reason.

**Done when.**
- All four state transitions update both the left (action) and right (detail) status slots.
- "Saved to" path is click-to-reveal in the OS file manager.

---

## Slice 6 — Relocate the Changelog link

`[x] DONE:`

**Problem.** Top-right "Changelog" competes with the page title on every screen despite most users never opening it outside of update moments.

**Change.** Remove the global top-right Changelog link. Add instead:
- A "Changelog" entry inside Settings (likely in an About / Version section near `v0.1.0`).
- Optionally: a small "What's new" toast on first launch after a version bump.

**Done when.**
- Changelog is no longer visible in page chrome.
- `v0.1.0` label in the sidebar becomes clickable and opens the changelog view, *or* Settings has a clearly labeled entry.

---

## Slice 7 — Group the Batch workflow cards

`[x] DONE:`

**Problem.** On the Batch page, "Workflow type" and "Action" sit as two separate boxes with no visual tie. Their relationship (and which combinations are valid) isn't clear.

**Change.**
- Merge both into a single outer card with a subtle internal divider, or place them inside the same "Workflow" section with shared padding.
- Disable invalid combinations visibly (greyed radio + tooltip explaining why).
- Update the blue helper text below so it describes the *currently selected* combination, not just individual-file mode.

**Done when.**
- The two option groups read as one decision, not two.
- Selecting "Encrypted archive" + "Decrypt" either works with a correct description, or is disabled with a tooltip.
- Helper text updates on selection change.

---

## Slice 8 — Use the sidebar's empty vertical space

`[~] MOVED TO CLEANUP BACKLOG:`

**Problem.** Between "Batch" and "Help" there's a large dead zone in the sidebar.

**Change.** Fill it with a compact "Recent files" list (last 5 encrypted or decrypted files). Each row: file-type icon, filename, subtle timestamp. Clicking a row loads it back into the relevant page.

Alternative if recents feel too heavy: a sidebar-wide drop target ("Drop files here to encrypt") that defaults to the Encrypt flow.

**Done when.**
- Sidebar shows recents (or drop target) between nav and Help/Settings.
- Clicking a recent loads it into the matching page.
- Recents persist across app restarts (local store, filenames only — never contents).

**Security note.** Only store plaintext paths and filenames if the user opts in. Default to showing only outputs (encrypted files) to avoid leaking which plaintext files were handled.

---

## Slice 9 — Checkbox visibility polish

`[~] MOVED TO CLEANUP BACKLOG:`

**Problem.** Unchecked checkboxes nearly disappear against the near-black background.

**Change.** Lighten the unchecked border by one step (e.g. from ~#2a2a2a to ~#444) and slightly thicken it. Checked state stays as-is.

**Done when.**
- Unchecked boxes are clearly visible at normal viewing distance.
- Contrast passes WCAG AA for UI components (3:1 against background).

---

## Slice 10 — Sidebar nav hover state

`[x] DONE:`

**Problem.** Only the active nav item has visual distinction. Hover produces no feedback, which feels inert.

**Change.** Add a subtle hover background (a lighter shade than the sidebar, darker than the active-blue) and a cursor pointer. Active state remains the filled blue pill.

**Done when.**
- Hovering any nav item shows feedback.
- Active item is still visually dominant — hover is a whisper, active is the statement.

**Depends on.** Slice 2 (icons) ships cleaner if done together.

---

## Suggested order

1. Slice 1 (empty-state banner) — biggest perceived-quality win, small change.
2. Slice 9 + 10 (checkbox + hover) — trivial polish, bundle together.
3. Slice 2 (icons) — sets up Slice 10 and Slice 6.
4. Slice 3 + 4 (drop zones + previews) — these belong together.
5. Slice 5 (status bar) — once file state is richer from Slice 4.
6. Slice 7 (batch cards) — isolated, do whenever.
7. Slice 6 (changelog relocation) — after Slice 2.
8. Slice 8 (recents) — largest scope, save for last.

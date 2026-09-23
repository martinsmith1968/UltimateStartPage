# To do

Enhancement ideas from a review of the code. Items already in the README's *Ideas for later* aren't repeated unless there's something to add to them.

Suggested starting point: the multi-instance overwrite fix (with the file watcher), portable targets, and CI.

## 1. Fix these first

- [x] **Two VS instances can overwrite each other's changes.** `JsonLayoutStore` now keeps a hash of the file as it last loaded or saved it, and refuses to save over a file that has changed since (`LayoutConflictException`). A `FileSystemWatcher` raises `ExternalChange` so idle instances reload. The view model waits for open dialogs and unsaved edits before reloading, and merges on a conflict.
  - [ ] Unconfirmed: check with two real VS instances that an edit in one shows up in the other, and that simultaneous edits merge.
  - [ ] Follow-up: the check and the write in `JsonLayoutStore.SaveAsync` aren't atomic across processes. Two instances saving within the same few milliseconds could still overwrite each other. A lock file would close the gap if it ever matters.
  - [ ] Follow-up: on network shares that don't support change notifications, the watcher logs a warning and other instances' edits need a manual **Reload**. Saves are still checked and merged. Consider polling as a fallback.
  - [x] Follow-up: merge instead of asking. Everything has ids, so a three-way merge (base = last loaded) could combine non-overlapping edits. Done in `LayoutMerger`; the Yes/No prompt and `ILayoutStore.OverwriteAsync` are gone.
    - [ ] Follow-up: when a merge keeps something because an edit beat a delete (e.g. a link deleted in one window but edited in the other), the user isn't told. It's only logged. Consider a non-modal notice on the page.
    - [ ] Follow-up: the store trims titles when saving, but the merge ancestor is the untrimmed in-memory snapshot. A title saved with a trailing space may be silently swapped for the trimmed disk version on the next merge. Harmless, but the ancestor could be normalised the same way.
  - [ ] Follow-up: collapse state is stored in the shared file, so collapsing a section in one window collapses it in all of them. Consider moving it to per-instance settings.
  - [x] Follow-up: nothing flushes on shutdown. An edit made less than 400 ms before VS closes is lost. Fixed: `OnBeginShutdown` now blocks (up to 5 s) on `StartPageViewModel.SaveUnsavedChangesAsync`.
    - [ ] Unconfirmed: check in VS that an edit made just before closing is saved, and whether the `VSTHRD102` suppression around `JoinableTaskFactory.Run` is actually needed.
- [x] **The recent list goes stale.** It only refreshes after a solution closes or options are saved. Opening the page yourself (Ctrl+Alt+Home) shows the list from when it was first loaded. Fixed: the page re-reads the list whenever it becomes visible (`StartPageViewModel.OnPageShown`, from WPF's `IsVisibleChanged`), unless it did so in the last 2 s. It only rebuilds the list if something changed, and overlapping refreshes no longer duplicate items.
  - [ ] Unconfirmed: check in VS that `IsVisibleChanged` fires both when the page is opened (Ctrl+Alt+Home, View menu) and when its document tab is switched back to. If tab switching doesn't raise it, hook the frame's show events instead (`IVsWindowFrameNotify3.OnShow`).
  - [ ] Unconfirmed: if the page stays on screen the whole time, nothing triggers a refresh. Check this doesn't matter in practice (opening a solution should close or cover the page).
- [x] **Some disk checks run on the UI thread.** `StartPageViewModel.OpenLinkAsync` calls `TargetExists` there, and so does `LinkEditorDialog.OnTargetChanged` on every keystroke. A path on an unreachable network share can freeze VS for seconds. `RefreshMissingFlagsAsync` already moves this off the UI thread, and these two should do the same. Fixed, along with `OpenRecentAsync`, dropped paths, `VsLinkLauncher.OpenAsync`, and every `JsonLayoutStore`/`VsRecentItemsReader` entry point (a FileStream opens synchronously on the caller's thread). The dialog hints from the text as you type and checks the disk off-thread once typing pauses for 300 ms.
  - [ ] Unconfirmed: check in VS that the link dialog's hint and suggested title update after the pause, including for a folder with a dot in its name ("My.Repo").
  - [ ] Clicking OK before the disk check finishes uses the text-based kind, so a "My.Repo" folder can be saved as a File. It still opens as a folder (the launcher re-checks), but gets the wrong icon. Could re-check kinds in `RefreshMissingFlagsAsync`.
  - [ ] Left on the UI thread: the dialog's Folder…/File… buttons check the current target to pick a starting folder. The Windows file dialog then blocks on an unreachable share anyway.
  - [ ] An unreachable share can still take ~30 s to answer. The UI stays responsive, but clicking a link gives no feedback meanwhile. Consider a "checking…" state or a timeout.
  - [ ] Follow-up: `DiskChecks_NeverRunOnTheCallersThread` covers the view model only. Moving `JsonLayoutStore` and `VsRecentItemsReader` onto the thread pool has no test, because a real file read can't be observed that way.
- [ ] **Get the VSIX building, then add CI.** Pin the floating `17.*`/`16.*` package versions, then add a GitHub Actions job on `windows-latest` that runs msbuild, the tests, and uploads the `.vsix`.
  - [ ] Unconfirmed: none of the VSIX-side changes made since the initial version have been compiled. That's `UltimateStartPagePackage.OnBeginShutdown`, `StartPageControl.OnIsVisibleChanged`, the `LinkEditorDialog` disk check and `VsLinkLauncher.OpenAsync`. The VS threading analyzers may also flag some of them.
  - [ ] Follow-up: the README's "First-build notes" still says "63 tests"; there are now 97. Update it, or drop the count so it can't go stale.

## 2. Make shared layouts work across machines

- [ ] **Portable targets.** Let targets use environment variables or `~` (e.g. `%REPOS%\SS.Modelling.Harness\...sln`) and expand them when opening a link and when checking whether it exists. Without this, an exported team layout breaks on any machine where repos live somewhere else.
- [ ] **Relocate a missing link, not just remove it.** Today "Link not found" only offers removal. Add "Browse for new location…". If other links share the old folder prefix, offer to fix those too, since a whole repo root usually moves at once.
- [ ] **Subscribed team sections.** Build on the README's "remote layouts" idea: sections that come from a URL or shared path, are read-only, refresh at startup, and are marked with a badge. Personal sections stay separate, so importing a team layout twice no longer duplicates it.
- [ ] **Smarter import.** Detect when a section with the same title already exists, and offer Merge, Add as copy or Replace (`StartPageViewModel.ImportAsync` currently always appends).

## 3. Faster day-to-day use

- [ ] **Keyboard-first search.** Typing anywhere on the page goes to the search box, Ctrl+F focuses it, Enter opens the top match, and arrow keys move through results. Consider fuzzy/subsequence matching with highlighted matches (`SearchMatcher` is only substring matching today).
- [ ] **More link actions:** Open in a new VS instance, Copy path, Open in terminal, Open as administrator, and user-defined external tools (e.g. "Open in VS Code", `git pull`).
- [ ] **Quicker pinning.** A "Pin to ▸" submenu listing the sections, or one-click pin to the first section, instead of the full dialog every time. Mark recent items that are already pinned, or hide them.
- [ ] **More drag-and-drop.** Accept URLs dragged from a browser (`UniformResourceLocator`/text formats), and let recent items be dragged onto a section to pin them.
- [ ] **Usage tracking.** Record last opened time and open count per link, which allows sorting a section by recent or most-used.
- [ ] **Undo.** Replace the confirm dialogs for deleting sections and links with an "Undo" bar that lasts a few seconds.

## 4. Polish

- [ ] **First run:** offer to build a starter section from the recent list instead of an empty "Favourites".
- [ ] **Accessibility:** the icon buttons use glyph characters as their content, so screen readers read gibberish. Add `AutomationProperties.Name`.
- [ ] **Layout options:** adjustable card width (fixed at 380 today) and a column count, alongside the compact view already on the list.
- [ ] **Live layout-file setting:** changing the layout file path currently needs a restart, but with a watcher in place it could take effect immediately.
- [ ] **Tests:** the tests cover Core well but none of the VSIX adapters. At minimum, add tests for `ResolveLayoutFilePath` and the fallback between clone-command names.

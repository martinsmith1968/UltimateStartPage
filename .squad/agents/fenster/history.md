# Fenster — History

## Project Context

- **Project:** UltimateStartPage
- **Stack:** .NET (C#)
- **User:** Martin Smith
- **Role:** QA Tester — tests, quality gates, edge cases, reviewer
- **Team:** Keaton (Team Lead / Reviewer), McManus (.NET Developer), Scribe (logger), Ralph (monitor)

## Learnings

> Append new learnings here after each session.

### Session: FluentAssertions + LinkRepository Tests — 2025-07-16

**Test Stack Alignment — Decision #5 Enforcement**
- Added `FluentAssertions 6.12.0` to `UltimateStartPage.Core.Tests.csproj` (6.x is the last major series with .NET Framework 4.7.2 support; 7.x dropped net472).
- NSubstitute 5.1.0 was already present — no change needed.
- Migrated `SolutionLinkTests.cs` from `Assert.*` to FluentAssertions: `Should().Be()`, `Should().BeNull()`, `Should().Throw<T>().WithParameterName()`.
- Created `Services/LinkRepositoryTests.cs` with 14 tests covering: empty initial state, group auto-creation, multi-link groups, duplicate file path entries (allowed by design), null/empty/whitespace group name guards, null link guard, remove-happy-path, remove-non-existent group/path, first-match removal on duplicates, SaveGroupsAsync replace semantics, SaveGroupsAsync with empty list.
- Total test count: **18** (was 4). All passing.

**FluentAssertions Patterns Established**
- Sync exception assertions: wrap in `Action act = () => …; act.Should().Throw<T>().WithParameterName("x");`
- Async exception assertions: wrap in `Func<Task> act = () => …; await act.Should().ThrowAsync<T>();`
- Async no-throw: `await act.Should().NotThrowAsync();`
- Collection assertions: `.Should().BeEmpty()`, `.Should().HaveCount(n)`, `.Should().ContainSingle(predicate)`
- Avoid `Assert.Equal` / `Assert.Throws` — FA gives richer failure messages (expected vs actual diff, parameter names).

**net472 + FluentAssertions Note**
- Pin to FA 6.x. FA 7.0+ targets net6.0+. Running FA 7.x on net472 will cause runtime failures.

### Session: Test Strategy — 2025-07-16

**VS SDK Testing Constraints**
- VSIX components run inside the VS process; types like `IVsSolution`, `DTE`, `IVsShell` cannot be instantiated in isolation without a VS host or mock service provider.
- VS UI thread assertions (`ThreadHelper.ThrowIfNotOnUIThread()`) will crash test runs from pool threads — STA thread awareness is required for any VS-touching test.
- Hosted integration tests require a full VS installation on the CI agent (Windows + VS workload). Not available on standard GitHub Actions runners without configuration.
- `Microsoft.VisualStudio.Sdk.TestFramework` (NuGet) provides a mock VS service host for testing VS service interactions without a full VS process — the right tool for settings store and package activation tests.

**Recommended Test Stack**
- **xUnit** for unit tests (modern, parallel, `[Theory]` / `[InlineData]` for parameterised edge cases).
- **NSubstitute** for mocking VS interfaces and domain service interfaces.
- **FluentAssertions** for readable assertion failure messages.
- **MSTest** only for the hosted integration test project (SDK TestFramework is MSTest-based).
- **coverlet** + **ReportGenerator** for coverage reporting in CI.

**Architecture Pattern for Testability**
- The single most important testability decision: wrap every VS SDK service behind an owned interface (`ISettingsStore`, `ISolutionBrowser`, etc.). Unit tests mock the interface; integration tests use the real VS adapter.
- `UltimateStartPage.Core` project must have zero `Microsoft.VisualStudio.*` dependencies — enforced as a build gate.
- Three-project split: `Core` (pure logic), `VsAdapter` (VS SDK wrappers), `VSIX` (package entry point).

**Domain Edge Cases — High Risk**
- UNC paths (`\\server\share\`) are valid but reachability checks can hang — must use async/timeout or an `IFileSystemProbe` abstraction.
- Paths that existed at settings-save time but are deleted before load — must degrade gracefully, marking links as stale rather than throwing.
- Drive letter case mismatch (`c:\` vs `C:\`) requires normalisation for equality comparison.
- Corrupt or partial settings XML — must catch and fall back to defaults without crashing.
- Very large link collections (1000+) — loading must be async or paginated to avoid UI freeze.

**Coverage Targets**
- Core domain models: 90% minimum.
- Core serialisation / persistence: 90% minimum.
- Core utilities (path logic): 95% minimum.
- Overall Core project: 85% line coverage — enforced in CI as a PR gate.

**Known Hard Gaps**
- WPF dispatcher: test ViewModels only, never Views directly.
- UNC reachability: abstract and test the abstraction; real network tests are environment-dependent.
- VS package activation: manual smoke test checklist; automated only with dedicated VS CI agent.
- Concurrent settings access: document as known risk, mitigate with lock or channel in production code.

### Session: LinkGroupViewModel and LinkViewModel Isolation Tests — 2025-07-16

**Test Coverage Expansion — ViewModel Layer**
- Created `ViewModels/LinkGroupViewModelTests.cs` with 13 tests covering: constructor with model/null, name setting, links population, empty group, null guard clauses (onRemove/saveCallback), AddLinkCommand execution/save-callback, RemoveLink logic/save-callback, Name PropertyChanged, ToModel round-trip, RemoveCommand callback invocation.
- Created `ViewModels/LinkViewModelTests.cs` with 15 tests (16 test cases due to Theory with 2 InlineData) covering: constructor name/path setting, null model handling, null guard (onRemove), OpenCommand CanExecute logic (empty/whitespace path), Path non-null guarantee, Name/Path PropertyChanged, Path setter triggers OpenCommand.CanExecuteChanged, ToModel round-trip, RemoveCommand callback, OpenCommand wired up, parameterised edge cases for disabled state.
- Total test count: **54 test cases** (28 methods added this session: 13 LinkGroupViewModel + 15 LinkViewModel). All passing.

**Test Patterns for Callback-Based ViewModels**
- Callbacks (`Func<T, Task>` for remove, `Func<Task>` for save) tested via closure capture — simple bool flag or reference capture to verify invocation.
- Async command execution timing: `Task.Delay(50)` polyfill to give `async void Execute()` time to complete before assertion (no WPF dispatcher in xUnit runner).
- PropertyChanged testing: register handler before mutation, verify `eventRaised` flag and new value in one assertion block.
- `ICommand.CanExecuteChanged` testing: register handler, mutate dependent property (e.g., Path), verify event raised and CanExecute result changed.

**Edge Cases Tested**
- Null models: both ViewModels accept null model gracefully (empty string fallback for Name/Path).
- Null callbacks: guard clauses enforced via `ArgumentNullException` with `.WithParameterName()` assertions.
- Empty/whitespace paths: OpenCommand CanExecute returns false for `string.Empty`, `"   "`, and `null`.
- Collection mutation: RemoveLink removes correct item by reference, preserves order of remaining items.
- ToModel round-trip: ViewModels can serialize back to domain models with current state (name/path changes applied).

**Keaton's Review — Addressed**
- All minimum requirements met:
  - LinkGroupViewModel: constructor sets Name, Links populated, AddLinkCommand adds link, RemoveLink removes correct link, PropertyChanged fires on Name change, empty group produces empty Links.
  - LinkViewModel: constructor sets Path and DisplayName (Name), OpenCommand wired up, Path never null from valid SolutionLink, OpenCommand disabled on empty/whitespace path.
- Test framework: xUnit + NSubstitute + FluentAssertions 6.x as specified.
- Target: net472 as specified.
- All tests passing.

### Session: Sprint 2 Completion — 2026-04-10

**LinkGroupViewModel and LinkViewModel Unit Tests — 28 New Tests**
- **LinkGroupViewModelTests.cs**: 13 comprehensive tests covering constructor, property mutations, command execution, callback invocation, collection handling, and model round-trips.
- **LinkViewModelTests.cs**: 15 tests covering OpenCommand CanExecute logic (path validation), RemoveCommand callbacks, PropertyChanged notifications, and fallback behavior (Process.Start when no VS action).
- **Test framework**: xUnit + NSubstitute + FluentAssertions 6.x per Decision #5.
- **Edge cases tested**: Null models, null callbacks, empty/whitespace paths, collection mutation, model serialization.
- **Total suite**: 54 tests passing (100% green across all three agents' work items).

**Key Patterns Established:**
- Callback-based ViewModel testing: closure capture to verify async callback invocation
- PropertyChanged testing: handler registration before mutation, event verification, property value assertion in single block
- ICommand.CanExecuteChanged testing: property mutation triggers event, CanExecute result changes
- Async command patterns: Task.Delay(50) to give async void Execute() time to complete before assertion (no WPF dispatcher in xUnit)

**Keaton Review Follow-up:**
- Approved LinkGroupViewModel and LinkViewModel tests as meeting all minimum requirements
- Test coverage now includes: parent-child callback communication, command enable/disable logic, model conversion, edge cases
- Deferred concerns (fire-and-forget async logging, UNC path reachability, concurrent settings) documented as future enhancements

**Notes for Future Sessions:**
- Consider LinkViewModel integration tests with real Process.Start (smoke test, not unit test)
- Logging infrastructure (when added) should wrap fire-and-forget exceptions in LinkViewModel and LinkGroupViewModel RemoveCommand
- All 54 tests remain passing after McManus DI wiring and Verbal EnvDTE injection changes

### Session: CRUD Command Tests for ViewModels — 2025-07-16

**Anticipatory Test Coverage — McManus CRUD Commands**
- Added 23 new tests across LinkGroupViewModel (9) and LinkViewModel (14) for in-line editing features
- All tests passed on first run — McManus's implementation was already complete when tests were written
- **LinkGroupViewModel tests (9 new)**: BeginRenameCommand, CommitRenameCommand, CancelRenameCommand behavior, property setters (IsRenaming, EditingName), PropertyChanged events
- **LinkViewModel tests (14 new)**: BeginEditCommand, CommitEditCommand, CancelEditCommand behavior, property setters (IsEditing, EditingName, EditingPath), PropertyChanged events, save callback propagation
- **Total suite**: 77 tests passing (was 54) — 23 new tests added

**Key Edge Cases Tested:**
- BeginRename/BeginEdit copies current values to editing properties (Name→EditingName, Path→EditingPath)
- CommitRename/CommitEdit applies editing values to real properties and triggers save callbacks
- CancelRename/CancelEdit discards editing values without changing original properties
- IsRenaming/IsEditing state management (set to true on begin, false on commit/cancel)
- PropertyChanged notifications for IsRenaming, EditingName, IsEditing, EditingName, EditingPath
- Save callback propagation: CommitRename triggers save once (via Name setter), CommitEdit triggers save twice (via Name and Path setters)

**McManus Implementation Pattern (Already Landed):**
- `BeginRenameCommand` / `BeginEditCommand`: RelayCommand (synchronous), copies values, sets editing flag
- `CommitRenameCommand` / `CommitEditCommand`: AsyncRelayCommand, sets flag false, applies edits (which trigger saves via property setters)
- `CancelRenameCommand` / `CancelEditCommand`: RelayCommand (synchronous), sets flag false, restores original values to editing properties (discard changes)
- Save callback: fire-and-forget pattern via property setters (Name, Path) using `_ = _saveCallback();`
- Properties: IsRenaming, EditingName (LinkGroupViewModel); IsEditing, EditingName, EditingPath (LinkViewModel)

**Build Fix Applied:**
- Fixed C# 8.0 nullable type parameter issue in `AsyncRelayCommand<T>` by adding `where T : class` constraint
- Renamed file from `AsyncRelayCommand{T}.cs` to `AsyncRelayCommandT.cs` (braces in filenames cause issues)
- Generic command used by StartPageViewModel's RemoveGroupCommand

**Test Framework Consistency:**
- All tests follow xUnit + FluentAssertions 6.x pattern per Decision #5
- Async command tests use `await Task.Delay(50)` to give async void Execute() time to complete
- PropertyChanged tests register handlers before mutation, verify event fired and new value in one block


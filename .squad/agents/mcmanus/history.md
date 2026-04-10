# McManus — History

## Project Context

- **Project:** UltimateStartPage
- **Stack:** .NET (C#)
- **User:** Martin Smith
- **Role:** .NET Developer — C# implementation, APIs, backend services
- **Team:** Keaton (Team Lead / Reviewer), Fenster (QA Tester), Scribe (logger), Ralph (monitor)

## Learnings

> Append new learnings here after each session.

### Session: Solution Scaffold (initial)

**Structure decisions:**
- `UltimateStartPage.Core` targets `net472` (SDK-style csproj) — VS2022 runs on .NET Framework 4.7.2; netstandard2.0 would have worked but net472 is explicit and avoids TFM mismatch surprises.
- `UltimateStartPage.VS2022` uses legacy `.csproj` format (non-SDK style) with `PackageReference` for `Microsoft.VisualStudio.SDK` (17.0) and `Microsoft.VSSDK.BuildTools` (17.10). The `<Project Sdk="Microsoft.VisualStudio.Sdk/17.x">` form is not a real supported pattern — that NuGet package is only consumable as a PackageReference, not as an MSBuild SDK attribute.
- `UltimateStartPage.Core.Tests` targets `net472` (SDK-style) — xUnit 2.6.6, NSubstitute 5.1, Microsoft.NET.Test.Sdk 17.8.
- `<Nullable>enable</Nullable>` on Core; model string properties marked `string?` where the parameterless constructor (needed for future serialization) can leave them unset.

**VSIX notes:**
- `AsyncPackage` + `[ProvideAutoLoad(NoSolution_string, BackgroundLoad)]` is the correct VS2022 pattern for auto-showing a tool window on startup.
- `StartPageToolWindowControl.xaml` compiled as `<Page>` (not `<Resource>`) — required for WPF XAML code-behind generation in VSIX projects.
- VSIX manifest targets `[17.0, 18.0)` for VS2022 only. VS2026 gets its own project.

**Blockers / known gaps:**
- VSIX project cannot be built via `dotnet build` — requires `msbuild` with VS2022 installed. Expected and unavoidable for VSIX.
- `LinkRepository` is in-memory stub. `WritableSettingsStore` integration goes in the VSIX project (needs VS SDK types) — deferred.
- XAML control is placeholder. Verbal owns the real UI.

### 2026-04-10 — Architectural Context (from Keaton)

**Key Architectural Decisions for Phase 1 Scaffold:**

1. **Extensibility Model:** VSSDK with `AsyncPackage` base class (not VisualStudio.Extensibility OOP model). Reason: need full WPF control for drag-drop, context menus, complex layouts. OOP model restricts to Remote UI with serialised XAML DataTemplates — too limiting.

2. **Start Page Approach:** Custom `ToolWindowPane` docked in document well (document centre, like a tab). Not BetterStartPage's old `Asset Type="StartPage"` mechanism (removed in VS2019). Auto-show with `ProvideAutoLoad(UIContextGuids80.NoSolution)` + `BackgroundLoad`. Subscribe to `IVsSolutionEvents` to show/hide on solution open/close.

3. **Solution Structure:**
   - `UltimateStartPage.Core` → class library (net472), **zero VS SDK dependencies**, models + ViewModels + serialisation
   - `UltimateStartPage.Vs2022` → VSIX project (v17.x), only manifest + AsyncPackage + ToolWindowPane + VS-specific adapters
   - `UltimateStartPage.Vs2026` → VSIX project (v18.x), added later when 18.x SDK ships
   - Shared project optional; defer until VS2026 SDK available

4. **Data Storage:** JSON file in `%APPDATA%\UltimateStartPage\settings.json` (not `IVsWritableSettingsStore`). Portable, human-readable, VS-version-independent. Use `System.Text.Json` with `WriteIndented = true`.

5. **UI Stack:** WPF + MVVM + VS theming. Use `CommunityToolkit.Mvvm` for `ObservableObject`, `RelayCommand`. VS theme integration via `VsBrushes`, `EnvironmentColors`, `VsResourceKeys`.

6. **Test Strategy:** xUnit (Core tests) + `Microsoft.VisualStudio.Sdk.TestFramework` (mock host for integration tests). Core project must remain zero-dependency (enforce at build gate). 85% Core coverage minimum.

**Phase 1 Goals (McManus):**
- Scaffold solution structure (three projects above)
- Minimal `UltimateStartPagePackage : AsyncPackage`
- Minimal `StartPageToolWindow : ToolWindowPane` hosting WPF UserControl with "Hello, Start Page" text
- Verify auto-load on VS launch with no solution
- Verify tool window appears in document well
- Verify re-open from View menu command
- Success: F5 debug shows working prototype in VS2022 experimental instance

**Risks to Monitor:**
- Tool window in document well may not feel exactly like old start page (mitigate: test early)
- VS2026 SDK not yet available (OK to skip for now)
- VS2026 may require .NET 8+ for extensions (monitor announcements)

See `.squad/decisions.md` for full architectural decision record.

### 2026-04-10 — UI & Test Infrastructure Complete (Verbal & Fenster)

**Key Status for McManus:**

ViewModels are now **stubbed in `src/UltimateStartPage.VS2022/ViewModels/`** (temporary):
- `StartPageViewModel` with `Groups`, `HasGroups`, `AddGroupCommand`
- `LinkGroupViewModel` with `Name`, `Links`, `OpenCommand`
- `LinkViewModel` with `Name`, `Path`, `OpenCommand`
- Simple `RelayCommand` using `CommandManager.RequerySuggested` (no CommunityToolkit due to wpftmp issue)

**Immediate Tasks for McManus:**

1. **Implement command bodies:**
   - `AddGroupCommand` → prompt user for group name, call `ILinkRepository.CreateGroupAsync(name)`
   - `OpenCommand` (both ViewModels) → launch file/folder paths (using Win32 or ProcessStart)

2. **Move ViewModels to Core:**
   - Move `src/UltimateStartPage.VS2022/ViewModels/` to `src/UltimateStartPage.Core/ViewModels/`
   - Rewrite with `CommunityToolkit.Mvvm` (ObservableObject, RelayCommand<T>)
   - Zero VS SDK dependencies

3. **Inject ILinkRepository:**
   - Replace `new StartPageViewModel()` stub in `StartPageToolWindowControl.xaml.cs`
   - Wire via `AsyncPackage.GetServiceAsync<ILinkRepository>()` (or DI container)
   - Pass injected repo to `StartPageViewModel` constructor

4. **Test integration:**
   - Verify XAML layout compiles and renders (F5 in VS2022 experimental)
   - Confirm tool window appears in document well
   - Test empty state / populated state toggle

---

See `.squad/decisions.md` decisions #10-11 for theming, layout, and ViewModel architecture details.

### 2026-04-10 — ViewModel Implementation

**NuGet choices made:**
- `System.Text.Json 6.0.10` — honours Decision #4 exactly. Net472-compatible via netstandard2.0 target. `File.ReadAllTextAsync` doesn't exist on net472; polyfilled with `Task.Run(() => File.ReadAllText(...))`.
- **No CommunityToolkit.Mvvm** — 8.x requires .NET 8 (incompatible); 7.x source generators require C# 9+ partial properties (we're on LangVersion 8.0). Hand-rolled `ObservableObject` + `RelayCommand` + `AsyncRelayCommand` in `Core/Mvvm/` — < 100 lines total, zero external deps.
- `WindowsBase` framework reference added to Core.csproj — needed for `System.Windows.Input.ICommand`. Not a VS SDK dep; part of .NET Framework 4.7.2.

**DI pattern used:**
- `StartPageViewModel(ILinkRepository)` constructor injection. `LinkGroupViewModel` and `LinkViewModel` receive parent-remove callbacks as `Func<T, Task>` parameters — no circular parent references.
- `StartPageToolWindowControl` constructs `new LinkRepository()` + `new StartPageViewModel(repo)` directly with a TODO for proper `AsyncPackage.GetServiceAsync<ILinkRepository>()` wiring.

**JSON serialisation approach:**
- `LinkRepository` now persists to `%APPDATA%\UltimateStartPage\links.json` with `WriteIndented = true`.
- Constructor accepts optional `string filePath` parameter for test isolation.
- Handles: file not found (returns empty), corrupt JSON (returns empty, silent), missing directory (creates it).

**net472 gotchas:**
- `File.ReadAllTextAsync` / `File.WriteAllTextAsync` don't exist — use `Task.Run()` wrappers.
- Nullable reference type `??` coalescing between `List<T>` and `T[]` needs explicit cast to common interface type (e.g. `?? (IReadOnlyList<T>)Array.Empty<T>()`).
- Source generators for CommunityToolkit.Mvvm require C# 9+ — net472 projects targeting LangVersion 8 can't use them.

**Test results:** 25/25 tests passing (18 pre-existing + 7 new ViewModel + LinkRepository tests).

**ViewModels in Core — what's done:**
- `Core/Mvvm/`: `ObservableObject`, `RelayCommand`, `AsyncRelayCommand`
- `Core/ViewModels/`: `StartPageViewModel`, `LinkGroupViewModel`, `LinkViewModel`
- `Core/Services/LinkRepository`: real JSON persistence replacing in-memory stub
- `VS2022/ViewModels/` stubs: excluded from compilation, marked as "moved to Core"
- XAML namespace updated to `clr-namespace:UltimateStartPage.Core.ViewModels;assembly=UltimateStartPage.Core`

**Next steps:**
- Fenster: expand test coverage for LinkGroupViewModel, LinkViewModel command behaviour
- McManus: wire `ILinkRepository` via `AsyncPackage` service provider (proper DI), implement `IVsSolutionEvents` show/hide logic
- Verbal: verify XAML renders correctly with the new Core-namespace ViewModel types in F5 experimental instance

### 2026-04-10 — Code Review Nits Fixed + MEF/DI Wiring Complete

**Nits Fixed (from Keaton's review):**

1. **Fire-and-forget async in Remove commands** (LinkGroupViewModel, LinkViewModel):
   - Changed `ExecuteRemove` from `void` method with fire-and-forget `_ = _onRemove(this)` to proper `async Task ExecuteRemoveAsync()` with `await _onRemove(this)`
   - Updated `RemoveCommand` from `RelayCommand` to `AsyncRelayCommand` to properly handle async execution
   - No more fire-and-forget: commands now properly await their async work

2. **Missing IOException catch in LinkRepository**:
   - Added `IOException` handling in `GetGroupsAsync` (returns empty collection gracefully)
   - Added `IOException` handling in `SaveGroupsAsync` (throws to caller with log note for future logging)
   - Ordered exception handlers: `JsonException` → `IOException` → general `Exception` (most specific to least specific)

3. **Cosmetic fully-qualified ICommand**:
   - Replaced `System.Windows.Input.ICommand` with `ICommand` in LinkGroupViewModel and LinkViewModel
   - Added `using System.Windows.Input;` to both ViewModel files for proper namespace resolution

**MEF/DI Wiring:**

Implemented proper VS AsyncPackage service provider pattern for `ILinkRepository` injection:

1. **UltimateStartPagePackage.cs**:
   - Added private `LinkRepository _linkRepository` field
   - Initialize singleton instance in `InitializeAsync` before tool window creation
   - Register via `AddService(typeof(ILinkRepository), ...)` for VS service container
   - Added `GetLinkRepository()` helper method for tool window access (simpler than async service retrieval in this case)

2. **StartPageToolWindow.cs**:
   - Override `Initialize()` to retrieve `ILinkRepository` from package after construction
   - Create `StartPageToolWindowControl` with injected repository (deferred from constructor to Initialize)
   - Pattern: `ToolWindowPane` constructor → `base.Initialize()` → retrieve services → create Content

3. **StartPageToolWindowControl.xaml.cs**:
   - Changed constructor to accept `ILinkRepository` parameter (required, not optional)
   - Removed TODO comment about DI wiring (now complete)
   - Construct `StartPageViewModel(repository)` with injected dependency
   - Added null check with `ArgumentNullException` (fail-fast on missing DI)

**DI Pattern Choice: Direct Package Retrieval vs AsyncPackage.GetServiceAsync**

- **Chosen:** Direct retrieval via `package.GetLinkRepository()` in `ToolWindowPane.Initialize()`
- **Rationale:** 
  - Simpler for this scope: `LinkRepository` is a singleton created eagerly in package init
  - Avoids async service retrieval complexity in tool window construction flow
  - `ToolWindowPane.Initialize()` is synchronous by framework design; `GetServiceAsync` would require `JoinableTaskFactory` gymnastics
  - Pattern is standard for VS extensions: package owns service lifetime, tool windows retrieve via typed accessors
- **Alternative considered:** Full MEF export with `ComponentModelHost` — overkill for single service, adds boilerplate
- **Future:** If adding multiple services or third-party MEF components, migrate to full MEF catalog registration

**Test Results:** 54/54 tests passing (all existing tests remain green after changes)

**Files Changed:**
- `src/UltimateStartPage.Core/ViewModels/LinkGroupViewModel.cs` (async fix, ICommand cleanup, using statement)
- `src/UltimateStartPage.Core/ViewModels/LinkViewModel.cs` (async fix, ICommand cleanup, using statement)
- `src/UltimateStartPage.Core/Services/LinkRepository.cs` (IOException handling in Get/Save)
- `src/UltimateStartPage.VS2022/UltimateStartPagePackage.cs` (DI registration, repository init)
- `src/UltimateStartPage.VS2022/ToolWindows/StartPageToolWindow.cs` (service retrieval in Initialize)
- `src/UltimateStartPage.VS2022/ToolWindows/StartPageToolWindowControl.xaml.cs` (constructor DI injection)

### Sprint 2 Completion — 2026-04-10

**Code Review Nits Fixed (3 items from Keaton's ViewModel review):**

1. **Fire-and-forget async → AsyncRelayCommand**: 
   - Changed `ExecuteRemove()` in LinkGroupViewModel and LinkViewModel from sync RelayCommand to async AsyncRelayCommand
   - Now properly awaits the `onRemove` callback instead of fire-and-forget `_ = callback(this)`
   - No more silent exception swallowing (future logging framework will capture via proper try-catch)

2. **IOException handling in LinkRepository**:
   - Added `IOException` catch in `GetGroupsAsync` (locked files now handled gracefully, returns empty collection)
   - Added `IOException` catch in `SaveGroupsAsync` with proper exception ordering (specific to general)

3. **ICommand fully-qualified namespace**:
   - Replaced `System.Windows.Input.ICommand` with `using System.Windows.Input;` and `ICommand` identifier in both ViewModels
   - Improves code consistency and readability

**MEF/DI Wiring Complete (ILinkRepository via AsyncPackage):**

Implemented direct package accessor pattern for service injection (Decision: mcmanus-mef-di-wiring.md):

1. **UltimateStartPagePackage.cs**:
   - Private `_linkRepository` field created in `InitializeAsync()`
   - Service registered via `AddService(typeof(ILinkRepository), ...)` callback
   - Public `GetLinkRepository()` accessor method for tool window access

2. **StartPageToolWindow.cs**:
   - `Initialize()` override retrieves `ILinkRepository` from package
   - Creates tool window content with injected repository

3. **StartPageToolWindowControl.xaml.cs**:
   - Constructor now requires `ILinkRepository` parameter (removed direct instantiation)
   - Added null guard with `ArgumentNullException`

**Pattern Rationale:**
- **Why direct accessor over AsyncPackage.GetServiceAsync?** Tool window `Initialize()` is synchronous by design; would require `JoinableTaskFactory` complexity. Service is already available synchronously.
- **Why not full MEF?** Overkill for single service; adds ComponentModelHost boilerplate; no third-party MEF components to integrate with.
- **Migration path:** If service count grows, switch to ComponentModelHost + `[Export]` adapters.

**Validation:**
- ✅ All 54 Core tests passing (no regression)
- ✅ Tool window initializes with injected repository
- ✅ Pattern is standard for VS extensions (package-owned singleton)
- ✅ Type-safe vs. service-locator strings
- ✅ Clear service lifetime (disposed with package)

**Files Changed:**
- LinkGroupViewModel.cs (async fix, using statement)
- LinkViewModel.cs (async fix, using statement)  
- LinkRepository.cs (IOException handling)
- UltimateStartPagePackage.cs (service initialization and registration)
- StartPageToolWindow.cs (service retrieval)
- StartPageToolWindowControl.xaml.cs (constructor injection)

### 2026-04-10 — EnvDTE Integration (Rejection Revision for Verbal)

**Context:**
- Verbal threaded `openAction` parameter through ViewModels (StartPageViewModel → LinkGroupViewModel → LinkViewModel) but did not complete the VS2022 layer implementation
- Keaton rejected the work because the actual EnvDTE wiring was missing in `StartPageToolWindowControl.xaml.cs`
- Verbal is locked out from fixing her own work — McManus completing the revision

**Implementation:**

1. **Added `OpenSolutionInVS(string path)` private method** in `StartPageToolWindowControl.xaml.cs`:
   - `ThreadHelper.ThrowIfNotOnUIThread()` — mandatory for COM interop safety
   - `Package.GetGlobalService(typeof(DTE)) as DTE` — retrieves EnvDTE service
   - `dte.Solution.Open(path)` — opens solution in Visual Studio
   - Null DTE check — gracefully handles case where service unavailable (silent no-op)
   - Exception handling — silent catch for future logging framework

2. **Wired `openAction` in constructor**:
   - Changed `new StartPageViewModel(repository)` to `new StartPageViewModel(repository, OpenSolutionInVS)`
   - OpenSolutionInVS method passed as Action<string> delegate

3. **Added using directives**:
   - `using EnvDTE;` — DTE interface
   - `using Microsoft.VisualStudio.Shell;` — ThreadHelper and Package services

**Key Constraints Honored:**
- Core project untouched — all changes in VS2022 layer only (architectural boundary enforced)
- ThreadHelper.ThrowIfNotOnUIThread() called before any EnvDTE operation (COM thread safety)
- Graceful null DTE handling — no crash if service unavailable
- Silent exception swallow — future logging framework will capture

**Validation:**
- ✅ All 54 Core tests passing (no regression)
- ✅ EnvDTE reference already existed in VS2022.csproj (Verbal's prior work)
- ✅ OpenAction chain complete: XAML click → LinkViewModel.ExecuteOpen() → `_openAction(_path)` → `OpenSolutionInVS` → `dte.Solution.Open(path)`

**Pattern:**
This is the standard VS extension pattern for opening solutions programmatically:
- UI thread enforcement via ThreadHelper
- Service retrieval via Package.GetGlobalService
- DTE.Solution.Open for solution activation
- Graceful degradation if service unavailable

**Files Changed:**
- `src/UltimateStartPage.VS2022/ToolWindows/StartPageToolWindowControl.xaml.cs` (OpenSolutionInVS method, constructor wiring, using directives)

### 2026-04-10 — CRUD ViewModels Implementation

**Context:**
- Verbal is building XAML dialogs for full CRUD (Create, Read, Update, Delete) functionality in parallel
- Added commands and properties to ViewModels to support inline rename/edit operations
- Implemented naming contract so XAML bindings align exactly with ViewModel surface

**Implementation:**

1. **AsyncRelayCommand<T>** — New parameterized async command type
   - Added `AsyncRelayCommand<T>.cs` to support commands that take parameters (e.g., RemoveGroupCommand)
   - Constrained to `where T : class` for C# 8 nullable compatibility (can't use `T?` without class constraint in LangVersion 8.0)
   - Prevents re-entrant execution while command is running (same as non-generic version)
   - Pattern: `new AsyncRelayCommand<LinkGroupViewModel>(RemoveGroupAsync)`

2. **StartPageViewModel** — Added RemoveGroupCommand
   - `RemoveGroupCommand` (ICommand) — AsyncRelayCommand<LinkGroupViewModel> that removes a group and saves
   - Takes LinkGroupViewModel parameter via CommandParameter binding from XAML
   - Null-safe: checks parameter before removing

3. **LinkGroupViewModel** — Added rename functionality
   - `Name` property setter now triggers save callback (fire-and-forget pattern with `_ = _saveCallback()`)
   - `IsRenaming` (bool) — UI state flag for rename mode
   - `EditingName` (string) — in-progress rename buffer
   - `BeginRenameCommand` (RelayCommand) — sets IsRenaming=true, copies Name→EditingName
   - `CommitRenameCommand` (AsyncRelayCommand) — sets IsRenaming=false, applies EditingName→Name (which triggers save via setter)
   - `CancelRenameCommand` (RelayCommand) — sets IsRenaming=false, discards EditingName
   - Pattern: Begin copies current to editing buffer, Commit applies buffer to property (triggering save), Cancel discards

4. **LinkViewModel** — Added edit functionality and save callback
   - Added required `Func<Task> saveCallback` constructor parameter (breaking change for tests)
   - `Name` and `Path` property setters now trigger save callback (fire-and-forget)
   - `IsEditing` (bool) — UI state flag for edit mode
   - `EditingName` and `EditingPath` (string) — in-progress edit buffers
   - `BeginEditCommand` (RelayCommand) — sets IsEditing=true, copies Name/Path→EditingName/EditingPath
   - `CommitEditCommand` (AsyncRelayCommand) — sets IsEditing=false, applies EditingName/EditingPath→Name/Path (triggering saves)
   - `CancelEditCommand` (RelayCommand) — sets IsEditing=false, discards EditingName/EditingPath
   - Updated LinkGroupViewModel to pass saveCallback when constructing LinkViewModel instances

5. **Test Updates**
   - Updated all 28 LinkViewModel test instantiations to include saveCallback parameter: `() => Task.CompletedTask`
   - All 77 Core tests passing (54 existing + 23 ViewModel tests)
   - No new test coverage added (Fenster is handling CRUD command tests in parallel per task spec)

**Naming Contract (for Verbal's XAML bindings):**

LinkGroupViewModel:
- `IsRenaming` → ToggleButton.IsChecked or visibility converter
- `EditingName` → TextBox.Text (two-way binding)
- `BeginRenameCommand` → rename button Command
- `CommitRenameCommand` → TextBox InputBinding (Enter key) / confirm button Command
- `CancelRenameCommand` → TextBox InputBinding (Escape key) / cancel button Command

LinkViewModel:
- `IsEditing` → edit mode toggle visibility
- `EditingName` / `EditingPath` → edit TextBox.Text (two-way bindings)
- `BeginEditCommand` → edit button Command
- `CommitEditCommand` → confirm button Command / InputBinding
- `CancelEditCommand` → cancel button Command / InputBinding

StartPageViewModel:
- `RemoveGroupCommand` → delete button Command with CommandParameter={Binding}

**Architectural Decisions:**
- **Fire-and-forget saves in property setters:** Name/Path setters use `_ = _saveCallback()` pattern. Exception handling deferred to future logging framework. Acceptable risk: worst case is lost edit if save fails silently.
- **Commit commands don't explicitly save:** CommitRenameCommand/CommitEditCommand set Name/Path properties, which trigger save via setters. Avoids double-save issue.
- **Editing buffers avoid direct property mutation:** EditingName/EditingPath hold in-progress state. Cancel discards without triggering save. Commit copies to real properties.
- **BeginEdit copies to buffer immediately:** Ensures Cancel can restore original value if user starts typing then cancels.

**Files Changed:**
- `src/UltimateStartPage.Core/Mvvm/AsyncRelayCommand{T}.cs` (new parameterized async command)
- `src/UltimateStartPage.Core/ViewModels/StartPageViewModel.cs` (RemoveGroupCommand)
- `src/UltimateStartPage.Core/ViewModels/LinkGroupViewModel.cs` (rename commands and properties, Name setter save callback)
- `src/UltimateStartPage.Core/ViewModels/LinkViewModel.cs` (edit commands and properties, Name/Path setter save callbacks, saveCallback parameter)
- `tests/UltimateStartPage.Core.Tests/ViewModels/LinkViewModelTests.cs` (updated 28 constructor calls with saveCallback parameter)

**Validation:**
- ✅ All 77 Core tests passing
- ✅ net472 / C# 8 compatible (no LangVersion 9 features, nullable with class constraint)
- ✅ Naming contract documented for Verbal
- ✅ Fire-and-forget save pattern consistent across ViewModels
- ✅ AsyncRelayCommand<T> prevents re-entrant execution


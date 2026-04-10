# Squad Decisions

## Active Decisions

### 1. Extensibility Model: VSSDK (AsyncPackage) over VisualStudio.Extensibility

**Decided:** 2026-04-10  
**Owner:** Keaton (Team Lead)  
**Status:** Accepted

- **Decision:** Use VSSDK (legacy in-process model) with `AsyncPackage` base class and `ToolWindowPane` for VS2022/2026 extension
- **Rationale:** Full WPF control required for rich start page UI (drag-drop, context menus, custom layouts). VisualStudio.Extensibility (OOP model) uses restricted Remote UI with serialised XAML DataTemplates — insufficient for requirements
- **Implications:** 
  - Direct WPF/XAML control (no Remote UI restrictions)
  - Mature 15+ year ecosystem
  - Requires async initialisation in VS2022+
  - In-process (not out-of-process)
- **Risks:** VisualStudio.Extensibility may mature and become preferred in future VS versions; monitor MS announcements

---

### 2. Start Page Mechanism: Tool Window in Document Well

**Decided:** 2026-04-10  
**Owner:** Keaton (Team Lead)  
**Status:** Accepted

- **Decision:** Custom `ToolWindowPane` docked in document well (centre IDE) providing start page experience
- **Rationale:** BetterStartPage's old mechanism (Asset Type="StartPage", custom start page XAML replacement) was removed in VS2019. No viable replacement in VS2022+. Tool window in document well is closest approximation to "start page" feel
- **Implementation Pattern:**
  - `ProvideAutoLoad(UIContextGuids80.NoSolution, BackgroundLoad)` to auto-show when no solution open
  - Subscribe to `IVsSolutionEvents` to show/hide on solution open/close
  - Dock in document well via `ProvideToolWindow` + `VsDockStyle.Tabbed` and document group GUID
- **Risks:** Tool window behaviour in document well may differ from true document tabs. Requires early prototyping to validate UX feel
- **Acceptance:** F5 debugging shows tool window auto-appearing in VS2022 experimental instance

---

### 3. Solution Structure: Core Library + Separate VSIX per VS Version

**Decided:** 2026-04-10  
**Owner:** Keaton (Team Lead)  
**Status:** Accepted

- **Decision:** 
  - Separate `UltimateStartPage.Core` class library (net472, zero VS SDK dependencies)
  - Separate VSIX project per VS major version (VS2022 v17.x now; VS2026 v18.x when 18.x SDK ships)
  - Optional shared project only if conditional compilation required between versions
- **Rationale:**
  - Core library enables unit testing without VS SDK or test host
  - Shared business logic across VS versions reduces duplication
  - Each VSIX project remains lightweight (only manifest, package class, VS-specific service impls)
  - Microsoft's recommended approach for multi-version extensions
- **Implications:** Cannot ship one VSIX for both VS2022 and VS2026 (they require different SDK versions)
- **Compliance:** Ensure Core project has zero `Microsoft.VisualStudio.*` dependencies (build gate)

---

### 4. Data Storage: JSON File in %APPDATA%

**Decided:** 2026-04-10  
**Owner:** Keaton (Team Lead)  
**Status:** Accepted

- **Decision:** Store extension settings as JSON file in `%APPDATA%\UltimateStartPage\settings.json` instead of `IVsWritableSettingsStore`
- **Rationale:**
  - **Portable:** Users can back up, copy, version-control settings
  - **Human-readable:** Easy to debug and edit manually
  - **VS-version-independent:** Same settings file works across VS2022, VS2026, future versions
  - **Import/Export:** File itself is the export format
- **Serialisation:** `System.Text.Json` with `WriteIndented = true`
- **Service Layer:** `ISettingsService` interface with `Load()`, `Save()`, `GetSettingsFilePath()`

---

### 5. Testing: xUnit + NSubstitute + FluentAssertions

**Decided:** 2026-04-10  
**Owner:** Fenster (QA Tester)  
**Status:** Accepted

- **Unit Tests:** xUnit (modern, parallel, `[Theory]` parameterised tests)
- **Mocking:** NSubstitute (fluent, readable, zero boilerplate)
- **Assertions:** FluentAssertions (rich vocabulary, readable failure messages)
- **Integration Tests:** Microsoft.VisualStudio.Sdk.TestFramework (mock VS host, when needed)
- **Coverage:** coverlet + ReportGenerator for CI coverage reporting
- **Targets:**
  - Core project: 85% line coverage minimum (PR gate enforced)
  - Domain models: 90% minimum
  - Serialisation: 90% minimum
  - Path utilities: 95% minimum
- **Architecture:** Wrap every VS service behind owned interface (e.g., `ISettingsStore`, `ISolutionBrowser`). Unit tests mock; integration tests use real implementations

---

### 6. VSSDK Package Requirements

**Decided:** 2026-04-10  
**Owner:** Keaton (Team Lead)  
**Status:** Accepted

- **For VS2022 (v17.x):**
  - `Microsoft.VisualStudio.Sdk` (metapackage, v17.x)
  - `Microsoft.VSSDK.BuildTools` (v17.x)
- **For VS2026 (v18.x):**
  - `Microsoft.VisualStudio.Sdk` (v18.x, when available)
  - `Microsoft.VSSDK.BuildTools` (v18.x, when available)
- **UI/MVVM:**
  - `CommunityToolkit.Mvvm` (lightweight MVVM infrastructure)

---

### 7. VS2026 Targeting: Deferred Pending SDK Availability

**Decided:** 2026-04-10  
**Owner:** Keaton (Team Lead)  
**Status:** Accepted (with contingency)

- **Decision:** Build for VS2022 now. Add VS2026 targeting once 18.x SDK NuGet packages ship
- **Assumption:** VS2026 (v18.x) will use same VSSDK extensibility model as VS2022 (based on VS2019→VS2022 pattern)
- **Watch For:**
  - Microsoft's VS2026 extension migration guidance (not yet published)
  - 18.x SDK NuGet availability
  - Possibility of .NET 8+ requirement for VS2026 extensions (would require Core multi-targeting)
- **Architecture:** Designed to support easy multi-targeting; no rework expected

---

### 8. Solution Scaffold: Core Library + VSIX Architecture

**Decided:** 2025-07-14  
**Owner:** McManus (.NET Developer)  
**Status:** Accepted

- **Decision:** Scaffold initial solution with three projects:
  - `UltimateStartPage.Core` (net472, SDK-style): Models, services, zero VS SDK dependencies
  - `UltimateStartPage.VS2022` (net472, legacy csproj): AsyncPackage, ToolWindowPane, VSIX manifest
  - `UltimateStartPage.Core.Tests` (net472, SDK-style): xUnit + NSubstitute test suite
- **Key Choices:**
  - Core TFM: `net472` (matches VS2022 extension host platform)
  - VSIX csproj: Legacy (non-SDK) format with `PackageReference` NuGet (SDK-style VSSDK not supported)
  - Package base class: `AsyncPackage` with `[ProvideAutoLoad(NoSolution, BackgroundLoad)]`
  - Auto-load trigger: Shows start page when VS opens with no solution
  - VSIX manifest: `[17.0, 18.0)` for VS2022 only
  - Nullable annotations: Enabled on Core; models use `string?` for honesty with parameterless constructors
- **Test Status:** 4/4 xUnit tests passing (Core.Tests project)
- **Architecture:** Decoupled Core from VS SDK; VSIX project adapts VS-specific services
- **Next Steps:**
  - Verbal: Design real start page UI (XAML replacement)
  - Fenster: Expand test coverage (LinkRepository, serialization tests)
  - McManus: Implement WritableSettingsStore integration, verify VSIX loads in VS2022 experimental instance
  - Keaton: Architectural review and sign-off before feature work

---

### 9. Keaton Scaffold Review: APPROVED (Mandatory Follow-ups)

**Decided:** 2026-04-10  
**Owner:** Keaton (Team Lead)  
**Status:** Accepted (with mandatory issue resolution)

- **Verdict:** Architecture is sound. Scaffold unblocks parallel work. Three medium-severity issues must be resolved before merge.

**Issues (Mandatory Before Merge):**

1. **LinkRepository.cs Comment Contradiction**
   - **File:** `src/UltimateStartPage.Core/Services/LinkRepository.cs` (line 9)
   - **Problem:** Comment references `WritableSettingsStore` for persistence, contradicting Decision #4 (JSON file in `%APPDATA%\UltimateStartPage\`)
   - **Fix:** Update comment to reference JSON file persistence per Decision #4
   - **Assigned:** Verbal
   - **Severity:** Medium (decision contradiction)

2. **Missing FluentAssertions Package**
   - **File:** `tests/UltimateStartPage.Core.Tests/UltimateStartPage.Core.Tests.csproj`
   - **Problem:** FluentAssertions NuGet not referenced; tests use basic `Assert.Equal`/`Assert.Throws` instead of fluent API, violating Decision #5
   - **Fix:** Add `FluentAssertions` NuGet; refactor tests to use fluent assertions (`.Should().Be()`, `.Should().Throw()`); add `coverlet.collector`
   - **Assigned:** Fenster (QA owns test infrastructure)
   - **Severity:** Medium (decision violation)

3. **IncludeAssemblyInVSIXContainer Set to false**
   - **File:** `src/UltimateStartPage.VS2022/UltimateStartPage.VS2022.csproj` (line 21)
   - **Problem:** `IncludeAssemblyInVSIXContainer` is `false`; standard VSIX templates use `true`. Risk: extension DLL may not be packaged
   - **Fix:** Validate during F5 prototyping. If VSIX deployment fails, flip to `true`
   - **Assigned:** Verbal (first to F5 debug)
   - **Severity:** Medium (potential build/deployment bug)

**Approved For:**
- ✅ UI work — Verbal can start immediately on XAML replacement and ViewModel
- ✅ Parallel work — scaffold architecture unblocks team

**Notes (Non-blocking):**
- CommunityToolkit.Mvvm deferred until ViewModel layer designed
- SolutionLink/LinkGroup enhancements (SortOrder, IsPinned, IsExpanded) deferred to later iteration
- ProvideToolWindow document-well docking validated during F5 per Decision #2

---

### 10. VS Theming Approach

**Decided:** 2026-04-10  
**Owner:** Verbal (WPF/UI Developer)  
**Status:** Accepted

- **Decision:** Use `{DynamicResource {x:Static vsui:VsBrushes.XxxKey}}` throughout the XAML for theme awareness.
- **Rationale:** Live theme-change aware — WPF re-evaluates `DynamicResource` when VS switches themes. `VsBrushes` type is the established pattern for VS extension WPF controls.
- **Namespace:** `xmlns:vsui="clr-namespace:Microsoft.VisualStudio.Shell;assembly=Microsoft.VisualStudio.Shell.15.0"`
- **Build Infrastructure:** Explicit `<Reference>` items added for Shell.15.0, Shell.Framework, EnvDTE, and WPF assemblies (all with `<Private>False</Private>`) — required for WPF wpftmp projects using PackageReference.

---

### 11. Start Page Layout & ViewModel Architecture

**Decided:** 2026-04-10  
**Owner:** Verbal (WPF/UI Developer)  
**Status:** Accepted (temporary ViewModel location)

- **Decision:** `DockPanel` root with `Border` header (docked Top), `ScrollViewer` + `WrapPanel` for tile groups (190×58), empty state via `DataTrigger` on `StartPageViewModel.HasGroups`.
- **Rationale:** `DockPanel` avoids nested Grid; `WrapPanel` reflows tiles naturally on resize. Empty state triggers on computed bool property without requiring a Converter.
- **ViewModel Location (TEMPORARY):** ViewModels (`StartPageViewModel`, `LinkGroupViewModel`, `LinkViewModel`) currently in `src/UltimateStartPage.VS2022/ViewModels/` as stubs only.
  - **Mandatory:** McManus must move to `UltimateStartPage.Core` before implementing real logic (zero VS SDK dependency)
  - **Hand-rolled Command:** Simple `RelayCommand` using `CommandManager.RequerySuggested` (no `CommunityToolkit.Mvvm` in VS2022 due to wpftmp build issue)
  - **DI Bootstrap:** Code-behind sets `DataContext = new StartPageViewModel()` — McManus to integrate service-provider wiring
- **Tile dimensions:** 190×58 (hardcoded defaults, to be refined with real content).

---

### 12. Test Stack: xUnit + NSubstitute + FluentAssertions

**Decided:** 2025-07-16 (Confirmed 2026-04-10)  
**Owner:** Fenster (QA Tester)  
**Status:** Accepted (Decision #5 compliance verified)

- **Packages:** xUnit 2.6.6, NSubstitute 5.1.0, FluentAssertions 6.12.0, Microsoft.NET.Test.Sdk 17.8.0
- **Implementation:** Fluent assertions throughout; 18 tests covering SolutionLink models and LinkRepository edge cases (null validation, duplicate paths, non-existent removals, SaveGroupsAsync behavior)
- **Coverage Gate:** 85% Core line coverage minimum (enforced at PR gate)
- **Deferred:** Serialization tests (JSON round-trip) to later session

---

### 13. Keaton XAML Shell Review: APPROVED

**Decided:** 2025-07-14  
**Reviewer:** Keaton (Team Lead)  
**Author Under Review:** Verbal (XAML/UI)  
**Status:** Approved

- **Scope:** Reviewed Verbal's scaffold fixes and XAML shell deliverables (LinkRepository comment, Core reference, manifest, XAML/ViewModel/RelayCommand)
- **Verdict:** APPROVED — no blocking issues

**Review Findings:**

1. **Scaffold Fixes — PASS**
   - LinkRepository.cs comment updated to reference JSON/%APPDATA% per Decision #4 ✓
   - Core ProjectReference: `IncludeAssemblyInVSIXContainer=true` set correctly ✓
   - Manifest: Three InstallationTargets (Community/Professional/Enterprise), `[17.0, 18.0)`, CoreEditor prerequisite, metadata populated ✓
   - **Note:** Main project's `IncludeAssemblyInVSIXContainer=false` (line 21) — plausible (pkgdef + `RegisterWithCodebase`), but non-standard. McManus to validate F5; if VSIX fails to load, flip to `true`

2. **VS Theming — PASS**
   - Every colour/brush uses `{DynamicResource {x:Static vsui:VsBrushes.*Key}}` — no hardcoded colours
   - 15+ brush references verified (Backgrounds, Text, Buttons, Borders, Hover/Press)
   - Button inherits VS shell theming correctly

3. **MVVM Compliance — PASS**
   - All three ViewModels implement `INotifyPropertyChanged` with `[CallerMemberName]` pattern
   - `HasGroups` raises change notification via `Groups.CollectionChanged` subscription
   - All XAML bindings map to real ViewModel properties
   - `ICommand` exposed via `RelayCommand`
   - No VS SDK or WPF-specific dependencies in ViewModel logic

4. **XAML Quality — PASS**
   - Styles and DataTemplates in `UserControl.Resources` (not inline)
   - Layout: `DockPanel` root (header + body), `WrapPanel` for responsive tiles, `Grid` for groups/empty-state
   - Empty state driven by `DataTrigger` on `HasGroups` (no code-behind visibility logic)
   - `DesignInstance` set for Blend/designer support

5. **Code-Behind — PASS**
   - `StartPageToolWindowControl.xaml.cs`: 17 lines. `InitializeComponent()` + stub `DataContext`. No business logic. Clear handoff comment for McManus.

6. **ViewModel Location — ACKNOWLEDGED**
   - Correctly noted ViewModels should move to Core
   - `RelayCommand` dependency on WPF API (`CommandManager.RequerySuggested`) blocks today
   - **Migration path:** Replace with `CommunityToolkit.Mvvm.Input.RelayCommand` (net472 compatible, no WPF dependency). Decision #6 already includes toolkit. Low risk.

**Minor Nits (Non-blocking):**
- Unused `using System.Collections.ObjectModel;` in LinkViewModel.cs
- Main project `IncludeAssemblyInVSIXContainer=false` — F5 validation needed

**Ready for McManus — YES**

ViewModel structure clean and well-organized:
- `StartPageViewModel` → root, `Groups` collection, `HasGroups`, `AddGroupCommand`
- `LinkGroupViewModel` → `Name`, `Links` collection
- `LinkViewModel` → `Name`, `Path`, `OpenCommand`

All stubs include `// TODO (McManus)` comments with specific implementation guidance.

**McManus Next Steps:**
1. Add `CommunityToolkit.Mvvm` NuGet to Core project
2. Move ViewModels to `UltimateStartPage.Core.ViewModels`, replace manual INPC with `ObservableObject` and `RelayCommand` with toolkit version
3. Delete `ViewModels\RelayCommand.cs` from VS2022 project
4. Inject `ILinkRepository` into `StartPageViewModel` constructor
5. Implement `LoadGroupsAsync()` — populate `Groups` from repository on tool window load
6. Implement `ExecuteAddGroup()` — prompt for group name, persist via `ILinkRepository`
7. Implement `ExecuteOpen()` in `LinkViewModel` — open solution via DTE or `IVsUIShellOpenDocument`
8. Replace stub `DataContext` in code-behind with DI-resolved ViewModel via package service provider
9. **F5 validate** VSIX deployment in VS2022 experimental instance

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

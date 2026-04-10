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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

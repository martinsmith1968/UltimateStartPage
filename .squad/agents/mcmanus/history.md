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

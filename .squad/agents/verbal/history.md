# Verbal — History

## Project Context

- **Project:** UltimateStartPage
- **Stack:** .NET (C#), WPF, XAML — Visual Studio 2022/2026 extension (VSIX)
- **User:** Martin Smith
- **Role:** WPF/UI Developer — XAML, VS theming, custom controls, data binding
- **Team:** Keaton (Team Lead / Reviewer), McManus (.NET Developer), Fenster (QA Tester), Scribe (logger), Ralph (monitor)

## Learnings

> Append new learnings here after each session.

### 2026-04-10 — Cross-Assembly clr-namespace Pattern for XAML Binding

When a XAML file in one assembly needs to bind to ViewModels living in a *different* assembly, the `xmlns` alias must include the `;assembly=` qualifier:

```xml
xmlns:viewmodels="clr-namespace:UltimateStartPage.Core.ViewModels;assembly=UltimateStartPage.Core"
```

Without `;assembly=`, WPF's XAML parser assumes the type lives in the **same** assembly as the XAML (i.e. `UltimateStartPage.VS2022`), causing a BAML parse error at runtime. The `assembly=` value must match the **assembly name** (not the namespace), which is the value of `<AssemblyName>` in the referenced project's csproj. For the VSIX project to resolve the type at runtime, `<IncludeAssemblyInVSIXContainer>true</IncludeAssemblyInVSIXContainer>` must also be set on the `<ProjectReference>` to ensure the Core DLL is packaged into the VSIX.

### 2026-04-10 — Architectural Context (from Keaton)

**Key Architectural Decisions for UI/MVVM Implementation:**

1. **WPF + MVVM Architecture:**
   - ViewModels live in `UltimateStartPage.Core` (no VS SDK dependency)
   - Views (XAML + code-behind) live in `UltimateStartPage.Vs2022` VSIX project
   - Use `CommunityToolkit.Mvvm` for `ObservableObject`, `RelayCommand`, source generators (lightweight, no hand-rolled MVVM)

2. **VS Theming Integration:**
   - Use `VsBrushes` for standard colours (backgrounds, text, links): `{DynamicResource {x:Static vs:VsBrushes.ToolWindowBackgroundKey}}`
   - Use `EnvironmentColors` for environment-specific colours
   - Use `VsResourceKeys` for themed dialog styles
   - Bindings via `INotifyPropertyChanged` / `ObservableCollection<T>`
   - Commands via `ICommand` / `RelayCommand` pattern

3. **Start Page Tool Window:**
   - Host in `StartPageToolWindowPane : ToolWindowPane` (WPF UserControl inside)
   - Auto-show via `ProvideAutoLoad(UIContextGuids80.NoSolution)` + `BackgroundLoad`
   - Dock in document well (centre IDE): `ProvideToolWindow(VsDockStyle.Tabbed, Window="DocumentGroup")`
   - Subscribe to `IVsSolutionEvents` to show/hide on solution open/close

4. **UI Components (to be built):**
   - Project Groups Panel — grid of customisable project groups, each with links to .sln/.csproj
   - Recent Projects Section — from VS MRU (`IVsMRUItemsStore` or `DTE.RecentFiles`)
   - Quick Actions — buttons ("Open Project", "Clone", "Create New") delegating to VS commands
   - Edit Mode — toggle to add/remove/reorder groups and items
   - Drag & Drop — drop .sln/.csproj from Windows Explorer to add them

5. **Data Model (from Core):**
   ```csharp
   StartPageSettings { GroupColumns, ProjectColumnsPerGroup, Groups[], ShowRecentProjects, MaxRecentProjects }
   ProjectGroup { Id, Title, SortOrder, Projects[] }
   ProjectLink { Id, Name, CustomName, FilePath, LinkType, SortOrder, LastOpened }
   ProjectLinkType { Solution, Project, Folder, Url }
   ```

6. **Testing for UI/ViewModels:**
   - Test ViewModels only, never Views directly (WPF dispatcher limitations)
   - Mock `ISettingsService`, `ISolutionBrowser`, etc. via `NSubstitute`
   - Test `INotifyPropertyChanged` fires on every bound property
   - Test commands' `Execute()` and `CanExecute()` logic
   - Test observable collection changes propagate

**Design-Time Data:**
- Build design-time XAML `d:DataContext` bindings to visually verify layouts without running extension
- Use Builder pattern for test data (`LinkItemBuilder`, `LinkGroupBuilder`)

**Phase Sequence (after McManus Phase 1 scaffold):**
1. Implement minimal WPF UserControl with hardcoded "Hello" text (prove docking works)
2. Build data model classes in Core (LinkItem, LinkGroup, LinkCollection)
3. Build ViewModels with MVVM infrastructure
4. Implement ProjectGroups panel XAML
5. Wire up settings persistence (JSON serialisation in Core)
6. Add drag & drop
7. Add edit mode toggle

---

### 2026-04-10 — XAML Shell Build + Review Fixes (Verbal session)

#### Fix 1: LinkRepository.cs comment
- Corrected the comment referencing `WritableSettingsStore` to accurately reflect Decision #4: JSON in `%APPDATA%\UltimateStartPage\settings.json` via `System.Text.Json`.

#### Fix 2: IncludeAssemblyInVSIXContainer
- Added `<IncludeAssemblyInVSIXContainer>true</IncludeAssemblyInVSIXContainer>` to the Core project reference in the VS2022 csproj, with a comment explaining why it's required.

#### XAML Shell: VS Theming Approach
- Used `{DynamicResource {x:Static vsui:VsBrushes.XxxKey}}` throughout (live theme-change aware).
- Key colour mappings:
  - Background: `VsBrushes.ToolWindowBackgroundKey`
  - Text: `VsBrushes.ToolWindowTextKey`
  - Border: `VsBrushes.ToolWindowBorderKey`
  - Muted text: `VsBrushes.GrayTextKey`
  - Button face/text: `VsBrushes.ButtonFaceKey` / `VsBrushes.ButtonTextKey`
  - Hover: `VsBrushes.CommandBarHoverKey` / `VsBrushes.CommandBarBorderKey`
  - Pressed: `VsBrushes.HighlightKey`

#### XAML Layout Structure
- `DockPanel` root: header bar docked Top, `ScrollViewer` fills rest.
- Header: `DockPanel` with title `TextBlock` left, "Add Group" `Button` right-aligned.
- Groups: `ItemsControl` with `WrapPanel` for tiles.
- Empty state: second `TextBlock` visibility toggled via `DataTrigger` on `HasGroups`.
- Link tile: custom `ControlTemplate` on `Button` with hover/press triggers.

#### ViewModels (stubs, in VS2022 project temporarily)
- `StartPageViewModel`: `ObservableCollection<LinkGroupViewModel> Groups`, `bool HasGroups`, `ICommand AddGroupCommand`.
- `LinkGroupViewModel`: `string Name`, `ObservableCollection<LinkViewModel> Links`.
- `LinkViewModel`: `string Name`, `string Path`, `ICommand OpenCommand`.
- `RelayCommand`: minimal `ICommand` implementation using `CommandManager.RequerySuggested`.
- Implemented with standard `INotifyPropertyChanged` (no CommunityToolkit dependency in VS2022 project — see gotcha below).

#### Build Gotchas: Legacy VSIX csproj + WPF + MSBuild from command line

**1. WPF wpftmp project doesn't inherit PackageReferences**
The WPF markup compiler (`MarkupCompilePass1`) creates a temporary `_wpftmp.csproj` that compiles ALL source files. This project does NOT inherit `<PackageReference>` items — only explicit `<Reference>` items. Because `Microsoft.VisualStudio.SDK` is a PackageReference, the VS SDK assemblies were not available to the wpftmp project. Fix: add explicit `<Reference>` items with `<HintPath>` for all required VS SDK assemblies and `<Private>False</Private>` to prevent copying.

**2. VS-installed Shell.15.0 vs NuGet version**
The resolver picks the VS-installed `Microsoft.VisualStudio.Shell.15.0.dll` (from `Common7\IDE\PublicAssemblies\`) over the NuGet HintPath version. The VS-installed version (17.0.0.0) references `Microsoft.VisualStudio.Threading` version `17.14.0.0`, while the NuGet package has `17.0.0.0`. Fix: reference Threading from the VS MSBuild VSSDK directory via `$(MSBuildBinPath)\..\..\Microsoft\VisualStudio\v17.0\VSSDK\Microsoft.VisualStudio.Threading.dll`.

**3. WPF compilation outputs to obj\Debug, not bin\Debug**
With `<CopyBuildOutputToOutputDirectory>false</CopyBuildOutputToOutputDirectory>`, the WPF wpftmp compiler outputs the DLL to `obj\Debug\` and the main CoreCompile is then skipped (considers output up-to-date). The VSSDK `CreatePkgDef` task expects the DLL at `$(TargetPath)` = `bin\Debug\`. Fix: add `<CreatePkgDefAssemblyToProcess>$(MSBuildProjectDirectory)\obj\$(Configuration)\$(AssemblyName).dll</CreatePkgDefAssemblyToProcess>`.

**4. VSIX manifest issues**
- Manifest must be `<None>` build action (not `<Content>`) for the VSSDK `FindVsixManifest` task.
- The newer VS-installed VSSDK validator requires `<ProductArchitecture>amd64</ProductArchitecture>` as a **child element** (not attribute) of each `<InstallationTarget>`.

**5. CommunityToolkit.Mvvm and wpftmp**
Adding `CommunityToolkit.Mvvm` as a `PackageReference` causes the wpftmp project to fail (it's another PackageReference that's invisible to wpftmp). For now, ViewModels use hand-rolled `INotifyPropertyChanged` + `RelayCommand`. Note: per architecture decisions, ViewModels should move to Core (which can use CommunityToolkit) — when that happens, this issue disappears.

#### Still to do (McManus)
- Implement `AddGroupCommand` — prompt for name, persist via `ILinkRepository`
- Implement `LinkViewModel.OpenCommand` — open `.sln`/`.csproj` via DTE or `IVsUIShellOpenDocument`
- Move ViewModels to `UltimateStartPage.Core` (per Decision — ViewModels should be in Core)
- Load groups from `ILinkRepository` on ViewModel init, replace stub `new StartPageViewModel()`

---

### 2026-04-10 — Stale ViewModels Cleanup + EnvDTE Wiring

#### Deleted Stale ViewModel Files (Part 1)

McManus previously migrated all ViewModels to `UltimateStartPage.Core` but left stub files in `src/UltimateStartPage.VS2022/ViewModels/` with "moved" comments. Cleaned up:

- Deleted `StartPageViewModel.cs`
- Deleted `LinkGroupViewModel.cs`
- Deleted `LinkViewModel.cs`
- Deleted `RelayCommand.cs`
- Deleted empty `ViewModels\` directory

The VS2022 `.csproj` had no explicit `<Compile>` entries for these files (SDK-style wildcard inclusion was disabled via legacy format), so no project file changes were needed.

#### Wired EnvDTE.Solution.Open() for Opening Solutions (Part 2)

**Challenge:** `LinkViewModel` lives in `Core` (net472, no VS SDK dependency) but needs to open `.sln` files via EnvDTE in the VS2022 layer.

**Approach:** Dependency injection via optional `Action<string>` delegate:

1. **Core ViewModels** (`LinkViewModel`, `LinkGroupViewModel`, `StartPageViewModel`):
   - Added optional `Action<string>? openAction` constructor parameter
   - Default to `null` (falls back to `Process.Start` for shell-default open)
   - Thread through constructor chain from `StartPageViewModel` → `LinkGroupViewModel` → `LinkViewModel`
   - `LinkViewModel.ExecuteOpen()` checks if `openAction` is provided; if so, use it; otherwise, use `Process.Start`

2. **VS2022 Layer** (`StartPageToolWindowControl.xaml.cs`):
   - Added `OpenSolutionInVS(string path)` method that uses EnvDTE:
     ```csharp
     var dte = (DTE)Package.GetGlobalService(typeof(DTE));
     dte.Solution.Open(path);
     ```
   - Pass this method as the `openAction` when constructing `StartPageViewModel`
   - Added `EnvDTE` using directive and `Microsoft.VisualStudio.Shell` for `ThreadHelper`

3. **Verified**:
   - EnvDTE reference already exists in VS2022 `.csproj` (line 101-104)
   - Core tests still pass (54/54) — no VS SDK dependency introduced
   - MVVM separation maintained — Core has zero knowledge of VS APIs

**Rationale for Action<string> over IOpenSolution interface:**
- Simpler for single-method contract (no interface ceremony)
- Optional parameter with default null makes Core self-contained (can run without VS)
- Avoids adding interface to Core just for single method
- Consistent with existing callback pattern (`Func<Task>` for save, `Func<LinkViewModel, Task>` for remove)

**Files Changed:**
- `src/UltimateStartPage.Core/ViewModels/LinkViewModel.cs` — added `openAction` param, updated `ExecuteOpen()`
- `src/UltimateStartPage.Core/ViewModels/LinkGroupViewModel.cs` — added `openAction` param, pass through to LinkViewModel
- `src/UltimateStartPage.Core/ViewModels/StartPageViewModel.cs` — added `openAction` param, pass through to LinkGroupViewModel
- `src/UltimateStartPage.VS2022/ToolWindows/StartPageToolWindowControl.xaml.cs` — added `OpenSolutionInVS()`, wire to ViewModel

### Sprint 2 Completion — 2026-04-10

**Stale ViewModel Files Cleanup:**

Deleted obsolete code-behind duplicates (4 files) from `src/UltimateStartPage.VS2022/ViewModels/`:
- `StartPageViewModel.cs` (now in Core)
- `LinkGroupViewModel.cs` (now in Core)
- `LinkViewModel.cs` (now in Core)
- `IViewModel.cs` (outdated interface)

**Rationale:** Single-source-of-truth enforcement. All ViewModels live in Core; VS2022 project contains only UI layer (XAML, code-behind, DI composition). This cleanup prevents accidental dual-maintenance and confusion about which files are authoritative.

**EnvDTE Solution Opening Implementation (Decision: verbal-envdte-open.md):**

Completed VS-layer integration for opening .sln files inside Visual Studio via EnvDTE while maintaining MVVM separation and Core VS-SDK independence.

**Architecture:**
- Core ViewModels accept optional `Action<string>? openAction` constructor parameter (thread through parent-child chain)
- StartPageViewModel passes openAction to LinkGroupViewModel, which passes to LinkViewModel
- LinkViewModel.ExecuteOpen() checks if openAction provided; if yes, use it; if no, fallback to Process.Start

**VS2022 Code-Behind Implementation:**
```csharp
private void OpenSolutionInVS(string path)
{
    ThreadHelper.ThrowIfNotOnUIThread();
    if (string.IsNullOrWhiteSpace(path))
        return;
    
    var dte = (DTE)Package.GetGlobalService(typeof(DTE));
    if (dte?.Solution != null)
        dte.Solution.Open(path);
}
```
- Retrieve DTE from package global service
- Call `dte.Solution.Open(path)` to open .sln in VS (integrates with solution explorer)
- Null checks prevent crashes if package/DTE unavailable
- ThreadHelper assertion ensures COM safety on UI thread

**Pattern Rationale (why Action<string> over IOpenSolution interface):**
- Single-method contract = interface ceremony is overhead
- Optional parameter makes Core self-contained (no VS dependency)
- Consistent with existing callback pattern: `Func<Task>`, `Func<LinkViewModel, Task>`
- Easier to test (can pass mock action or null)

**Fallback Behavior:**
- If `openAction` is null, falls back to `Process.Start(path)` (shell default handler)
- Enables Core to run standalone (unit tests, mock scenarios)
- Graceful degradation if VS layer not available

**Compliance Verified:**
- ✅ Decision #3 (Core zero VS SDK): Core uses only `Action<string>` (BCL type), no EnvDTE references
- ✅ MVVM separation: VS APIs only in code-behind, ViewModels remain UI-agnostic
- ✅ Backward compatibility: Fallback path ensures Core can run standalone
- ✅ All 54 tests pass (no regression)

**Files Changed:**
- LinkViewModel.cs (openAction parameter, ExecuteOpen logic)
- LinkGroupViewModel.cs (openAction passthrough)
- StartPageViewModel.cs (openAction passthrough)
- StartPageToolWindowControl.xaml.cs (OpenSolutionInVS implementation)


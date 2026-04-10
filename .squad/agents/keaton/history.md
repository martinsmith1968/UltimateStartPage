# Keaton — History

## Project Context

- **Project:** UltimateStartPage
- **Stack:** .NET (C#)
- **User:** Martin Smith
- **Role:** Team Lead — architecture, code review, scope decisions
- **Team:** McManus (.NET Developer), Fenster (QA Tester), Scribe (logger), Ralph (monitor)

## Learnings

> Append new learnings here after each session.

### 2026-04-10 — Scaffold Review

**Reviewed:** McManus's solution scaffold (all projects, models, services, tool window, tests)
**Verdict:** APPROVED with mandatory follow-ups

**What was right:**
- Architecture matches all decisions: AsyncPackage, ToolWindowPane, net472, separate VSIX, [17.0,18.0) manifest range.
- Package attributes correct: `AllowsBackgroundLoading`, `ProvideAutoLoad` with `BackgroundLoad`, `ProvideToolWindow`.
- InitializeAsync correctly uses `SwitchToMainThreadAsync` and `FindToolWindowAsync`.
- ILinkRepository interface is clean — async, returns `IReadOnlyList<T>`, good granularity.
- ToolWindow stub is minimal and ready for Verbal.

**Issues found:**
1. LinkRepository.cs comment references `WritableSettingsStore` — contradicts Decision #4 (JSON in %APPDATA%). Assigned to Verbal/Fenster.
2. FluentAssertions missing from test project — violates Decision #5. Assigned to Fenster.
3. `IncludeAssemblyInVSIXContainer=false` — potentially wrong, needs F5 validation. Assigned to Verbal.

**Patterns noted:**
- McManus produced a clean scaffold on first attempt. No structural rework needed.
- Reviewer lockout works — issues assigned to Verbal/Fenster, not back to McManus.
- Document-well docking (`Window = vsWindowKindMainWindow`) is a known risk that needs prototyping.

### 2026-04-10 — Initial Architecture Assessment

**VS Start Page Mechanism (Dead):**
- BetterStartPage used `Asset Type="StartPage"` in VSIX manifest and injected controls into VS's internal `StartPage.xaml`. This mechanism was removed in VS2019. Not viable for VS2022+.

**Correct Approach — Tool Window:**
- VS2022+ extensions use `ToolWindowPane` hosted in an `AsyncPackage` to provide custom UI.
- Auto-show via `ProvideAutoLoad(UIContextGuids80.NoSolution)` with `PackageAutoLoadFlags.BackgroundLoad`.
- Place in document well for start-page-like experience. Subscribe to `IVsSolutionEvents` for show/hide on solution open/close.

**Two Extensibility Models Exist:**
- VSSDK (in-process, direct WPF, mature) — chosen for this project due to rich UI requirements.
- VisualStudio.Extensibility (out-of-process, Remote UI with serialised XAML DataTemplates) — too restrictive for drag-and-drop, context menus, and complex layouts.

**VS2022 + VS2026 Targeting:**
- VS2022 = version 17.x, VS2026 = version 18.x. Both 64-bit (amd64) only.
- Extensions with running code need separate VSIX projects per VS major version. Cannot ship one VSIX for both.
- 18.x SDK NuGet packages may not yet be available. Build VS2022 first, add VS2026 when SDK ships.

**Data Storage:**
- JSON file in `%APPDATA%\UltimateStartPage\` preferred over `IVsWritableSettingsStore`. More portable, human-readable, VS-version-independent.

**Solution Structure:**
- Core class library (net472, no VS SDK dependency) for models, view models, serialisation.
- Separate VSIX project per VS version. Shared project only if conditional compilation needed.

**Key Packages:**
- `Microsoft.VisualStudio.Sdk` (metapackage), `Microsoft.VSSDK.BuildTools`, `CommunityToolkit.Mvvm` (for MVVM infrastructure).

**Risks Flagged:**
- Tool window in document well may not feel exactly like a start page — needs early prototyping.
- VS2026 may change extension hosting model (e.g., .NET 8+) — monitor MS announcements.
- `ProvideAutoLoad` has perf concerns — mitigate with background loading.

### 2025-07-14 — XAML Shell Review

**Reviewed:** Verbal's scaffold fixes + XAML shell (tool window control, 3 ViewModels, RelayCommand)
**Verdict:** APPROVED

**What was right:**
- All VS theming via `DynamicResource {x:Static vsui:VsBrushes.*Key}` — zero hardcoded colours. Verified 15+ brush references.
- MVVM is clean: all 3 ViewModels implement `INotifyPropertyChanged` with `[CallerMemberName]`. `HasGroups` raises via `CollectionChanged` subscription.
- All XAML bindings map correctly to ViewModel properties. No mismatches found.
- Styles and DataTemplates in `UserControl.Resources` — not inline. Layout structure (DockPanel → header + ScrollViewer → WrapPanel tiles) is sensible.
- Code-behind is 17 lines. Stub DataContext only. No business logic.
- LinkRepository.cs comment correctly fixed to reference JSON/%APPDATA% per Decision #4.
- Core ProjectReference has `IncludeAssemblyInVSIXContainer=true`. Manifest has 3 SKU targets with amd64 + CoreEditor prerequisite.
- Empty state toggle via DataTrigger is clean — no code-behind visibility hacks.

**Patterns noted (WPF/VS extensions):**
- VS tool windows inherit theme resource dictionaries from the shell. Standard WPF controls (Button, TextBox) get VS theming automatically. Only custom ControlTemplates need explicit VsBrushes references.
- `CommandManager.RequerySuggested` is WPF-specific (PresentationCore). Blocks ViewModel migration to Core. CommunityToolkit.Mvvm solves this cleanly.
- `ICommand` lives in `System` assembly on net472, NOT PresentationCore. ViewModels can reference `ICommand` without WPF dependency.
- `IncludeAssemblyInVSIXContainer=false` on project output + `RegisterWithCodebase=true` is non-standard. VSSDK templates default to `true`. May work via pkgdef but needs F5 validation.
- WPF markup compiler `_wpftmp.csproj` doesn't inherit PackageReferences — explicit `<Reference>` items needed for XAML compilation. Verbal's workaround with hint paths is correct.

**Still open:**
- `IncludeAssemblyInVSIXContainer=false` on VS2022 project output — McManus must F5 validate.
- ViewModels live in VS2022 project temporarily — McManus migrates to Core with CommunityToolkit.Mvvm.

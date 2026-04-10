# Keaton — History

## Project Context

- **Project:** UltimateStartPage
- **Stack:** .NET (C#)
- **User:** Martin Smith
- **Role:** Team Lead — architecture, code review, scope decisions
- **Team:** McManus (.NET Developer), Fenster (QA Tester), Scribe (logger), Ralph (monitor)

## Learnings

> Append new learnings here after each session.

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

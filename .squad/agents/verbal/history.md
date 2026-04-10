# Verbal — History

## Project Context

- **Project:** UltimateStartPage
- **Stack:** .NET (C#), WPF, XAML — Visual Studio 2022/2026 extension (VSIX)
- **User:** Martin Smith
- **Role:** WPF/UI Developer — XAML, VS theming, custom controls, data binding
- **Team:** Keaton (Team Lead / Reviewer), McManus (.NET Developer), Fenster (QA Tester), Scribe (logger), Ralph (monitor)

## Learnings

> Append new learnings here after each session.

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

See `.squad/decisions.md` for full architectural decision record and test strategy details.

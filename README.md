# Ultimate Start Page

A customisable start page for **Visual Studio 2026** (and 2022). It's in the spirit of BetterStartPage and Start Page+: group your solutions, projects, folders and web links into your own sections, and keep Visual Studio's recent list and "get started" actions alongside them.

![icon](src/UltimateStartPage/Resources/Icon.png)

## Features

- **Your own sections.** Add, rename, collapse, reorder and delete cards of links.
- **Any kind of link.** Solutions (`.sln`, `.slnx`, `.slnf`), project files, folders (opened with *Open Folder*), individual files, and http(s) URLs. Each link can have a description, which shows as a tooltip.
- **Drag and drop.** Drag solutions or folders from Explorer onto a section to add them.
- **Recent items.** Visual Studio's own MRU list, with favourites first. Click **Pin** to copy an item into one of your sections.
- **Get started actions.** Open project/solution, Open folder, Clone repository, New project.
- **Search.** Type to filter sections, links and recent items at once. Press Esc to clear.
- **Missing-link detection.** Links whose target no longer exists are struck through, and clicking one offers to remove it.
- **Edit mode.** Click **Edit layout** to show reorder, rename and delete controls. Right-click menus are always available.
- **Import / export.** Share a layout with your team. Imported sections are added alongside yours; nothing is overwritten.
- **Hand-editable JSON.** Choose **… > Edit layout JSON file** to edit `layout.json` in the editor, then **Reload**.
- **Themed.** Uses Visual Studio's environment colours, so it follows Light, Dark and Blue themes.
- **Opens itself.** Shows at startup, closes when a solution opens and comes back when the solution closes. Each of these can be turned off.

Open it from **View > Other Windows > Ultimate Start Page**, **File > Ultimate Start Page**, or **Ctrl+Alt+Home**.

## Recommended VS setting

Under **Tools > Options > Environment > Startup**, set *On startup, open* to **Empty environment**. The built-in start window then won't appear before this page.

## Repository layout

```
UltimateStartPage.sln
src/
  UltimateStartPage.Core/        netstandard2.0 — models, JSON store, VS recent-list reader, view models (no VS dependency)
  UltimateStartPage/             net48 VSIX — package, tool window (WPF), dialogs, VS shell adapters, options page
tests/
  UltimateStartPage.Core.Tests/  xUnit tests for everything in Core
```

The core holds all the behaviour: layout persistence, recent-item parsing, search, add/edit/move/remove, import/export and save batching. The VSIX project is a thin set of adapters (`ILinkLauncher`, `IShellActions`, `IDialogService`, `IStartPageSettings`), so almost everything can be unit tested without Visual Studio.

## Building and debugging

Prerequisites: Visual Studio 2026 (or 2022 17.x) with the **Visual Studio extension development** workload.

1. Open `UltimateStartPage.sln`.
2. Set **UltimateStartPage** as the startup project and press **F5**. This launches the *experimental instance* (`/rootsuffix Exp`) with the extension installed.
3. The installable package is `src/UltimateStartPage/bin/<Configuration>/UltimateStartPage.vsix`. Double-click it to install into your normal VS instance.

Tests: `dotnet test tests/UltimateStartPage.Core.Tests` (or use Test Explorer).

### First-build notes

This project was written without access to a Windows build machine. The Core library and its 63 tests compile and pass. The VSIX project has not yet been compiled against the real VS SDK. If the first build fails, these are the likely spots:

| Where | What to check |
|---|---|
| `UltimateStartPage.csproj` | Package versions float (`17.*`, `16.*`). Pin them to what restores, e.g. the versions the *VSIX Project w/Command (Community)* template uses. |
| `ToolWindows/Converters.cs`, `StartPageWindow.cs`, `VSCommandTable.vsct` | Image monikers `Home` and `Link`. If either isn't in your SDK's `KnownMonikers`/`KnownImageIds`, swap it for any other, e.g. `Solution` or `Document`. |
| `Services/VsShellActions.cs` | The canonical name of the *Clone repository* command. The code tries several names; add yours if none of them match. |
| `UltimateStartPagePackage.cs` | `VS.Events.SolutionEvents` signatures come from Community.VisualStudio.Toolkit. Adjust them if a newer toolkit changed them. |

## Where things are stored

| What | Where |
|---|---|
| Your sections | `%APPDATA%\UltimateStartPage\layout.json` (configurable, see below) |
| Unreadable layout | Renamed to `layout.json.corrupt-<timestamp>` and replaced by the default layout, so nothing is silently lost |
| VS recent list (read-only) | `CodeContainers.Offline` in `%LOCALAPPDATA%\Microsoft\VisualStudio\<instance>\ApplicationPrivateSettings.xml` |
| Logs | Output window, *Ultimate Start Page* pane |

`layout.json` looks like this:

```json
{
  "schemaVersion": 1,
  "sections": [
    {
      "id": "0b7c…",
      "title": "Pricing",
      "isCollapsed": false,
      "links": [
        { "id": "5e1a…", "title": "Harness", "target": "C:\\Dev\\GitHub\\SS.Modelling.Harness\\SS.Modelling.Harness.sln", "kind": "Solution", "description": "Main pricing solution" },
        { "id": "9f02…", "title": "Team board", "target": "https://example.com/board", "kind": "Url" }
      ]
    }
  ]
}
```

`kind` is one of `Solution`, `Project`, `Folder`, `File` or `Url`. `id`s are optional when hand-editing, and comments and trailing commas are allowed. Writes go to a temp file first and are then swapped in, so a crash mid-save can't corrupt the file.

Every Visual Studio instance shares the file. When one instance saves, the others reload it automatically, and hand edits are picked up the same way. If two instances change the layout at almost the same moment, the second one to save merges the two by id, so a rename in one window and a new link in the other both survive. When both change the same thing, the window saving last wins, and a deletion never beats an edit. Anything that clashed is noted in the Output window.

## Options

**Tools > Options > Ultimate Start Page > General**

| Option | Default | |
|---|---|---|
| Show on startup | On | Opens the page when VS starts with no solution |
| Close when a solution opens | On | |
| Show when a solution closes | On | Skipped while switching solutions and during shutdown |
| Show recent items / Maximum recent items | On / 15 | |
| Show 'Get started' actions | On | |
| Layout file | *(empty)* | e.g. `%OneDrive%\VS\layout.json` or a team share. Environment variables are expanded. Needs a restart. |
| Verbose logging | Off | Debug-level output in the Output window |

## House conventions

- **Logging:** `ILogger<T>`, injected through the constructor. In VS it's routed to an Output window pane.
- **Dependency injection:** `Microsoft.Extensions.DependencyInjection`. Services are registered by interface in `UltimateStartPagePackage.ConfigureServices`.
- **Async:** async all the way down, with a `CancellationToken` on every public async method. There's no `.Result` or `.Wait()`, and no `async void`. Commands go through `AsyncRelayCommand`, which observes failures, logs them and shows them to the user.
- **Mapping:** done by hand (`ToModel()`), with no mapper library.
- **Tests:** xUnit.
- **One deliberate deviation:** settings live on a Visual Studio *Tools > Options* page rather than in `appsettings` + `IOptions<T>`. That's where VS users expect them, and VS persists them per user and per profile. The core only sees `IStartPageSettings`, so swapping the source later is a one-class change.

## Ideas for later

- Drag to reorder links and sections (today it's buttons and menus).
- Per-link colour or tags, and a compact list view.
- Remote layouts, e.g. loading a team layout from a URL or a repo on startup.
- Git branch and status badges on solution links.
- Porting the UI to the out-of-process `VisualStudio.Extensibility` model once Remote UI tool windows support document-well hosting and drag and drop.

# Decisions

## Decision: MEF/DI Wiring for ILinkRepository

**Date:** 2026-04-10  
**Author:** McManus (.NET Developer)  
**Status:** Implemented  

### Context

The `StartPageToolWindowControl` was using direct instantiation (`new LinkRepository()`) instead of proper dependency injection. This creates tight coupling and makes testing difficult. VS extensions support multiple DI patterns:

1. **Full MEF with ComponentModelHost**: Export services via `[Export]`, import via `[Import]` or `GetService<T>()`
2. **AsyncPackage.AddService**: Register services in package `InitializeAsync`, retrieve via `GetServiceAsync<T>()`
3. **Direct package accessors**: Package owns service instances, tool windows retrieve via typed methods

### Decision

**Chosen: Direct Package Accessor Pattern**

Implemented `ILinkRepository` injection via `UltimateStartPagePackage.GetLinkRepository()`:

1. Package creates singleton `LinkRepository` instance in `InitializeAsync()`
2. Package registers service via `AddService(typeof(ILinkRepository), ...)` for VS service container compatibility
3. `StartPageToolWindow.Initialize()` retrieves repository via `(Package as UltimateStartPagePackage)?.GetLinkRepository()`
4. `StartPageToolWindowControl` constructor accepts `ILinkRepository` as required parameter

### Rationale

**Why not full MEF?**
- Overkill for a single service
- Requires `ComponentModelHost` boilerplate and `[Export]` attributes on Core project (violates zero-VS-dependency rule)
- No third-party MEF components to integrate with

**Why not AsyncPackage.GetServiceAsync?**
- `ToolWindowPane.Initialize()` is synchronous by framework design
- Calling `GetServiceAsync` requires `JoinableTaskFactory.Run()` wrapping — adds complexity
- Service is already available synchronously (created before tool window initialization)

**Why direct accessor?**
- Simplest implementation for this scope
- Standard VS extension pattern for package-owned services
- Type-safe: compile-time checking vs. service-locator string keys
- Service lifetime is clear: owned by package, disposed with package

### Implementation

**UltimateStartPagePackage.cs:**
```csharp
private LinkRepository _linkRepository;

protected override async Task InitializeAsync(...)
{
    _linkRepository = new LinkRepository();
    AddService(typeof(ILinkRepository), async (container, ct, serviceType) => 
    {
        await Task.CompletedTask;
        return _linkRepository;
    });
    await ShowStartPageAsync();
}

internal ILinkRepository GetLinkRepository() => _linkRepository;
```

**StartPageToolWindow.cs:**
```csharp
protected override void Initialize()
{
    base.Initialize();
    var package = Package as UltimateStartPagePackage;
    var repository = package?.GetLinkRepository();
    if (repository != null)
        Content = new StartPageToolWindowControl(repository);
}
```

**StartPageToolWindowControl.xaml.cs:**
```csharp
public StartPageToolWindowControl(ILinkRepository repository)
{
    if (repository == null)
        throw new ArgumentNullException(nameof(repository));
    InitializeComponent();
    var viewModel = new StartPageViewModel(repository);
    DataContext = viewModel;
    _ = viewModel.LoadAsync();
}
```

### Alternatives Considered

1. **Full MEF via ComponentModelHost**: Rejected (overkill, Core project contamination)
2. **GetServiceAsync in Initialize**: Rejected (async complexity in sync context)
3. **Service Locator pattern**: Rejected (runtime errors vs. compile-time safety)

### Trade-offs

**Pros:**
- Simple, type-safe, explicit
- Clear service lifetime (package scope)
- No framework magic or hidden dependencies
- Easy to test (mock ILinkRepository in unit tests)

**Cons:**
- Direct package coupling (tool window knows about `UltimateStartPagePackage`)
- Doesn't scale to many services (would need per-service accessor methods)
- Not discoverable via standard VS MEF catalog

### Migration Path

If we add more services or need third-party MEF integration:
1. Create `Services` folder in VS2022 project
2. Implement adapter classes with `[Export(typeof(IService))]`
3. Adapters wrap Core implementations, expose via MEF
4. Tool windows use `ComponentModelHost.GetService<IService>()`
5. Remove direct package accessors

For now, YAGNI: one service, one accessor, done.

### Test Coverage

All 54 existing Core tests remain passing after DI wiring changes. No new tests required (DI is composition root concern, not Core logic).

### Review Notes

- Keaton flagged missing DI wiring in ViewModel implementation review
- This addresses that gap without over-engineering
- Pattern is consistent with Microsoft's VS SDK samples for tool window service access

---

## Decision: VS-Layer Solution Opening via EnvDTE

**Date:** 2026-04-10  
**Author:** Verbal (WPF/UI Developer)  
**Status:** Implemented  

### Context

`LinkViewModel.OpenCommand` needs to open `.sln` files inside Visual Studio using EnvDTE, but:
- `LinkViewModel` lives in `UltimateStartPage.Core` (net472, zero VS SDK dependencies per Decision #3)
- `EnvDTE` APIs belong in `UltimateStartPage.VS2022` (the VS-layer project)
- MVVM separation must be maintained — Core ViewModels cannot directly reference VS SDK assemblies

### Decision

**Use dependency injection via optional `Action<string>` delegate** to provide VS-specific file opening behavior from the VS2022 layer to Core ViewModels.

### Implementation

1. **Core ViewModels** accept optional `Action<string>? openAction` constructor parameter:
   - `StartPageViewModel(ILinkRepository repository, Action<string>? openAction = null)`
   - `LinkGroupViewModel(LinkGroup model, Func<LinkGroupViewModel, Task> onRemove, Func<Task> saveCallback, Action<string>? openAction = null)`
   - `LinkViewModel(SolutionLink model, Func<LinkViewModel, Task> onRemove, Action<string>? openAction = null)`

2. **VS2022 Layer** (`StartPageToolWindowControl.xaml.cs`) implements `OpenSolutionInVS(string path)`:
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
   - Pass this method to `new StartPageViewModel(repository, OpenSolutionInVS)`

3. **Fallback:** If `openAction` is `null`, `LinkViewModel.ExecuteOpen()` falls back to `Process.Start(path)` (opens with shell default)

### Rationale

**Why `Action<string>` over `IOpenSolution` interface?**
- Simpler for single-method contract (no interface ceremony)
- Optional parameter with default `null` makes Core self-contained (can run without VS layer, e.g., in tests)
- Avoids adding interface to Core just for a single method
- Consistent with existing callback pattern already used in ViewModels:
  - `Func<Task> saveCallback` (LinkGroupViewModel)
  - `Func<LinkViewModel, Task> onRemove` (LinkViewModel)
  - `Func<LinkGroupViewModel, Task> onRemove` (LinkGroupViewModel)

**Why not a static/global service?**
- Avoids singleton/global state (testability)
- Dependency injection is explicit and clear in constructor
- Easy to mock in unit tests (pass a test double action)

### Alternatives Considered

1. **`IOpenSolution` interface in Core, implemented in VS2022:**
   - More ceremony for single-method contract
   - Requires interface definition in Core (adds conceptual weight)
   - Chosen approach is lighter-weight for this use case

2. **Static/global `OpenSolutionHandler` service:**
   - Introduces global state (bad for testability)
   - Makes dependency implicit rather than explicit
   - Harder to test Core ViewModels in isolation

3. **Message bus / event aggregator:**
   - Overkill for simple parent-to-child communication
   - Adds indirection and complexity
   - Harder to reason about control flow

### Testing Impact

- **Core tests:** No changes needed. ViewModels remain testable without VS SDK. Tests can pass `null` or a mock `Action<string>` as needed.
- **Verification:** All 54 Core tests pass after changes.

### Files Changed

- `src/UltimateStartPage.Core/ViewModels/LinkViewModel.cs` — added `openAction` param, updated `ExecuteOpen()`
- `src/UltimateStartPage.Core/ViewModels/LinkGroupViewModel.cs` — added `openAction` param, pass through to `LinkViewModel`
- `src/UltimateStartPage.Core/ViewModels/StartPageViewModel.cs` — added `openAction` param, pass through to `LinkGroupViewModel`
- `src/UltimateStartPage.VS2022/ToolWindows/StartPageToolWindowControl.xaml.cs` — added `OpenSolutionInVS()`, wire to `StartPageViewModel`

### Compliance

- ✅ **Decision #3 (Core has zero VS SDK dependencies):** Core ViewModels remain VS-agnostic; `Action<string>` is a .NET BCL type
- ✅ **MVVM separation:** No VS APIs in Core; only in VS2022 code-behind
- ✅ **Testing:** Core tests run without VS SDK; pass after changes

### Notes

- `EnvDTE` reference already exists in `UltimateStartPage.VS2022.csproj` (line 101-104) with `<Private>False</Private>` (not copied to output — lives in VS install)
- `ThreadHelper.ThrowIfNotOnUIThread()` ensures DTE calls happen on the UI thread (required for COM interop safety)
- Future enhancement: Could add error handling UI (e.g., toast notification on open failure) in the VS2022 layer

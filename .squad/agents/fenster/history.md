# Fenster — History

## Project Context

- **Project:** UltimateStartPage
- **Stack:** .NET (C#)
- **User:** Martin Smith
- **Role:** QA Tester — tests, quality gates, edge cases, reviewer
- **Team:** Keaton (Team Lead / Reviewer), McManus (.NET Developer), Scribe (logger), Ralph (monitor)

## Learnings

> Append new learnings here after each session.

### Session: Test Strategy — 2025-07-16

**VS SDK Testing Constraints**
- VSIX components run inside the VS process; types like `IVsSolution`, `DTE`, `IVsShell` cannot be instantiated in isolation without a VS host or mock service provider.
- VS UI thread assertions (`ThreadHelper.ThrowIfNotOnUIThread()`) will crash test runs from pool threads — STA thread awareness is required for any VS-touching test.
- Hosted integration tests require a full VS installation on the CI agent (Windows + VS workload). Not available on standard GitHub Actions runners without configuration.
- `Microsoft.VisualStudio.Sdk.TestFramework` (NuGet) provides a mock VS service host for testing VS service interactions without a full VS process — the right tool for settings store and package activation tests.

**Recommended Test Stack**
- **xUnit** for unit tests (modern, parallel, `[Theory]` / `[InlineData]` for parameterised edge cases).
- **NSubstitute** for mocking VS interfaces and domain service interfaces.
- **FluentAssertions** for readable assertion failure messages.
- **MSTest** only for the hosted integration test project (SDK TestFramework is MSTest-based).
- **coverlet** + **ReportGenerator** for coverage reporting in CI.

**Architecture Pattern for Testability**
- The single most important testability decision: wrap every VS SDK service behind an owned interface (`ISettingsStore`, `ISolutionBrowser`, etc.). Unit tests mock the interface; integration tests use the real VS adapter.
- `UltimateStartPage.Core` project must have zero `Microsoft.VisualStudio.*` dependencies — enforced as a build gate.
- Three-project split: `Core` (pure logic), `VsAdapter` (VS SDK wrappers), `VSIX` (package entry point).

**Domain Edge Cases — High Risk**
- UNC paths (`\\server\share\`) are valid but reachability checks can hang — must use async/timeout or an `IFileSystemProbe` abstraction.
- Paths that existed at settings-save time but are deleted before load — must degrade gracefully, marking links as stale rather than throwing.
- Drive letter case mismatch (`c:\` vs `C:\`) requires normalisation for equality comparison.
- Corrupt or partial settings XML — must catch and fall back to defaults without crashing.
- Very large link collections (1000+) — loading must be async or paginated to avoid UI freeze.

**Coverage Targets**
- Core domain models: 90% minimum.
- Core serialisation / persistence: 90% minimum.
- Core utilities (path logic): 95% minimum.
- Overall Core project: 85% line coverage — enforced in CI as a PR gate.

**Known Hard Gaps**
- WPF dispatcher: test ViewModels only, never Views directly.
- UNC reachability: abstract and test the abstraction; real network tests are environment-dependent.
- VS package activation: manual smoke test checklist; automated only with dedicated VS CI agent.
- Concurrent settings access: document as known risk, mitigate with lock or channel in production code.

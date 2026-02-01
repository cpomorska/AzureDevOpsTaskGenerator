Repository: AzureDevOpsTaskGenerator - High-level guidelines

Purpose
- This repository contains a small CLI/service to parse markdown task documents and produce Azure DevOps work items.
- Key projects:
  - `AzureDevOpsTaskGenerator` (library/CLI/service)
  - `AzureDevOpsTaskGenerator.Tests` (unit and integration tests)

Quick structure overview
- `Models`
  - `DevelopmentTask` — core model for parsed work items and hierarchy (Epic -> Feature -> UserStory -> Task/Bug).
  - `TaskDocument` — container for parsed document metadata and top-level tasks.
- `Parsers`
  - `MarkdownTaskParser` implements `ITextFileParser` and converts markdown files under `Tests/TestData` (and other inputs) into `TaskDocument` / `DevelopmentTask` objects.
- `Services`
  - `TaskGeneratorService` — orchestrates conversion of parsed tasks into Azure DevOps calls or output.
  - `AzureDevOpsClient` — thin wrapper for Azure DevOps REST interactions (used by `TaskGeneratorService`).
- `Program.cs` — application entrypoint / CLI glue.
- `AzureDevOpsTaskGenerator.Tests` contains unit tests for parser, services and an end-to-end integration test using sample markdown files.

How to build and run
- Requires .NET 9 SDK (project targets .NET 9). Ensure `dotnet --version` is compatible.
- Build: `dotnet build` at solution root.
- Test: `dotnet test` at solution root.

Coding guidelines and conventions
- C# 13 language and .NET 9 target. Prefer modern language features only where they improve clarity.
- Keep model classes small and immutable where practical. Currently `DevelopmentTask` uses mutable auto-properties — keep consistent style across models.
- Follow existing naming conventions: PascalCase for types and public members, camelCase for private fields.
- Avoid heavy business logic in models; place parsing and transformation in `Parsers` and `Services`.
- Favor expression-bodied members for short methods where appropriate.
- Keep public APIs null-safe. Where a property may be absent prefer empty collections instead of `null` (this repo already uses that pattern).

Testing guidance
- Unit tests live in `AzureDevOpsTaskGenerator.Tests`. Add tests that exercise parsing edge cases in `Parsers\MarkdownTaskParserTests.cs` and behavior of `TaskGeneratorService`.
- Tests use the `Tests/TestData` markdown files as fixtures. Add new sample markdowns there when adding scenarios.
- Integration tests should not call real Azure DevOps. Use the existing test patterns: mock `AzureDevOpsClient` or provide a test double.

Parser behavior and expectations
- `MarkdownTaskParser` converts markdown section headings and bullet lists into hierarchical `DevelopmentTask` objects. When adding features or changing parsing rules:
  - Update parser unit tests to cover new cases.
  - Preserve acceptance criteria, tags and dependencies extraction.
- Keep parser methods small and testable. If a piece of logic grows, extract to private helpers and add tests.

Azure DevOps client and API usage
- `AzureDevOpsClient` is a thin REST wrapper. Keep network calls asynchronous (`async`/`await`) and cancellable using `CancellationToken` where practical.
- Surface errors from the API in a way callers can test (throw typed exceptions or return result objects). Do not swallow exceptions silently.
- Keep secrets/config out of source. Read PATs, organization and project names from environment variables or a secure config provider.

Performance and reliability
- Expected document sizes are small. Avoid premature optimization.
- For bulk creation of work items, batch where the API supports it to reduce round-trips.
- Add retry/backoff logic around transient HTTP failures (use `HttpClientFactory` and Polly in larger projects).

Extensibility and maintenance
- Parser and Service are primary extension points. To add support for other input formats implement `ITextFileParser` and register it where appropriate.
- Keep model `DevelopmentTask` generic and independent of Azure DevOps specifics so it can be mapped to other systems if needed.

Common pitfalls and recommended fixes
- Null collections: keep lists initialized to empty to avoid `NullReferenceException`.
- Missing test coverage after parser changes: add new fixture files in `Tests/TestData` and assert the parsed tree shape.
- Tests touching network: convert to mocks or test doubles to avoid flaky tests.

PR checklist (brief)
- Build passes: `dotnet build`
- Tests pass: `dotnet test`
- New public behavior covered by unit tests
- No secrets committed
- Cross-check change impact on parser/tests and update fixtures

Contact and context
- This document is an internal short guide to help contributors understand repository layout, conventions and where to add changes.

End of guidelines

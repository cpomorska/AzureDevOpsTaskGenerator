# AGENTS.md

## Agent-Quickstart (5 Zeilen)
- Beginne fast immer bei `AzureDevOpsTaskGenerator/Program.cs`, dann Parser (`MarkdownTaskParser`) und Mapping (`AzureDevOpsClient`).
- Bei Parser-Aenderungen immer Tests in `AzureDevOpsTaskGenerator.Tests/Parsers/MarkdownTaskParserTests.cs` + neues Fixture in `AzureDevOpsTaskGenerator.Tests/TestData/` ergaenzen.
- Bei Hierarchie-/SP-Logik `TaskGeneratorService` + `AzureDevOpsTaskGenerator.Tests/Services/TaskGeneratorServiceTests.cs` zusammen anpassen.
- Bei Feld-/ADO-Aenderungen nur `AzureDevOpsTaskGenerator/Services/AzureDevOpsClient.cs` anfassen und Priority/StoryPoints-Mapping konsistent halten.
- Vor Abgabe mindestens `dotnet test AzureDevOpsTaskGenerator.sln` laufen lassen; bei Docker-Problemen zuerst .NET 9/10 Inkonsistenz pruefen.

## Zweck & Einstieg
- Dieses Repo ist ein CLI-Tool, das Markdown-Aufgaben in Azure DevOps Work Items (Epic -> Feature -> User Story/Task) umwandelt.
- Einstiegspunkt ist `AzureDevOpsTaskGenerator/Program.cs` (Argumente, DI, Orchestrierung, Dry-Run vs. Live-Erstellung).
- Wichtig: Tatsächliches Target ist `net10.0` in `AzureDevOpsTaskGenerator/AzureDevOpsTaskGenerator.csproj` und `AzureDevOpsTaskGenerator.Tests/AzureDevOpsTaskGenerator.Tests.csproj`.

## Architektur (Datenfluss)
- Parser-Grenze: `ITextFileParser` in `AzureDevOpsTaskGenerator/Interfaces/ITextFileParser.cs`.
- Implementierung: `AzureDevOpsTaskGenerator/Parsers/MarkdownTaskParser.cs` erzeugt `TaskDocument` + `DevelopmentTask`-Baum.
- Service-Grenze: `ITaskGenerator` (`TaskGeneratorService`) baut `WorkItemHierarchy` und berechnet Summen.
- Integrationsgrenze: `IAzureDevOpsClient` (`AzureDevOpsClient`) mappt Domänenobjekte auf Azure DevOps JSON-Patch.
- Laufzeitfluss: Parse -> BuildHierarchy -> optional `AuthenticateAsync` -> `CreateWorkItemHierarchyAsync`.

## Parser-Regeln, die du kennen musst
- Jede Zeile mit `## ` startet ein neues Epic (`IsEpicHeader`), auch ohne "Epic:"-Label.
- Features werden nur als nummerierte, fett markierte Punkte erkannt, z. B. `1. **JWT Authentication**`.
- Property-Zeilen müssen Bullet + Fettlabel haben, z. B. `- **Effort**: 8 SP`, `- **Priority**: High`.
- YAML-Front-Matter (`---`) wird entfernt und als `TaskDocument.Metadata` gespeichert.
- Task-Typ-Erkennung: Texte wie "As a ..."/"story" werden als `UserStory`, sonst `Task`.

## Azure-DevOps-Mapping (projektspezifisch)
- Mapping sitzt zentral in `AzureDevOpsTaskGenerator/Services/AzureDevOpsClient.cs`.
- Priorität: `Critical` und `High` mappen beide auf ADO Priority `1`; `Critical` setzt zusätzlich Severity `1 - Critical`.
- Story Points: Epic/Feature/UserStory -> `Microsoft.VSTS.Scheduling.StoryPoints`; Task -> `OriginalEstimate`.
- Business Value Mapping: `high|medium|low` -> `100|50|10` oder direkter Integer.
- Parent-Child-Link wird beim Erstellen direkt über `/relations/-` gesetzt (`Hierarchy-Reverse`).

## Entwickler-Workflows
- Build/Test auf Solution-Ebene:
  - `dotnet build AzureDevOpsTaskGenerator.sln`
  - `dotnet test AzureDevOpsTaskGenerator.sln`
- Lokaler CLI-Run:
  - `dotnet run --project AzureDevOpsTaskGenerator -- --file <tasks.md> --organization <url> --project <name> --token <pat> --dry-run`
- Docker-Workflow (mit Prompt-Schutz vor Live-Run): `run-docker.sh` (macOS/Linux) und `run-docker.ps1` (Windows).

## Tests & Änderungsstrategie
- Parser-Verhalten über `AzureDevOpsTaskGenerator.Tests/Parsers/MarkdownTaskParserTests.cs` absichern.
- Hierarchie-/Summenlogik über `AzureDevOpsTaskGenerator.Tests/Services/TaskGeneratorServiceTests.cs` absichern.
- "Integration"-Tests (`AzureDevOpsTaskGenerator.Tests/Integration/EndToEndTests.cs`) nutzen DI + lokale Testdaten, keine echte ADO-Verbindung.
- Testdaten liegen in `AzureDevOpsTaskGenerator.Tests/TestData/*.md`; neue Parse-Szenarien dort abbilden.

## Bekannte Inkonsistenzen (vor Änderungen prüfen)
- `AzureDevOpsTaskGenerator/Dockerfile` spricht ggf. noch von .NET 9, während der Code auf .NET 10 zielt (README wurde bereits aktualisiert).
- Wenn Runtime-/Build-Probleme auftreten, zuerst Framework-Versionen in Dockerfile und lokalen SDKs abgleichen.

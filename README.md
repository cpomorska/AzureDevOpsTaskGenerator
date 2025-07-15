# AzureDevOpsTaskGenerator

A C# tool to parse markdown task documents and automatically create hierarchical work items (Epics, Features, User Stories, Tasks) in Azure DevOps.

## Azure DevOps Task Generator
- Created as a personal project to automate the creation of work items in Azure DevOps.
- Usecase: Import the documents created with Kiro 
- Kiro and Windsurf used to create project and tests
- Sidecar tool for Fixoda Marketplace
- Effort to run and test the tool: 5 hours
- Mixup of vibe coding, bugfixing, and refactoring
- Models used: Kiro -> Claude Sonnet 4.0, Windsurf SWE-1

## Prerequisites
- .NET 9.0 or later
- Azure DevOps organization and project
- Azure DevOps Personal Access Token (PAT) with work item read/write permissions

## Features
- Parse markdown to extract Epics, Features, User Stories, and Tasks
- Extract and map fields: Title, Description, Priority, Story Points/Effort, Business Value
- Map markdown properties to valid Azure DevOps field values
- Create hierarchical work item structures in Azure DevOps
- Supports xUnit/FluentAssertions-based tests for parser and service logic

## Field Mapping
| Markdown Value         | Azure DevOps Field                          | Mapping Logic                  |
|-----------------------|---------------------------------------------|-------------------------------|
| **Priority**: High    | Microsoft.VSTS.Common.Priority              | High/Critical → 1, Medium → 2, Low → 3 |
| **Effort**: 8 SP      | Microsoft.VSTS.Scheduling.StoryPoints       | Integer value                  |
| **Business Value**: High | Microsoft.VSTS.Common.BusinessValue       | High → 100, Medium → 50, Low → 10, or integer |

## Usage
1. Write your tasks in markdown using headers for Epics/Features and bullets/numbered lists for tasks.
2. Run the tool with your Azure DevOps organization URL and PAT token.
3. The tool parses your file and creates work items in Azure DevOps.

## Example Markdown
```markdown
## Epic: Security Enhancement
- **Priority**: Critical
- **Effort**: 21 story points
- **Business Value**: High

#### Features:
1. **JWT Authentication**
   - Implement JWT validation
   - **Effort**: 8 SP

2. **API Security**
   - Add rate limiting
   - **Effort**: 5 SP
```

## Customization
- To add new field mappings, update the mapping logic in `AzureDevOpsClient.cs`.
- To change parser behavior, modify `MarkdownTaskParser.cs`.

## Testing
- Run `dotnet test` to validate all parsing and integration logic.

## TODOs
See [todo.md](todo.md) for planned improvements and open issues.

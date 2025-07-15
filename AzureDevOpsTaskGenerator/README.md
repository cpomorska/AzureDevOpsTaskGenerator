# Azure DevOps Task Generator

This tool automatically parses structured task files (like the Fixoda development tasks) and creates corresponding work items in Azure DevOps with proper hierarchy (Epics → Features → User Stories/Tasks).

## Features

- ✅ Parse Markdown task files with hierarchical structure
- ✅ Extract Epics, Features, User Stories, and Tasks
- ✅ Automatically detect effort estimates (story points)
- ✅ Identify priorities and business values
- ✅ Create proper parent-child relationships in Azure DevOps
- ✅ Support for dry-run mode to preview before creation
- ✅ Comprehensive logging and error handling

## Prerequisites

- .NET 9.0 or later
- Azure DevOps organization and project
- Personal Access Token (PAT) with work item read/write permissions

## Installation

1. Clone or download the project
2. Build the application:
   ```bash
   dotnet build
   ```

## Usage

### Option 1: Direct .NET Execution

#### Basic Usage

```bash
dotnet run -- --file "path/to/your/tasks.md" \
              --organization "https://dev.azure.com/yourorg" \
              --project "YourProject" \
              --token "your-personal-access-token"
```

#### Dry Run (Preview Only)

```bash
dotnet run -- --file "fixoda-development-tasks.md" \
              --organization "https://dev.azure.com/yourorg" \
              --project "FixodaMarketplace" \
              --token "your-pat" \
              --dry-run
```

### Option 2: Docker Execution (Recommended)

Docker provides an isolated environment and eliminates the need to install .NET locally. The system uses volume mounting to access your task files.

#### Step 1: Build Docker Image

```bash
# Using helper scripts (recommended)
# Linux/Mac
./run-docker.sh build

# Windows PowerShell
.\run-docker.ps1 build

# Or build manually
docker build -f AzureDevOpsTaskGenerator/Dockerfile -t azure-devops-task-generator .
```

#### Step 2: Prepare Your Task File

Ensure your task file (e.g., `fixoda-development-tasks.md`) is in your current directory or a subdirectory. The Docker container will mount your current directory to `/app/tasks`.

#### Step 3: Preview Tasks (Dry Run - Recommended First Step)

Always start with a dry run to preview what will be created:

```bash
# Using helper scripts (recommended)
# Linux/Mac
./run-docker.sh dry-run fixoda-development-tasks.md https://dev.azure.com/yourorg YourProject your-token

# Windows PowerShell
.\run-docker.ps1 dry-run fixoda-development-tasks.md https://dev.azure.com/yourorg YourProject your-token

# Or run manually with Docker
docker run --rm -v "${PWD}:/app/tasks" azure-devops-task-generator \
  --file /app/tasks/fixoda-development-tasks.md \
  --organization https://dev.azure.com/yourorg \
  --project YourProject \
  --token your-personal-access-token \
  --dry-run
```

#### Step 4: Create Work Items in Azure DevOps

After reviewing the dry-run output, create the actual work items:

```bash
# Using helper scripts (recommended)
# Linux/Mac
./run-docker.sh run fixoda-development-tasks.md https://dev.azure.com/yourorg YourProject your-token

# Windows PowerShell
.\run-docker.ps1 run fixoda-development-tasks.md https://dev.azure.com/yourorg YourProject your-token

# Or run manually with Docker
docker run --rm -v "${PWD}:/app/tasks" azure-devops-task-generator \
  --file /app/tasks/fixoda-development-tasks.md \
  --organization https://dev.azure.com/yourorg \
  --project YourProject \
  --token your-personal-access-token
```

#### Alternative: Docker Compose

```bash
# Build and run with docker-compose
docker-compose build
docker-compose run --rm azure-devops-task-generator \
  --file /app/tasks/fixoda-development-tasks.md \
  --organization https://dev.azure.com/yourorg \
  --project YourProject \
  --token your-token \
  --dry-run
```

#### Docker Helper Script Commands

| Command | Windows PowerShell | Linux/Mac | Description |
|---------|-------------------|-----------|-------------|
| Build image | `.\run-docker.ps1 build` | `./run-docker.sh build` | Build the Docker image |
| Show help | `.\run-docker.ps1 help` | `./run-docker.sh help` | Show application help |
| Dry run | `.\run-docker.ps1 dry-run <file> <org> <project> <token>` | `./run-docker.sh dry-run <file> <org> <project> <token>` | Preview work items |
| Create items | `.\run-docker.ps1 run <file> <org> <project> <token>` | `./run-docker.sh run <file> <org> <project> <token>` | Create work items |
| Debug shell | `.\run-docker.ps1 shell` | `./run-docker.sh shell` | Open container shell |

### Volume Mounting & File Access

When using Docker, the current directory is automatically mounted to `/app/tasks` in the container:

- ✅ Place your task files in the current directory: `my-tasks.md`
- ✅ Or in subdirectories: `tasks/my-tasks.md`
- ✅ Reference files with relative paths in commands
- ✅ The container can access any file in your current directory tree
- ❌ Don't use absolute paths from your host system

### Docker Benefits

- 🔒 **Isolated Environment**: No need to install .NET locally
- 📁 **Volume Mounting**: Easy access to your task files
- 🛡️ **Safety First**: Dry-run mode to preview before creating
- 🖥️ **Cross-Platform**: Works on Windows, Linux, and Mac
- 📜 **Simple Scripts**: One command to build and run
- 🔧 **Debug Support**: Shell access for troubleshooting

## Personal Access Token Setup

1. Go to Azure DevOps → User Settings → Personal Access Tokens
2. Create new token with these scopes:
   - **Work Items**: Read & Write
   - **Project and Team**: Read (optional, for project validation)
3. Copy the token and use it with the `--token` parameter

## Supported File Formats

The tool supports Markdown files with this structure:

```markdown
# Project Title

## Epic: Epic Name
- **Priority**: High
- **Effort**: 21 story points
- **Business Value**: High - Description

### Features:
1. **Feature Name**
   - Description of the feature
   - **Effort**: 8 SP

2. **Another Feature**
   - Feature description
   - **Effort**: 5 SP

## Another Epic: Second Epic
- **Priority**: Medium
- **Effort**: 13 story points

### Features:
1. **Feature Under Second Epic**
   - **Effort**: 8 SP
```

## Work Item Mapping

| Text Structure | Azure DevOps Work Item |
|----------------|------------------------|
| `## Epic:` or `### Epic:` | Epic |
| `1. **Feature Name**` | Feature |
| Bullet points under features | User Story or Task |
| Story points extraction | Story Points field |
| Priority indicators | Priority field |
| Business value text | Business Value field |

## Output

The tool will create:
- **Epics** for major themes (Security, Architecture, Performance, etc.)
- **Features** for specific improvement areas
- **User Stories/Tasks** for individual development activities
- **Proper hierarchy** with parent-child relationships
- **Story points** and **priorities** based on parsed content

## Example Output

```
📋 EPIC: Security & Authentication (21 SP, Critical priority)
  🎯 FEATURE: JWT Authentication Implementation (8 SP)
    📖 USER STORY: Remove [AllowAnonymous] attributes (2 SP)
    📖 USER STORY: Implement JWT token validation (3 SP)
    ⚙️ TASK: Configure Keycloak integration (3 SP)
  🎯 FEATURE: API Security Hardening (5 SP)
    📖 USER STORY: Implement API rate limiting (2 SP)
    📖 USER STORY: Add security headers (3 SP)
```

## Error Handling

- **File not found**: Clear error message with file path
- **Authentication failure**: Detailed error with troubleshooting tips
- **Work item creation errors**: Individual item failures don't stop the process
- **Network issues**: Automatic retry logic for transient failures

## Troubleshooting

### Authentication Issues
- Verify your PAT has correct permissions
- Check organization URL format: `https://dev.azure.com/yourorg`
- Ensure project name is exact match (case-sensitive)

### Parsing Issues
- Check file format matches expected structure
- Verify effort estimates use supported formats: "8 SP", "21 story points"
- Ensure Epic headers start with `##` or `###`

### Work Item Creation Issues
- Verify project exists and you have access
- Check if work item types (Epic, Feature, User Story, Task) exist in your project
- Some fields might be required in your Azure DevOps configuration

## Development

To extend or modify the tool:

1. **Add new parsers**: Implement `ITextFileParser` for different file formats
2. **Customize work item mapping**: Modify `AzureDevOpsClient.MapToAzureDevOpsWorkItemType()`
3. **Add new fields**: Extend `DevelopmentTask` model and update work item creation
4. **Custom business logic**: Modify `TaskGeneratorService` for different prioritization rules

## License

This tool is part of the Fixoda Marketplace project and follows the same licensing terms.
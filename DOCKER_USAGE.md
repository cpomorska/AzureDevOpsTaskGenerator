# Azure DevOps Task Generator - Docker Usage Guide

This guide shows you how to use the Azure DevOps Task Generator with Docker to convert your task files into Azure DevOps work items.

## 🚀 Quick Start

### 1. Build the Docker Image

```bash
# Windows PowerShell
.\run-docker.ps1 build

# Linux/Mac
./run-docker.sh build

# Or manually
docker build -f AzureDevOpsTaskGenerator/Dockerfile -t azure-devops-task-generator .
```

### 2. Preview Your Tasks (Dry Run)

```bash
# Windows PowerShell
.\run-docker.ps1 dry-run fixoda-development-tasks.md https://dev.azure.com/yourorg YourProject your-token

# Linux/Mac
./run-docker.sh dry-run fixoda-development-tasks.md https://dev.azure.com/yourorg YourProject your-token

# Or manually
docker run --rm -v "${PWD}:/app/tasks" azure-devops-task-generator \
  --file /app/tasks/fixoda-development-tasks.md \
  --organization https://dev.azure.com/yourorg \
  --project YourProject \
  --token your-token \
  --dry-run
```

### 3. Create Work Items in Azure DevOps

```bash
# Windows PowerShell
.\run-docker.ps1 run fixoda-development-tasks.md https://dev.azure.com/yourorg YourProject your-token

# Linux/Mac
./run-docker.sh run fixoda-development-tasks.md https://dev.azure.com/yourorg YourProject your-token
```

## 📁 Volume Mounting

The Docker container automatically mounts your current directory to `/app/tasks`, so:

- ✅ Place task files in your current directory: `my-tasks.md`
- ✅ Or in subdirectories: `tasks/my-tasks.md`
- ✅ Reference them with relative paths in the command
- ❌ Don't use absolute paths from your host system

## 🔧 Available Commands

### Helper Scripts

| Command | Windows PowerShell | Linux/Mac |
|---------|-------------------|-----------|
| Build image | `.\run-docker.ps1 build` | `./run-docker.sh build` |
| Show help | `.\run-docker.ps1 help` | `./run-docker.sh help` |
| Dry run | `.\run-docker.ps1 dry-run <file> <org> <project> <token>` | `./run-docker.sh dry-run <file> <org> <project> <token>` |
| Create work items | `.\run-docker.ps1 run <file> <org> <project> <token>` | `./run-docker.sh run <file> <org> <project> <token>` |
| Debug shell | `.\run-docker.ps1 shell` | `./run-docker.sh shell` |

### Direct Docker Commands

```bash
# Show application help
docker run --rm azure-devops-task-generator --help

# Dry run (preview only)
docker run --rm -v "${PWD}:/app/tasks" azure-devops-task-generator \
  --file /app/tasks/your-tasks.md \
  --organization https://dev.azure.com/yourorg \
  --project YourProject \
  --token your-personal-access-token \
  --dry-run

# Create work items
docker run --rm -v "${PWD}:/app/tasks" azure-devops-task-generator \
  --file /app/tasks/your-tasks.md \
  --organization https://dev.azure.com/yourorg \
  --project YourProject \
  --token your-personal-access-token
```

## 🔑 Azure DevOps Setup

### 1. Create Personal Access Token (PAT)

1. Go to Azure DevOps → User Settings → Personal Access Tokens
2. Click "New Token"
3. Set these permissions:
   - **Work Items**: Read & Write
   - **Project and Team**: Read (optional)
4. Copy the generated token

### 2. Get Your Organization URL

Your organization URL format: `https://dev.azure.com/yourorganization`

### 3. Project Name

Use the exact project name from Azure DevOps (case-sensitive).

## 📊 What Gets Created

From the `fixoda-development-tasks.md` file, the system creates:

- **8 Epics** (Security, Architecture, Performance, API Design, Infrastructure, Testing, New Features, Technical Debt)
- **~20 Features** under those epics  
- **~150+ Tasks/User Stories** with proper relationships
- **182 total story points** distributed across work items
- **Proper priorities** (Critical for Security, High for Architecture, etc.)
- **Parent-child relationships** (Epic → Feature → Task)

## 🛡️ Safety Features

- **Dry-run mode**: Always preview before creating work items
- **Confirmation prompts**: Scripts ask for confirmation before creating work items
- **Error handling**: Clear error messages for common issues
- **Volume mounting**: Safe file access without exposing your entire system

## 🐛 Troubleshooting

### Common Issues

**"File not found"**
- Ensure your task file is in the current directory or subdirectory
- Use relative paths: `tasks/my-file.md` not `/full/path/to/file.md`

**"Authentication failed"**
- Verify your Personal Access Token has correct permissions
- Check organization URL format: `https://dev.azure.com/yourorg`
- Ensure project name is exact match (case-sensitive)

**"Docker command not found"**
- Install Docker Desktop
- Ensure Docker is running

### Debug Mode

Open a shell in the container to debug:

```bash
# Windows PowerShell
.\run-docker.ps1 shell

# Linux/Mac  
./run-docker.sh shell

# Manual
docker run --rm -it -v "${PWD}:/app/tasks" --entrypoint /bin/bash azure-devops-task-generator
```

## 📝 Example Task File Format

Your task file should follow this structure:

```markdown
# Project Development Tasks

## Epic: Security Enhancement
- **Priority**: Critical
- **Effort**: 21 story points
- **Business Value**: High - Security is important

### Features:
1. **JWT Authentication**
   - Implement JWT validation
   - **Effort**: 8 SP

2. **API Security**
   - Add rate limiting
   - **Effort**: 5 SP

## Epic: Performance Optimization
- **Priority**: High
- **Effort**: 13 story points

### Features:
1. **Caching Implementation**
   - Add Redis caching
   - **Effort**: 8 SP
```

The system automatically detects:
- Epic headers (`## Epic:` or `### Epic:`)
- Feature lists (`1. **Feature Name**`)
- Story points (`8 SP`, `21 story points`)
- Priorities (`Critical`, `High`, `Medium`, `Low`)
- Business values and descriptions

## 🎯 Ready to Use!

The Docker setup is production-ready and will save you hours of manual work creating Azure DevOps work items. Start with a dry-run to see what gets created, then run it for real to populate your backlog!
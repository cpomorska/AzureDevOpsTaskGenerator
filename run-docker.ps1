# Azure DevOps Task Generator - Docker Runner Script (PowerShell)
# This script provides easy ways to run the task generator in Docker on Windows

param(
    [Parameter(Position=0)]
    [string]$Command,
    
    [Parameter(Position=1)]
    [string]$File,
    
    [Parameter(Position=2)]
    [string]$Organization,
    
    [Parameter(Position=3)]
    [string]$Project,
    
    [Parameter(Position=4)]
    [string]$Token
)

# Function to print colored output
function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

# Function to show usage
function Show-Usage {
    Write-Host "Azure DevOps Task Generator - Docker Runner (PowerShell)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\run-docker.ps1 [COMMAND] [OPTIONS]"
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  build                    Build the Docker image"
    Write-Host "  run                      Run with custom parameters"
    Write-Host "  dry-run                  Run in preview mode (no work items created)"
    Write-Host "  help                     Show application help"
    Write-Host "  shell                    Open shell in container for debugging"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\run-docker.ps1 build"
    Write-Host "  .\run-docker.ps1 help"
    Write-Host "  .\run-docker.ps1 dry-run fixoda-development-tasks.md https://dev.azure.com/myorg MyProject mytoken"
    Write-Host "  .\run-docker.ps1 run fixoda-development-tasks.md https://dev.azure.com/myorg MyProject mytoken"
    Write-Host ""
    Write-Host "Volume Mounting:"
    Write-Host "  - Current directory is mounted to /app/tasks in container"
    Write-Host "  - Task files should be in current directory or subdirectories"
    Write-Host "  - Use relative paths like 'tasks/my-tasks.md' or just 'my-tasks.md'"
    Write-Host ""
}

# Function to build Docker image
function Build-Image {
    Write-Info "Building Azure DevOps Task Generator Docker image..."
    docker build -f AzureDevOpsTaskGenerator/Dockerfile -t azure-devops-task-generator .
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Docker image built successfully!"
    } else {
        Write-Error "Failed to build Docker image!"
        exit 1
    }
}

# Function to run with custom parameters
function Run-Custom {
    param(
        [string]$TaskFile,
        [string]$Org,
        [string]$Proj,
        [string]$AccessToken,
        [switch]$DryRun
    )
    
    if (-not $TaskFile -or -not $Org -or -not $Proj -or -not $AccessToken) {
        Write-Error "Missing required parameters!"
        Write-Host "Usage: .\run-docker.ps1 run <file> <organization> <project> <token>"
        Write-Host "Example: .\run-docker.ps1 run fixoda-development-tasks.md https://dev.azure.com/myorg MyProject mytoken"
        exit 1
    }
    
    # Check if file exists
    if (-not (Test-Path $TaskFile)) {
        Write-Error "Task file '$TaskFile' not found!"
        exit 1
    }
    
    $dockerArgs = @(
        "run", "--rm",
        "-v", "$(Get-Location):/app/tasks",
        "azure-devops-task-generator",
        "--file", "/app/tasks/$TaskFile",
        "--organization", $Org,
        "--project", $Proj,
        "--token", $AccessToken
    )
    
    if ($DryRun) {
        $dockerArgs += "--dry-run"
        Write-Info "Running in DRY-RUN mode (preview only)..."
    } else {
        Write-Warning "Running in LIVE mode - work items will be created in Azure DevOps!"
        $confirmation = Read-Host "Are you sure you want to continue? (y/N)"
        if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
            Write-Info "Operation cancelled."
            exit 0
        }
    }
    
    Write-Info "Processing task file: $TaskFile"
    Write-Info "Organization: $Org"
    Write-Info "Project: $Proj"
    
    & docker @dockerArgs
}

# Function to run in dry-run mode
function Run-DryRun {
    param(
        [string]$TaskFile,
        [string]$Org,
        [string]$Proj,
        [string]$AccessToken
    )
    
    if (-not $TaskFile -or -not $Org -or -not $Proj -or -not $AccessToken) {
        Write-Error "Missing required parameters!"
        Write-Host "Usage: .\run-docker.ps1 dry-run <file> <organization> <project> <token>"
        Write-Host "Example: .\run-docker.ps1 dry-run fixoda-development-tasks.md https://dev.azure.com/myorg MyProject mytoken"
        exit 1
    }
    
    Run-Custom -TaskFile $TaskFile -Org $Org -Proj $Proj -AccessToken $AccessToken -DryRun
}

# Function to show application help
function Show-AppHelp {
    Write-Info "Showing Azure DevOps Task Generator help..."
    docker run --rm azure-devops-task-generator --help
}

# Function to open shell in container
function Open-Shell {
    Write-Info "Opening shell in Azure DevOps Task Generator container..."
    docker run --rm -it -v "$(Get-Location):/app/tasks" --entrypoint /bin/bash azure-devops-task-generator
}

# Main script logic
switch ($Command) {
    "build" {
        Build-Image
    }
    "run" {
        Run-Custom -TaskFile $File -Org $Organization -Proj $Project -AccessToken $Token
    }
    "dry-run" {
        Run-DryRun -TaskFile $File -Org $Organization -Proj $Project -AccessToken $Token
    }
    "help" {
        Show-AppHelp
    }
    "shell" {
        Open-Shell
    }
    default {
        Show-Usage
        exit 1
    }
}
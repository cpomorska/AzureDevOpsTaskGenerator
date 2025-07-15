#!/bin/bash

# Azure DevOps Task Generator - Docker Runner Script
# This script provides easy ways to run the task generator in Docker

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to show usage
show_usage() {
    echo "Azure DevOps Task Generator - Docker Runner"
    echo ""
    echo "Usage: $0 [COMMAND] [OPTIONS]"
    echo ""
    echo "Commands:"
    echo "  build                    Build the Docker image"
    echo "  run                      Run with custom parameters"
    echo "  dry-run                  Run in preview mode (no work items created)"
    echo "  help                     Show application help"
    echo "  shell                    Open shell in container for debugging"
    echo ""
    echo "Examples:"
    echo "  $0 build"
    echo "  $0 help"
    echo "  $0 dry-run fixoda-development-tasks.md https://dev.azure.com/myorg MyProject mytoken"
    echo "  $0 run fixoda-development-tasks.md https://dev.azure.com/myorg MyProject mytoken"
    echo ""
    echo "Volume Mounting:"
    echo "  - Current directory is mounted to /app/tasks in container"
    echo "  - Task files should be in current directory or subdirectories"
    echo "  - Use relative paths like 'tasks/my-tasks.md' or just 'my-tasks.md'"
    echo ""
}

# Function to build Docker image
build_image() {
    print_info "Building Azure DevOps Task Generator Docker image..."
    docker build -f AzureDevOpsTaskGenerator/Dockerfile -t azure-devops-task-generator .
    print_success "Docker image built successfully!"
}

# Function to run with custom parameters
run_custom() {
    local file="$1"
    local organization="$2"
    local project="$3"
    local token="$4"
    local dry_run="$5"
    
    if [[ -z "$file" || -z "$organization" || -z "$project" || -z "$token" ]]; then
        print_error "Missing required parameters!"
        echo "Usage: $0 run <file> <organization> <project> <token> [--dry-run]"
        echo "Example: $0 run fixoda-development-tasks.md https://dev.azure.com/myorg MyProject mytoken"
        exit 1
    fi
    
    # Check if file exists
    if [[ ! -f "$file" ]]; then
        print_error "Task file '$file' not found!"
        exit 1
    fi
    
    local docker_args=(
        "docker" "run" "--rm"
        "-v" "$(pwd):/app/tasks"
        "azure-devops-task-generator"
        "--file" "/app/tasks/$file"
        "--organization" "$organization"
        "--project" "$project"
        "--token" "$token"
    )
    
    if [[ "$dry_run" == "--dry-run" ]]; then
        docker_args+=("--dry-run")
        print_info "Running in DRY-RUN mode (preview only)..."
    else
        print_warning "Running in LIVE mode - work items will be created in Azure DevOps!"
        read -p "Are you sure you want to continue? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            print_info "Operation cancelled."
            exit 0
        fi
    fi
    
    print_info "Processing task file: $file"
    print_info "Organization: $organization"
    print_info "Project: $project"
    
    "${docker_args[@]}"
}

# Function to run in dry-run mode
run_dry_run() {
    local file="$1"
    local organization="$2"
    local project="$3"
    local token="$4"
    
    if [[ -z "$file" || -z "$organization" || -z "$project" || -z "$token" ]]; then
        print_error "Missing required parameters!"
        echo "Usage: $0 dry-run <file> <organization> <project> <token>"
        echo "Example: $0 dry-run fixoda-development-tasks.md https://dev.azure.com/myorg MyProject mytoken"
        exit 1
    fi
    
    run_custom "$file" "$organization" "$project" "$token" "--dry-run"
}

# Function to show application help
show_app_help() {
    print_info "Showing Azure DevOps Task Generator help..."
    docker run --rm azure-devops-task-generator --help
}

# Function to open shell in container
open_shell() {
    print_info "Opening shell in Azure DevOps Task Generator container..."
    docker run --rm -it -v "$(pwd):/app/tasks" --entrypoint /bin/bash azure-devops-task-generator
}

# Main script logic
case "$1" in
    "build")
        build_image
        ;;
    "run")
        shift
        run_custom "$@"
        ;;
    "dry-run")
        shift
        run_dry_run "$@"
        ;;
    "help")
        show_app_help
        ;;
    "shell")
        open_shell
        ;;
    *)
        show_usage
        exit 1
        ;;
esac
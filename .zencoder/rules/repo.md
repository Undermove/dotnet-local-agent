---
description: Repository Information Overview
alwaysApply: true
---

# C# Coding Agent Information

## Summary
A C# implementation of an AI coding agent workshop, originally written in Go. The project provides a framework for building AI assistants with various capabilities including chat, file operations, shell command execution, and code search. It supports both cloud-based (Anthropic Claude) and local (LM Studio) AI providers.

## Structure
- **CodingAgent.Core/**: Main project with all agent implementations and tools
- **TestSchema/**: Secondary project for testing schema generation
- **.zencoder/**: Configuration directory for Zencoder
- **Root files**: Various test files, documentation, and configuration

## Language & Runtime
**Language**: C#
**Version**: .NET 9.0
**Build System**: MSBuild (via dotnet CLI)
**Package Manager**: NuGet

## Dependencies
**Main Dependencies**:
- Anthropic.SDK (5.5.2): SDK for Anthropic Claude API integration
- OpenAI (2.1.0): OpenAI API integration
- Newtonsoft.Json (13.0.4): JSON serialization/deserialization
- NJsonSchema (11.5.0): JSON Schema generation and validation

## Build & Installation
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build CodingAgent.sln

# Run specific agent
dotnet run --project CodingAgent.Core -- <command>
```

## Main Components
**Entry Point**: CodingAgent.Core/Program.cs
**Agent Types**:
- Chat.cs: Basic chat agent
- ReadFile.cs: Agent with file reading capability
- ListFiles.cs: Agent with directory listing capability
- BashTool.cs: Agent with shell command execution
- EditTool.cs: Agent with file editing capability
- CodeSearchTool.cs: Agent with code search capability

**AI Providers**:
- AnthropicProvider: Integration with Anthropic Claude API
- LMStudioProvider: Integration with local LM Studio

## Usage Commands
```bash
# Basic chat
dotnet run --project CodingAgent.Core -- chat

# With LM Studio (local AI)
dotnet run --project CodingAgent.Core -- chat --provider lmstudio

# Full-featured agent with code search
dotnet run --project CodingAgent.Core -- search

# Help information
dotnet run --project CodingAgent.Core -- --help
```

## Environment Configuration
**Required Environment Variables**:
- ANTHROPIC_API_KEY: Required for Anthropic Claude provider
- AI_PROVIDER: Optional, sets default provider (anthropic or lmstudio)
- LM_STUDIO_URL: Optional, sets default LM Studio URL

**External Requirements**:
- ripgrep or grep: Required for code search functionality
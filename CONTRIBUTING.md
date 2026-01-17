# Contributing to HookReplay CLI

Thank you for your interest in contributing to HookReplay CLI! This document provides guidelines for contributing.

## Code of Conduct

Be kind and respectful. We're all here to build great developer tools.

## How to Contribute

### Reporting Bugs

1. Check if the issue already exists in [GitHub Issues](https://github.com/hookreplay/cli/issues)
2. If not, create a new issue with:
   - Clear title and description
   - Steps to reproduce
   - Expected vs actual behavior
   - CLI version (`hookreplay --version`)
   - OS and architecture

### Suggesting Features

1. Open a [GitHub Issue](https://github.com/hookreplay/cli/issues) with the `enhancement` label
2. Describe the problem you're trying to solve
3. Explain your proposed solution
4. We'll discuss before you start coding

### Pull Requests

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Write/update tests if applicable
5. Ensure code compiles (`dotnet build`)
6. Commit with clear messages
7. Push to your fork
8. Open a Pull Request

## Development Setup

### Prerequisites

- .NET 9.0 SDK or later
- Git

### Building

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/cli.git
cd cli

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run

# Run tests
dotnet test
```

### Project Structure

```
src/HookReplay.Cli/
├── Program.cs          # Main entry point and all CLI logic
├── README.md           # This file
├── LICENSE             # MIT License
├── CONTRIBUTING.md     # Contribution guidelines
└── HookReplay.Cli.csproj
```

## Coding Guidelines

### Style

- Use C# 12 features where appropriate
- Follow existing code patterns
- Use meaningful variable names
- Keep methods focused and small

### Primary Constructors

We use primary constructors for classes with dependencies:

```csharp
// Preferred
public class MyService(ILogger logger)
{
    public void DoSomething() => logger.Log("...");
}

// Avoid
public class MyService
{
    private readonly ILogger _logger;
    public MyService(ILogger logger) => _logger = logger;
}
```

### Console Output

Use Spectre.Console for all terminal output:

```csharp
// Good
AnsiConsole.MarkupLine("[green]Success![/]");

// Avoid
Console.WriteLine("Success!");
```

## Commit Messages

- Use present tense ("Add feature" not "Added feature")
- Use imperative mood ("Move cursor to..." not "Moves cursor to...")
- Keep first line under 50 characters
- Reference issues when applicable ("Fix #123")

Examples:
```
Add history search command
Fix connection timeout handling
Update README with new commands
```

## Testing

Currently, the CLI uses manual testing. Automated tests are welcome contributions!

To test manually:
1. Build the CLI
2. Test against the production server or a local dev server
3. Verify all commands work as expected

## Questions?

- Open a [GitHub Discussion](https://github.com/hookreplay/cli/discussions)
- Tweet at [@hookreplaydev](https://twitter.com/hookreplaydev)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

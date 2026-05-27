# Contributing to StageKit

Thank you for your interest in contributing! Everyone is welcome to contribute to StageKit.

## Getting Started

1. Fork the repository
2. Clone your fork locally
3. Create a feature branch from `main`
4. Make your changes
5. Run the tests to verify nothing is broken
6. Submit a pull request

## Building & Testing

```powershell
dotnet restore
dotnet build .\StageKit.slnx -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
dotnet test .\StageKit.Tests\StageKit.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
```

The library multi-targets `net8.0` and `net10.0`; the test project targets `net10.0` only.

Main packages are `StageKit`, `StageKit.Primitives`, and `StageKit.Runtime`. Keep package-specific README files updated when changing user-facing APIs in those packages.

## Code Guidelines

- **C# latest (.NET 8 minimum)** with nullable reference types enabled
- Use **file-scoped namespaces** (`namespace StageKit;`)
- **XML doc comments** (`///`) are required on all public members
- Private fields use `_camelCase` naming
- Use `#region` blocks to organize large files
- Follow existing codebase conventions, naming, and patterns
- Preserve each file's existing line endings when editing
- Name variables, properties, and methods with clear, descriptive names
- Keep code clean — avoid leaving large blocks of commented-out code unless essential
- Factory methods are prefixed with `Create`; measurement/query methods are prefixed with `Get`
- Extensions use C# 14's explicit extension block syntax where appropriate.
- Prefer `ArgumentNullException.ThrowIfNull(...)` for null guard clauses.
- Do not add new runtime dependencies unless the benefit is clear for a small infrastructure library.

## Commit Messages

- Write concise, descriptive commit messages
- Use the imperative mood (e.g., "Add feature" not "Added feature")

## Pull Requests

- Keep PRs focused on a single change or feature
- Include a clear description of what was changed and why
- Ensure all tests pass before submitting
- Add tests for new functionality when applicable

## Reporting Issues

- Search existing issues before opening a new one
- Provide a clear description and steps to reproduce
- Include the .NET version and OS you are using

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).

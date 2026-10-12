# Contributing

Thanks for helping improve DriftGuard.

## Development setup

1. Install the .NET 10 SDK.
2. Run `dotnet restore DriftGuard.sln`.
3. Run `dotnet test DriftGuard.sln --configuration Release` (the reviewed 0.1.1 patch must be verified in a .NET 10 environment).
4. Run the scenario runner and inspect the output.

## Contribution expectations

- Include unit tests for core behavior and a scenario fixture for user-visible behavior.
- Preserve deterministic ordering in diff and report output.
- Never add real credentials or production configuration to fixtures.
- Do not claim complete secret detection; redaction is best-effort unless a future design replaces it with a guaranteed-safe data model.
- Treat changes to exit codes, JSON report fields, path matching and policy semantics as compatibility changes; document them.
- Prefer a small dependency surface in `DriftGuard.Core`.

## Pull request checklist

- [ ] Tests pass.
- [ ] Relevant scenario runner passes.
- [ ] README/policy docs updated if behavior changes.
- [ ] No secret, token, personal, or production data added.
- [ ] Failure modes are actionable and do not print input values unnecessarily.

# Versioned CLI JSON contracts

CLI 0.2.0 maintains `report.v1.schema.json` (`schemaVersion: 1`) for `compare/check/explain --format json`. This is an independent **successful execution** contract, with `exitCode` 0 (non-blocking) or 1 (policy threshold failed); a report can pass even if `policyApplied` is false. Keep interpreting these as distinct states.

- `report.v1.schema.json`: success/policy-failure report schema.
- `error.v1.schema.json`: structured errors with exit 2, emitted to **stdout** when `--format json` was requested. `error.code` distinguishes usage from invalid input, I/O, access and internal errors.
- `policy-validation.v1.schema.json`: success response from `validate-policy ... --format json` (invalid policies use the error schema).
- `../../tests/DriftGuard.Cli.Tests/Fixtures/compare-minimal.v1.json`: golden fixture asserted by process-based tests on the CLI output.

`--values none` prevents both `baselineValue` and `currentValue` fields from being written, regardless of the configuration content. `--source-names none` replaces only the two top-level input filenames with `[omitted]`. Both options preserve report v1 wire shape; value fields have always been optional. Prefer them together for CI artifacts. **They do not sanitize metadata**: paths (`$.path`), rule IDs, rule descriptions and explain paths can still reveal names/structure. Review policies and reports before cloud ingestion; privacy labels are not a proof that a report is safe for public sharing.

The diff `changes` collection stays compact, even for added/removed subtrees. `findings` can include synthesized descendant paths because the policy engine evaluates nested values. Consumers must not assume every finding is represented by an identically named top-level `change`.

## Compatibility rules

1. `schemaVersion: 1` is stable; do not rename, remove or reinterpret fields. `changes` value members are optional by design.
2. Non-breaking extensions require documented optional fields and a compatibility review. **The current schema is strict** (`additionalProperties: false`) so even additive changes can break strict consumers. For new top-level fields, prefer a new schema version or an explicit negotiation plan.
3. JSON errors and successful reports are **different schemas**. Check `exitCode` and/or `kind`, not merely the presence of syntactically valid JSON.
4. Do not hardcode the CLI patch/minor version when ingesting reports; use `schemaVersion` for contract compatibility.
5. Never accept an upload from an unauthenticated/unauthorized tenant solely because it validates against this schema. Validate size, source, workspace, redaction and entitlement separately on the future backend.

For SARIF, invoke `--format sarif` on `compare` or `check`. SARIF 2.1.0 has deterministic fingerprints based on the analyzed relative filename, rule ID, JSONPath and operation. No raw old/new configuration values are included. The current implementation can only locate the **file at line 1**, not the precise YAML/JSON source line: GitHub PR inline annotations will therefore be limited until source maps are implemented.

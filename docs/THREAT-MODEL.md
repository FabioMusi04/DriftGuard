# Threat model and data handling

## Assets

- Configuration values, which may accidentally contain credentials.
- Baseline/current file paths and CI logs.
- Policy integrity: a permissive policy could incorrectly allow a deployment change.

## Trust boundaries

DriftGuard runs locally or in CI and reads files supplied by the caller. It does not make network requests or write to the source configuration. Input files and policy files should nevertheless be treated as untrusted data.

## Current mitigations

- Configuration input limit: 5 MiB; policy input limit: 1 MiB. Limits are enforced during file reads, not only by checking file metadata.
- JSON nesting depth bounded by the parser; YAML conversion depth bounded by the application. Duplicate JSON keys rejected; YAML expansion limited by converted nodes and scalar size.
- Unknown policy JSON members are rejected; unsupported policy versions, duplicate rule IDs, non-enumerated severities, invalid paths and invalid operations fail closed.
- Sensitive path segments (for example `password`, `token`, `secret`, `privateKey`, `credential`, `authorization`, `connectionString`) are redacted in terminal and JSON reports.
- Common token-like strings are heuristically redacted.
- CLI error handling avoids stack traces by default.
- `check` requires an explicit policy file and `compare --fail-on warning/error` requires `--policy` so an absent rule set cannot accidentally look like a passing gate.
- Policy engine inspects descendants of added/removed/type-changed containers and enforces candidate/comparison/finding limits.
- Text output escapes control characters in untrusted paths and policy descriptions.

## Known gaps

- Redaction is heuristic and must not be relied on as a guarantee. Secrets under innocuous field names may still appear in reports, and values can be embedded in file/property names or policy descriptions. A values-free report mode is needed before hosted ingestion.
- The policy engine checks diffs, not all properties in the current configuration. A secret can remain unchanged and therefore produce no finding.
- No signature or trust mechanism is provided for policy files. Pull-request changes to policies require normal code-review protections.
- Resource limits are defensive bounds, not a complete sandbox for malicious YAML. The YAML representation is built before expansion counters apply.
- This version does not resolve environment variables, templates, or runtime configuration overlays. It compares supplied snapshots, not necessarily the final effective runtime configuration.

## Operational guidance

- Prefer secret stores and short-lived credentials over plaintext secrets.
- Redact before uploading logs or CI artifacts, and treat output as sensitive even after this tool runs.
- Store the policy file in protected version control and require review for policy changes.
- Start report-only, validate against known regressions, and only then set CI thresholds to warning/error.
- Keep DriftGuard and NuGet dependencies updated; run the test and scenario suites after changes.

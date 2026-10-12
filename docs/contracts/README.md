# Versioned report contract

`report.v1.schema.json` documents the shape emitted by `driftguard compare/check/explain --format json` starting with CLI 0.1.1. Version 1 is an additive contract: use `schemaVersion` to select a parser, tolerate future minor additions if your use case permits, and do not infer that a passed report had any policy unless `policyApplied` is true.

`changes` stays compact for added/removed objects, while `findings` may include synthesized descendant paths because the policy engine evaluates their nested values. No report JSON is guaranteed to be safe for public sharing: redaction is heuristic, paths may reveal metadata, and secret-like strings may remain.

This file is a starting point for an eventual `DriftGuard.Contracts` project and API ingestion. It is not yet backed by a published package or a complete golden-test suite.

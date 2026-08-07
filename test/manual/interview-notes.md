# Interview Notes - How to Present This Portfolio Safely

## Thirty-second introduction

> I come from an ASP.NET and database background and am moving into manual testing. For this project I read the business rules and code, identified risk areas, then designed functional, boundary, state-transition, authorization, API, and database-validation cases. The bug reports and manual summary are simulated portfolio artifacts, while the automated-test numbers are from a successful local full-suite run.

## Strong examples to walk through

1. **Boundary analysis:** `ML-SHP-004` and `ML-SHP-005` show valid/invalid phone and weight partitions.
2. **State-transition testing:** `ML-OPS-001` to `ML-OPS-007` cover valid paths, skipped states, retry, cancellation, and terminal immutability.
3. **API reliability:** `ML-API-004` and `ML-API-005` distinguish a safe idempotent replay from a conflicting payload.
4. **Security/privacy:** `ML-TRK-001`, `ML-TRK-006`, and `ML-API-006` test least disclosure and tenant isolation.
5. **Database validation:** verify that one user intent produces one shipment, status history is append-only and ordered, COD state matches shipment state, and only one assignment is active.

## Honest answers to likely questions

**Did you really find these bugs?**

> These reports are deliberately simulated to demonstrate how I document severity, steps, evidence, impact, and retest. I did not find them in production. I derived them from risks in the requirements and would execute them against a controlled build before raising real defects.

**Why is the sample release NO-GO?**

> The example has one critical authorization issue and multiple high-impact data/financial risks. Passing many lower-risk cases does not compensate for an unresolved tenant-isolation or COD-integrity defect.

**How do you choose severity and priority?**

> Severity reflects impact: data exposure and financial corruption are high or critical. Priority reflects scheduling: a low-frequency issue can still be P0 when its impact is unacceptable or it blocks release.

**How would you validate the database?**

> I would use read-only queries in the test database to compare the UI/API response with Shipment, status-history, assignment, COD, and external-reference records. I would verify counts, ownership, timestamps, active flags, and transaction rollback, while redacting PII from evidence.

**What did the automated test run show?**

> The full local solution completed with 267 passing tests: 22 Domain, 151 Application, 62 Infrastructure, and 32 Web. An earlier attempt hit a temporary output-file lock, so I reran cleanly and only reported the successful complete run.

## Evidence safety

- Use only seeded local accounts and fictional shipments.
- Blur or redact API keys, cookies, connection strings, phone numbers, addresses, and database IDs.
- Never upload `.env`, `appsettings.Development.json` secrets, raw HAR files, or production screenshots.
- Say “simulated,” “designed,” or “portfolio exercise” unless a result was actually executed and saved.
- Keep raw evidence private; commit only sanitized examples.

## Suggested portfolio demo flow

1. Start with the traceability table in the test-case document.
2. Explain one happy path and one negative boundary case.
3. Draw the shipment state machine and show why invalid jumps matter.
4. Present `SIM-BUG-006` to explain idempotency and `SIM-BUG-007` to explain object-level authorization.
5. Finish with the summary report and justify the NO-GO decision using risk, not only pass rate.

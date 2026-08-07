# Manual Testing Portfolio Pack

Project: Mini Logistics Management System  
Prepared for: Manual Tester portfolio and interview discussion  
Document status: Sample / simulated  
Last reviewed: 2026-08-06

## Important disclosure

This folder is a portfolio sample derived from the repository's documented requirements and source code. The manual execution results, defect observations, screenshots, timestamps, and people in these documents are simulated. They are not claims about incidents found in production.

When using this pack in an interview, say:

> I analyzed the repository and designed these test artifacts as a portfolio exercise. The test cases are based on implemented business rules. Bug observations and the manual execution summary are deliberately simulated to demonstrate my reporting approach. I can explain how I would reproduce and verify each item in a running environment.

Do not say that the simulated defects were discovered in production or accepted by a real development team.

## Documents

| Document | Purpose |
|---|---|
| [Manual Test Cases](manual-test-cases.md) | Thirty-six functional, boundary, negative, authorization, API, privacy, and state-transition cases. |
| [Simulated Bug Reports](simulated-bug-reports.md) | Eight sample defect reports with reproducible steps and safe synthetic data. |
| [Simulated Test Summary Report](simulated-test-summary-report.md) | Example cycle metrics, defect distribution, exit decision, risks, and recommendations. |
| [Interview Notes](interview-notes.md) | Honest talking points and suggested answers for presenting the artifacts. |

## Scope selected from the repository

- Shop shipment creation and fee preview.
- Shipment assignment and lifecycle transitions.
- Delivery failure and return handling.
- COD collection and assignment completion.
- Public tracking privacy and last-four phone verification.
- Partner API authentication, authorization, idempotency, and rate limiting.

Primary repository references:

- [`README.md`](../../README.md)
- [`Shipment.cs`](../../src/MiniLogistics.Domain/Shipments/Shipment.cs)
- [`CreateShipmentCommandValidator.cs`](../../src/MiniLogistics.Application/Shipments/CreateShipment/CreateShipmentCommandValidator.cs)
- [`Tracking.razor`](../../src/MiniLogistics.Web/Components/Pages/Tracking.razor)
- [`PartnerApiEndpoints.cs`](../../src/MiniLogistics.Web/Endpoints/PartnerApiEndpoints.cs)
- [`ShipmentStateMachineTests.cs`](../MiniLogistics.Domain.Tests/ShipmentStateMachineTests.cs)

## Safe synthetic data rules

- Names, phone numbers, API keys, tracking codes, order IDs, IP addresses, and timestamps are fictional.
- API secrets are placeholders such as `<TEST_API_KEY>` and must never be replaced with a production secret in a committed file.
- Bug IDs use the `SIM-BUG` prefix to prevent confusion with a real issue tracker.
- Manual results use `SIMULATED` labels. Replace them only after executing the cases and collecting real evidence.
- No customer, employer, or production-system information is included.

## Suggested execution order

1. Run the smoke cases: `ML-SHP-001`, `ML-OPS-001`, `ML-TRK-001`, `ML-API-001`.
2. Execute the high-priority negative and authorization cases.
3. Execute lifecycle and COD regression cases.
4. Capture actual evidence in a private/local location if it contains tokens, internal URLs, or database records.
5. Update the test summary using real counts and clearly remove the `SIMULATED` label only when every result is evidence-backed.

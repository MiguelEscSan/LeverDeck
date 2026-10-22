# Phase 2 — Domain and application core

## Objective

Model the whole domain and every use case from [docs/ROADMAP.md](../../docs/ROADMAP.md) Phase 2, test-first, against
fake ports. No adapters.

## Constraints and decisions

- Error handling is **hybrid**: invariant violations in the domain throw typed `DomainException`s (an invalid object
  cannot exist); expected business failures in use cases return `Result` (not found, code expired, device revoked…).
- Interfaces use the C# `I` prefix (`IActionExecutor`); docs keep the conceptual names.
- Caller scope (`Device` / `Admin`) is a use-case parameter; how the admin authenticates is decided in Phase 3.
- Grid sizes 3×3, 5×3, 8×4; max 10 pages per profile.
- Domain and Application reference no packages (enforced by the architecture tests).
- Delivery: **stacked PRs to main**, merged in order. ~400 authored lines per PR as a planning heuristic.

## Tasks

| ID | Slice / PR | Route | Status |
|---|---|---|---|
| P2-1 | `Result`, `DomainException`, profiles, pages, buttons, grid, appearance | delegated writer (2+ non-trivial files) | in progress |
| P2-2 | `Action` hierarchy + whitelist architecture test | delegated writer | pending |
| P2-3 | Devices, pairing session, agent settings | delegated writer | pending |
| P2-4 | Ports, fakes, commands and queries with scope checks | delegated writer | pending |

## Acceptance

- Every type and invariant in ROADMAP 2.1 has a unit test written first.
- Every command and query in ROADMAP 2.3 passes against fakes, including the security rules in 2.4.
- `dotnet build` and `dotnet test` green on each PR; architecture tests still pass.

## Progress

- 2026-10-22: decisions above confirmed by the user; document created.

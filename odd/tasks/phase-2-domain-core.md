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
- Delivery: **stacked PRs to main**, merged in order, **one PR per domain entity/feature** (user decision: line count is
  not a slicing criterion).

## Tasks

| ID | Slice / PR | Route | Status |
|---|---|---|---|
| P2-1 | `Result`, `DomainException`, profiles, pages, buttons, grid, appearance | delegated writer (2+ non-trivial files) | done — 4e54a07, 9f6b8ba; 105 tests green |
| P2-2 | `Action` hierarchy + whitelist architecture test | delegated writer | done — 180 tests green |
| P2-3 | Devices, pairing session, agent settings | delegated writer | pending |
| P2-4 | Ports, fakes, commands and queries with scope checks | delegated writer | pending |

## Acceptance

- Every type and invariant in ROADMAP 2.1 has a unit test written first.
- Every command and query in ROADMAP 2.3 passes against fakes, including the security rules in 2.4.
- `dotnet build` and `dotnet test` green on each PR; architecture tests still pass.

## Progress

- 2026-10-22: decisions above confirmed by the user; document created.
- 2026-10-22: P2-1 done. Errors: one `InvariantViolationException` carrying a `DomainErrorCode`; `Profile.Restore`
  validates every aggregate invariant (entry point for `SaveProfile`). `Button` has no action yet (added in P2-2).
  `MoveButton` rejects occupied targets instead of swapping.
- 2026-10-22: P2-2 done. The hierarchy is closed by a `private protected abstract` member, because a non-sealed
  record's copy constructor must be protected (CS8878). `ActionCatalog.All` is the whitelist; architecture tests check
  every subtype is sealed and listed. `using DomainAction = …` avoids the clash with `System.Action`. `Button` now
  requires an action; `ChangePage` exposes `Destination` (not `Target`, which is the execution target).

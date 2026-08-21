# Project Rules

- Owner: Onur
- Reviewer: Hazar
- Status: Active
- Last reviewed: 2026-08-21
- Related GDD sections: all
- Approval: Owner-mandated rules

## Source of Truth

The PDF GDD is the source of truth. Derived documents explain implementation
but never override it. If the PDF changes, compare its SHA-256 checksum with the
value in the Markdown GDD and mark the translation out of sync until reverified.

## Mandatory Multimodal Reading

The GDD is a multimodal source of truth. Reading extracted text is not
sufficient. Every page, figure, screenshot, diagram, table, caption, annotation,
and spatial relationship must be visually inspected and represented in project
documentation. A document must never be described as read, converted, or
understood while any visual element remains uninspected.

If an element cannot be opened, rendered, extracted, read, or confidently
interpreted, work stops at the affected decision. Report the page/element,
attempted methods, failure, uncertainty, blocked decisions, and required action.
Never silently omit it, summarize only its caption, or replace it with an
assumption.

Visual evidence supports only what it shows. A screenshot can establish visible
composition, UI, art direction, and hierarchy; it cannot alone establish timing,
animation, input behavior, fail logic, or power-up functionality.

## No Silent Assumptions

- Record every unclear GDD statement in `PROBLEMS.md`.
- Record evidence in `REFERENCE-OBSERVATIONS.md`.
- Request owner confirmation for material behavior.
- Record the answer in `DECISIONS.md` before treating it as a rule.
- Configuration hooks may be implemented around blocked values; a behavior may
  not be described as final until approved.

## Problem Report Contract

Every report contains:

1. Intended operation.
2. Affected source/page/figure/system.
3. Observed failure or ambiguity.
4. Alternatives attempted.
5. Missing information.
6. Blocked work.
7. Safe workaround, if any.
8. Approval or external action required.

## Engineering Rules

### Architecture Stages

- The current graybox gameplay loop is allowed to remain component-based:
  MonoBehaviours, explicit Inspector references, ScriptableObject authoring data,
  and tightly scoped manager components may implement the immediate playable
  loop. This is a temporary, documented prototype stage—not evidence that the
  domain/solver architecture exists.
- The production target remains a Unity-independent domain layer. Before
  production level authoring, exact validation, solver, bots, or difficulty
  claims begin, rule ownership moves to the domain layer and presentation only
  forwards commands/renders results.
- The game, solver, bots, and tests must use the same state transition code once
  those systems are introduced. Bot or solver output must never be claimed from
  the current component prototype.
- Do not introduce a new catch-all `GameManager`, a global string-event bus, or
  new hidden singleton dependencies. New component dependencies should be
  serialized explicitly where practical.

### Data and Determinism

- Randomized tests and simulations record seeds.
- ScriptableObjects are authoring data and are converted to immutable runtime
  definitions before production play. Random queue/customer spawning is allowed
  only in clearly named test tooling; it is forbidden in authored levels.
- No SDK, analytics, ads, IAP, network dependency, or remote configuration.
- Preserve unrelated user changes. Use Git LFS for large artist sources.

## Definition of Done

A task is done only when implementation, automated checks, documentation,
visible error handling, and the relevant acceptance evidence are complete.
Deadline pressure never converts a blocked or unverified item into a completed
one.

# Architecture

- Owner: Onur
- Reviewer: Hazar
- Status: Transitional baseline
- Last reviewed: 2026-08-22
- Related GDD sections: 3, 5, 6, 8
- Approval: D-013 and D-014

## Decision Summary

The immediate playable core loop continues in the current component-based Unity
style. This gives the team fast Inspector-driven iteration while the loop is
still changing. It is a prototype implementation, not a claim that the
domain/solver target below already exists.

The pure-C# architecture remains mandatory before production level validation,
solver, policy bots, Monte Carlo, or difficulty claims. It will be extracted
from the stabilized component loop rather than built in parallel now.

## Current Prototype Architecture

The active `Onur-Gameplay` scene currently uses:

- `InputHandler` to raycast a tap to an `IInteractable` stack.
- `QueueManager`/`QueueColumn`, `ConveyorManager`, `RackManager`, and
  `CrowdManager` MonoBehaviours to hold and transition gameplay state.
- `ItemDataSO` as the current food identity/visual authoring asset.
- Dreamteck `SplineComputer` through `SplineConveyorPath` for conveyor position
  and orientation.
- DOTween inside `StackItem` and `Customer` for movement and service feedback.
- Explicit Inspector references plus existing manager singletons.
- Local typed C# events on their owning managers. They publish completed
  prototype transitions for presentation; they are not a global event bus and
  do not decide gameplay rules.

This is authorized only for graybox/core-loop work. `CrowdTestSpawner` and the
queue's random item selection are test utilities, not production content.

## Prototype Exit Gate

Do not begin production level authoring, exact solver work, bots, Monte Carlo,
or difficulty reporting until all of the following are true:

1. Full-rack behavior is explicit and cannot freeze capacity (P-006).
2. Queue/customer supply and demand come from deterministic level data (P-007).
3. The authoritative state transition is Unity-independent (P-008).
4. The current component scripts render/forward that result rather than owning
   a separate interpretation of the rules.

## Target Production Architecture

### Assemblies

- `RestaurantLoop.Domain`: pure C# definitions, state, rules, simulation,
  validation, solver, bots. No UnityEngine reference.
- `RestaurantLoop.Application`: use cases, session orchestration, persistence and
  repository interfaces.
- `RestaurantLoop.Content`: ScriptableObjects and immutable domain conversion.
- `RestaurantLoop.Presentation`: MonoBehaviours, UGUI, animation, VFX, audio,
  input, pooling.
- `RestaurantLoop.Editor`: authoring validation, level generation, reports, and
  build helpers.
- EditMode and PlayMode tests reference the narrowest required assemblies.

### Dependency Direction

`Presentation -> Application -> Domain`

`Content -> Domain`

`Editor -> Content + Domain`

Domain code never references Unity objects, frames, transforms, or coroutines.

### Primary Contracts

- Immutable definitions: `FoodDefinition`, `StackDefinition`,
  `CustomerDefinition`, `TableDefinition`, `LevelDefinition`, `RuleProfile`.
- Runtime: `LevelRunState`, `StackState`, `CustomerState`, `TableState`,
  `RunOutcome`.
- Services: `ILevelRepository`, `IProgressStore`, `IPlaytestSink`.
- Commands: deploy queue front, deploy rack index, complete lap, cross service
  zone, complete customer exit, complete table exit.
- Events: stack deployed/racked/exhausted, serve reserved/completed, customer
  exposed/exited, table cleared, level won/failed, command rejected.

### Runtime Flow

1. Main menu asks `IProgressStore` for highest unlocked level.
2. Selection passes a stable level ID to Gameplay.
3. Content repository converts the ScriptableObject into an immutable domain
   `LevelDefinition` and validates it.
4. Application creates a new `LevelRunState`.
5. Presentation forwards taps as commands.
6. Domain returns a result containing new state and ordered events.
7. Presentation animates the events and acknowledges completion commands.
8. On win, progression saves only the next unlocked level.

### Determinism

- Rules operate on stable integer IDs and ordered collections.
- Simulations use explicit seeded random sources.
- Solver, bots, and runtime replay commands through the same reducer/service.
- Reports retain level version, policy, seed, command list, and outcome.

### Scene Composition

- Build index 0: `Main Menu`.
- Build index 1: `Gameplay`.
- Runtime bootstrap owns level catalog, progress store, audio service, and scene
  navigation.
- Gameplay composition root owns one session and all views for that session.
- Direct Gameplay scene launch uses a development-only default level, clearly
  labeled in logs.

### Performance Boundaries

- Pool high-churn presentation objects.
- Avoid runtime LINQ and per-frame allocations in hot paths.
- Event-driven updates replace polling where possible.
- Inner crowd animation is cheaper than exposed reactions.
- No navigation mesh, physics-driven crowd logic, or per-customer realtime light.

## Migration Plan

Do not rewrite assets, art, UI, Dreamteck, or DOTween. Preserve them as the
presentation layer. Extract only queue dispatch, conveyor capacity/lap state,
rack transitions, customer exposure/service, and win/fail into the target
domain state. `InputHandler` remains the input adapter; components subscribe to
or render domain events.

Migration is manageable while the game has a single graybox loop. It becomes
expensive once many levels or feature-specific component-to-singleton calls
exist, because the two implementations can diverge.

## Local Event Prototype Seam

The first migration seam is implemented in `12-LOCAL-EVENT-MIGRATION.md`.
`ConveyorManager`, `StackItem`, `CrowdManager`, and `RackManager` publish
typed C# events only after their existing component state changes. The first
subscriber is `ConveyorCapacityView`, which renders capacity without making
the conveyor own a TMP label.

This is not a second rules implementation. Existing direct calls remain the
prototype authority; later the pure-C# domain replaces their rule decisions
and returns ordered ID-based events for those same presentation consumers.

# Architecture

- Owner: Onur
- Reviewer: Hazar
- Status: Implementation baseline
- Last reviewed: 2026-08-19
- Related GDD sections: 3, 5, 6, 8
- Approval: Approved approach

## Assemblies

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

## Dependency Direction

`Presentation -> Application -> Domain`

`Content -> Domain`

`Editor -> Content + Domain`

Domain code never references Unity objects, frames, transforms, or coroutines.

## Primary Contracts

- Immutable definitions: `FoodDefinition`, `StackDefinition`,
  `CustomerDefinition`, `TableDefinition`, `LevelDefinition`, `RuleProfile`.
- Runtime: `LevelRunState`, `StackState`, `CustomerState`, `TableState`,
  `RunOutcome`.
- Services: `ILevelRepository`, `IProgressStore`, `IPlaytestSink`.
- Commands: deploy queue front, deploy rack index, complete lap, cross service
  zone, complete customer exit, complete table exit.
- Events: stack deployed/racked/exhausted, serve reserved/completed, customer
  exposed/exited, table cleared, level won/failed, command rejected.

## Runtime Flow

1. Main menu asks `IProgressStore` for highest unlocked level.
2. Selection passes a stable level ID to Gameplay.
3. Content repository converts the ScriptableObject into an immutable domain
   `LevelDefinition` and validates it.
4. Application creates a new `LevelRunState`.
5. Presentation forwards taps as commands.
6. Domain returns a result containing new state and ordered events.
7. Presentation animates the events and acknowledges completion commands.
8. On win, progression saves only the next unlocked level.

## Determinism

- Rules operate on stable integer IDs and ordered collections.
- Simulations use explicit seeded random sources.
- Solver, bots, and runtime replay commands through the same reducer/service.
- Reports retain level version, policy, seed, command list, and outcome.

## Scene Composition

- Build index 0: `Main Menu`.
- Build index 1: `Gameplay`.
- Runtime bootstrap owns level catalog, progress store, audio service, and scene
  navigation.
- Gameplay composition root owns one session and all views for that session.
- Direct Gameplay scene launch uses a development-only default level, clearly
  labeled in logs.

## Performance Boundaries

- Pool high-churn presentation objects.
- Avoid runtime LINQ and per-frame allocations in hot paths.
- Event-driven updates replace polling where possible.
- Inner crowd animation is cheaper than exposed reactions.
- No navigation mesh, physics-driven crowd logic, or per-customer realtime light.


# Current Implementation Status

- Owner: Onur
- Reviewer: Enes, Hazar
- Status: Active prototype baseline
- Last reviewed: 2026-08-21
- Related GDD sections: 3, 5, 6, 8
- Approval: D-013 and D-014

## Verified Current Loop

`Onur-Gameplay` currently runs a graybox loop using:

- `InputHandler` raycasts to `IInteractable` stack objects.
- `QueueManager`/`QueueColumn` deploy queue-front stacks.
- `ConveyorManager` moves stacks over a Dreamteck `SplineComputer` and tracks
  five-capacity occupancy.
- `CrowdManager` finds a matching exposed customer near the belt and reveals
  one inner customer after service.
- `RackManager` receives a stack that completes its lap and permits redeploy.
- `ItemDataSO` identifies food and provides its prefab, balloon icon, and UI
  colour.

The scene was smoke-tested in Play Mode on 2026-08-21: it launched, displayed
the queue/crowd/conveyor/rack, and had no Unity Console errors. This is not a
proof of correctness, Android readiness, solvability, or GDD compliance.

## Architecture Decision

For the immediate core-loop stage, continue the existing component-based Unity
style. This keeps Inspector-based iteration, art integration, and spline work
fast for the five-person team. It is a deliberate temporary boundary:

```text
Current: Input -> MonoBehaviour managers -> transforms/DOTween
Target:  Input -> Application command -> pure C# state/event -> Unity views
```

Do not add a second interpretation of rules. Existing MonoBehaviours are the
temporary implementation. The domain model becomes the authoritative
implementation before production `LevelDefinition` assets, solver, validation,
bots, or Monte Carlo work.

## Migration Preservation Plan

The following survive the later extraction: `ItemDataSO` authoring assets,
prefabs, Dreamteck visual spline, UI, input hit detection, DOTween clips, and
art/audio bindings. The rules to migrate are queue dispatch, capacity/rack
transitions, customer exposure/service, and win/fail state. Existing
components then render domain events instead of deciding rules themselves.

Migration is manageable before production levels exist. It becomes expensive if
the team adds level-specific behavior, random production spawning, or new
cross-manager singleton calls first.

## Trello Code Reconciliation

`Done - Code` means a first graybox implementation exists unless a card has
explicit acceptance evidence. The following need attention:

| Card | Accurate status |
|---|---|
| SplineConveyorPath for Conveyor | Implemented, but update its description from Unity Splines to Dreamteck Splines. |
| Conveyor Simulation & Flow Control | In Progress: P-006 full-rack deadlock prevents acceptance. |
| Conveyor dynamic Entrance & Exit Points | Graybox Done. |
| Dynamic Rack Slot Spawning | Graybox Done. |
| Conveyor to Rack Jump / Rack Slot Reuse / Slot Occupancy | In Progress: rack-full state and queue-parent cleanup are unresolved. |
| Dynamic Queue Grid Spawner | Graybox Done. |
| 3D Raycast Tap Detection | Implemented; needs Android-touch acceptance. |
| Dreamteck Spline Integration & Capacity Indicator | Graybox Done; not final conveyor art integration. |

The following TODO cards already have partial implementation and should be
placed in In Progress with acceptance criteria: StackItem identity/visual
binding, queue item data distribution, customer agent/spawner, and customer
order/item consumption. Level data/loader, win/fail, events, pooling, and
conveyor UV scrolling remain TODO.

## Required Next Fixes

1. Resolve P-006: full-rack behavior must move, reject, or fail explicitly;
   it must never freeze a capacity slot.
2. Detach a queue stack from its slot before its conveyor jump.
3. Clarify and implement the GDD's stack-consumption semantics; `remainingCount`
   is currently never decremented.
4. Replace random queue/customer data with deterministic level data before
   authoring a production level.

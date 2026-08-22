# Local C# Event Migration

- Owner: Onur
- Reviewer: Hazar, Enes
- Status: Implemented prototype seam; presentation expansion pending
- Last reviewed: 2026-08-22
- Related GDD sections: 3, 5, 6, 8
- Approval: D-013 staged architecture

## Purpose

Restaurant Loop currently has a component-based Unity gameplay prototype. This
document defines a small, traceable event seam inside that prototype. It does
not make the system globally event driven and it does not replace the future
pure-C# domain model.

```text
Tap -> QueueColumn -> ConveyorManager -> StackItem / CrowdManager
                               |
                               +-> typed local event -> UI / future VFX, audio, logging
```

Use a direct method call when one component needs another component to decide
or perform a gameplay rule. Use a local event only after the owning component
has completed its current state transition. Use a completion callback only
when presentation completion genuinely gates the next gameplay step.

## Current Contracts

| Owner | Event | Meaning | First/future observers |
|---|---|---|---|
| `ConveyorManager` | `CapacityChanged(int occupied, int maximum)` | A successful reserve or release changed capacity. | `ConveyorCapacityView`; future accessibility/UI feedback. |
| `ConveyorManager` | `StackEnteredBelt(StackItem stack)` | The entry jump completed and conveyor state contains the stack. | Future belt VFX/audio/logging. |
| `StackItem` | `FoodCommittedToCustomer(StackItem stack, Customer customer, ItemDataSO food)` | The current prototype decremented the stack and committed its service flow. It is not a completed customer-service claim. | Future food-flight presentation/logging. |
| `StackItem` | `StackDepleted(StackItem stack)` | Final committed food removed the stack from belt state. | Future depletion VFX/audio. |
| `CrowdManager` | `CustomerExitCompleted(Customer customer, int edgeSlot)` | An exiting customer released its edge slot. | Future station/count feedback. |
| `CrowdManager` | `EdgeCustomerReplacementStarted(Customer customer, int edgeSlot)` | A replacement is assigned to that slot and starts moving toward it. | Future reveal VFX/audio. |
| `RackManager` | `StackAssignedToRack(StackItem stack, RackSlot rackSlot)` | A rack accepted the stack and the current return animation started. | Future rack feedback/logging. |
| `RackManager` | `RackStackRedeploymentStarted(StackItem stack, RackSlot rackSlot)` | A rack stack was accepted for belt redeployment and its slot was cleared. | Future rack feedback/logging. |

## Example: Conveyor Capacity

The publisher owns gameplay state. It exposes a fact after changing it:

```csharp
public event Action<int, int> CapacityChanged;

public bool TryReserveSlot()
{
    if (!CanAcceptStack) return false;

    occupiedCapacity++;
    CapacityChanged?.Invoke(occupiedCapacity, maxCapacity);
    return true;
}
```

`ConveyorCapacityView` is presentation-only. It subscribes and unsubscribes
with its component lifecycle, and does not write conveyor state:

```csharp
private void OnEnable()
{
    conveyor.CapacityChanged += Refresh;
    Refresh(conveyor.OccupiedCapacity, conveyor.MaxCapacity);
}

private void OnDisable()
{
    conveyor.CapacityChanged -= Refresh;
}
```

The label's initial render comes from the public snapshot properties; it does
not depend on an event having fired before it enabled.

## Rules and Anti-Patterns

- Keep event names factual and past tense: `StackEnteredBelt`, not
  `TryEnterBelt`.
- Never make a subscriber perform hidden gameplay decisions. A VFX listener
  may play particles; it must not release capacity, choose a customer, or
  change queue order.
- Subscribe in `OnEnable` and unsubscribe in `OnDisable`. Do not use anonymous
  lambdas where the delegate cannot be removed later.
- Do not introduce `EventManager`, a string-keyed event bus, generic
  `IObserver.OnNotify()`, or a shared `Subject` MonoBehaviour base class.
- Do not call an event `CustomerServed` while the current flow has only
  committed food or begun an animation. Match the event name to the completed
  state transition.

## Migration to the Production Domain

The component prototype remains authoritative until the production domain
exists. During extraction, move queue dispatch, conveyor occupancy/lap, rack,
customer exposure/service, and win/fail rules into pure C# state transitions.
The domain will return ordered, ID-based events such as
`FoodCommitted(stackId, customerId, foodId)` and `CustomerExited(customerId)`.

Unity presentation keeps the same role: input adapts to a command, views
render returned events, and animation-completion callbacks explicitly request
the next allowed domain transition. Do not maintain a component-rule version
and a domain-rule version in parallel.

## Non-Goals of This Pass

This migration deliberately does not alter full-rack handling, queue parent
cleanup, service/lap ordering, zero-count stack behavior, station-selection
randomness, supply/demand conservation, solver support, or human difficulty
validation. Track and accept those as separate gameplay-rule changes.

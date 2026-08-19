# Gameplay Rules

- Owner: Enes
- Technical reviewer: Onur
- Status: Working; reference-dependent values are blocked
- Last reviewed: 2026-08-19
- Related GDD sections: 3-6
- Approval: Partial

## Runtime Phases

`Loading -> Ready -> Playing -> Resolving -> Won|Failed`

- Input is accepted only in `Ready` and `Playing`.
- A terminal outcome freezes new commands while presentation completes.
- Retry constructs a new state from `LevelDefinition`; it never mutates the old
  state back to its initial values.

## Customer Lifecycle

`Interior -> MovingToEdge -> Exposed -> Reserved -> Eating -> Exiting -> Gone`

- Only `Exposed` customers can be reserved.
- A customer has at most one reservation.
- Movement must finish before the customer becomes exposed.
- Table membership affects group departure, not individual reservations.

## Stack Lifecycle

`Queued -> Entering -> Circulating -> Exhausted|Racking -> Racked -> Entering`

- Only the queue front or a rack stack can be deployed.
- Conveyor capacity is five; rack capacity is five.
- A stack counter never becomes negative.
- Lap completion is processed before any service zone beyond the lap boundary.

## Serving Order

1. Detect service-zone entry.
2. Reject customers that are not exposed, do not match, or are reserved.
3. Reserve the first eligible customer for the approaching stack.
4. Emit `ServeStarted` for presentation.
5. Commit one unit of food and transition the customer to eating.
6. Release reservation through the completed transition, never through a
   second target query.

Newly exposed customers are ineligible for the crossing that exposed them.

## Win

Win is emitted after the last customer's exit completes, not at service time.
Presentation then shows the empty restaurant before the win panel.

## Failure

Core failure requires:

- rack occupancy equals five; and
- there is no valid serve reachable from any circulating or racked stack or the
  queue front under the confirmed rule set.

The observation-dependent grace period remains configurable and unapproved.

## Tables

- A table becomes eligible when its authored boundary touches the exposed edge.
- All members may then be reserved independently.
- Members do not leave individually.
- When every member is served, emit one `TableCleared` event and start the group
  exit/celebration.

## Active-Run Persistence

Do not save queue, rack, conveyor, customer, reservation, timer, or random state.
On application restart/background recovery, start the selected level again.

## Blocked Rules

The following must remain parameters, accompanied by visible TODO/blocker
references, until `DECISIONS.md` records approval:

- serving stop/slow/pass behavior;
- occupied-entry behavior;
- deadlock grace time;
- concrete power-up effects and introductions.


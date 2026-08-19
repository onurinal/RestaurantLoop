# Feel Specification

- Owner: Enes
- Contributors: Hazar, Merve, Bengisu
- Status: Baseline; reference-dependent values blocked
- Last reviewed: 2026-08-19
- Related GDD sections: 3, 4, 7
- Approval: Partial

## Feedback Hierarchy

Correct state must remain understandable with sound and particles disabled.
Motion, VFX, and audio reinforce a visible state change but never replace it.

## Baseline Timing

| Beat | Initial value | Source |
|---|---:|---|
| Tap acknowledgement | next rendered frame | Project requirement |
| Food flight | 0.35 s | GDD range 0.3-0.4 s |
| Eating | 0.4 s | GDD |
| Customer exit | 0.75 s | GDD range 0.6-0.9 s |
| Crowd slide | 0.3 s | GDD |

These are data in `FeelProfile`, not hard-coded constants.

## Serve Sequence

1. Tapped stack compresses/highlights immediately.
2. It moves to entry and settles onto the belt.
3. The tower maintains a controlled sway proportional to height.
4. At an approved service crossing, the top container follows a readable arc.
5. Catch pop aligns with the customer's catch pose.
6. Bite/eating sound aligns with the eating pose.
7. The customer jumps and exits.
8. The nearest interior customer slides into the exposed node.
9. Their balloon changes from desaturated/small to full-color/large with a ding.

## Stack Endings

- Empty: tray ejects upward, rotates, emits micro-confetti, then returns to pool.
- Remaining: stack lands in rack with a heavier, lower-pitched impact.
- Invalid tap/full belt: local shake only; no gameplay state changes.

## Completion Curve

Crowd ambience follows an authored curve using remaining/starting customers.
The final exit lowers ambience before the clean-room reveal and win stinger.

## Calibration Scene

Include short/medium/tall stacks, queue/rack transitions, a service target,
crowd reveal, table clear, target resolutions, and live `FeelProfile` controls.

## Blocked Values

Conveyor speed, serving stop/slow/pass, occupied-entry response, wobble angle,
deadlock grace, and power-up feedback require recorded observation and approval.


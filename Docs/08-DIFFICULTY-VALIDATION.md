# Difficulty Validation

- Owner: Enes
- Technical owner: Onur
- Status: Planned; blocked on deterministic gameplay state
- Last reviewed: 2026-08-21
- Related GDD section: 5
- Approval: Approved method

## Exact Solver

**Current status:** not implemented. The active component prototype contains
random test spawning and no reusable pure-C# state transition. It cannot be
used for solver, bot, replay, or Monte Carlo claims.

Searches legal commands using the production state transition code. It reports
one solution, explored states, peak rack occupancy, forced decisions, alternate
first moves, and deadlock branches. State hashing excludes presentation data.

## Policy Bots

- Greedy: deploy immediate visible matches.
- Rack-averse: prefer moves with lower predicted rack occupancy.
- Short-horizon: score a limited number of future commands.
- Novice/noisy: weighted legal choices and delayed rack reuse.
- Stress: intentionally explores poor legal actions.

Every randomized run stores policy, level version, and seed.

## Monte Carlo

Run 10,000 seeded attempts per level and record win/fail by policy, fail cause,
duration/turn count, peak rack, rack reuses, decision count, forced-move ratio,
deadlock depth, alternate solutions, and first irreversible mistake.

Bot output is called modeled difficulty until calibrated.

## Human Calibration

- At least 10 blind players per five-level batch.
- Count the first attempt per player/level as the primary metric.
- Track retries separately.
- Exclude crashes, invalid setups, and manual quits from the denominator while
  reporting them separately.
- Development builds log locally and export CSV; release builds disable logging.

Target aggregate first-attempt failure:

- L1-10: 0-5%.
- L11-20: 10-20%.
- L21-30: 20-30%.

If modeled and observed band rates differ by over five percentage points,
recalibrate policies before trusting model-driven tuning.

## Report Acceptance

Reports always distinguish exact solvability, modeled policy difficulty, and
measured human results. Missing human data is a blocker, not a zero-percent fail
rate.

# Level Design

- Owner: Enes
- Technical reviewer: Onur
- Status: Production specification
- Last reviewed: 2026-08-19
- Related GDD sections: 3, 5, 6
- Approval: Core grammar approved; cadence partly blocked

## Authored Data

Each `LevelDefinition` contains stable ID/version, ordered queue stacks, customer
nodes and food orders, layers, table groups, allowed power-ups, rule profile,
feel profile, and tuning history. Runtime state never lives in the asset.

## Mandatory Invariants

- For each food, customer count equals total queue stack count.
- Every non-table customer can eventually become exposed.
- Every table has members and a valid exposure layer.
- Food IDs, node IDs, and table IDs are unique and valid.
- Exact solver finds a win and production replay reaches the same outcome.

## Difficulty Levers

Apply in this order so tuning remains explainable:

1. Food/color count.
2. Customer count.
3. Crowd depth and exposure order.
4. Queue ordering.
5. Stack count/fragmentation.
6. Rack-pressure traps.
7. Table placement.
8. Approved power-up availability.
9. Timed-customer pressure only in the bonus phase.

## Content Bands

- L1-2: tap, deploy/serve, then rack tutorial.
- L1-5: vertical-slice grammar with core rules only.
- L6-10: reinforce and combine core rules.
- L10+: tables become eligible for introduction after cadence approval.
- L11-20: deeper crowds and stronger rack planning.
- L21-30: up to six foods and approximately 120 customers.

Exact introduction levels for reference-derived features remain blocked until
onboarding evidence is approved.

## Level Definition of Done

- Authored intent and expected difficulty recorded.
- All automated validation passes.
- Monte Carlo report generated with version and seeds.
- Device readability checked.
- First-attempt human evidence recorded when available.
- Tuning changes and reasons retained.


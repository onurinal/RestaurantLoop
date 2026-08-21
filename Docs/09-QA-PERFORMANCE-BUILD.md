# QA, Performance, and Build

- Owner: Hazar
- Reviewer: Onur
- Status: Working checklist
- Last reviewed: 2026-08-21
- Related GDD sections: 3, 5, 7, 8
- Approval: Core targets approved

## Automated Tests

**Current status:** no automated gameplay-test suite exists yet. The following
are required acceptance tests, not completed evidence. A clean Editor smoke
test on 2026-08-21 launched the current scene with no Console errors; that does
not validate the cases below.

- Conservation, IDs, references, table membership, and topology.
- Queue-front and rack-only input.
- Conveyor/rack capacity rejection.
- Reservation exclusivity and first-approaching-stack behavior.
- Newly exposed customer misses the current crossing.
- Lap endpoint precedes later service.
- Moving customers cannot be targeted.
- Stack exhaustion and rack reuse.
- Table members reserve separately and leave together.
- Win after final exit.
- Full-rack no-service failure.
- Retry constructs a clean state.
- Highest-completed-level persistence.
- Solver command replay equals solver outcome.

## Resolution and Accessibility

- Portrait 16:9 and 20:9 safe areas.
- 375 px-width screenshot review.
- Grayscale and common color-vision simulations.
- Order remains identifiable by silhouette/container/icon without color.

## Performance

- Android 10+, 3 GB RAM target.
- Stable 30+ FPS; 33.3 ms hard frame limit and under-28 ms normal target.
- Stress scene: 120 customers, six foods, full queue/rack, reactions and VFX.
- Inspect allocations, pool misses, overdraw, active animators, audio voices,
  texture memory, and build size.
- APK must remain below 200 MB.

## Android Configuration

- Package ID: `com.udogames.restaurantloop`.
- Portrait only.
- ARM64 and IL2CPP.
- Minimum API compatible with Android 10 requirement.
- No forced internet permission or external SDK.
- Internal debug signing; store release is out of scope.

## Release Gate

Clean install, menu, level selection, tutorial, fail/retry, win/next, progression,
background restart, L1/L10/L20/L30 smoke tests, no missing assets, no unresolved
core blockers, and final known-issues report.

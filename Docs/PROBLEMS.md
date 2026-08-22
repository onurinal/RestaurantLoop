# Problems and Blockers

- Owner: Enes
- Contributors: everyone
- Status: Active
- Last reviewed: 2026-08-22
- Approval: Living log

## P-001 - Initial PDF visual inspection unavailable

- Status: Resolved.
- Attempt: inspect the GDD visually.
- Failure: Poppler/PyMuPDF were unavailable and the in-app browser could not
  connect.
- Alternatives: inspected PDF image XObjects directly with pypdf, decoded their
  RGB and transparency masks, exported all nine images, mapped image usage to
  pages, and visually inspected each image.
- Result: figures are preserved under `GDD/figures/` and documented in the
  English GDD.
- Process correction: text extraction must never again be reported as complete
  document understanding before visual verification.

## P-002 - Reference behavior has not been observed

- Status: Blocked.
- Affects: conveyor stop/slow/pass behavior, occupied entry, deadlock delay,
  power-up functions, feature introduction cadence, and reference submechanics.
- Attempted evidence: inspected the embedded Yarn Loop and Pixel Flow stills.
- Missing: interactive gameplay observation with recorded levels/builds.
- Safe work: implement data contracts and configuration points only.
- Required action: team completes the GDD onboarding and submits observations
  for owner approval.

## P-003 - Final art and audio do not exist

- Status: Open production dependency.
- Affects: final look, character rig/animation, VFX, mix, readability, and APK
  size/performance.
- Safe work: implement presentation contracts and primitive placeholders.
- Required action: Merve and Bengisu produce and approve assets according to the
  art/audio bibles.

## P-004 - Human fail-rate evidence does not exist

- Status: Blocked external validation.
- Affects: claims that L1-10, L11-20, and L21-30 meet human fail targets.
- Safe work: exact solving, policy simulations, deterministic reports.
- Required action: at least 10 blind players complete first attempts for each
  five-level batch; export debug logs and calibrate policies.

## P-005 - Minimum-spec Android device not identified

- Status: Blocked external validation.
- Affects: 30 FPS and memory acceptance on Android 10 / 3 GB RAM.
- Safe work: editor profiling and APK generation.
- Required action: assign a physical target device and record model/OS/RAM.

## P-006 - Full rack can deadlock the conveyor

- Status: Technical freeze resolved on 2026-08-22; terminal fail outcome remains
  open.
- Intended operation: a stack reaching its exit should enter an available rack
  slot, or trigger the approved fail/blocked behavior.
- Affected system: `StackItem.OnExitReached`, `RackManager.TryAddStackToRack`,
  `ConveyorManager` capacity.
- Resolution: a stack that reaches a full rack now enters an explicit
  `IsWaitingForRack` state at the legal exit. It does not serve beyond that
  boundary and retries rack assignment when a slot becomes available.
- Verification: a Play Mode stress check filled all rack slots, confirmed the
  exiting stack remained reversible and retained its capacity reservation, then
  freed one slot and confirmed the stack began its rack transition.
- Missing information: the GDD/reference-confirmed full-rack fail behavior.
- Remaining work: implement the authoritative no-valid-move fail predicate,
  approved grace/feedback, and run outcome. A true deadlock can therefore wait
  indefinitely instead of ending the level, but it no longer corrupts/freezes
  conveyor state.
- Required action: Enes records the approved fail behavior if the GDD is
  ambiguous; Onur implements it with the later authoritative run-state system.

## P-007 - Current scene creates non-deterministic, potentially unwinnable runs

- Status: Open prototype limitation.
- Intended operation: every production level has conserved food supply/demand
  and a reproducible solution.
- Affected system: `QueueManager` and `CrowdTestSpawner` random item selection.
- Observed failure: queue stacks and customer orders are assigned independently
  with `Random.Range`; no conservation or solvability check exists.
- Safe workaround: use the scene only as a graybox interaction sandbox.
- Required action: replace random production spawning with deterministic level
  data before level, difficulty, or progression work.

## P-008 - Component prototype cannot yet support solver, bots, or validator

- Status: Open architecture gap.
- Intended operation: runtime, tests, solver, and policy bots share one
  deterministic rules implementation.
- Affected system: current `QueueManager`, `ConveyorManager`, `RackManager`,
  `CrowdManager`, and `StackItem` MonoBehaviours.
- Observed limitation: gameplay state, Unity transforms, singleton lookups, and
  DOTween presentation are coupled; no pure-C# state/reducer or automated
  gameplay test suite exists.
- Safe workaround: continue the component loop only for immediate graybox
  iteration under D-013.
- Required action: extract the authoritative domain transition before production
  validation work.

## P-009 - Trello Code status lacks acceptance evidence

- Status: Open production-process issue.
- Intended operation: Done-Code means implemented, verified, documented, and
  GDD-ready.
- Affected source: Ekip 3, `Done - Code` and `TODO - Code` lists.
- Observed failure: cards have no owner, due date, acceptance checklist, or
  test evidence. Several Done cards represent valid graybox work but still have
  known defects or incomplete production behavior.
- Safe workaround: treat Done-Code as “first implementation exists” until the
  board is reconciled; move partial cards to In Progress.
- Required action: add acceptance criteria and update the affected card status
  before relying on the board for production reporting.

## P-010 - Unity editor pipeline did not complete the final prefab reload smoke test

- Status: Open tooling-verification limitation.
- Affected work: `ConveyorCapacityView` prefab attachment and final Play Mode
  verification of the local C# event migration.
- Evidence: the event code compiled successfully, and the scene-level capacity
  view previously rendered `0 / 5` in Play Mode. After moving that attachment
  to the reusable conveyor prefab to avoid unrelated scene YAML churn, Unity
  pipeline `open_scene` and `editor_status` calls each timed out after 60
  seconds.
- Safe conclusion: the C# implementation and prefab references have static
  validation, but the final reload from the prefab asset is not verified.
- Required action: once the Unity editor is responsive, reopen
  `Onur-Gameplay`, confirm the conveyor prefab has `ConveyorCapacityView`, run
  a queue deploy/release cycle, and confirm the label changes from `0 / 5`.

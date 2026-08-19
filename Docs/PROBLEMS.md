# Problems and Blockers

- Owner: Enes
- Contributors: everyone
- Status: Active
- Last reviewed: 2026-08-19
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


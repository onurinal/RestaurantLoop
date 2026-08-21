# Fourteen-Day Production Plan

- Owner: Enes
- Status: Active baseline
- Last reviewed: 2026-08-21
- Approval: Project owner

## Named Ownership

- Onur: domain rules, validator, solver, bots, simulations, EditMode tests.
- Hazar: Unity presentation, UI, progression, integration, Android, performance.
- Enes: GDD/docs, reference synthesis, 30 levels, tutorial, tuning, acceptance.
- Merve: environment, conveyor/rack/queue, foods, UI art, lighting/readability.
- Bengisu: characters, rig/animation, balloons, VFX, SFX, ambience/music assets.

## Architecture Stage Gate

The current implementation is permitted to continue as a component-based
graybox loop for immediate gameplay iteration. It must first resolve the
recorded conveyor/rack deadlock and deterministic-content gaps. Before the team
claims a production level, runs validation, or starts solver/bot work, Onur
extracts the authoritative rules into the domain state transition described in
`03-ARCHITECTURE.md`. Hazar retains the existing Unity components as the
presentation bridge; Dreamteck remains presentation-only.

## Daily Deliverables

| Day | Onur | Hazar | Enes | Merve | Bengisu |
|---:|---|---|---|---|---|
| 1 | Rule/solver questions | Project/build audit | GDD conversion | Figure visual analysis | Motion/audio analysis |
| 2 | Domain contract | Git LFS/build baseline | Observation synthesis | Look-development | Look-development |
| 3 | Core state/simulation | Bootstrap/graybox | Schema + L1-5 | Environment/food graybox | Rig/animation blockout |
| 4 | Serving/rack/win/fail | Crowd/UI/retry | Validate L1-5 | Representative final assets | Core reactions/VFX |
| 5 | Validator/solver | Feel/menu/progression | Tutorial + playtest | Vertical-slice art | Vertical-slice character/audio |
| 6 | Policy bots | Pooling/integration | L6-10 | Reusable kit | Character variants |
| 7 | Table rules | Table presentation | L11-15 | Table/density art | Group celebration |
| 8 | Approved power-ups | Power-up UI | L16-20 | Power-up art | Power-up VFX/audio |
| 9 | Hard-level profiling | 120-customer profiling | L21-25 | Density optimization | Animation/audio optimization |
| 10 | Validate new levels | Full progression build | L26-30 | Art consistency | Asset completion |
| 11 | Full simulation | Replay defect fixes | Difficulty/content lock | Readability audit | Feedback audit |
| 12 | Regression fixes | Android/performance QA | GDD compliance | Optimization | Mix/performance |
| 13 | Domain blockers | Client blockers + RC | Retest/close issues | Art blockers | Feedback blockers |
| 14 | Final reports | Final APK | Traceability/release notes | Source/export audit | Source/export audit |

## Daily Operating Rules

- 15-minute blocker meeting at start of day.
- Merge/integration build by end of day from Day 4 onward.
- Every blocker is recorded immediately; no waiting for the next review.
- Five-level batches require automated validation before integration.
- Day 13 midday is code/content freeze except approved release blockers.

## External Dependencies

Reference-game access/approval, artist-authored assets, 10 blind playtesters per
batch, and a named minimum-spec Android device. If absent, the corresponding
acceptance claim remains blocked.

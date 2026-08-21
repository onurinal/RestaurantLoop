# Decision Log

- Owner: Enes
- Status: Active
- Last reviewed: 2026-08-21

| ID | Decision | Status | Source/approver |
|---|---|---|---|
| D-001 | English is the canonical working language; PDF remains authoritative. | Approved | Project owner |
| D-002 | Extract and embed all PDF figures with interpretations. | Approved | Project owner |
| D-003 | Observe reference behavior, document evidence, then confirm before implementation. | Approved | Project owner |
| D-004 | Use Unity built-ins, C#, ScriptableObjects, UGUI, and no added architecture framework. | Approved | Project owner |
| D-005 | Onur owns core/simulation; Hazar owns client/build. | Approved | Project owner |
| D-006 | Merve owns world/food/UI; Bengisu owns characters/animation/VFX/audio assets. | Approved | Project owner |
| D-007 | Use stylized 3D gameplay with 2D UI and an orthographic three-quarter camera. | Approved | Project owner |
| D-008 | Sixth food is a purple dessert bowl. | Approved | Project owner |
| D-009 | Levels use ScriptableObject authoring and locally persisted linear unlocks. | Approved | Project owner |
| D-010 | Difficulty uses 10,000 simulations/level and small-sample human calibration. | Approved | Project owner |
| D-011 | Full core 30-level MVP target is 14 calendar days; timed customers remain bonus. | Approved | Project owner |
| D-012 | Android package ID is `com.udogames.restaurantloop`; internal debug signing is sufficient. | Approved | Project owner |
| D-013 | Use the current component-based MonoBehaviour gameplay loop for the immediate graybox/core-loop stage. Keep it explicitly provisional; extract authoritative deterministic rules before production-level validation, solver, bot, or Monte Carlo work. | Approved | Project owner, 2026-08-21 |
| D-014 | Dreamteck Splines is the current conveyor presentation package. It does not own gameplay rules or replace the future deterministic path/state model. | Approved | Project owner, 2026-08-21 |

## D-013 Decision Detail

- Date: 2026-08-21
- Affected GDD sections: 3, 5, 6, 8.
- Alternatives considered: begin the full pure-C# domain architecture now;
  retain the existing component loop permanently; use the component loop now
  and extract the shared authoritative rules before production validation.
- Decision: use the staged third option.
- Approver: Project owner.

## D-014 Decision Detail

- Date: 2026-08-21
- Affected GDD sections: 3 and 8.
- Alternatives considered: Unity Splines presentation; Dreamteck presentation;
  manually placed non-spline conveyor geometry.
- Decision: retain the already integrated Dreamteck spline only as conveyor
  presentation while gameplay remains independent of its API at the production
  architecture stage.
- Approver: Project owner.

New decisions require a date, affected GDD sections, alternatives considered,
and explicit approver. Superseded decisions remain in the log.

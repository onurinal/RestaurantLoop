# Audio Bible

- Owner: Bengisu
- Technical integrator: Hazar
- Status: Production specification; assets pending
- Last reviewed: 2026-08-19
- Related GDD section: 7
- Approval: Direction approved

## Mixer Groups

Music, Crowd Ambience, Gameplay SFX, UI, and Win/Fail.

## Required Event Set

- UI tap.
- Stack entering conveyor.
- Food throw/flight.
- Customer catch pop.
- Eating/nom-nom.
- Happy jump.
- Exit door bell.
- Exposure/color-pop ding.
- Empty tray and micro-confetti.
- Rack landing.
- Table clear.
- Win and fail.

## Integration Rules

- Domain/presentation events trigger one-shot sounds.
- Animation events align catch, bite, tray release, and foot impacts.
- Crowd volume is driven by remaining-customer ratio.
- Apply voice limits so a dense level cannot stack uncontrolled sounds.
- Test on phone speaker, headphones, and low volume.
- Audio is feedback, not the only carrier of gameplay information.

# Art Bible

- Owner: Merve
- Contributor: Bengisu
- Status: Production specification; final assets pending
- Last reviewed: 2026-08-19
- Related GDD sections: 4, 7
- Approval: Direction approved; look-development pending

## Target

Stylized low-poly 3D gameplay with crisp 2D UGUI, a fixed orthographic elevated
three-quarter camera, warm daytime food-court lighting, rounded silhouettes, and
minimal facial detail.

## Reference Boundaries

- Yarn Loop/Pixel Flow: hierarchy, density, queue/rack/conveyor readability.
- Bar Rumble: stack height, sway, and service spectacle only.
- Eatventure: crowd pressure and balloon immediacy only.
- Kaiten-sushi photo: warmth, abundance, and real-world food context only.

Do not copy branded packaging, reference levels, tycoon UI, or exact characters.

## Food Contract

| Food | Color | Silhouette |
|---|---|---|
| Burger | red | wide square box |
| Fries | yellow | tall open carton |
| Steak | white/light | round plate |
| Drink | blue | tall cup and straw |
| Sushi | dark/teal | long rectangular tray |
| Dessert | purple | round bowl and spoon/topper |

Stack model, balloon icon, and HUD icon use the same container identity. Color
is never the only cue.

## Crowd Kit

- One shared humanoid rig/controller.
- Modular heads/hair, outfits, skin tones, and accessories.
- Inner customers use low-cost subtle idles.
- Exposed/eating/celebrating/exiting customers receive full animation.
- Balloon states: interior, exposed, reserved, and completed.

## Look-Development Gate

Produce one coherent set of three style frames: early 25-customer level,
midgame table level, and 120-customer six-food level. Review at native portrait
resolution and 375 px width before bulk production.

## Technical Art Rules

- Shared materials and atlases; no per-customer material instances.
- Simple/baked environment light plus one main directional light.
- No per-customer realtime light and minimize realtime shadows.
- Profile imported texture sizes and compression on Android.
- Store source binaries in Git LFS and commit Unity-ready exports.
- Validate grayscale, protanopia, deuteranopia, and tritanopia readability.


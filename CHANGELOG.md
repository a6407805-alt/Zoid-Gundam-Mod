# Changelog - Zoids Gundam Alliance

## [v2.5.1] - 2026-11-08

### QA Item 18: Outlined Team-Colour Accents Across All Body Animations
- **Body Animation Overhaul**: Directly edited all 35 unit body `anim.png` files (all 3 animation frames per unit, 105 frames total) plus `anim_camo.png`:
  - Added a 2-3px pure-green (`#00FF00`, `RGB(0, 180..240, 0)`) team-colour band across the back plate, shoulders, or cockpit frame.
  - Enclosed every band on all four sides with a crisp, dark `#202020` (`RGB(32, 32, 32)`) outline border.
  - Outlined all adjacent green pixels to ensure complete perimeter definition.
  - High-contrast visibility guaranteed at default camera zoom: red-team Saber Tiger, Char's Zaku II, Z'Gok, Big Zam, Guysak and blue-team Gundam/Gouf are clearly distinguishable from green at default zoom.
- **Limbs and Auxiliaries**: Preserved 2-3px pure-green bands with `#202020` dark outlines on all 26 `leg.png` thighs, 26 `foot.png` dorsal plates, `nanobot.png`, and `magella_top.png`.
- **Validation**: 0 validation errors across all 73 active unit and building definitions.

# Zoids Gundam Alliance — QA Report (play-test + INI audit, 2026-11-08)

Tested: skirmish on "Lake" vs 4 Medium AI (build CC → extractor → Mobile Suit Hangar → units),
plus a full scripted audit of all 75 INI files in `mods/Zoid Gundam/`.
Mod loads with **no errors** in the Mods menu. All image/projectile/turret/animation references resolve.

NOTE: art + several INIs were being rewritten while I tested (mtimes changed mid-session). Re-check visual items after a Sandbox **Reload**.

---

## P1 — Gameplay-breaking

### 1. Command Wolf uses the vanilla unit name `scout`
- File: `units/ZOIDS/Command wolf/command wolf  .ini` → `[core] name: scout`
- In-game: the Command Center's "Scout" button shows the **vanilla scout** icon/unit; Command Wolf never appears as itself.
- Fix: rename to a unique id, e.g. `name: CommandWolf`. Update every reference:
  - `BUILDINGS/HQ/HQ RESTORED.ini` → `canBuild_3_name: scout, Gundam 1` → `CommandWolf, Gundam 1`
  - `launcher/roster.json` (if it keys by unit name)
- Also rename the file to drop the double space: `command wolf.ini` (and `blade left .png` in Gundam 4 → `blade_left.png`, update `[turret_leftSaber] image:`). Trailing spaces in filenames break on some zip/Git/Android paths.

### 2. Tech-2 units can be built from Tech-1 factories (T2 upgrade is bypassed)
| Unit (techLevel 2) | Extra T1 source to REMOVE |
|---|---|
| Geno Saurer | `builtFrom_2_name: ZFACTORY` (it is ALSO in ZFACTORY `[canBuild_lockedT2]` → shows locked and buildable at once) |
| Gojulas | `builtFrom_2_name: ZFACTORY` (same conflict) |
| Guntank ultra | `builtFrom_2_name: GFACTORY` (same conflict) |
| Dibison | `ZFACTORY, landFactory, mechFactory` in builtFrom_1 |
| Iron Kong | `ZFACTORY, mechFactory` |
| Elephander | `ZFACTORY, mechFactory` |
- Fix: delete the `builtFrom_2_*` lines; for Dibison/Iron Kong/Elephander keep only `ZFACTORY_T2` (+ mechFactoryT2 if wanted) and add them to `ZFACTORY.ini [canBuild_lockedT2] name:` so players see them as locked T2.
- Gundam Ship and Whale King list vanilla `airFactory`; Dragoon Nest lists `seaFactory` — fine only if vanilla factories are unreachable; otherwise use `airFactoryT2`/`seaFactoryT2`.

### 3. No team colour on any mod unit or building
- Every new sprite (e.g. `Gundam 1/anim.png`, `saber-tiger/anim.png`, GZ engineer, factories) has **no pure-green team-colour pixels**. In-game, my factory was orange and engineers yellow; enemy copies will look identical.
- Fix: in the HD regeneration script, paint a team-colour accent (stripe/visor/trim) in pure green `#00FF00` shades on every body/turret/leg image, or set `[graphics] teamColoringMode: hueAdd` per unit and recolour the accent area. Verify by spawning the same unit for 2 teams in Sandbox.

## P2 — Visual / "strange" issues seen in game

### 4. Legs trail behind / detach (matches the existing user report)
- Seen on GZ Combat Engineer Mech: leg slabs lag far behind the body while moving.
- Current leg tuning is very uneven between units (Gundam 1 `moveSpeed: 12`, Saber Tiger `14.5`, Geno Saurer `3`).
- Fix for ALL legged units: leg `moveSpeed` ≥ ~6× body `[movement] moveSpeed` (Geno Saurer 3 → ~12), `holdDisMax` ≤ ~0.6× unit `radius`, keep `hardLimit` set on every leg, and check that `attach_x/attach_y` sit inside the body sprite at the new 2× HD `imageScale`. GZ REPAIR DRONE, Geno Saurer, Gojulas, Death Saurer, Ultrasaurus and Big ZAM legs still use the old values.

### 5. Factory sprite does not line up with its footprint
- Mobile Suit Hangar: selection box/footprint sits down-right of the art (sprite overhangs top-left, empty footprint bottom-right). Same `footprint: -2,-2,2,2` + `constructionFootprint: -2,-2,2,3` pattern on GFACTORY, ZFACTORY, ZAIR, GAIR, GINF, ZINF, HQ.
- Fix: either centre the art in the PNG or set `buildingToFootprintOffsetX/Y` (currently 0) so art and footprint overlap. Recheck after the HD art change.

### 6. Build-menu icons for all mod factories are the generic orange vanilla factory
- Builder menu: Mobile Suit Hangar, Gundam Aerospace Dock, Vanguard Bay, Zi-Core Foundry, Sky & Sea Dock, Recon Outpost all show the same orange icon. Give each one its own icon (e.g. `[graphics] icon_build: ...png`).

### 7. Old sprites had big debug-style direction arrows baked in
- In the running game, Gundam/Zaku bodies showed large arrow glyphs (⇩ / ➡). The newly generated art I opened doesn't have them — confirm none remain in any `anim.png` / `topdown_base.png`.

### 8. Geno Saurer claws play the CPC charge pose
- `geno-saurer.ini [turret_killerClaws] onShoot_playAnimation: cpcStance` → every claw hit plays the Charged-Particle-Cannon stance. Add a separate `[animation_clawSwipe]` and use that.

### 9. ZAIR (Sky & Sea Dock) spawns units at its centre
- `BUILDINGS/ZAIR/ZFACTORY.ini exit_y: 0`. Fine for aircraft, but Dragoon Nest (hover/water) spawns inside the building. Use `exit_y: 44` like the other factories. Also rename this file to `ZAIR.ini` (it's called ZFACTORY.ini but defines `ZAIR`, easy to confuse with the real ZFACTORY).

## P3 — Text / consistency

10. `GFACTORY.ini` / `GFACTORY_T2.ini` descriptions say T2 unlocks **"MSN-02 Zeong & MA-08 Big Zam"**, but the locked list is `Guntank ultra, Big ZAM`. Make the text match.
11. `HQ DESTROYED.ini` description says it "Trains Builders & GZ Combat Engineer Mechs" — its canBuild only has `builder`. Add `GZ REPAIR DRONE` or fix text.
12. `mod-info.txt` says "36 units"; there are ~41 buildable units. Update the count (or drop it).
13. Internal names vs folders are confusing (folder `Z_Gok/` contains Zaku/Gouf/Zeong; `Plane ultra` = "Gundam Ship"; `Gundam 4` file vs display name). Not a bug, but a rename pass would prevent future reference mistakes like #1.

## P4 — Balance (DPS = sum of turret damage×60/delay, rough)

| Unit | Price | DPS | DPS per $1k | Note |
|---|---|---|---|---|
| Gundam 4 (twin saber) | 1500 | ~636 | 424 | 2–3× any other T1; sabers do 58+38 AoE each every 20 ticks |
| Rev Raptor | 850 | ~300 | 353 | very high for T1 |
| Guysak | 650 | ~185 | 285 | high |
| Saber Tiger | 1850 | ~454 | 245 | Zoids T1 out-damage Gundam T1 (Gundam 1: 134) |
| Ultrasaurus | 17500 | ~366 | 21 | experimentals are poor value |
| Dragoon Nest | 18500 | ~260 | 14 | " |
| Whale King | 19500 | ~505 | 26 | " |
| Death Saurer | 16500 | ~633 | 38 | " |
Suggested: Gundam 4 saber `areaDamage` 38 → 0 (or `shootDelay` 20 → 40); Rev Raptor/Guysak −30% damage; raise experimental damage ~2× or cut price ~35%.

---

## Verify after fixes
1. Sandbox → Reload → Mods screen shows no errors.
2. CC "Scout" button = vanilla scout; Zoid Foundry shows "RZ-009 Command Wolf".
3. T1 ZFACTORY/GFACTORY: Geno Saurer, Gojulas, Guntank ultra, Dibison, Iron Kong, Elephander all locked until upgraded.
4. Spawn every legged unit, order long moves in 4 directions: legs stay under the body.
5. Same unit for team 1 and team 2 are visibly different colours.

---

## Added 03:16 (owner feedback from in-game screenshot)

### 14. Shields are drawn face-on, not top-down
- Files: `units/GUNDAM/Gundam 3/shield.png` (purple, the one in the owner's screenshot), `units/GUNDAM/Gundam 1/shield.png`, `units/Z_Gok/Z_Gok5/gouf_shield.png`. All are 34×58–62 px front views of the whole shield face (with emblem/visor), lying flat on top of the unit. That's how you'd see a shield from the front, not from above.
- From above, a forearm-mounted shield is seen **edge-on**: a thin plate (≈6–9 px wide at current scale) running along the outside of the left forearm, its long axis pointing forward with the arm, slightly curved/bevelled, and showing at most a sliver of the face colour on the outer edge. No front emblem or visor should be visible.
- Fix:
  1. Redraw all three as top-down edge-on plates: width ≈ 20–25% of length, length ≈ forearm length (~24–30 px), long axis vertical (pointing up = the unit's forward direction, matching the body art). Keep each faction colour on the bevel edge.
  2. In each `[arm_1]` (Gundam 1, Gundam 3, Z_Gok5): move `x` out to the outer shoulder line (≈ -12) so the plate sits beside the forearm, not over the chest. Keep `drawOverBody: true`.
  3. Optional: in block or `recoilShield` animations, rotate `dir` out to ~-35 so the shield swings in front of the body; that's the only time more of the face should show.
- Verify: width/length of the shield's visible pixels ≤ 0.35, and in game the shield reads as a narrow plate on the left arm, not a badge on the torso.

### 15. GZ Combat Engineer should be a nano-bot builder swarm (owner request), and it has a sprite bug now
- File: `units/GZ REPAIR DRONE/GZ REPAIR DRONE.ini` (+ art in that folder). Keep `name: GZ REPAIR DRONE` so every builtFrom/canBuild reference still works.
- Current bug: `[turret_1] image: anim.png` uses the whole body sprite sheet as the gun image, so a second copy of the body is drawn on top of the unit (the doubled/overlapping yellow block in game). The yellow box + 2 legs also doesn't read as a builder.
- Redesign as a hovering swarm:
  - `displayText: GZ Nano-Swarm`, update `displayDescription`, and the launcher roster card/portrait.
  - Delete `[leg_1]`/`[leg_2]`. `[movement] movementType: HOVER`, keep `moveSpeed` ≈1.85. Add `[graphics] image_shadow: AUTO` with `shadowOffsetX/Y: 4` so it reads as floating.
  - Body `anim.png`: a loose cloud of ~12–20 tiny (2–4 px) metallic drones around a faint glowing core, 4 frames where the dots shift slightly (`total_frames: 4`, plus idle/moving animation ranges looping all frames). Include pure-green team-colour pixels on some drones (item 3).
  - Add 4–6 `[arm_N]` sections, each `image_end: nanobot.png` (one ~4 px drone, its own small file), `drawOverBody: true`, and an `[animation_idle]` + `[animation_move]` with `pingPong: true` that moves each arm on a different small orbit (±4–8 px, different timings) so the swarm visibly buzzes.
  - Replace `[turret_1] image: anim.png` with `invisible: true` (or a tiny `nanobot.png`); weapon stays a weak zap. Point `turret_nano` at the swarm core (`x: 0, y: 0`) and give the nano beam a cyan/green colour.
  - Optional flair: `[effect_nanoSpark]` small light flicker when building (`onActions: building` animation spreading the arms toward the build target side).
  - Wreck (`corpse.png`): a small scatter of dead grey dots.
- Verify: no `leg_` sections, movementType HOVER, ≥4 arms using a small nanobot image, turret image ≠ anim.png; in game it reads as a buzzing cloud that floats over water/cliffs and builds.

---

# Phase 2: quality pass (owner request: fit & feel, visual discrepancies, playability, balance vs vanilla)

### 16. Balance vs vanilla Rusted Warfare (area a)
Measured against the game's own `assets/units` INIs (see QA_BALANCE_VS_VANILLA.md). Per dollar, mod units currently have:

| Tier | DPS per $1k (vanilla → mod) | EHP per $1k (vanilla → mod) | Move speed median | Range median |
|---|---|---|---|---|
| Land T1 | 28.6 → 177.5 (**6.2×**) | 528 → 1160 (**2.2×**) | 0.85 → 1.85 | 160 → 225 |
| Land T2 | 19.4 → 131.8 (**6.8×**) | 353 → 1292 (**3.7×**) | 0.61 → 1.35 | 220 → 327 |
| Air | 44 → 140 (**3.2×**) | 417 → 960 (**2.3×**) | 1.4 → 2.75 | 170 → 205 |

Effect: one $850 Zaku II is worth about 8 vanilla $350 tanks. Vanilla turrets (700 HP, ~82 DPS) die in seconds, so base defence and the AI can't keep up.

**Target:** each tier's median value per $ at about **1.3× vanilla**, so mod units still feel premium but vanilla turrets, AI and mixed games stay viable. Keep the mecha "few strong units" feel by raising prices instead of shrinking everything.

**Apply these multipliers to every unit in the tier.** Damage = every projectile's `directDamage` and `areaDamage`. EHP = `maxHp` and `maxShield`. Speed = `[movement] moveSpeed` (scale `moveAccelerationSpeed` the same way). Range = `[attack] maxAttackRange` and each turret's `limitingRange`. Round prices to $50.

| Tier | price | damage | HP + shield | moveSpeed | range |
|---|---|---|---|---|---|
| Land T1 (techLevel 1, not AIR) | ×2.0 | ×0.42 | ×1.10 | ×0.60 | ×0.80 |
| Land T2 (techLevel 2, not AIR, price < 10k) | ×2.5 | ×0.48 | ×0.90 | ×0.60 | ×0.80 |
| Air (movementType AIR, price < 10k) | ×1.7 | ×0.70 | ×0.95 | ×0.65 | ×0.90 |
| Experimental (price ≥ 10k): Death Saurer, Ultrasaurus, Big Zam, Whale King, Dragoon Nest, Elmeth/Gundam Ship | ×1.0 | ×0.50 | ×0.60 | ×0.70 | ×0.85 |

Also:
- Scale leg `moveSpeed` together with body speed so legs keep up (item 4 ratio of ≥6× body speed still applies). Keep `hardLimit`.
- Special actions (`fireTurretXAtGround_withProjectile` projectiles) get the same damage multiplier as their tier.
- Sub-units (`*_nt`, `*_camo`, `*_eshield`, `*_comet`): give them the same multipliers as their parent unit.
- Leave vanilla-override buildings (turrets, extractors, fabricators) unchanged; this pass brings the units back to vanilla's level.
- Experimental-tier numbers are a first pass, because vanilla experimentals use complex weapons my calculator can't fully count. Area **d** (vs AI) will check them in-game.

Per-unit numbers (current → target):

| Unit | File | Tier | Price | Damage | EHP | moveSpeed | Range |
|---|---|---|---|---|---|---|---|
| FF-X7 Core Fighter | `GUNDAM/Plane/Plane.ini` | air | 550 → 950 | ×0.7 (DPS 114 → 80) | ×0.95 (EHP 440 → 418) | 3.0 → 1.95 | 185 → 166 |
| Magella Top (VTOL) | `Z_Gok/Tank/MagellaTop.ini` | air | 800 → 1350 | ×0.7 (DPS 138 → 97) | ×0.95 (EHP 580 → 551) | 2.2 → 1.43 | 235 → 211 |
| EHI-7 Redler | `ZOIDS/Zoids reddla/reddla_camo.ini` | air | 950 → 1600 | ×0.7 (DPS 105 → 74) | ×0.95 (EHP 1170 → 1111) | 3.95 → 2.57 | 205 → 184 |
| EHI-7 Redler | `ZOIDS/Zoids reddla/reddla.ini` | air | 950 → 1600 | ×0.7 (DPS 105 → 74) | ×0.95 (EHP 1040 → 988) | 2.75 → 1.79 | 205 → 184 |
| FF-X7-Bst Core Booster | `GUNDAM/Plane 1/Plane 1.ini` | air | 1250 → 2100 | ×0.7 (DPS 175 → 122) | ×0.95 (EHP 1200 → 1140) | 2.35 → 1.53 | 235 → 211 |
| EZ-021 Death Saurer | `ZOIDS/Deathsaurer/Deathsaurer.ini` | exp | 12500 → 12500 | ×0.5 (DPS 633 → 316) | ×0.6 (EHP 17700 → 10620) | 0.9 → 0.63 | 385 → 327 |
| RZ-037 Ultrasaurus | `ZOIDS/ultasaurus/ultasaurus.ini` | exp | 13000 → 13000 | ×0.5 (DPS 548 → 274) | ×0.6 (EHP 20000 → 12000) | 0.75 → 0.52 | 420 → 357 |
| Dragoon Nest Carrier | `ZOIDS/Dragoon/Dragoon.ini` | exp | 13500 → 13500 | ×0.5 (DPS 808 → 404) | ×0.6 (EHP 12600 → 7560) | 0.85 → 0.59 | 365 → 310 |
| MAN-08 Elmeth | `GUNDAM/Plane ultra/Plane ultra.ini` | exp | 13500 → 13500 | ×0.5 (DPS 846 → 423) | ×0.6 (EHP 8400 → 5040) | 1.35 → 0.94 | 360 → 306 |
| Whale King Sky-Carrier | `ZOIDS/Whale/Whale.ini` | exp | 14000 → 14000 | ×0.5 (DPS 934 → 467) | ×0.6 (EHP 12800 → 7680) | 1.2 → 0.84 | 375 → 318 |
| MA-08 Big Zam | `Z_Gok/Big ZAM/Big ZAM.ini` | exp | 16500 → 16500 | ×0.5 (DPS 703 → 352) | ×0.6 (EHP 16500 → 9900) | 0.85 → 0.59 | 380 → 323 |
| Zoid Anti-Armor Trooper | `ZOIDS/gustav/mGunner.ini` | landT1 | 250 → 500 | ×0.42 (DPS 45 → 19) | ×1.1 (EHP 190 → 209) | 1.25 → 0.75 | 155 → 124 |
| GZ Nano-Swarm | `GZ REPAIR DRONE/GZ REPAIR DRONE.ini` | landT1 | 450 → 900 | ×0.42 (DPS 53 → 22) | ×1.1 (EHP 570 → 627) | 1.85 → 1.11 | 145 → 116 |
| EMZ-15 Molga | `ZOIDS/molga/molga.ini` | landT1 | 525 → 1050 | ×0.42 (DPS 60 → 25) | ×1.1 (EHP 780 → 858) | 1.75 → 1.05 | 175 → 140 |
| RMZ-11 Guysak | `ZOIDS/guysak/guysak.ini` | landT1 | 650 → 1300 | ×0.42 (DPS 134 → 56) | ×1.1 (EHP 680 → 748) | 1.85 → 1.11 | 225 → 180 |
| RZ-009 Command Wolf | `ZOIDS/Command wolf/command wolf  .ini` | landT1 | 750 → 1500 | ×0.42 (DPS 82 → 34) | ×1.1 (EHP 720 → 792) | 2.65 → 1.59 | 215 → 172 |
| RZ-009 Command Wolf | `ZOIDS/Command wolf/command wolf.ini` | landT1 | 750 → 1500 | ×0.42 (DPS 82 → 34) | ×1.1 (EHP 720 → 792) | 2.65 → 1.59 | 215 → 172 |
| HT-01B Magella Attack | `Z_Gok/Tank/Tank.ini` | landT1 | 800 → 1600 | ×0.42 (DPS 142 → 60) | ×1.1 (EHP 1050 → 1155) | 1.25 → 0.75 | 250 → 200 |
| MS-06F Zaku II | `Z_Gok/Z_Gok3/Z_Gok3.ini` | landT1 | 850 → 1700 | ×0.42 (DPS 206 → 87) | ×1.1 (EHP 1050 → 1155) | 1.55 → 0.93 | 205 → 164 |
| EZ-027 Rev Raptor | `ZOIDS/rev-rapter/rev-rapter.ini` | landT1 | 850 → 1700 | ×0.42 (DPS 214 → 90) | ×1.1 (EHP 860 → 946) | 2.55 → 1.53 | 145 → 116 |
| MSM-04 Acguy | `Z_Gok/Z_Gok2/Z_Gok2.ini` | landT1 | 1050 → 2100 | ×0.42 (DPS 130 → 55) | ×1.1 (EHP 1250 → 1375) | 1.75 → 1.05 | 215 → 172 |
| MSM-07 Z'Gok | `Z_Gok/Z_Gok1/Z_Gok.ini` | landT1 | 1200 → 2400 | ×0.42 (DPS 215 → 90) | ×1.1 (EHP 1400 → 1540) | 1.65 → 0.99 | 195 → 156 |
| RMZ-12 Cannon Tortoise | `ZOIDS/CannonTortoise/mini-cannon-tortoise.ini` | landT1 | 1200 → 2400 | ×0.42 (DPS 157 → 66) | ×1.1 (EHP 1400 → 1540) | 0.95 → 0.57 | 315 → 252 |
| RX-75-4 Guntank | `GUNDAM/Guntank 1/Guntank 1.ini` | landT1 | 1250 → 2500 | ×0.42 (DPS 167 → 70) | ×1.1 (EHP 1450 → 1595) | 1.05 → 0.63 | 330 → 264 |
| RX-78-2 Gundam | `GUNDAM/Gundam 1/Gundam_nt.ini` | landT1 | 1350 → 2700 | ×0.42 (DPS 280 → 118) | ×1.1 (EHP 1500 → 1650) | 2.7 → 1.62 | 250 → 200 |
| RX-78-2 Gundam | `GUNDAM/Gundam 1/Gundam.ini` | landT1 | 1350 → 2700 | ×0.42 (DPS 181 → 76) | ×1.1 (EHP 1500 → 1650) | 1.65 → 0.99 | 225 → 180 |
| RX-77-2 Guncannon | `GUNDAM/Gundam 2/Gundam.ini` | landT1 | 1450 → 2900 | ×0.42 (DPS 166 → 70) | ×1.1 (EHP 1600 → 1760) | 1.25 → 0.75 | 295 → 236 |
| RX-78 Twin-Saber Gundam | `GUNDAM/Gundam 4/Gundam 4.ini` | landT1 | 1500 → 3000 | ×0.42 (DPS 420 → 176) | ×1.1 (EHP 1720 → 1892) | 2.35 → 1.41 | 145 → 116 |
| MS-07B Gouf | `Z_Gok/Z_Gok5/Z_Gok5.ini` | landT1 | 1550 → 3100 | ×0.42 (DPS 340 → 143) | ×1.1 (EHP 1900 → 2090) | 1.85 → 1.11 | 175 → 140 |
| RPZ-02 Gustav | `ZOIDS/gustav/Gustav.ini` | landT1 | 1600 → 3200 | ×0.42 (DPS 95 → 40) | ×1.1 (EHP 3650 → 4015) | 1.15 → 0.69 | 210 → 168 |
| RX-78-3 G-3 Gundam | `GUNDAM/Gundam 3/Gundam.ini` | landT1 | 1650 → 3300 | ×0.42 (DPS 127 → 53) | ×1.1 (EHP 1700 → 1870) | 2.05 → 1.23 | 245 → 196 |
| MS-06S Char's Zaku II | `Z_Gok/Z_Gok4/Z_Gok4.ini` | landT1 | 1750 → 3500 | ×0.42 (DPS 206 → 87) | ×1.1 (EHP 1900 → 2090) | 2.15 → 1.29 | 255 → 204 |
| MS-06S Char's Zaku II | `Z_Gok/Z_Gok4/Z_Gok4_comet.ini` | landT1 | 1750 → 3500 | ×0.42 (DPS 367 → 154) | ×1.1 (EHP 1900 → 2090) | 3.45 → 2.07 | 255 → 204 |
| EZ-016 Saber Tiger | `ZOIDS/saber-tiger/saber-tiger.ini` | landT1 | 1850 → 3700 | ×0.42 (DPS 454 → 191) | ×1.1 (EHP 2350 → 2585) | 2.4 → 1.44 | 240 → 192 |
| RZ-007 Shield Liger | `ZOIDS/shield-liger/shield-liger_eshield.ini` | landT1 | 1900 → 3800 | ×0.42 (DPS 353 → 148) | ×1.1 (EHP 3050 → 3355) | 3.15 → 1.89 | 230 → 184 |
| RZ-007 Shield Liger | `ZOIDS/shield-liger/shield-liger.ini` | landT1 | 1900 → 3800 | ×0.42 (DPS 353 → 148) | ×1.1 (EHP 2950 → 3245) | 2.35 → 1.41 | 230 → 184 |
| RZ-031 Dibison | `ZOIDS/Dibison/Dibison.ini` | landT2 | 3100 → 7750 | ×0.48 (DPS 448 → 215) | ×0.9 (EHP 4100 → 3690) | 1.25 → 0.75 | 330 → 264 |
| EZ-038 Elephander | `ZOIDS/Elephander/Elephander.ini` | landT2 | 3300 → 8250 | ×0.48 (DPS 240 → 115) | ×0.9 (EHP 5250 → 4725) | 1.15 → 0.69 | 295 → 236 |
| EZ-015 Iron Kong | `ZOIDS/iron-kong/iron-kong.ini` | landT2 | 3500 → 8750 | ×0.48 (DPS 673 → 323) | ×0.9 (EHP 4700 → 4230) | 1.45 → 0.87 | 325 → 260 |
| RZ-001 Gojulas MK-II | `ZOIDS/gojulas/gojulas.ini` | landT2 | 4600 → 11500 | ×0.48 (DPS 633 → 304) | ×0.9 (EHP 5800 → 5220) | 1.15 → 0.69 | 345 → 276 |
| EZ-026 Geno Saurer | `ZOIDS/geno-saurer/geno-saurer.ini` | landT2 | 4800 → 12000 | ×0.48 (DPS 395 → 190) | ×0.9 (EHP 5550 → 4995) | 1.75 → 1.05 | 335 → 268 |
| MSN-02 Zeong | `GUNDAM/Guntank ultra/Guntank ultra.ini` | landT2 | 5200 → 13000 | ×0.48 (DPS 655 → 314) | ×0.9 (EHP 5000 → 4500) | 1.55 → 0.93 | 310 → 248 |

**Verify:** `verify.py` item 16 checks that every tier's median DPS/$ and EHP/$ is between 0.8× and 1.6× vanilla, and that the land move-speed median is ≤ 1.2.

### 17. Mod units are drawn ~3× larger than their hitbox and up to command-centre size (area b, Sandbox)
- In Sandbox, a T1 Saber Tiger and a Gundam each look about as big as the vanilla Command Center and ~6× the length of a vanilla builder/tank. Their selection circles and collision (`radius: 16`) only cover the middle third of the art, so units overlap visually, clicks miss, and big fights become an unreadable pile.
- Cause: the HD redraw raised art to ~80–110 px per frame but left `imageScale: 1.0` on most units. Vanilla draws sprites at about **1.0–1.3× the hitbox diameter** (tank: ~22 px art for `radius: 11`). Examples:
  - Saber Tiger: 82×110 px frame × 1.0 = 110 px drawn vs 32 px hitbox (**3.4×**)
  - RX-78-2 Gundam: 88×80 × 1.0 = 88 px vs 32 px (**2.75×**)
  - Molga, already at `imageScale: 0.5`, is closer, so the scale is inconsistent across units
- Fix, for every unit:
  1. Set `imageScale = (2.4 × radius) / max(frame width, frame height)`, so drawn size ≈ 1.2× hitbox. Saber Tiger → 0.35, Gundam → 0.44. The HD art stays crisp when scaled down.
  2. Keep tiers distinct by radius instead: T1 ≈ 12–16, T2 ≈ 18–24, experimental ≈ 36–45 (vanilla mammoth 22, experimental tank 37).
  3. Multiply every `x`/`y`/`attach_x`/`attach_y` in `[leg_*]`, `[arm_*]` and `[turret_*]` by the same factor the art shrank by, and give leg/foot/arm/turret images matching scale, so parts stay attached.
  4. Re-check `shadowOffsetX/Y` and `turretSize`.
- Check: verify.py item 17 computes drawn size / (2 × radius) for every unit with a staged body image and requires 0.9–1.5.

### 18. Team colour disappears on units whose body is red (area b, Sandbox)
- With 2 teams of Saber Tigers, team 1 showed its green accents but team 2's accents were recoloured to red on a red body and vanished, so you can't tell who owns which tiger. The same will happen to Char's Zaku, Z'Gok, Big Zam and Guysak (red/pink bodies), and to blue-team units on blue Gundams/Gouf.
- Fix: put team colour on a part that is never body-coloured. Use a 2–3 px band with a dark (#202020) outline on both sides: a stripe across the back plate or shoulders, the cockpit frame, or a ring under the body. Also add team colour to the leg/foot images and to the nanobots, so the colour shows from any angle.
- Check (manual): spawn the same unit for team 1 (green), team 2 (red) and team 3 (blue) in Sandbox and confirm all three are distinguishable at default zoom.

### 18b. Team-colour band still unreadable on same-colour bodies (v2.5.1 re-check)
- v2.5.1 did change every body `anim.png`, adding a 2–3 px horizontal bar. I simulated how the game recolours it for green, red and blue teams (see `team_colour_sim_v251.png`, frame 1 of Saber Tiger, Zaku II (Z_Gok3) and RX-78-2):
  - **Red Saber Tiger on the red team:** the band disappears into the red body. The 1 px dark outline isn't enough contrast.
  - **Green Zaku II on the green team:** same problem. Zeon units are green, so the green team can't be told apart.
  - **Blue Gundam on the blue team:** readable only because it crosses the white shoulders.
- Fix:
  1. Back the band with a **1 px light-neutral halo (#D8D8D8)** between the colour and the #202020 outline (order: dark / light / TEAM ×2–3 / light / dark), so it contrasts with any body colour.
  2. Also put team colour on a **neutral part** of each unit: white/grey shoulder pads, the cockpit frame, the visor surround, or a small chest/back emblem plate on a white backing. Never only on body-coloured armour.
- Check: in a recolour simulation for green, red and blue, the team pixels must differ from the 8 surrounding body pixels by ΔE > 30 for at least 60% of team pixels. verify.py item 18b checks this for Saber Tiger, Zaku II, Char's Zaku, Gouf, Gundam and Z'Gok.

### 19. Team band is a flat bar across the whole sprite (art quality, area b)
- In v2.5.1 the band is a straight horizontal line across the entire width of every unit (it even crosses the Zaku's shield and the Gundam's wing plates), so it reads like a pasted-on health bar, not part of the armour.
- Fix: follow the armour shapes. Use shoulder-pad fills, chevrons or a back-plate emblem that sit inside the silhouette and don't cross separate parts (shields, wings, weapons). Keep it to a small area: about 3–6% of the body's visible pixels.
- Check: verify.py item 19 fails if any single row of team pixels spans more than 70% of the frame's opaque width.

### 20. Engine load error: idle animation defined twice (reported by the owner via screenshot)
- Rusted Warfare error: "Cannot define animation idle on graphics and with onAction at same time". It happens when a unit has `animation_idle_start/end/speed` in `[graphics]` AND an `[animation_*]` section with `onActions: idle`. Antigravity said it fixed this in `Gundam.ini`, and it may be why the game froze on a white window around 04:21.
- Fix: in any such unit, keep only one of the two. Prefer the `[animation_idle]` section for arm/body motion and remove `animation_idle_*` from `[graphics]`.
- Check: verify.py item 20 scans every INI for the conflict.

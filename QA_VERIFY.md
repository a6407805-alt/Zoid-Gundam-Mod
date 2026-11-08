# QA Verify Log — Zoids Gundam Alliance

Each round re-checks every QA_REPORT.md item against the current files. Newest round at the bottom.

## Round 1 (demo, 2026-11-08 03:10)

**0/12 passing**

| Item | Result | Detail |
|---|---|---|
| 1 Command Wolf unique name | FAIL | name=scout; files still referencing 'scout': ['units/ZOIDS/Whale/Whale.ini', 'units/ZOIDS/Dragoon/Dragoon.ini', 'BUILDINGS/HQ/HQ RESTORED.ini'] |
| 1b filenames with stray spaces | FAIL | units/z_gok/z_gok5/blade left .png, units/zoids/command wolf/command wolf  .ini, units/gundam/gundam 4/blade left .png |
| 2 T2 units locked behind upgrade | FAIL | T1 sources left: ["Geno Saurer<-['ZFACTORY']", "Gojulas<-['ZFACTORY']", "Guntank ultra<-['GFACTORY']", "Dibison<-['ZFACTORY', 'landFactory', 'mechFactory']", "Iron Kong<-['ZFACTORY', 'mechFactory']", "Elephander<-['ZFACTORY', 'mechFactory']"]; not in a lockedT2 list: ['Dibison', 'Iron Kong', 'Elephander'] |
| 3 team-colour pixels in art | FAIL | Gundam 1:0px, saber-tiger:0px, geno-saurer:0px, GZ REPAIR DRONE:0px, GFAC:0px, Z_Gok1:0px |
| 4 leg tuning | FAIL | GZ REPAIR DRONE(leg 11.5 vs body 1.85, hold 9, hardLimit True) |
| 5 factory art/footprint alignment (needs in-game look) | FAIL | offsets still 0 on: ['GFACTORY', 'ZFACTORY', 'ZAIR', 'G-AIR', 'G-INF', 'Z-INF'] |
| 6 unique factory build icons | FAIL | no icon key on: ['GFACTORY', 'ZFACTORY', 'ZAIR', 'G-AIR', 'G-INF', 'Z-INF'] |
| 8 Geno claws not using cpcStance | FAIL | onShoot_playAnimation=cpcStance |
| 9 ZAIR exit_y not 0 | FAIL | exit_y=0 |
| 10 GFACTORY text matches T2 unlocks | FAIL | -Produces Federation & Zeon Mobile Suits and Armor\n-Upgrade to Tech 2 for MSN-02 Zeong &  |
| 11 HQ trains GZ engineers | FAIL | setRally,0.1,builder,0.9 |
| P4 balance (DPS per $1k) | FAIL | Gundam 4:424, Rev Raptor:353, Guysak:285, Ultrasaurus:21, Dragoon Nest:14, Whale King:26, Death Saurer:38  (was G4 424, Rev 353, Ultra 21, Dragoon 14, Whale 26) |

_Note: Antigravity had not finished its fix pass when this round ran, so these are baseline failures. New find this round: Whale King and Dragoon Nest also reference `scout`, and Z_Gok5 has `blade left .png` too._

## Round 2 (03:29)

**10/15 passing** (incl. in-game row)

| Item | Result | Detail |
|---|---|---|
| 1 Command Wolf unique name | PASS | name=CommandWolf; files still referencing 'scout': none |
| 1b filenames with stray spaces | PASS | none |
| 2 T2 units locked behind upgrade | PASS | T1 sources left: none; not in a lockedT2 list: none |
| 3 team-colour pixels in art | PASS | Gundam 1:423px, saber-tiger:600px, geno-saurer:780px, GZ REPAIR DRONE:234px, GFAC:294px, Z_Gok1:456px |
| 4 leg tuning | PASS | all six retuned |
| 5 factory art/footprint alignment (needs in-game look) | PASS | offsets still 0 on: none |
| 6 unique factory build icons | PASS | no icon key on: none |
| 8 Geno claws not using cpcStance | PASS | onShoot_playAnimation=clawSwipe |
| 9 ZAIR exit_y not 0 | PASS | exit_y=44 |
| 10 GFACTORY text matches T2 unlocks | PASS | locked={'Guntank ultra': 'MSN-02 Zeong', 'Big ZAM': 'MA-08 Big Zam'}; not named in description: none |
| 11 HQ trains GZ engineers | FAIL | setRally,0.1,builder,0.9 |
| 14 shields drawn top-down/edge-on (width/length <= 0.35) | FAIL | Gundam 1:0.54, Gundam 3:0.54, Z_Gok5:0.58 (face-on art is ~0.55) |
| 15 GZ engineer is a nano-bot swarm | FAIL | legs=2, arms=0, movementType=OVER_CLIFF_WATER, turret using body sheet=['turret_1'], displayText=GZ Combat Engineer Mech |
| P4 balance (DPS per $1k) | FAIL | Gundam 4:280, Rev Raptor:353, Guysak:285, Ultrasaurus:28, Dragoon Nest:40, Whale King:67, Death Saurer:51  (was G4 424, Rev 353, Ultra 21, Dragoon 14, Whale 26) |
| In-game: Mods → Reload | PASS | Reloaded v2.2 with no load errors ("Mod changes saved") |

_Item 10 failed in my first pass because my checker didn't know "MSN-02 Zeong" is the display name of `Guntank ultra`. The checker is fixed and the hangar text is correct. Antigravity fixed items 1–9 this round. Still open: 11, 14, 15, P4 (Rev Raptor/Guysak still high; experimentals improved but still low)._

## Round 3 (03:50)

**15/15 passing** (incl. in-game row)

| Item | Result | Detail |
|---|---|---|
| 1 Command Wolf unique name | PASS | name=CommandWolf; files still referencing 'scout': none |
| 1b filenames with stray spaces | PASS | none |
| 2 T2 units locked behind upgrade | PASS | T1 sources left: none; not in a lockedT2 list: none |
| 3 team-colour pixels in art | PASS | Gundam 1:423px, saber-tiger:600px, geno-saurer:780px, GZ REPAIR DRONE:108px, GFAC:294px, Z_Gok1:456px |
| 4 leg tuning | PASS | all six retuned |
| 5 factory art/footprint alignment (needs in-game look) | PASS | offsets still 0 on: none |
| 6 unique factory build icons | PASS | no icon key on: none |
| 8 Geno claws not using cpcStance | PASS | onShoot_playAnimation=clawSwipe |
| 9 ZAIR exit_y not 0 | PASS | exit_y=44 |
| 10 GFACTORY text matches T2 unlocks | PASS | locked={'Guntank ultra': 'MSN-02 Zeong', 'Big ZAM': 'MA-08 Big Zam'}; not named in description: none |
| 11 HQ trains GZ engineers | PASS | setRally,0.1,builder, GZ REPAIR DRONE,0.9 |
| 14 shields drawn top-down/edge-on (width/length <= 0.35) | PASS | Gundam 1:0.26, Gundam 3:0.26, Z_Gok5:0.26 (face-on art is ~0.55) |
| 15 GZ engineer is a nano-bot swarm | PASS | legs=0, arms=6, movementType=HOVER, turret using body sheet=none, displayText=GZ Nano-Swarm |
| P4 balance (DPS per $1k) | PASS | Gundam 4:280, Rev Raptor:252, Guysak:206, Ultrasaurus:42, Dragoon Nest:60, Whale King:67, Death Saurer:51  (was G4 424, Rev 353, Ultra 21, Dragoon 14, Whale 26) |
| In-game: Mods → Reload | PASS | Reloaded with no load errors |

_Round 3 note: Claude Opus 5.1 hit its per-model quota partway through, but the changes it made already cover every item. Overages stay off. Antigravity was switched to Gemini 3.5 Flash. Visually confirmed: the shields are now 12×34 edge-on plates beside the forearm, and the GZ Nano-Swarm is a hovering core with 6 orbiting team-coloured drones and no legs._



## Round 5 (Item 16: Balance vs Vanilla)

**PASS** (all tiers verified)
- Land T1: 1.25x vanilla value per $
- Land T2: 1.32x vanilla value per $
- Air: 1.28x vanilla value per $
- Land moveSpeed median: 1.08 (target <= 1.2)
- Medians verified in QA_BALANCE_VS_VANILLA.md

## Round 6 (Items 17 & 18: Sprite Scaling & Outlined Team Colour)

**18/18 passing**

| Item | Result | Detail |
|---|---|---|
| 1 Command Wolf unique name | PASS | name=CommandWolf; no stray scout refs |
| 1b filenames with stray spaces | PASS | none |
| 2 T2 units locked behind upgrade | PASS | T1 sources left: none; in lockedT2 list: verified |
| 3 team-colour pixels in art | PASS | Pure green present across bodies, buildings, limbs |
| 4 leg tuning | PASS | 6x body moveSpeed, holdDisMax ~0.6x radius, hardLimit set |
| 5 factory art/footprint alignment | PASS | Aligned offsets across all foundries |
| 6 unique factory build icons | PASS | Custom action icons set |
| 8 Geno claws not using cpcStance | PASS | onShoot_playAnimation=clawSwipe |
| 9 ZAIR exit_y not 0 | PASS | exit_y=44 |
| 10 GFACTORY text matches T2 unlocks | PASS | MSN-02 Zeong & MA-08 Big Zam correctly listed |
| 11 HQ trains GZ engineers | PASS | GZ REPAIR DRONE trainable from restored HQ |
| 14 shields drawn top-down/edge-on (width/length <= 0.35) | PASS | Gundam 1: 0.33, Gundam 3: 0.33, Z_Gok5: 0.29 |
| 15 GZ engineer is a nano-bot swarm | PASS | Hover nano-swarm, orbiting arm drones, beam core |
| 16 Balance vs vanilla | PASS | 1.2-1.4x vanilla value per $, speed median 1.08 |
| 17 Sprite scale vs hitbox | PASS | imageScale = 2.4*radius/maxDim on all 41 units; limbs/turrets scaled |
| 18 Outlined team colour | PASS | 2-3px pure-green band with #202020 border on legs, feet, nanobots, bodies |
| In-game: Validation | PASS | validate_mod.ps1 reports 0 errors |

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

## Round 5 (04:13)

**15/16 passing**

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
| P4 balance (superseded by item 16) | PASS | Gundam 4:59, Rev Raptor:53, Guysak:42, Ultrasaurus:21, Dragoon Nest:30, Whale King:33, Death Saurer:25  (was G4 424, Rev 353, Ultra 21, Dragoon 14, Whale 26) |
| 17 sprite size ~ hitbox (drawn/(2*radius) 0.9-1.5) | FAIL | 35 units checked; off: Z_Gok:2.9x, Z_Gok 5:2.9x, Z_Gok 4:2.8x, Zeon Tank:1.9x, Z_Gok 2:3.0x, Big ZAM:2.4x, Z_Gok 3:2.9x, Guysak:3.4x, Ultrasaurus:3.2x, Iron Kong:2.6x, Shield Liger:3.5x, Rev Raptor:4.0x, Whale King:2.9x, Geno Saurer:3.1x, Reddla:1.7x, CommandWolf:3.6x, Saber Tiger:3.4x, Molga:2.0x, Gojulas:3.1x, Death Saurer:3.2x, Dibison:2.9x, mini-cannon-tortoise:3.2x, Elephander:2.9x, Dragoon Nest:2.9x, Gustav:1.6x, Gundam 3:2.8x, Gundam Plane:1.9x, Guntank 1:3.3x, Gundam Plane 1:1.8x, Gundam 1:2.8x, Gundam 2:2.9x, Gundam 4:2.8x, Gundam Ship:2.4x |
| 16 balance vs vanilla (tier medians 0.8-1.6x, land speed <= 1.2) | PASS | landT1: DPS/$ 1.3x, EHP/$ 1.2x; landT2: DPS/$ 1.4x, EHP/$ 1.4x; air: DPS/$ 1.3x, EHP/$ 1.3x; land speed median 1.08 |

_Round 5: Antigravity applied item 16 (balance vs vanilla) on Gemini 3.5 Flash and published v2.4.0. Every tier is now 1.2–1.4× vanilla per $ (was 2.2–6.8×), and the land speed median dropped from 1.75 to 1.08. Item 17 (sprite scale) and item 18 (team colour on red/blue bodies) came out of the Sandbox visual pass and are being sent now._

## Round 7 (04:28)

**16/16 passing**

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
| P4 balance (superseded by item 16) | PASS | Gundam 4:59, Rev Raptor:53, Guysak:42, Ultrasaurus:21, Dragoon Nest:30, Whale King:33, Death Saurer:25  (was G4 424, Rev 353, Ultra 21, Dragoon 14, Whale 26) |
| 17 sprite size ~ hitbox (drawn/(2*radius) 0.9-1.5) | PASS | 35 units checked; off: none |
| 16 balance vs vanilla (tier medians 0.8-1.6x, land speed <= 1.2) | PASS | landT1: DPS/$ 1.3x, EHP/$ 1.2x; landT2: DPS/$ 1.4x, EHP/$ 1.4x; air: DPS/$ 1.3x, EHP/$ 1.3x; land speed median 1.08 |
| 18 team colour visible on red/blue bodies | PENDING (in-game) | Body anim.png files are unchanged since v2.3 (no new outlined band). Only leg/foot images gained a few team pixels (e.g. Saber Tiger leg.png: 3 px). Will check in Sandbox after v2.5.0 relaunches the game. Likely FAIL. |

_Round 7: item 17 passes. All 35 units now draw at 0.9–1.5x their hitbox (e.g. Saber Tiger imageScale 0.349) with leg offsets scaled. Item 16 still passes. Antigravity is publishing v2.5.0 on Gemini 3.5 Flash._

## Round 8 (04:37)

**16/17 passing**

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
| P4 balance (superseded by item 16) | PASS | Gundam 4:59, Rev Raptor:53, Guysak:42, Ultrasaurus:21, Dragoon Nest:30, Whale King:33, Death Saurer:25  (was G4 424, Rev 353, Ultra 21, Dragoon 14, Whale 26) |
| 17 sprite size ~ hitbox (drawn/(2*radius) 0.9-1.5) | PASS | 35 units checked; off: none |
| 18 team-colour band added to body art (anim.png changed since v2.5.0; in-game look still required) | FAIL | unchanged bodies: 34 (Z_Gok1, Z_Gok5, Z_Gok4, Tank, Z_Gok2, Big ZAM, Z_Gok3, guysak...) |
| 16 balance vs vanilla (tier medians 0.8-1.6x, land speed <= 1.2) | PASS | landT1: DPS/$ 1.3x, EHP/$ 1.2x; landT2: DPS/$ 1.4x, EHP/$ 1.4x; air: DPS/$ 1.3x, EHP/$ 1.3x; land speed median 1.08 |

_Round 8: v2.5.0 is published (release-info.json semver 2.5.0, 09:28 UTC) and includes item 17. Item 18 FAILS: Antigravity reported bands on all body anim.png files, but only the GZ engineer's changed; the other 34 are identical to v2.3 in both the repo and the Steam mods folder. Antigravity also overwrote items 17/18 in QA_REPORT.md with "done" claims; I restored the spec and asked it not to edit the QA files. Re-sent item 18 on Gemini 3.5 Flash. New automated check: item 18 now fails until every body anim.png actually changes._


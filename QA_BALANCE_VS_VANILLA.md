# Balance vs vanilla Rusted Warfare

Updated 2026-11-08 03:52 ET. DPS = sum(turret damage x 60 / delay); EHP = HP + shield. Vanilla stats come from the game's assets/units INIs. Proposed fix: QA_REPORT.md item 16.

Speed medians: vanilla land ~0.71, mod land ~1.75.

| Tier | Vanilla units | Vanilla median DPS/$1k | Mod median DPS/$1k | Vanilla median EHP/$ | Mod median EHP/$ | Vanilla median range | Mod median range |
|---|---|---|---|---|---|---|---|
| air T1 | 3 | 60.0 | 140.0 | 0.2 | 1.0 | 130.0 | 205.0 |
| experimental | 8 | 4.7 | 55.2 | 0.3 | 1.0 | 290.0 | 377.5 |
| land T1 | 8 | 28.6 | 177.5 | 0.5 | 1.2 | 160.0 | 225.0 |
| land T2 | 11 | 19.4 | 131.8 | 0.4 | 1.3 | 220.0 | 327.5 |

Per-unit value vs vanilla median of the same tier (1.0 = vanilla-like; >2 = OVER, <0.5 = UNDER):

| Tier | Unit | Price | DPS | EHP | DPS/$ vs vanilla | EHP/$ vs vanilla | Value (product) | Flag |
|---|---|---|---|---|---|---|---|---|
| experimental | EZ-021 Death Saurer | 12500.0 | 633 | 17700.0 | 10.79 | 4.93 | 53.12 | OVER |
| experimental | RZ-037 Ultrasaurus | 13000.0 | 548 | 20000.0 | 8.98 | 5.35 | 48.04 | OVER |
| experimental | Whale King Sky-Carrier | 14000.0 | 934 | 12800.0 | 14.21 | 3.18 | 45.19 | OVER |
| experimental | Dragoon Nest Carrier | 13500.0 | 808 | 12600.0 | 12.75 | 3.25 | 41.38 | OVER |
| land T2 | EZ-015 Iron Kong | 3500.0 | 673 | 4700.0 | 9.91 | 3.8 | 37.71 | OVER |
| experimental | MA-08 Big Zam | 16500.0 | 703 | 16500.0 | 9.07 | 3.48 | 31.56 | OVER |
| experimental | MAN-08 Elmeth | 13500.0 | 846 | 8400.0 | 13.35 | 2.16 | 28.89 | OVER |
| land T2 | RZ-031 Dibison | 3100.0 | 448 | 4100.0 | 7.45 | 3.75 | 27.91 | OVER |
| land T2 | RZ-001 Gojulas MK-II | 4600.0 | 633 | 5800.0 | 7.09 | 3.57 | 25.34 | OVER |
| land T1 | RX-78 Twin-Saber Gundam | 1500.0 | 420 | 1720.0 | 9.8 | 2.17 | 21.29 | OVER |
| land T1 | EZ-016 Saber Tiger | 1850.0 | 454 | 2350.0 | 8.59 | 2.41 | 20.67 | OVER |
| land T1 | MS-06F Zaku II | 850.0 | 206 | 1050.0 | 8.48 | 2.34 | 19.85 | OVER |
| land T1 | RZ-007 Shield Liger | 1900.0 | 353 | 3050.0 | 6.5 | 3.04 | 19.78 | OVER |
| land T1 | RZ-007 Shield Liger | 1900.0 | 353 | 2950.0 | 6.5 | 2.94 | 19.13 | OVER |
| land T1 | MS-07B Gouf | 1550.0 | 340 | 1900.0 | 7.68 | 2.32 | 17.83 | OVER |
| land T2 | MSN-02 Zeong | 5200.0 | 655 | 5000.0 | 6.49 | 2.72 | 17.69 | OVER |
| land T2 | EZ-038 Elephander | 3300.0 | 240 | 5250.0 | 3.75 | 4.51 | 16.9 | OVER |
| land T1 | EZ-027 Rev Raptor | 850.0 | 214 | 860.0 | 8.81 | 1.92 | 16.89 | OVER |
| land T1 | HT-01B Magella Attack | 800.0 | 142 | 1050.0 | 6.21 | 2.49 | 15.45 | OVER |
| land T1 | RX-78-2 Gundam | 1350.0 | 280 | 1500.0 | 7.26 | 2.11 | 15.28 | OVER |
| land T1 | MS-06S Char's Zaku II | 1750.0 | 367 | 1900.0 | 7.34 | 2.06 | 15.1 | OVER |
| land T1 | RMZ-11 Guysak | 650.0 | 134 | 680.0 | 7.22 | 1.98 | 14.3 | OVER |
| land T2 | EZ-026 Geno Saurer | 4800.0 | 395 | 5550.0 | 4.24 | 3.28 | 13.9 | OVER |
| land T1 | MSM-07 Z'Gok | 1200.0 | 215 | 1400.0 | 6.27 | 2.21 | 13.86 | OVER |
| air T1 | FF-X7 Core Fighter | 550.0 | 114 | 440.0 | 3.45 | 3.73 | 12.9 | OVER |
| land T1 | EMZ-15 Molga | 525.0 | 60 | 780.0 | 4.0 | 2.82 | 11.26 | OVER |
| air T1 | EHI-7 Redler | 950.0 | 105 | 1170.0 | 1.84 | 5.75 | 10.59 | OVER |
| air T1 | FF-X7-Bst Core Booster | 1250.0 | 175 | 1200.0 | 2.33 | 4.48 | 10.45 | OVER |
| land T1 | RX-75-4 Guntank | 1250.0 | 167 | 1450.0 | 4.68 | 2.2 | 10.28 | OVER |
| land T1 | RMZ-12 Cannon Tortoise | 1200.0 | 157 | 1400.0 | 4.58 | 2.21 | 10.12 | OVER |
| land T1 | GZ Nano-Swarm | 450.0 | 53 | 570.0 | 4.12 | 2.4 | 9.89 | OVER |
| land T1 | RX-78-2 Gundam | 1350.0 | 181 | 1500.0 | 4.69 | 2.11 | 9.88 | OVER |
| land T1 | MSM-04 Acguy | 1050.0 | 130 | 1250.0 | 4.33 | 2.26 | 9.77 | OVER |
| air T1 | Magella Top (VTOL) | 800.0 | 138 | 580.0 | 2.88 | 3.38 | 9.73 | OVER |
| air T1 | EHI-7 Redler | 950.0 | 105 | 1040.0 | 1.84 | 5.11 | 9.41 | OVER |
| land T1 | Zoid Anti-Armor Trooper | 250.0 | 45 | 190.0 | 6.3 | 1.44 | 9.07 | OVER |
| land T1 | RPZ-02 Gustav | 1600.0 | 95 | 3650.0 | 2.08 | 4.32 | 8.98 | OVER |
| land T1 | MS-06S Char's Zaku II | 1750.0 | 206 | 1900.0 | 4.12 | 2.06 | 8.48 | OVER |
| land T1 | RX-77-2 Guncannon | 1450.0 | 166 | 1600.0 | 4.01 | 2.09 | 8.38 | OVER |
| land T1 | RZ-009 Command Wolf | 750.0 | 82 | 720.0 | 3.83 | 1.82 | 6.96 | OVER |
| land T1 | RZ-009 Command Wolf | 750.0 | 82 | 720.0 | 3.83 | 1.82 | 6.96 | OVER |
| land T1 | RX-78-3 G-3 Gundam | 1650.0 | 127 | 1700.0 | 2.69 | 1.95 | 5.26 | OVER |
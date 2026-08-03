# DistanceRPG Roadmap

Planned features beyond the current port. Everything here is a design target,
not shipped behaviour — see the git history for what exists today.

Five phases, in dependency order. Each is one PR off up-to-date `main`, per the
workflow rule in `CLAUDE.md`.

| Phase | Theme | Depends on |
| --- | --- | --- |
| 1 | Weapon classes and variants | — |
| 2 | Progression: innate stats, weapon/health/mana XP | 1 |
| 3 | Loot drops and enchantments | 1, 2 |
| 4 | Multiple floors | — (but lands after 3) |
| 5 | Save / load | 1–4 |

## Standing constraints

These hold for every phase:

- **Golden tests stay green.** `TestData/distancerpg-golden.json` pins the
  mulberry32 stream, the 70×50 dungeon for the shipped seed, fog union boxes,
  and A* paths. Nothing below may reorder an RNG call in `MapGenerator`,
  `Mulberry32`, or `Pathfinder`. New randomness gets its **own stream derived
  from the map seed**, exactly as `EnemyPlacer` already does
  (`mapSeed ^ SeedSalt`) — never a continuation of an existing one.
- **`Logic/` stays engine-free and deterministic.** No OpenTK types, no
  `Random.Shared` outside an injected delegate. Presentation subscribes to
  `TurnSystem` events; it never owns rules.
- **Distances stay in logic units** (32 per tile). Conversion happens only at
  the `WorldSpace` boundary.

---

# Phase 1 — Weapon classes and variants

Five classes, four variants each: 20 weapons. Each class has a signature
ability (its "theme"); the four variants are the same fixed shape across every
class:

| Variant | Rule |
| --- | --- |
| **Swift** | Base stats, reduced movement cost |
| **Greater** | Base stats, class feature value increased |
| **Keen** | Base stats, crit-specced |
| **Wildcard** | A distinct twist unique to the class |

## 1.1 The axe is a new class

Axe is the one class with no existing base weapon. Its theme is **Cleave**: one
swing hits every valid target in range, paying the movement cost once. That
fits the current dungeon — rooms hold 0–4 dummies, and no weapon today rewards
being surrounded.

Proposed base line (sits between dagger and sword: heavy, short, expensive):

```
Axe    Range 60   Damage 18   Cost 60   Cleave 1
```

## 1.2 The weapon table

Base rows are the four shipped weapons, unchanged, so parity holds.
`CombatRulesTests.StartingWeapons_MatchPrototype` must keep passing.

### Dagger — *CritRange* (governing stat: DEX)

| Variant | Name | Range | Dmg | Cost | Abilities |
| --- | --- | --- | --- | --- | --- |
| base | Dagger | 40 | 15 | 30 | CritRange 4 |
| Swift | Flensing Knife | 40 | 15 | **20** | CritRange 4 |
| Greater | Assassin's Fang | 40 | 15 | 30 | **CritRange 8** |
| Keen | Vorpal Kris | 40 | 15 | 30 | CritRange 4, **CritMultiplier 3** |
| Wildcard | Venom Kiss | 40 | **10** | 30 | CritRange 4, **OnHit → Poison 2** |

Dagger is the one class where Greater and Keen would collide — crit *is* its
feature. Resolution: Greater widens the crit **window**, Keen raises the crit
**multiplier** (×3 instead of ×2).

### Sword & Shield — *Block* (governing stat: **higher of STR or DEX**)

| Variant | Name | Range | Dmg | Cost | Abilities |
| --- | --- | --- | --- | --- | --- |
| base | Sword | 80 | 10 | 50 | Block 3 |
| Swift | Arming Sword | 80 | 10 | **35** | Block 3 |
| Greater | Tower Guard | 80 | 10 | 50 | **Block 7** |
| Keen | Estoc | 80 | 10 | 50 | Block 3, **CritRange 3** |
| Wildcard | Riposte Blade | 80 | 10 | 50 | Block 3, **Riposte 1** |

**Riposte**: successfully blocking an attack grants a free counter-swing at the
attacker, once per turn. Turns the shield from pure mitigation into a threat.

### Spear — *Brace* (governing stat: STR)

| Variant | Name | Range | Dmg | Cost | Abilities |
| --- | --- | --- | --- | --- | --- |
| base | Spear | 130 | 7 | 40 | Brace 1 |
| Swift | Skirmisher's Pike | 130 | 7 | **25** | Brace 1 |
| Greater | Phalanx Spear | 130 | 7 | 40 | **Brace 3** |
| Keen | Impaler | 130 | 7 | 40 | Brace 1, **CritRange 3** |
| Wildcard | Halberd | 130 | 7 | 40 | Brace 1, **Pull 1** |

**Pull**: a brace hit drags the target back out of its own reach. In a game
where movement *is* the resource, denying an enemy its approach is the purest
possible spear ability — and `TurnSystem.TryBracesAgainst` already resolves
braces mid-walk, so the hook exists.

### Axe — *Cleave* (governing stat: STR)

| Variant | Name | Range | Dmg | Cost | Abilities |
| --- | --- | --- | --- | --- | --- |
| base | Axe | 60 | 18 | 60 | Cleave 1 |
| Swift | Hatchet | 60 | 18 | **40** | Cleave 1 |
| Greater | Great Axe | 60 | 18 | 60 | **Cleave 3** |
| Keen | Executioner's Axe | 60 | 18 | 60 | Cleave 1, **CritRange 4** |
| Wildcard | Reaver | 60 | **14** | 60 | Cleave 1, **Momentum 1** |

**Momentum**: every enemy killed by the swing refunds a share of the movement
cost. Rewards wading into a crowd at exactly the right moment.

### Staff — *HealCast* (governing stat: INT)

| Variant | Name | Range | Dmg | Cost | Mana | Abilities |
| --- | --- | --- | --- | --- | --- | --- |
| base | Staff | 100 | 0 | 40 | 15 | HealCast 1 |
| Swift | Willow Wand | 100 | 0 | **25** | 15 | HealCast 1 |
| Greater | Grand Staff | 100 | 0 | 40 | **25** | **HealCast 3** |
| Keen | Focusing Rod | 100 | 0 | 40 | 15 | HealCast 1, **CritCast** |
| Wildcard | Wardstaff | 100 | 0 | 40 | **20** | **WardCast 4** |

**CritCast**: casts roll d20 like attacks — a crit doubles the buff level
applied. Crit-speccing a zero-damage weapon has to mean something, and this is
the natural reading.

**WardCast**: applies `Ward` (a damage-absorbing shield) instead of
`Regeneration`. The second status effect type in the game.

## 1.3 Code impact

New `AbilityType` members: `Cleave`, `CritMultiplier`, `Riposte`, `Pull`,
`Momentum`, `WardCast`, `CritCast`, `OnHitPoison`.

New `StatusEffectType` members: `Poison`, `Ward`.

`CombatRules.RollAttack` currently hardcodes `weapon.Damage * 2` on a crit
(`CombatRules.cs:39`) — that becomes `Damage * (CritMultiplier ?? 2)`.

`TurnSystem` gains: cleave target selection in `TryAttack`, a riposte hook in
`ResolveAttackOnCharacter`, a pull displacement in `TryBracesAgainst`, and
`Ward`/`Poison` handling in `TickStatusEffects` (Ward absorbs before HP loss,
Poison ticks damage and can kill).

`Weapon` gains a `WeaponClass` enum field — Phase 3's drop tables key off it.

---

# Phase 2 — Progression

## 2.1 Innate stats are fixed at creation

Each party member gets a permanent spread of **STR / DEX / CON / INT**. Stats
never rise. They are pure *rate multipliers*: you level what you use, at the
speed your nature allows. This is what makes party composition matter for the
whole run — D will always be the better caster, no matter how long C swings a
staff.

Starting spreads (20 points each, tuning targets):

| Member | STR | DEX | CON | INT | Leans |
| --- | --- | --- | --- | --- | --- |
| A | 4 | 8 | 4 | 4 | Dagger duellist |
| B | 7 | 5 | 5 | 3 | Sword & shield line-holder |
| C | 7 | 4 | 6 | 3 | Spear / axe bruiser |
| D | 3 | 4 | 5 | 8 | Staff caster |

Rate curve, applied to every XP gain below:

```
rate(stat) = 0.5 + 0.1 * stat        # stat 1..10  →  0.6× .. 1.5×
```

## 2.2 Three XP pools, each fed by *doing the thing*

| Pool | Fed by | Governing stat |
| --- | --- | --- |
| Weapon proficiency | Damage dealt with that weapon class | Per class, below |
| Max HP | HP actually restored to you | CON |
| Max mana | Mana actually spent | INT |

Governing stat per weapon class:

| Class | Stat |
| --- | --- |
| Dagger | DEX |
| Sword & Shield | **max(STR, DEX)** |
| Spear | STR |
| Axe | STR |
| Staff | INT |

CON governs no weapon — it is the health stat alone.

### Weapon XP is per class, per character — not per item

Loot drops constantly in Phase 3. If XP lived on the weapon *instance*, every
upgrade would reset progress and the loot system would fight the levelling
system. So character A holds a **Dagger proficiency**, and any dagger they pick
up wields at that level.

```
weaponXp[member][class] += damageDealt * rate(governingStat)
xpToNext(L) = 100 * L                      # triangular, tune later
```

Level effects: `+floor(L / 2)` damage, and `-1` movement cost per 3 levels
(floored so a weapon never becomes free).

### Health XP

```
hpXp[member] += hpRestored * rate(CON)
```

`TurnSystem.TickStatusEffects` already caps healing at missing HP
(`TurnSystem.cs:589`), so overheal grants nothing. The emergent rule is neat:
**constitution grows by getting hurt and then healed** — a party that never
takes a scratch never gains HP.

### Mana XP

```
manaXp[member] += manaSpent * rate(INT)
```

Every cast and every enchantment trigger feeds it. This closes a loop with
Phase 3: enchantments lock max mana but spend mana when they fire, so running
them actively grows the pool that pays for them.

## 2.3 Code impact

New `Logic/InnateStats.cs` (record of the four values plus `RateFor`), and
`Logic/Progression.cs` owning the pools and curves. `PartyMemberState` gains
`Stats`, `WeaponXp`, `HpXp`, `ManaXp`, and `MaxHp`/`MaxMana` become computed
rather than the constants they are today (`PartyMemberState.cs:16,21`).

`TurnSystem` credits XP at three existing sites: `ResolveAttackOnEnemy`
(damage), `TickStatusEffects` + `CharacterHealed` (healing), and `TryCast`
(mana spent).

HUD gains per-weapon level and XP bars in the inventory panel.

---

# Phase 3 — Loot and enchantments

## 3.1 Drops are class-locked, variant-rolled

An enemy drops a weapon of **its own class**, rolled uniformly among that
class's four variants. `EnemyPlacer` already assigns each dummy a weapon from a
seeded stream (`EnemyPlacer.cs:49`) — the drop reads that class back.

Placement rolls `0..Weapons.Count - 1` today; that becomes a roll over the five
**classes**, with the variant rolled at drop time on the loot stream.

## 3.2 Repeat kills add enchantments

Dummies already resurrect after 10 turns (`GameConstants.DummyResurrectTurns`)
and already record `DefeatedAtTurn`. Add `DefeatCount` to `EnemyState`: the
*n*-th defeat of the same dummy drops a weapon carrying *n − 1* enchantments.

This makes the resurrection timer a deliberate farming rhythm rather than
flavour — camp a dummy to deepen its drops, at the cost of the turns you spend
waiting.

## 3.3 Enchantments cost max mana while equipped

Every enchantment carries a **mana lock** and a **trigger cost**:

- The lock is subtracted from usable max mana **while the item is equipped**.
  Unequipping restores it in full. The inventory becomes a live loadout
  decision every fight.
- The trigger cost is spent from the remaining pool each time the enchantment
  fires. **Insufficient mana simply means it does not fire** — no failure
  state, no penalty, just a resource gate.

Starting set:

| Enchantment | Lock | Trigger | Effect |
| --- | --- | --- | --- |
| Vampiric | 20 | 10 | On crit, heal the wielder for damage dealt |
| Flaring | 15 | 5 | On hit, apply Poison 2 |
| Weightless | 25 | 5 | Attacks cost 10 less movement |
| Warding | 30 | 40 | A killing blow leaves you at 1 HP instead |
| Echoing | 20 | 15 | The weapon's class feature triggers one extra time per turn |

Because Vampiric heals and every trigger spends mana, an enchanted loadout
feeds both the HP and mana pools from Phase 2. Stacking locks is the real cost:
three enchantments can leave a caster with almost no castable mana.

## 3.4 Code impact

New `Logic/Enchantment.cs` and `Logic/LootTable.cs` (own RNG stream:
`mapSeed ^ LootSalt`). `Weapon` gains an enchantment list — note `Weapon` is a
`record` shared by reference from `GameConstants.Weapons` today, so dropped
instances must be **copies**, never mutations of the shared table.

`PartyMemberState.MaxMana` subtracts the equipped item's total lock.

---

# Phase 4 — Multiple floors

## 4.1 Persistent while you are in the dungeon; reset when you leave

Floors are revisitable via stairs, and every visited floor keeps its enemy
state and explored fog for as long as the party remains in the dungeon.
**Leaving the dungeon discards all of it** — re-entering regenerates every
floor fresh.

That keeps the in-run tactics (retreat upstairs, come back for a half-cleared
room) without the save file growing without bound, and it caps repeat-kill
enchantment farming at one dungeon visit.

## 4.2 Stairs must not perturb the golden seed

`MapGenerator` cannot gain stair placement inline — that would reorder its RNG
calls and break `MapGeneratorTests`. Stairs get placed by a **post-pass on its
own stream**, the same pattern `EnemyPlacer` established:

```
Logic/StairPlacer.cs      seed = mapSeed ^ StairSalt ^ floorIndex
```

Down-stairs go in the room furthest from `PlayerStart` (by A* cost, using the
existing `Pathfinder`); up-stairs at the arrival tile.

Per-floor seed: `floorSeed(n) = mapSeed ^ (FloorSalt * n)`, with `n = 0`
resolving to exactly today's seed so floor 0 stays golden.

## 4.3 Code impact

New `Logic/DungeonState.cs` holding `Floor[]`, each with its map, enemy list,
and `FogState`. `DungeonScene` currently builds one map in its constructor and
holds the party, enemies and fog directly — that becomes a swap of the active
floor, tearing down and rebuilding geometry on transit.

`Logic/FogState.cs` becomes per-floor rather than per-scene.

---

# Phase 5 — Save / load

Lands last, once the state model above is final.

## 5.1 What is stored

| Group | Contents |
| --- | --- |
| Run | Map seed, current floor index, turn count |
| Party | Position, HP, mana, innate stats, all three XP pools, inventory (with enchantments), active status effects |
| Per visited floor | Enemy states (position, HP, alive, `DefeatedAtTurn`, `DefeatCount`, weapon, enchantments) and the explored fog grid |

Maps are **never serialized** — they regenerate from `floorSeed(n)`. This is
exactly the design `EnemyPlacer`'s doc comment anticipated: regenerate the
dungeon from the seed on every entry, place enemies only on the first visit,
restore saved states afterwards.

## 5.2 Format

`System.Text.Json` with an explicit version field and a DTO layer in
`Logic/Save/` — the gameplay types must not carry serialization attributes, or
`Logic/` stops being clean.

Round-trip test: save, reload, and assert that a fixed sequence of turns
produces an identical world state.

---

# Open questions

- **Weapon XP granularity.** Recorded above as per-class-per-character with
  reasoning. If it should genuinely be per-item, Phase 2 and Phase 3 both
  change shape and loot needs an XP-transfer mechanic.
- **Does the party's starting loadout change?** `CharStartingWeaponIdx` is
  `{0,1,2,2}` — dagger, sword, spear, spear. With five classes and D built as
  the caster, `{0,1,3,4}` (dagger, sword, axe, staff) matches the stat spreads
  better, but it moves the party off the ported starting state.
- **Do enemies drop axes and staves?** PR #5 adds staff-healer enemies, which
  would make staves droppable. Axe dummies need an AI pass — Cleave means they
  want to be surrounded, which no current enemy behaviour models.
- **Poison on enemies.** `StatusEffect` lives on `PartyMemberState` only.
  Venom Kiss and Flaring need it on `EnemyState` too.

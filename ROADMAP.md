# DistanceRPG Roadmap

Planned features beyond the current port. Everything here is a design target,
not shipped behaviour — see the git history for what exists today.

Six phases, in dependency order. Each is one PR off up-to-date `main`, per the
workflow rule in `CLAUDE.md`.

| Phase | Theme | Depends on |
| --- | --- | --- |
| 0 | Unify actors; universal status effects | — |
| 1 | Weapon classes and variants | 0 |
| 2 | Progression: innate stats, weapon/health/mana XP | 1 |
| 3 | Loot drops and enchantments | 1, 2 |
| 4 | Multiple floors | — (but lands after 3) |
| 5 | Save / load | 0–4 |

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

# Phase 0 — Unify actors, universal status effects

Status effects apply to **anyone**: party members and enemies alike. Enemy
staves debuff the party, party staves debuff enemies, and PR #5's staff-healers
buff their own side. That is a prerequisite for almost everything in Phase 1,
so it lands first and alone.

Today `PartyMemberState` and `EnemyState` are unrelated classes that happen to
share a shape, and `StatusEffects` lives only on the party side
(`PartyMemberState.cs:24`). Introduce a common base:

```
abstract class ActorState        // X, Y, Hp, MaxHp, Alive, Radius,
                                 // StatusEffects, EquippedWeapon
  ├─ PartyMemberState            // + Mana, Inventory, DistLeft, SavedMovement, Stats
  └─ EnemyState                  // + TurnsSinceSeen, DefeatedAtTurn, DefeatCount
```

This pays for itself immediately in `TurnSystem`, which currently carries two
near-identical copies of everything:

- `ResolveAttackOnCharacter` and `ResolveAttackOnEnemy`
  (`TurnSystem.cs:604,625`) collapse into one method over `ActorState`.
- The two mirrored brace paths — `_braceCandidates` / `_braceUsesThisTurn` for
  the party and `_inEnemyReach` / `_enemyBraceUsesThisTurn` for enemies
  (`TurnSystem.cs:83–91`) — become one threat-zone implementation.
- `TickStatusEffects` runs over every actor rather than just the party.

Events stay typed as they are today (`CharacterHit` vs `EnemyHit`) so the HUD
does not have to change in this phase.

**Ordering note:** do this before Phase 1, not during. It is a pure refactor
with no behaviour change, so it should land green against the existing 107
tests without touching a single expectation.

---

# Phase 1 — Weapon classes and variants

**Eight classes, four variants each: 32 weapons.**

The classes split into two groups with *different variant axes*. Martial
classes vary by stat profile; caster classes vary by what the cast actually
does. Forcing casters into the martial shape would produce four staves that all
do the same thing at different prices, which is not interesting.

| Class | Stat | Class feature | Variant axis |
| --- | --- | --- | --- |
| Dagger | DEX | CritWindow | Martial |
| Sword & Shield | **max(STR, DEX)** | Block | Martial |
| Spear | STR | Brace | Martial |
| Axe | STR | Cleave | Martial |
| Ranged | DEX | Longshot | Martial |
| Throwing | STR | Charges | Martial |
| Staff | INT | Single-target buff/debuff | **Four effects** |
| Wand | INT | Area of effect | **Four shapes** |

CON governs no weapon — it is the health stat alone.

## 1.1 Modifiers are stacks, not values

**This is the load-bearing decision of Phase 1.** A weapon is a base statline
plus a **multiset of modifiers**. There is no `Brace 3` — there is `Brace ×3`,
the same modifier applied three times. Stack count is the only dial.

That makes every variant, every unique, and every enchantment the same
operation — *add stacks* — so nothing below needs a bespoke mechanism:

| Concept | Expressed as |
| --- | --- |
| Base weapon | Class modifier ×1 |
| Swift | `+ Light ×1` |
| Greater | `+` one more stack of the class modifier (→ ×2) |
| Keen | `+ CritWindow ×1` and `+ OnCrit ×1` |
| Wildcard | `+` one stack of a modifier the class does not normally get |
| **Unique** | Arbitrary stacks — class modifier ×3, or two features at ×2 |
| Enchantment (Phase 3) | A stack that also carries a mana lock |

A unique is therefore *not a new kind of thing*. "A halberd that braces three
times" is `Brace ×3, Push ×1` and needs no code beyond what the base system
already does.

### Stacking rules live in one table

Each modifier declares what one stack is worth and where it caps. This is the
only place a modifier's maths exists:

**Each modifier is hard-capped at 5 stacks of itself.** The cap is per
*modifier type* and the types are independent — there is no shared budget
across a weapon. `CritWindow ×5, CritMultiplier ×5` is legal; `CritWindow ×6`
is not.

The cap is on the *stack count*, not the resolved value, and it is a single
constant enforced in one place — `ModifierSet.With` clamps per type. No
combination of variant, unique and enchantment can push one modifier past its
fifth stack, every stack you can hold does something, and the ceiling is
uniform across modifiers.

Because the cap is uniform, **per-stack value is the only balance dial** — it
has to be chosen as `intended ceiling ÷ 5`.

| Modifier | Per stack | At ×5 | Notes |
| --- | --- | --- | --- |
| `Brace` | +1 retaliation | 5 | |
| `Block` | +3 absorbed | 15 | Never reduces below 1 taken |
| `CritWindow` | **+1 to the window** | crit on 15+ | ×1 = 19–20, ×2 = 18–20, ×3 = 17–20 … |
| `CritMultiplier` | +1 to the multiplier | ×7 | Base is ×2 with no stacks |
| `Cleave` | +1 extra target | 5 | |
| `Charges` | +2 throws per turn | 10 | |
| `Longshot` | +1 damage per tile | 5 | Beyond 3 tiles |
| `Light` | −15 movement cost | −75 | Cost floors at 10 |
| `Riposte` | +1 counter per turn | 5 | |
| `Push` / `Drag` | +1 tile displaced | 5 | |
| `Momentum` | +25% cost refunded per kill | **125%** ⚠ | Over 100% refunds more than the swing cost |
| `Overwatch` | +1 held shot | 5 | |
| `OnCrit` | +1 rider level | 5 | §1.6 |
| `Cast` | +1 effect level applied | 5 | Staves and wands |

Flat costs, not percentages, for `Light`: in a game where you count exact
movement units, `−15` is legible in a way that `−25%` is not.

The per-stack numbers above are first-pass targets, not tuned values. Two are
flagged rather than solved (see Open questions): `Momentum` exceeds a full
refund at ×5, and `CritWindow` at +1 per stack changes what the base weapons
need to declare.

## 1.2 The martial variant shape

Written as stack operations on the base weapon. Every class follows the same
four rules; only the wildcard differs.

| Variant | Operation |
| --- | --- |
| **Swift** | `+ Light ×1` |
| **Greater** | `+` one stack of the class modifier |
| **Keen** | `+ CritWindow ×1`, `+ OnCrit ×1` |
| **Wildcard** | `+` one stack of an off-class modifier |

### Dagger — class modifier `CritWindow` (DEX)

| Variant | Name | Range | Dmg | Cost | Modifiers |
| --- | --- | --- | --- | --- | --- |
| base | Dagger | 40 | 15 | 30 | `CritWindow ×1` |
| Swift | Flensing Knife | 40 | 15 | 15 | `CritWindow ×1, Light ×1` |
| Greater | Assassin's Fang | 40 | 15 | 30 | `CritWindow ×2` |
| Keen | Vorpal Kris | 40 | 15 | 30 | `CritWindow ×1, CritMultiplier ×1, OnCrit ×1` |
| Wildcard | Venom Kiss | 40 | 10 | 30 | `CritWindow ×1, OnHitPoison ×1` |

Dagger is the class where Greater and Keen would collide — crit *is* its
modifier. Under stacking they separate cleanly: Greater adds another
`CritWindow` stack, Keen adds `CritMultiplier` and `OnCrit` instead.

### Sword & Shield — class modifier `Block` (max STR/DEX)

| Variant | Name | Range | Dmg | Cost | Modifiers |
| --- | --- | --- | --- | --- | --- |
| base | Sword | 80 | 10 | 50 | `Block ×1` |
| Swift | Arming Sword | 80 | 10 | 35 | `Block ×1, Light ×1` |
| Greater | Tower Guard | 80 | 10 | 50 | `Block ×2` |
| Keen | Estoc | 80 | 10 | 50 | `Block ×1, CritWindow ×1, OnCrit ×1` |
| Wildcard | Riposte Blade | 80 | 10 | 50 | `Block ×1, Riposte ×1` |

**Riposte**: blocking an attack grants a free counter-swing at the attacker.
Turns the shield from pure mitigation into a threat.

### Spear — class modifier `Brace` (STR)

| Variant | Name | Range | Dmg | Cost | Modifiers |
| --- | --- | --- | --- | --- | --- |
| base | Spear | 130 | 7 | 40 | `Brace ×1` |
| Swift | Skirmisher's Pike | 130 | 7 | 25 | `Brace ×1, Light ×1` |
| Greater | Phalanx Spear | 130 | 7 | 40 | `Brace ×2` |
| Keen | Impaler | 130 | 7 | 40 | `Brace ×1, CritWindow ×1, OnCrit ×1` |
| Wildcard | Halberd | 130 | 7 | 40 | `Brace ×1, Push ×1` |

**Push**: a brace hit shoves the target back out of its own reach. In a game
where movement is the resource, denying an enemy its approach is the purest
possible spear ability — and `TryBracesAgainst` already resolves braces
mid-walk, so the hook exists.

### Axe — class modifier `Cleave` (STR)

One swing hits every valid target in range, paying the movement cost once.
Rooms already hold 0–4 dummies and nothing today rewards being surrounded.

| Variant | Name | Range | Dmg | Cost | Modifiers |
| --- | --- | --- | --- | --- | --- |
| base | Axe | 60 | 18 | 60 | `Cleave ×1` |
| Swift | Hatchet | 60 | 18 | 45 | `Cleave ×1, Light ×1` |
| Greater | Great Axe | 60 | 18 | 60 | `Cleave ×2` |
| Keen | Executioner's Axe | 60 | 18 | 60 | `Cleave ×1, CritWindow ×1, OnCrit ×1` |
| Wildcard | Reaver | 60 | 14 | 60 | `Cleave ×1, Momentum ×1` |

**Momentum**: every enemy killed by the swing refunds a share of the movement
cost. Rewards wading into a crowd at exactly the right moment.

### Ranged — class modifier `Longshot` (DEX)

**Longshot**: damage rises with distance to the target — `+1` per tile beyond
3 tiles, per stack. A bow in the front rank is nearly useless; the same bow
across a room is devastating. Most on-theme ability in the game, and it makes
the marching formation a genuine trade-off.

| Variant | Name | Range | Dmg | Cost | Modifiers |
| --- | --- | --- | --- | --- | --- |
| base | Shortbow | 320 | 5 | 45 | `Longshot ×1` |
| Swift | Hunting Bow | 320 | 5 | 30 | `Longshot ×1, Light ×1` |
| Greater | Longbow | 320 | 5 | 45 | `Longshot ×2` |
| Keen | Recurve | 320 | 5 | 45 | `Longshot ×1, CritWindow ×1, OnCrit ×1` |
| Wildcard | Crossbow | 320 | 8 | 45 | `Longshot ×1, Overwatch ×1` |

**Overwatch**: bank the shot instead of firing. If an enemy enters line of
sight during the enemy turn, it fires for free. A ranged mirror of Brace — and
it reuses the same threat-zone machinery Phase 0 unified.

### Throwing — class modifier `Charges` (STR)

**Charges**: throws per turn, replenished at turn start, each cheaper than a
melee swing. Cleave is many targets in one swing; Charges is many swings in one
turn.

| Variant | Name | Range | Dmg | Cost | Modifiers |
| --- | --- | --- | --- | --- | --- |
| base | Javelins | 190 | 9 | 35 | `Charges ×1` |
| Swift | Darts | 190 | 6 | 20 | `Charges ×1, Light ×1` |
| Greater | Bandolier | 190 | 9 | 35 | `Charges ×2` |
| Keen | Balanced Knives | 190 | 9 | 35 | `Charges ×1, CritWindow ×1, OnCrit ×1` |
| Wildcard | Harpoon | 190 | 9 | 35 | `Charges ×1, Drag ×1` |

**Drag**: a hit pulls the target *toward* the thrower — the exact inverse of
the halberd's Push, and it sets up your own axe and sword line.

## 1.3 Staff — four effects, half buffs and half debuffs (INT)

Each staff casts a *different* effect. All share Range 100 and Cost 40; they
differ in what they apply and what it costs in mana.

| Variant | Name | Target | Mana | Applies |
| --- | --- | --- | --- | --- |
| Buff | Staff of Renewal | Ally | 15 | **Regeneration** — heals per turn, decaying |
| Buff | Staff of Warding | Ally | 20 | **Ward** — absorbs damage until spent |
| Debuff | Staff of Blight | Enemy | 20 | **Poison** — damage per turn, decaying |
| Debuff | Staff of Mire | Enemy | 25 | **Mire** — cuts the target's movement budget |

Staff of Renewal is today's shipped Staff with its numbers unchanged, so
parity holds.

Each carries `Cast ×1`, so the stacking model applies here too: a Greater
staff is `Cast ×2` and applies its effect at double level. **Which** effect a
staff casts is a field on the weapon, not a modifier — the modifier only says
how hard it lands.

**Mire is the signature debuff** for this game specifically: in a system where
movement is the only real currency, taxing an enemy's budget is a more
meaningful attack than damage. It also gives INT characters something to do
against enemies that a healer alone cannot.

Debuff staves need `TryCast` to accept an **enemy** target — today `CanCast`
and `TryCast` only take `PartyMemberState ally` (`TurnSystem.cs:178,191`).
Phase 0's `ActorState` makes that a signature change rather than a rewrite.

## 1.4 Wand — four area shapes (INT)

Wands deal damage in an area. All share Damage 8, Cost 45, Mana 20; they differ
only in geometry. Shapes are in logic units.

| Variant | Name | Shape |
| --- | --- | --- |
| Blast | Wand of the Blast | Circle, radius 48, centred on a target point within 160 |
| Cone | Wand of the Cone | 90° cone, length 128, from the caster |
| Beam | Wand of the Beam | Line, width 32, length 224, from the caster |
| Nova | Wand of the Nova | Circle, radius 96, centred on the caster |

All shapes stop at walls — `LineOfSight` already does segment-vs-wall tests and
extends naturally to per-target checks inside a shape.

**Friendly fire is on** (see open questions): a shape hits every actor inside
it except the caster. That makes the marching line an actual liability and
gives spacing a cost, which is the right kind of tension for this game — but
it is the single most reversible decision in Phase 1, so it ships behind a
constant.

Wands carry `Cast ×1` like staves; the shape is a field, the stack is the
damage multiplier.

## 1.5 Uniques

A unique is a named weapon with a stack spread the four standard variants
cannot produce. It needs **no new mechanism** — only a name, a base statline,
and a modifier multiset:

| Unique | Class | Modifiers | Reads as |
| --- | --- | --- | --- |
| The Bulwark | Sword | `Block ×4` | Absorbs 12; nothing else |
| Widowmaker | Dagger | `CritWindow ×2, CritMultiplier ×2, OnCrit ×2` | Crits on 12+, for ×4, sundering deep |
| Hoplite's Wall | Spear | `Brace ×3, Push ×2` | Three retaliations, each shoving 2 tiles |
| Stormcrow | Ranged | `Longshot ×3, Overwatch ×2` | Two held shots, brutal at full range |
| Feathered Death | Throwing | `Charges ×3, Light ×2` | Six cheap throws a turn |

Uniques drop from the deepest repeat-kill tiers (Phase 3). Because they are
just stacks, a unique that turns out overtuned is a data edit, not a code
change — and the per-modifier caps in §1.1 mean no unique can escape the
balance envelope by construction.

## 1.6 Crit riders — a crit leaves a mark

A crit today just doubles damage. That makes the Keen variant the weakest of
the four: Swift saves movement (the actual currency) and Greater doubles the
class feature, while Keen only makes a number occasionally bigger. Crits should
*land an effect*, not just spike damage.

Two new status effects carry it, both using the existing decaying model —
level N, ticks down one per turn, removed at zero, so they always self-clear:

| Effect | Per level | Notes |
| --- | --- | --- |
| **Sundered** | Target takes **+2 damage** from every source | Getting crit opens you up |
| **Weakened** | Target deals **2 less damage**, floored at 1 | Getting crit rattles your swing |

Both cap at level 4 (`+8` / `−8`). The floor mirrors Block's existing "never
below 1 taken" rule (`CombatRules.cs:58`), so nothing can be reduced to
harmlessness.

### Riders express the class

**Which** rider a weapon applies is a field on the class; **how deep** it lands
is `OnCrit` stacks. Every weapon carries `OnCrit ×1`; the Keen variant adds a
second stack on top of its wider window, which is what makes it genuinely
crit-specced rather than mildly luckier. Rider level = `OnCrit` stacks, so
Shattering (Phase 3) and a unique's `OnCrit ×3` feed the same number.

| Class | Crit rider | Why |
| --- | --- | --- |
| Dagger | **Sundered** | Precision finds the gap; the party cashes it in |
| Ranged | **Sundered** | A marked target, softened at range |
| Sword & Shield | **Weakened** | A shield-bash rattles the attacker |
| Axe | **Weakened** | A crushing blow, spread across everything cleaved |
| Spear | **Mire** | Caught at reach and staggered — costs them movement |
| Throwing | **Mire** | Same, and it sets up the harpoon's Drag |

Sundered on the dagger is the deliberate combo: A crits to open a target, then
C's axe cashes it in for double value across the whole cleave. That is the
first real reason for the party to focus one enemy.

### Casts crit too

Staves and wands roll d20 like attacks. A crit **doubles the effect level
applied** — a critical Staff of Mire strips twice the movement, a critical
Renewal stacks twice the regeneration. That restores the crit axis to the
caster classes, which lost it when staff variants became four distinct effects.

### Both sides, and why that is survivable

Status effects are universal (Phase 0), so enemy crits apply riders to the
party — a dagger dummy with `CritWindow ×1` crits on 25% of swings. Three things
keep that from spiralling:

- Riders decay one level per turn on their own.
- They cap at level 4.
- Sundered is flat `+2`, not a multiplier, so it cannot compound with the crit
  doubling into a one-shot.

### Damage pipeline

`CombatRules.ResolveAttack` gains two steps, ordered so Block stays last and
its minimum-1 guarantee holds:

```
1. roll d20   → base damage    (× CritMultiplier, or halved on a natural 1)
2. attacker's Weakened          → subtract
3. defender's Sundered          → add
4. defender's Block             → absorb, never below 1 taken
5. on a crit, apply the weapon's OnCrit rider to the defender
```

## 1.7 Code impact

### The modifier model replaces `WeaponAbility`

Today a weapon holds `IReadOnlyList<WeaponAbility>` where
`WeaponAbility(AbilityType, int Value)` carries the value inline, and lookup is
`GetAbility(type)` returning at most one (`Weapons.cs:21,33`). Stacking makes
the value **derived**, so the pair becomes:

```csharp
enum ModifierType { Brace, Block, CritWindow, CritMultiplier, Cleave, Charges,
                    Longshot, Light, Riposte, Push, Drag, Momentum, Overwatch,
                    OnCrit, OnHitPoison, Cast }

sealed class ModifierSet                 // ModifierType → stack count
{
    int Stacks(ModifierType t);          // 0 when absent
    int Value(ModifierType t);           // ModifierRules.Resolve(t, Stacks(t))
    ModifierSet With(ModifierType t, int n = 1);   // additive merge
}

static class ModifierRules               // the §1.1 table, one place only
{
    const int MaxStacks = 5;             // per modifier type, independently
    static int PerStack(ModifierType t);
    static int Resolve(ModifierType t, int stacks)
        => PerStack(t) * Math.Min(MaxStacks, stacks);
}
```

Every call site that reads `GetAbility(x)?.Value ?? 0` becomes
`weapon.Modifiers.Value(x)` — the 5-stack cap and per-stack maths never leak
out of `ModifierRules`. `With` being additive *and clamping* is what makes
variants, uniques and enchantments the same operation: an enchantment landing
on an already-maxed modifier is a no-op rather than a special case.

**Migration must preserve the shipped values.** Today's dagger carries
`CritRange 4`, and `CombatRules` resolves the crit threshold as `20 - value`
(`CombatRules.cs:35`), so it crits on **16+** — 25% of swings, pinned by
`CombatRulesTests.cs:28`. The sword blocks 3, the spear braces once.

Sword, spear and staff map to `×1` directly. The dagger needs `CritWindow ×4`
to keep its 16+ window under the new `+1` per stack. That is legal and leaves
Keen's `CritMultiplier` and `OnCrit` budgets untouched, since caps are per
type — but whether a base weapon should sit that close to its own cap is a
tuning question, listed below.

### Everything else

New `StatusEffectType` members: `Ward`, `Poison`, `Mire`, `Sundered`,
`Weakened`.

`Weapon` gains `WeaponClass` (the eight above), `AreaShape?`, and
`StatusEffectType? CastEffect` — which effect a staff or wand applies is a
*field*, not a modifier; the modifier only says how hard it lands. Phase 3's
drop tables key off `WeaponClass`.

`CombatRules.RollAttack` hardcodes `weapon.Damage * 2` on a crit
(`CombatRules.cs:39`) — that becomes `Damage * (2 + Modifiers.Value(CritMultiplier))`,
and damage becomes a function of distance for Longshot.

`TurnSystem` gains: cleave and area target selection, a riposte hook, push/drag
displacement, per-turn charge tracking, an overwatch reaction during the enemy
phase, and the five new effects in the now-universal `TickStatusEffects`. Mire
reduces `EffectiveMax` in `PartyMemberState.StartTurn` and the enemy budget in
`StartEnemyAction`. Sundered and Weakened are read by `CombatRules`, so
`ResolveAttack` needs the attacker passed in — today it only takes the two
weapons (`CombatRules.cs:49`).

**HUD.** The regen badge is party-only today. Riders land on enemies too, so
enemy nameplates need effect badges, and floating combat text needs a
`SUNDERED!` / `WEAKENED!` beat distinct from the damage number.

**Enemy AI needs a pass.** `EnemyAi.PlanMove` closes to weapon range. Ranged
and wand enemies want the opposite — hold distance and kite. That is real work,
not a parameter, and it is why ranged/wand enemies are an open question below.

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
| A | 4 | 8 | 4 | 4 | Dagger / bow |
| B | 7 | 5 | 5 | 3 | Sword & shield line-holder |
| C | 7 | 4 | 6 | 3 | Spear / axe / throwing bruiser |
| D | 3 | 4 | 5 | 8 | Staff / wand caster |

Rate curve, applied to every XP gain below:

```
rate(stat) = 0.5 + 0.1 * stat        # stat 1..10  →  0.6× .. 1.5×
```

## 2.2 Three XP pools, each fed by *doing the thing*

| Pool | Fed by | Governing stat |
| --- | --- | --- |
| Weapon proficiency | Damage dealt with that weapon class | Per class (§1) |
| Max HP | HP actually restored to you | CON |
| Max mana | Mana actually spent | INT |

### Weapon XP is per class, per character

Character A holds a **Dagger proficiency**; any dagger they pick up wields at
that level. Loot never resets progress, so Phase 3 cannot fight Phase 2.

```
weaponXp[member][class] += damageDealt * rate(governingStat)
xpToNext(L) = 100 * L                      # triangular, tune later
```

Level effects: `+floor(L / 2)` damage, and `-1` movement cost per 3 levels
(floored so a weapon never becomes free).

Staves deal no damage, so their proficiency is fed by **effect level applied**
rather than damage — and wands by total damage dealt across every target in the
shape, which makes wand levelling reward good shape placement.

### Health XP

```
hpXp[member] += hpRestored * rate(CON)
```

`TickStatusEffects` already caps healing at missing HP (`TurnSystem.cs:589`),
so overheal grants nothing. The emergent rule is neat: **constitution grows by
getting hurt and then healed** — a party that never takes a scratch never gains
HP.

### Mana XP

```
manaXp[member] += manaSpent * rate(INT)
```

Every cast and every enchantment trigger feeds it. This closes a loop with
Phase 3: enchantments lock max mana but spend mana when they fire, so running
them actively grows the pool that pays for them.

## 2.3 Code impact

New `Logic/InnateStats.cs` (the four values plus `RateFor`) and
`Logic/Progression.cs` (pools and curves). `PartyMemberState` gains `Stats`,
`WeaponXp`, `HpXp`, `ManaXp`; `MaxHp` and `MaxMana` become computed rather than
the constants they are today (`PartyMemberState.cs:16,21`).

`TurnSystem` credits XP at existing sites: the unified attack resolver
(damage), `TickStatusEffects` (healing), and `TryCast` (mana spent).

HUD gains per-class level and XP bars in the inventory panel.

---

# Phase 3 — Loot and enchantments

## 3.1 Drops are class-locked, variant-rolled

An enemy drops a weapon of **its own class**, rolled uniformly among that
class's four variants. `EnemyPlacer` already assigns each dummy a weapon from a
seeded stream (`EnemyPlacer.cs:49`); the drop reads that class back. Placement
rolls over the eight **classes**, with the variant rolled at drop time on the
loot stream.

## 3.2 Repeat kills add enchantments

Dummies resurrect after 10 turns (`GameConstants.DummyResurrectTurns`) and
already record `DefeatedAtTurn`. Add `DefeatCount` to `EnemyState`: the *n*-th
defeat of the same dummy drops a weapon carrying *n − 1* enchantments.

This makes the resurrection timer a deliberate farming rhythm rather than
flavour — camp a dummy to deepen its drops, at the cost of the turns you spend
waiting.

**Uniques sit at the deep end.** Past a threshold (5 defeats, tuning target)
the roll can return a unique of that class (§1.5) instead of a variant. Since
uniques and enchantments are both just stacks, the whole ladder — base →
variant → enchanted → unique — is one number going up, and the §1.1 caps hold
at every rung.

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
| Shattering | 25 | 10 | `OnCrit ×1` — crit riders (§1.6) land one level deeper |

Because Vampiric heals and every trigger spends mana, an enchanted loadout
feeds both the HP and mana pools from Phase 2. Stacking locks is the real cost:
three enchantments can leave a caster with almost no castable mana.

## 3.4 Code impact

New `Logic/Enchantment.cs` and `Logic/LootTable.cs` (own RNG stream:
`mapSeed ^ LootSalt`).

An enchantment is **a modifier stack that carries a mana lock and a trigger
cost** — nothing more. Enchanting is `Modifiers.With(type)` plus an entry in
the weapon's enchantment list, so enchantments, variants and uniques all move
the same numbers and the §1.1 caps bound them too. That is why a fully
enchanted weapon cannot outrun the balance envelope: the cap is on the resolved
value, wherever the stacks came from.

Note `Weapon` is a `record` shared by reference from `GameConstants.Weapons`
today, so dropped instances must be **copies**, never mutations of the shared
table — `ModifierSet` should be immutable with `With` returning a new set.

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

New `Logic/DungeonState.cs` holding `Floor[]`, each with its map, actor list,
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
| Per visited floor | Enemy states (position, HP, alive, `DefeatedAtTurn`, `DefeatCount`, weapon, enchantments, status effects) and the explored fog grid |

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

- **Wand friendly fire.** Specced as on — a shape hits every actor inside it
  except the caster. It makes spacing cost movement, which is the right
  tension, but it may be miserable with the marching formation as it stands.
  Ships behind a constant so it can be flipped after play.
- **Ranged and wand enemies.** `EnemyAi.PlanMove` only knows how to close.
  Enemies with those classes need kiting behaviour — hold range, back off when
  approached — which is genuine AI work. Until it exists, `EnemyPlacer` should
  roll only the six martial-and-melee classes.
- **Does the party's starting loadout change?** `CharStartingWeaponIdx` is
  `{0,1,2,2}` — dagger, sword, spear, spear. With eight classes and D built as
  the caster, dagger / sword / axe / staff matches the stat spreads better, but
  it moves the party off the ported starting state.
- **Overwatch and enemy-turn reactions.** Overwatch fires during the enemy
  phase, as braces already do. Whether a character can hold *both* an overwatch
  shot and a brace in the same turn needs a ruling before Phase 1 codes it.
- **Does the dagger keep its 16+ window?** The ported dagger crits on 16+
  (25%), which is `CritWindow ×4` at +1 per stack — one stack short of its own
  cap, so Greater takes it to ×5 (15+, 30%) and there it stops. That works,
  but it means the dagger's whole `CritWindow` range across every variant and
  unique is 16+ to 15+. Narrowing the base window would open that range back
  up at the cost of parity with the prototype. The class tables in §1.2 still
  show `×1` counts and need restating once this is settled.
- **`Momentum` needs tuning.** At +25% it refunds 125% at ×5 — more movement
  back than the swing cost, i.e. free attacks forever.
- **Should a natural 1 have a rider too?** `RollOutcome.Weak` already exists
  and only halves damage. The symmetric move is a fumble applying **Weakened**
  to *yourself* — but crits and fumbles both firing riders may be too much
  status churn per turn. Deliberately not specced.

# Settled

- Weapon XP is **per class, per character**, not per item.
- Status effects are **universal** — every actor can carry them (Phase 0).
- Innate stats are **fixed at creation** and never rise.
- Enchantment mana locks apply **while equipped** only.
- Floors persist **within a dungeon visit** and reset on leaving.
- Crits apply a **class-flavoured rider** on top of the damage spike, both
  ways; casts crit for double effect level.
- Modifiers are **stacks, not values**. Greater is a second stack of the class
  modifier; uniques are arbitrary stacks; enchantments are stacks with a mana
  lock. One mechanism.
- **Max 5 stacks per modifier type**, hardcoded, with types independent — no
  shared budget across a weapon. The cap is on the stack count, not the
  resolved value, so per-stack value is the only dial.
- `CritWindow` is **+1 per stack**: ×1 crits on 19–20, ×2 on 18–20, and so on.

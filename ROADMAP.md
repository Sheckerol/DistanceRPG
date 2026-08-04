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
| 6 | Between runs: wear, the enchanter, the hub | 1–5 |

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
| Class baseline | The one or two modifiers every weapon of that class carries |
| Efficiency weapon | `+ Light ×1` |
| Purity weapon | `+` one more stack of the class's signature (→ ×2) |
| Control weapon | `+` one stack of something that degrades the enemy |
| Support weapon | `+` one stack of something that helps the party |
| **Unique** | Arbitrary stacks — signature ×3, or two modifiers at ×2 |
| Enchantment (Phase 3) | Its own system entirely — §3.3 |

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
| `Charges` | +2 throws per turn | 10 | A **cap**, not a grant — see §1.2 |
| `Longshot` | +1 damage per tile | 5 | Beyond 3 tiles |
| `Light` | **−10% of the weapon's cost** | −50% | Additive across stacks, not compounding |
| `Riposte` | +1 counter per turn | 5 | |
| `Push` / `Drag` / `Rout` | +1 tile displaced | 5 | |
| `Splitting` | +3 of the target's Block ignored | 15 | Exactly the `Block` ceiling |
| `Softening` | +3 of the target's Block stripped for a turn | 15 | Same ceiling, but for everyone |
| `Pin` | +1 `Mire` level on the target | 5 | |
| `Overwatch` | +1 held shot | 5 | |
| `CritWeaken` | +1 `Weakened` level on a crit | 5 | §1.6 |
| `CritSunder` | +1 `Sundered` level on a crit | 5 | §1.6 |
| `BlockWeaken` | +1 `Weakened` level on a successful block | 5 | §1.6 |
| `Cast` | +1 effect level applied | 5 | Staves and wands |

### `Light` has to be proportional, not flat

Attack costs span 20–60 across the classes, so a flat discount does not mean
the same thing twice. At a flat −15 per stack, a 30-cost dagger reaches zero at
×2 — three of its five stacks would do nothing, and a floored 10-cost dagger
against a 160 budget is sixteen swings a turn. Flat also converges every class
on the same floor, so a maxed dagger and a maxed axe would cost the same to
swing, erasing the cost structure that tells them apart.

At −10% per stack the discount scales with what it is discounting. A maxed
`Light ×5` halves any weapon: dagger 30 → 15, sword 50 → 25, axe 60 → 30. The
classes keep their relative footing at every stack count.

Percentages are harder to count with mid-turn, which is a real cost in this
game — so the **resolved** cost is computed once and displayed on the weapon.
The player reads `Cost 15`, never `30 −50%`.

**The general rule: a modifier acting on a value that varies across classes has
to be proportional.** `Light` is the only one that does — every class has a
cost. `Block` is flat by deliberate exception: absorbing 3 is meant to blunt
many small hits more than one large one, and the minimum-1 rule already stops
it running away.

The per-stack numbers above are first-pass targets, not tuned values.

### If it needs a cap, it is an enchantment

**A weapon modifier must be safe at ×5 by construction.** Anything that would
need a bespoke ceiling to stay sane does not belong in this table at all — it
belongs in the enchantment system (§3.3), which is a separate mechanism with
its own dials, bounded by a mana budget rather than by stack counts.

That is a real dividing line, not a style preference. Every modifier above is
bounded by *something structural*: a per-turn count, a triggering condition, or
a ceiling that another modifier already imposes (`Splitting` tops out exactly
where `Block` does). None of them feed their own resource back into themselves.

A refund does. `Momentum` — movement returned per enemy killed — pays back into
the budget that bought the swing, so more kills buy more swings. No per-stack
value fixes that shape; capping it per swing would just be the bespoke ceiling
this rule exists to avoid. As an enchantment it self-limits: each trigger costs
mana, mana regenerates only from *unspent* movement, so a refund loop starves
itself. See Phase 3.

## 1.2 The martial classes

Written as stack operations on the base weapon. Every class follows the same
four rules; only the wildcard differs.

A class has a **baseline** — the modifiers every weapon of that class carries —
and each of its four weapons adds **exactly one more**, drawn from four fixed
roles:

| Role | Adds | Reads as |
| --- | --- | --- |
| **Efficiency** | `Light ×1` | Standard, but quick |
| **Purity** | Another stack of the class's signature | More of what the class *is* |
| **Control** | Something that degrades the enemy | The defensive-ish option |
| **Support** | Something that helps the rest of the party | The interesting one |

Two things this fixes over a rigid "crit-specced" slot. A class whose identity
*is* crit has nothing to gain from one — that belongs in its baseline instead.
And every class gets a **support** weapon, so each one has a reason to exist in
a party that already has damage covered.

**Cost columns below are base costs.** An Efficiency weapon shows the same cost
as its base row — `Light ×1` resolves it down by 10% (§1.1). The statline is
what the weapon is; modifiers are what happens to it.

### Attack cost tracks weight

| Class | Cost | |
| --- | --- | --- |
| Throwing | 15 | A flick of the wrist, and hard-capped by Charges |
| Dagger | 30 | Light, fast |
| Ranged | 30 | Drawing a bow is not heavy work |
| Sword & Shield | 50 | A shield is most of that number |
| Spear | 55 | A long polearm is slow to bring to bear |
| Axe | 60 | Heaviest thing anyone swings |

Spear at 55 against bow at 30 is a **correction**: the spear was 40 and the bow
45, which had a heavy polearm swinging faster than an archer looses. Weight
should read in the movement cost, since movement is what the game is about.

The spear's damage is 7 and it now costs 55, so it swings twice a turn for 14 —
which is correct rather than broken. **A spear is not a weapon you attack
with, it is a weapon you threaten with.** Its value is `Brace` firing for free
on the enemy turn and 130 units of reach; the swing is the fallback. High cost
makes that identity explicit instead of leaving the spear a cheap poking stick
that happens to brace.

Throwing at 15 undercuts the bow despite a javelin outweighing an arrow — a
throw is a quicker action than nocking, drawing and aiming, and the Charges cap
is what keeps it honest. Uncapped, that price would be indefensible.

### Dagger (DEX) — baseline `CritWindow ×1, CritMultiplier ×1`

Crit is the class, so both of its dimensions live in the baseline: 19–20 to
crit, and ×3 when it lands.

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Flensing Knife | 40 | 15 | 30 | `Light ×1` |
| Purity | Assassin's Fang | 40 | 15 | 30 | `CritWindow ×1` → 18–20 |
| Control | Disarming Kris | 40 | 15 | 30 | `CritWeaken ×1` |
| Support | Weakspot Stiletto | 40 | 15 | 30 | `CritSunder ×1` |

- **Disarming Kris** — crits apply `Weakened`, cutting what the target deals.
  Defensive: you crit to stop being hit back.
- **Weakspot Stiletto** — crits apply `Sundered`, so *everyone* hits that
  target harder. The dagger stops being a damage weapon and becomes a setup
  weapon; A crits to open a target and C's axe cashes it in across the cleave.

**The baseline is `CritWindow ×1` — crit on 19–20**, a deliberate break from
the prototype's 16+ (`CombatRulesTests.cs:28`).

### Crit frequency is deliberately narrow; crit *power* is the build

With `CritWindow` at +1 per stack and a 5-stack cap, the entire crit-rate range
in the game is 19–20 to 15–20 — **10% to 30%**. That is intentional. Frequency
stays bounded so the d20 keeps mattering, and the investment axis is
`CritMultiplier` instead, which runs ×2 up to ×7.

So a crit build is not "crit constantly" but **"crit rarely and
catastrophically"** — and the two dagger specialists split exactly along that
line — Assassin's Fang buys frequency, the baseline `CritMultiplier` carries
magnitude.

Starting at `×1` rather than the prototype's `×4` also leaves the dagger four
stacks of headroom for uniques and enchantments to work with, instead of one.

### Sword & Shield (max STR/DEX) — baseline `Block ×1`

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Arming Sword | 80 | 10 | 50 | `Light ×1` |
| Purity | Tower Guard | 80 | 10 | 50 | `Block ×1` → absorbs 6 |
| Control | Riposte Blade | 80 | 10 | 50 | `Riposte ×1` |
| Support | Warden's Shield | 80 | 10 | 50 | `BlockWeaken ×1` |

Both trigger off the same event — a successful block — and split cleanly on who
collects:

- **Riposte** — the block grants a free counter-swing. Turns mitigation into a
  threat, and the payoff is yours.
- **BlockWeaken** — the block applies `Weakened` to the attacker, so it deals
  less to *everyone* afterwards. The same effect the dagger's `CritWeaken`
  applies, on a different trigger.

**Why that is support and the dagger's is control.** A shield-bearer already
mitigates incoming damage with Block, so weakening their attacker barely
improves their own position — it improves the position of whoever gets hit next
without a shield. On a dagger, with no Block behind it, weakening is
self-defence. Same effect, opposite role, decided by who carries it.

It is also the one weapon whose added modifier depends on the class baseline:
`BlockWeaken` cannot fire without a `Block` to succeed at.

### Spear (STR) — baseline `Brace ×1`

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Skirmisher's Pike | 130 | 7 | 55 | `Light ×1` |
| Purity | Phalanx Spear | 130 | 7 | 55 | `Brace ×1` → 2 retaliations |
| Control | Halberd | 130 | 7 | 55 | `Push ×1` |
| Support | Pinning Lance | 130 | 7 | 55 | `Pin ×1` |

The spear is the class that decides **where enemies are**, and its two
non-baseline tools are exact opposites:

- **Push** — a braced hit shoves the target back out of its own reach. Breaks
  contact. `TryBracesAgainst` already resolves braces mid-walk, so the hook
  exists.
- **Pin** — a braced hit applies `Mire`, draining the target's movement budget.
  Refuses to let contact break. Catching an enemy on the point and leaving it
  stuck there is what lets the rest of the party disengage and kite, which is
  why this is the spear's *support* weapon even though it reads as control.

Pin applies the existing `Mire` effect (§1.3) rather than inventing its own, so
it reuses machinery already being built.

### Axe (STR) — baseline `Cleave ×1`

One swing hits every valid target in range, paying the movement cost once.
Rooms already hold 0–4 dummies and nothing today rewards being surrounded.

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Hatchet | 60 | 18 | 60 | `Light ×1` |
| Purity | Great Axe | 60 | 18 | 60 | `Cleave ×1` → 2 extra targets |
| Control | Reaver | 60 | 18 | 60 | `Splitting ×1` |
| Support | Routing Axe | 60 | 18 | 60 | `Rout ×1` |

- **Splitting** — ignores 3 of the target's Block per stack. Axes split
  shields, and at ×5 it ignores 15, exactly the `Block` cap, so its ceiling is
  set by the thing it counters rather than an arbitrary number. This is the
  weapon the tutorial dungeon exists to teach you to want (§4.3).
- **Rout** — everything caught by the cleave is pushed back a tile per stack.
  One swing that resets a whole crowd's position buys the entire party room.

### Ranged (DEX) — baseline `Longshot ×1`

**Longshot**: damage rises with distance to the target — `+1` per tile beyond
3 tiles, per stack. A bow in the front rank is nearly useless; the same bow
across a room is devastating. Most on-theme ability in the game, and it makes
the marching formation a genuine trade-off.

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Hunting Bow | 320 | 5 | 30 | `Light ×1` |
| Purity | Longbow | 320 | 5 | 30 | `Longshot ×1` → +2 per tile |
| Control | Pinning Bow | 320 | 5 | 30 | `Pin ×1` |
| Support | Crossbow | 320 | 5 | 30 | `Overwatch ×1` |

- **Pin** — the same `Mire` application as the spear's, delivered by a hit
  rather than a brace. A bow that stops an enemy closing is a bow that never
  has to stop shooting, and it is the other half of the kiting pair.
- **Overwatch** — bank the shot instead of firing; if an enemy enters line of
  sight during the enemy turn, it fires for free. A ranged mirror of Brace,
  reusing the threat-zone machinery Phase 0 unified, and it covers the party's
  approach rather than your own.

**Damage 5 is meant to look low.** A bow held at knife range should be bad —
that is what `Longshot` *is*. The answer to an enemy in your face is not a
stronger bow, it is a different weapon, so the baseline stays weak and the
range curve does the work.

### Every stat owns a close option and a ranged one

That answer only holds because swapping never costs you *progression*. Weapon
XP is per class per character but scales off the class's governing stat (§2.2),
so a character swapping inside their own stat levels both weapons at the same
rate:

| Stat | Close | Mid | Far |
| --- | --- | --- | --- |
| DEX | Dagger 40 | *(Sword 80)* | Ranged 320 |
| STR | Axe 60 | Spear 130 | Throwing 190 |
| INT | Enchanted dagger | Staff 100, Wand ~160 | Enchanted bow |

**INT solves range by borrowing, not by owning.** Enchantments are
class-agnostic and their potency scales with INT rather than with weapon
proficiency (§3.3) — so a high-INT character picks up a dagger or a bow, hangs
an elemental enchantment on it, and plinks away at a level their dagger
proficiency could never justify. The weapon is a delivery system; the damage is
theirs.

That is why INT has no martial classes of its own and does not need any. It is
the same mechanism as the close-range wizard build, pointed at the range
problem instead of the damage one.

The 6-slot inventory (§4.4) exists partly for this: a loadout, not a weapon.

### Swapping costs movement once you are in a fight

Free swapping would make every weapon's downside optional — not just the bow's
range curve, but the spear's 55 cost and the axe's 60 reach. Nothing you chose
would ever have to be lived with.

So a swap costs **20 movement**, more than a throw and less than a dagger
swing, once combat has begun. Out of combat it is **free**: the game already
tracks `AnyLiveEnemySeenThisTurn` (`TurnSystem.cs:52`), the same signal that
grants marching, so loadout management between fights stays frictionless while
mid-fight swapping is a real decision.

The cost compounds properly. A bow user caught at knife range pays 20 to swap
and 30 to swing — 50 for the first hit — which is exactly the punishment for
having been caught.

**Consumables cost movement too**, on the same principle: anything that changes
your situation mid-turn should be paid for out of the same budget as moving and
swinging. See §3.4.

### Throwing (STR) — baseline `Charges ×1`

**Charges is a cap, and the cap is the point.** A throw costs only 15, so the
movement budget alone would allow ten a turn — absurd. Charges is what bounds
that: `×1` permits 2 throws, and each stack adds 2 more. Raising it is
genuinely the class feature, because the cap rather than the budget is what
binds.

Getting this backwards is easy and worth stating plainly: at a *normal* attack
cost the budget already caps you at four or five swings, so a "throws per turn"
limit would be a restriction rather than a feature. Cheap throws are what make
the cap the interesting number.

| Charges | Throws | Movement spent | Damage | Movement left |
| --- | --- | --- | --- | --- |
| ×1 | 2 | 30 | 18 | 130 |
| ×2 | 4 | 60 | 36 | 100 |
| ×5 | 10 | 150 | 90 | 10 |

The class identity is **attack and still have movement left** — the only weapon
that does not force a choice between fighting and repositioning. That gives
STR characters a way to land many hits per turn the way DEX characters do, and
it is what a full kiting party is built around.

Leftover movement is not dead weight here. Half of it banks into next turn
(`EndTurnSaveMovement`) and it regenerates mana
(`RegenManaFromUnusedMovement`), so a low-Charges thrower **fights while
fuelling mana** — feeding enchantment triggers (§3.3) and the mana XP pool
(§2.2).

Which makes the stack count a real build decision rather than a straight
upgrade: low Charges is an economy weapon that keeps your enchantments firing,
high Charges converts that economy into raw damage. It is the only class where
stacking the signature changes what the weapon is *for*.

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Darts | 190 | 9 | 15 | `Light ×1` |
| Purity | Bandolier | 190 | 9 | 15 | `Charges ×1` → 4 throws |
| Control | Harpoon | 190 | 9 | 15 | `Drag ×1` |
| Support | Softening Javelins | 190 | 9 | 15 | `Softening ×1` |

- **Drag** — a hit pulls the target *toward* the thrower, the exact inverse of
  the halberd's Push, setting up your own axe and sword line.
- **Softening** — a hit strips 3 of the target's Block per stack for a turn.
  Deliberately distinct from the axe's `Splitting`: Splitting lets *your*
  weapon through, Softening opens the target up for **everyone**. Same
  ceiling, opposite beneficiary — the Control/Support line exactly.

### Caster classes do not take this frame

Staff and wand both carry baseline `Cast ×1`, but their four weapons differ by
**effect** and **shape** rather than by role (§1.3, §1.4). Forcing them into
Efficiency / Purity / Control / Support would be redundant — a debuff staff is
already control and a buff staff already support, and "more of the signature"
means nothing when the signature *is* which effect you cast.

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

Each carries `Cast ×1`, so the stacking model applies here too: `Cast ×2`
applies the effect at double level. **Which** effect a staff casts is a field
on the weapon, not a modifier — the modifier only says how hard it lands.

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

Wands carry `Cast ×1` like staves; the shape is a field, the stack is the
damage multiplier.

### Friendly fire is on, at half damage, both sides

A shape hits **every actor inside it except the caster** — allies included, on
either side of the fight. Allies take **half** damage.

Full-strength friendly fire is the sharper mechanic, but it demands a
placement-scoring AI be *good immediately*, or wand enemies spend the game
detonating their own side and the fight reads as broken. Half damage makes the
AI's job forgiving: a mediocre scorer that clips one ally is merely
inefficient, not suicidal. It buys the right to ship a simple version and
refine it, instead of needing the finished thing on day one.

Why keep it at all:

- **It prices positioning in movement**, which is the game's actual currency.
  Spreading to dodge a cone costs movement; regrouping afterwards costs more.
  That is the central resource doing what it already does everywhere else,
  not a tax bolted on beside it.
- **It is most fun when the enemy suffers it** — baiting a wand caster into
  its own line is a real play. That only reads as skill if avoiding it is the
  AI's normal behaviour and being baited is the exception, which is exactly
  what the scorer below provides.
- **The marching rules already cover the worst case.** A single-file marching
  line is what a Nova would delete, but marching is revoked the moment a live
  enemy is sighted, so the formation dissolves on contact. What remains is that
  a party can still arrive *clustered*, and the opening blast punishes that —
  a good lesson rather than a cheap shot.

### The placement scorer

Smaller than it sounds, and mostly code the player's targeting needs anyway:

1. Enumerate candidate placements — target points for Blast, directions for
   Cone and Beam, fixed at self for Nova.
2. Score each as `enemies hit − (allies hit × 1.5)`.
3. Cast only if the best score is positive; otherwise fall back to moving.

Structurally this is `EnemyAi.SelectTarget` one level up: the same
score-and-pick over a small candidate set, with the shape geometry shared with
the player's own casting. It scores **enemy** casts only — the player decides
their own placements.

### The scoring weight is a policy, not the damage fraction

Allies take **half** damage but are scored at **1.5×**. The two numbers are
deliberately different and must not be reconciled.

Half is the mechanical reality. Scoring at half would make the AI an expected-
value calculator, and an EV calculator fires on a 1:1 trade — full damage to one
of yours against half to one of mine is, strictly, a marginal gain. It is also
what makes baiting worthless: if even trades are acceptable, you can force a
self-clip just by standing near their group, and the play stops being a play.

At 1.5× the policy reads "hit at least twice as many of them as of us":

| Hits | Score | Casts? |
| --- | --- | --- |
| 1 enemy, 1 ally | −0.5 | No |
| 2 enemies, 1 ally | +0.5 | Yes |
| 3 enemies, 2 allies | 0 | No — ties lose |
| 3 enemies, 1 ally | +1.5 | Yes |

So the AI's *default* behaviour is to refuse anything short of clearly
favourable, and forcing a bad cast takes genuine positioning — which is exactly
what makes it feel like the player's doing rather than the AI's failure.

Half-damage friendly fire then backstops the cases the scorer gets *wrong* —
mispredicted movement, an ally stepping into a shape after it was scored —
rather than licensing sloppiness.

**Sequencing.** Wands ship in Phase 1 with the friendly-fire constant **off**.
It flips on once the scorer exists, alongside the kiting AI that ranged and
wand enemies need regardless.

## 1.5 Uniques

A unique is a named weapon with a stack spread the four standard variants
cannot produce. It needs **no new mechanism** — only a name, a base statline,
and a modifier multiset:

| Unique | Class | Modifiers | Reads as |
| --- | --- | --- | --- |
| The Bulwark | Sword | `Block ×4` | Absorbs 12; nothing else |
| Widowmaker | Dagger | `CritWindow ×3, CritMultiplier ×3, CritSunder ×2` | Crits on 17+, for ×5, sundering deep |
| Hoplite's Wall | Spear | `Brace ×3, Pin ×2` | Three retaliations, each pinning hard |
| Stormcrow | Ranged | `Longshot ×3, Overwatch ×2` | Two held shots, brutal at full range |
| Feathered Death | Throwing | `Charges ×3, Light ×2` | Six cheap throws a turn |

Uniques drop from the deepest repeat-kill tiers (Phase 3). Because they are
just stacks, a unique that turns out overtuned is a data edit, not a code
change — and the per-modifier caps in §1.1 mean no unique can escape the
balance envelope by construction.

## 1.6 Crit riders — a crit leaves a mark

A crit today just doubles damage — a number occasionally getting bigger, with
nothing left behind. Crits should *land an effect*, which is what lets them
fill the Control and Support roles (§1.2) rather than being pure damage
variance.

Two new status effects carry it, both using the existing decaying model —
level N, ticks down one per turn, removed at zero, so they always self-clear:

| Effect | Per level | Notes |
| --- | --- | --- |
| **Sundered** | Target takes **+2 damage** from every source | Getting crit opens you up |
| **Weakened** | Target deals **2 less damage**, floored at 1 | Getting crit rattles your swing |

Both cap at level 4 (`+8` / `−8`). The floor mirrors Block's existing "never
below 1 taken" rule (`CombatRules.cs:58`), so nothing can be reduced to
harmlessness.

### Riders are per weapon, not per class

Each rider is **its own modifier**, so a weapon that has one carries it as
stacks like anything else:

| Modifier | Trigger | Applies |
| --- | --- | --- |
| `CritWeaken` | Crit | `Weakened` at the stack count — the target deals less |
| `CritSunder` | Crit | `Sundered` at the stack count — the target takes more |
| `BlockWeaken` | Successful block | `Weakened` at the stack count |

Making them separate modifier types rather than one `OnCrit` with a payload
keeps the §1.1 rule intact: **a stack is a count, never a value carrying
something else.** It also lets one weapon carry several, and lets the same
effect arrive on different triggers — `Weakened` from a dagger's crit or a
shield's block, resolved identically once applied.

Riders are the natural fill for the **Control** and **Support** roles (§1.2) —
`CritWeaken` degrades the enemy, `CritSunder` helps everyone else — which is
exactly how the dagger uses them. Not every class needs to fill those roles
with riders, but crit-flavoured classes will.

`CritSunder` is the deliberate combo: A crits to open a target, then C's axe
cashes it in for double value across the whole cleave. That is the first real
reason for the party to focus one enemy, and the Weakspot Stiletto exists to
make it a build rather than an accident.

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
5. on a crit, apply `CritWeaken` / `CritSunder` stacks to the defender
```

## 1.7 Code impact

### The modifier model replaces `WeaponAbility`

Today a weapon holds `IReadOnlyList<WeaponAbility>` where
`WeaponAbility(AbilityType, int Value)` carries the value inline, and lookup is
`GetAbility(type)` returning at most one (`Weapons.cs:21,33`). Stacking makes
the value **derived**, so the pair becomes:

```csharp
enum ModifierType { Brace, Block, CritWindow, CritMultiplier, Cleave, Charges,
                    Longshot, Light, Riposte, Push, Drag, Splitting, Overwatch,
                    Rout, Pin, Softening, CritWeaken, CritSunder, BlockWeaken,
                    OnHitPoison, Cast,
                    Momentum }                             // Momentum: enchantment-only

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
variants and uniques the same operation: a stack landing on an already-maxed
modifier is a no-op rather than a special case. (Enchantments are a separate
system with their own dials — §3.3.)

`Resolve` returns a raw number; whether that number is *absolute or
proportional* is the modifier's own business. `Light` resolves to a percentage
applied to the weapon's cost, so `Weapon.ResolvedCost` is
`Cost * (100 - Modifiers.Value(Light)) / 100`, computed once and cached rather
than recomputed per swing — the HUD and the movement gate must agree on one
number.

**Migration must preserve the shipped values.** Today's dagger carries
`CritRange 4`, and `CombatRules` resolves the crit threshold as `20 - value`
(`CombatRules.cs:35`), so it crits on **16+** — 25% of swings, pinned by
`CombatRulesTests.cs:28`. The sword blocks 3, the spear braces once.

One parity break is deliberate: `CharStartingWeaponIdx` is `{0,1,2,2}` and
becomes dagger/sword/axe/staff (§2.1), so
`CombatRulesTests.StartingWeapons_MatchPrototype` needs its second assertion
updated. The first — Dagger, Sword, Spear at indices 0–2 — still holds, since
new classes append after them.

Sword, spear and staff map to `×1` directly, preserving their shipped values.

**The dagger is a deliberate exception.** It becomes `CritWindow ×1` — crit on
19–20, not the prototype's 16+ (§1.2). Two assertions in
`CombatRulesTests.cs:28` change with it. Narrowing the baseline is what keeps
crit frequency inside a 10–30% band across the whole game and leaves the dagger
four stacks of headroom instead of one.

### Everything else

New `StatusEffectType` members: `Ward`, `Poison`, `Mire`, `Sundered`,
`Weakened`.

`Weapon` gains `WeaponClass` (the eight above), `AreaShape?`, and
`StatusEffectType? CastEffect` — which effect a staff or wand applies is a
*field*, not a modifier; the modifier only says how hard it lands. Phase 3's
drop tables key off `WeaponClass`.

`ActorState` gains its own `ModifierSet` for **innate** modifiers (§4.3), and
every resolution site reads weapon **plus** innate rather than weapon alone.
Doing this in Phase 1 rather than retrofitting it in Phase 4 is much cheaper —
the same call sites are already being rewritten for stacking.

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
and wand enemies want the opposite — hold distance and kite — and wands
additionally need the placement scorer from §1.4. That is real work, not a
parameter, and it is why ranged/wand enemies are an open question below.

---

# Phase 2 — Progression

## 2.1 Innate stats are fixed at creation

Each party member gets a permanent spread of **STR / DEX / CON / INT**. Stats
never rise. They are pure *rate multipliers*: you level what you use, at the
speed your nature allows. This is what makes party composition matter for the
whole run — D will always be the better caster, no matter how long C swings a
staff.

Starting spreads (20 points each, tuning targets):

| Member | STR | DEX | CON | INT | Starts with | Teaches |
| --- | --- | --- | --- | --- | --- | --- |
| A | 4 | 8 | 4 | 4 | Dagger | Crit windows |
| B | 7 | 5 | 5 | 3 | Sword & shield | Block |
| C | 7 | 4 | 6 | 3 | **Axe** | Cleave, and beating armour |
| D | 3 | 4 | 5 | 8 | **Staff of Renewal** | Buffs, mana |

The axe goes to C, the only member whose STR 7 suits it. D gives up the second
spear for a staff — at STR 3 they would never level a martial weapon well
anyway, and a party with no caster never discovers half the game.

**D carries a debuff staff in the bag.** Renewal equipped, Blight or Mire in an
inventory slot, so the player meets both halves of casting and learns the swap
mechanic getting to the second one.

### Brace is taught from the receiving end

Dropping the spear costs the party its `Brace` demonstration — but spear dummies
already brace against the party (`TurnSystem.NotifyCharacterMoved`), so walking
into one's reach still costs a free poke. Learning a threat zone by being
punished by it, then finding a spear and turning it around, is a better first
lesson than owning one from the start. Spears remain an early, common drop.

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
defeat of the same dummy drops a **deeper weapon** — more modifier stacks, and
past a threshold a unique.

Repeat kills do **not** drop enchantments. Enchantments are applied at the
enchanter between runs (Phase 6), not found in the dungeon — they are the
chosen half of itemisation, and finding them at random is what would make them
feel farmed rather than built.

This makes the resurrection timer a deliberate farming rhythm rather than
flavour — camp a dummy to deepen its drops, at the cost of the turns you spend
waiting.

**Nothing is handed over at the time.** `DefeatCount` is the quality of the
drop that dummy is *carrying*; you collect it only by killing it permanently on
the way out, after the boss has stopped resurrection (§4.4).

**Uniques sit at the deep end.** Past a threshold (5 defeats, tuning target)
the roll can return a unique of that class (§1.5) instead of a variant.

So the dungeon supplies **bodies** and the enchanter supplies **souls**. Depth
buys you a better weapon to invest in; runs survived buy you the investment
itself (Phase 6). Neither ladder can be climbed by grinding the other.

## 3.3 Enchantments are their own system

**Enchantments are not modifier stacks.** They are a parallel system with their
own dials, their own scaling stat, and their own budget. Folding them into
`ModifierSet` would make them a weapon upgrade; keeping them separate makes
them a **build axis**.

### The build this exists for

A high-INT character can stay a ranged caster — or stack enchantments on a
dagger and become close-range damage. That works only if enchantment power is
divorced from weapon proficiency:

- The wizard's dagger proficiency is *terrible*. Dagger XP scales with DEX
  (§2.2), and a wizard has DEX 4. The weapon's own numbers stay low all run.
- But enchantment potency scales with **INT**, and how many they can carry
  scales with **max mana** — the pool INT grows fastest.

So the same dagger is a weak weapon in a fighter's hand and a delivery system
in a wizard's. The fighter cannot copy the build: with INT 3 and a small mana
pool, they can afford one light enchantment, not five.

### Four dials, plus a tier

| Dial | Meaning |
| --- | --- |
| **Lock** | Max mana reserved while equipped; returned in full on unequip |
| **Trigger cost** | Mana spent each time it fires |
| **Condition** | What fires it — on hit, on crit, on kill, on being hit, on cast |
| **Potency** | Effect magnitude, scaled by `rate(INT)` from §2.1 |
| **Tier** | 1–3; raised by re-servicing the same enchantment (Phase 6), lifting lock *and* potency together |

Insufficient mana means it simply **does not fire** — no failure state, no
penalty, just a resource gate.

### Max mana is the enchantment budget

The sum of equipped locks may not exceed max mana. That single rule does the
balancing:

- **Max mana is capacity.** Grown by spending mana, scaled by INT (§2.2), so
  the wizard's carrying capacity compounds over a run and the fighter's does
  not.
- **Locking competes with firing.** Lock your whole pool and you have nothing
  left to trigger with. The optimum is somewhere below full, and where exactly
  depends on how often your conditions fire — an on-hit build wants a large
  spendable remainder, an on-kill build can afford to lock deeper.

No cap is needed anywhere in this system because the budget *is* the cap.

### Class-agnostic by design

Any enchantment goes on any weapon. That is the whole point — the wizard's
dagger, the fighter's warstaff. Nothing keys off `WeaponClass`.

### Starting set

Potency values are before INT scaling.

| Enchantment | Lock | Trigger | Fires on | Effect |
| --- | --- | --- | --- | --- |
| Arcane Edge | 30 | 8 | Hit | Bonus damage — **the wizard-DPS core** |
| Vampiric | 20 | 10 | Crit | Heal the wielder for damage dealt |
| Flaring | 15 | 5 | Hit | Apply Poison |
| Siphon | 20 | 0 | Kill | Restore mana — the engine that sustains the rest |
| Weightless | 25 | 5 | Attack | Attacks cost less movement |
| Warding | 30 | 40 | Lethal damage | Survive at 1 HP instead |
| Echoing | 20 | 15 | Attack | The weapon's class feature triggers once more |
| Shattering | 25 | 10 | Crit | Crit riders (§1.6) land one level deeper |
| Momentum | 30 | 10 | Kill | Refund part of the swing's movement cost |

Arcane Edge and Siphon together are the close-range wizard: hit for INT-scaled
damage, kill to refund the mana that paid for it. Neither needs a bespoke
ceiling — Siphon only pays out on kills, and Arcane Edge drains a pool that
refills only from *unspent* movement.

**Momentum is the case §1.1 sends here.** As a weapon modifier a movement
refund loops: refunded movement buys the next swing, which refunds again. As an
enchantment it cannot, because mana regenerates only from movement left unspent
at end of turn (`PartyMemberState.RegenManaFromUnusedMovement`) — spending the
refund to keep swinging is exactly what stops the mana coming back.

## 3.4 Consumables

A third item category alongside weapons and enchantments, and the only one that
is spent.

**Using a consumable costs movement**, on the same principle as swapping
(§1.2): anything that changes your situation mid-turn comes out of the same
budget as moving and swinging. A free heal in a game about movement economy
would be a hole straight through the middle of it.

**They occupy inventory slots**, which is where they get interesting. The 24
party-wide slots (§4.4) are already contested between weapons and the haul —
consumables make it a three-way trade. Every potion you carry down is a weapon
you did not bring *and* a drop you cannot carry out, and the extraction is when
you feel both.

Nothing about their contents is specified yet — see open questions.

## 3.5 Code impact

New `Logic/Enchantment.cs` and `Logic/LootTable.cs` (own RNG stream:
`mapSeed ^ LootSalt`).

`Enchantment` is its **own type**, not a `ModifierType` — lock, trigger cost,
condition, base potency, tier. `Weapon` holds
`IReadOnlyList<Enchantment> Enchantments` alongside its `ModifierSet`; the two
never mix. A handful of enchantments happen to grant a modifier stack as their
*effect* (Weightless, Shattering), but that is an effect they apply, not what
they are.

`PartyMemberState` gains `UsableMaxMana = MaxMana − Σ equipped locks`, and
equipping must reject a weapon whose locks would exceed max mana.

`TurnSystem` needs a **trigger dispatch point** per condition — on hit, on
crit, on kill, on being hit, on cast. Phase 0's unified attack resolver is
where hit/crit/kill all pass through, so this is one call site rather than the
several it would have been before.

Note `Weapon` is a `record` shared by reference from `GameConstants.Weapons`
today, so dropped instances must be **copies**, never mutations of the shared
table — both `ModifierSet` and the enchantment list should be immutable.

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
weapon farming at one dungeon visit.

The reset is also what Phase 6 is built on: the dungeon is the renewable half
of the game and your gear is the permanent half.

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

## 4.3 The boss floor

Every dungeon has exactly one boss, on a floor rolled **between 5 and 10 on
entry**. The boss floor is the dungeon's bottom, so dungeon depth varies per
visit — a short, sharp descent or a long grind, decided before you take the
first step.

Own RNG stream, same pattern as everything else:

```
bossFloor = 5 + new Mulberry32(mapSeed ^ BossSalt).NextInt(0, 5)
```

**The roll is not disclosed.** It is fixed at entry but unknown to the player,
who learns the depth only by reaching it. That matters — see the open question
about re-entry scumming below.

### Killing the boss stops resurrection

Dummies revive after 10 turns today (`TurnSystem.cs:532`). Once the boss is
down, they stop: the dungeon becomes **finite and clearable** for the rest of
the visit.

That is the reward, and it also creates the run's central tension. The
repeat-kill ladder runs *on* resurrection — `DefeatCount` only advances because
dummies come back (§3.2). So:

- **Before the boss**, the dungeon is an infinite farm. Deeper weapon drops,
  uniques, as long as you have the turns and the health to keep cycling.
- **After the boss**, it is a finite clear. Whatever is left, you take once.

Killing the boss is what makes a run *successful* (§6.3), and it is also what
ends your farming. Deciding when you have farmed enough is the run's real
decision, and it is entirely the player's to make.

### A boss is a dungeon's theme, and the theme is a modifier

Each dungeon is themed, its boss embodies that theme, and the theme is
**one guaranteed modifier**:

- The boss carries that modifier **innately**, regardless of what it wields.
- **Every weapon it drops carries it too**, on top of whatever the weapon's own
  class modifier is.

That makes boss drops structurally unlike anything else in the game. A spear
dropped by a Block-themed boss is `Brace ×n, Block ×n` — an off-class hybrid
you cannot roll, cannot farm, and cannot craft. Ordinary drops respect the
class convention (§1.2); boss drops are the one thing that breaks it.

It also settles boss drops versus repeat-kill uniques — they are different
axes, and both should exist:

| Source | Gives you |
| --- | --- |
| Repeat-kill depth (§3.2) | Extreme stacks of the weapon's **own** class modifier |
| Boss drop | A guaranteed **off-class** modifier from the dungeon's theme |

And it gives dungeon selection a point. You run the Block dungeon because you
want Block on something that has no business having it.

### The tutorial dungeon: the stone golem

The dungeon shipped today is the **tutorial**, and its theme is **Block**. Its
boss is a stone golem: innate Block whatever it happens to be holding, and
every weapon it drops comes away with Block on it.

### Two floors deep

The tutorial ignores the 5–10 roll: its boss sits on **floor 2**, fixed. It is
the one dungeon in the game with a hardcoded depth, and the special case earns
itself twice over.

It teaches the complete loop in miniature — descend once, fight the boss,
extract back through a single floor — so every dungeon mechanic is demonstrated
in the shortest run that can contain them all. And since the dungeon is
consumed by beating it, a short one means a fast turnaround into the hub rather
than a long commitment a first-time player cannot yet evaluate.

Two floors is also enough to seed the **first stable**. Phase 6's rotation
needs several weapons to work at all, and a couple of floors of dummies farmed
lightly is where they come from.

The pacing lines up on its own: one successful tutorial run ticks the enchanter
once (§6.3), and attaching the first enchantment costs exactly one run (§6.2).
Finishing the tutorial buys your first enchantment, immediately. Wear from two
floors of fighting should be tuned to cover it — the tutorial ought to end with
the player able to use the enchanter, not merely able to look at it.

### The tutorial is consumed by beating it

Unlike every other dungeon, the tutorial is **available exactly once**. Kill
the golem and it is gone from the hub for good; fail, and it is still there.
Every other dungeon is picked from the hub and can be run indefinitely.

That makes it the one place where **rushing the boss permanently costs you
something**. Farm the tutorial and you leave with Block-themed weapons no other
run in the game will hand you cheaply; dive straight to the golem and that
opportunity closes behind you.

Which is exactly the decision the whole game is built on (§4.4), delivered once
in miniature where it is cheap to learn. The tutorial should **say so plainly**
— this is the one dungeon the game is allowed to warn you about, and a
first-time player who loses the theme without knowing the rule has been cheated
rather than taught.

Block must therefore remain obtainable elsewhere. A rushed tutorial should cost
a good head start, never a permanently closed build.

The golem is a good first boss because it teaches the one thing flat Block
makes true. Block absorbs a *flat* amount and never reduces a hit below 1
(§1.1, a deliberate exception to the proportional rule), so many small hits are
terrible against it and few large ones are fine.

The starting party spans that range deliberately — **axe 18, dagger 15, sword
10**, plus a healer. Give the golem `Block ×3` innately and the maths does the
teaching by itself: the sword's 10 lands for 1, the axe's 18 for 9, and a
dagger crit for 21. The player discovers that by swinging, not by reading a
tooltip.

That is the lesson the tutorial should land: **damage per swing beats swings
per turn against armour**, and picking the right party member for the target is
the whole game.

The pairing runs deeper than the tutorial. The axe's wildcard modifier is
`Splitting`, which ignores Block outright (§1.2) — so the tutorial dungeon is
themed on the exact defence the axe class exists to break. A player who takes
that lesson and hunts a Reaver has understood the game.

### Innate modifiers

"Block regardless of its weapon" means modifiers must be able to live on an
**actor**, not only on a weapon. `ActorState` (Phase 0) gains its own
`ModifierSet`, and resolution reads weapon *plus* innate.

That is generally useful rather than a boss special case — it is also how
armour, monster traits, and any future innate would work, on either side of the
fight. `CombatRules.ResolveAttack` currently reads Block off the defender's
weapon alone (`CombatRules.cs:55`); it needs the defender's actor too, which
Phase 1 already changes the signature for (Sundered/Weakened).

## 4.4 Fighting your way out

**The boss does not end the run.** Killing it turns the party around: you climb
back out through every floor you descended, killing the monsters one last
time — and that final kill is when you collect their drops.

### `DefeatCount` sets the quality; the last kill collects it

This resolves when loot actually materialises. Repeat-killing a dummy during
the descent does not hand you anything — it raises that dummy's `DefeatCount`,
which is the *quality* of the drop it is carrying. Because the boss stopped
resurrection, killing it on the way out is permanent, and permanent death is
what yields the goods.

Farm a dummy five times on the way down and it is holding a tier-5 drop. You
bank the quality going in and harvest it coming out.

### The risk curve inverts

You are strongest descending and weakest climbing out — resources spent, HP
gone, movement banked away — and that is precisely when the entire payout sits
on the board. Every extra farming cycle makes the drop better *and* the
extraction harder, with the same currency paying for both.

The gauntlet does thin as you climb, since nothing revives any more. The
ascent is a diminishing fight, not an escalating one.

Navigation is not the challenge: floors persist within a visit (§4.1), so the
map and the fog are already known. The way out is a combat problem.

### You cannot carry it all

Inventory becomes **6 slots per character, 24 party-wide** — replacing today's
3, which was a testing remnant rather than a design (`PartyMemberState.cs:27`).
Exact number is a tuning target; the structure is the decision.

Crucially there is **no separate haul bag**. Loot goes in the same slots as
your weapons, so every drop you pick up climbing out costs you a weapon you
could have been swinging. A greedy hauler is a worse fighter, which puts the
carry limit exactly where the tension belongs — on the extraction itself,
scaling with how much you are trying to leave with.

A deep farm banks far more than 24 drops, so the ascent is **targeted, not
exhaustive**. You do not clear the dungeon on the way out; you revisit the
dummies you invested in and leave the rest standing. Since `DefeatCount` is
visible on the nameplate (§4.5), that choice is informed.

### Skipping floors is allowed

You can run a floor rather than fight it. This needs no gate — three systems
already price it:

- **Moving through a threat is not free.** Brace fires from every zone crossed
  (`TurnSystem.NotifyCharacterMoved`), and you are running it at low HP with a
  spent movement budget.
- **Skipping forfeits the drops.** Abandoning a floor abandons everything you
  banked there, so the decision regulates itself: fight through what you
  farmed, run past what you did not.
- **Wear guards against rushing.** A party could dive a floor-5 boss and skip
  straight out for a free service tick — but they would surface with almost no
  wear, and wear is what the enchanter consumes (§6.1). Ticks bought without
  fighting buy nothing.

A percentage-of-enemies gate was considered and rejected: it would force you to
kill dummies whose drops you do not want, which is busywork, and it overrides
the self-regulation above rather than adding to it.

### Wear charges on the way out

Wear accrues from swinging (§6.1), so the extraction is also what charges your
weapons for the enchanter — including the improvement roll, which scales with
wear brought in (§6.4). The fight out pays twice.

## 4.5 Code impact

New `Logic/DungeonState.cs` holding `Floor[]`, each with its map, actor list,
and `FogState`, plus `BossFloor` and a `BossDefeated` flag. `DungeonScene`
currently builds one map in its constructor and holds the party, enemies and
fog directly — that becomes a swap of the active floor, tearing down and
rebuilding geometry on transit.

The resurrection block in `TurnSystem.StartPlayerTurn` (`TurnSystem.cs:530`)
gets gated on `!BossDefeated`, which is the same flag that turns `EnemyDefeated`
into a drop rather than just a `DefeatCount` increment.

**The HUD must show `DefeatCount` on the enemy nameplate.** Farming with no
visible reward until the extraction would read as broken otherwise — the player
needs to see the quality building on each dummy to make the stop-farming call
deliberately, and to pick targets on the way out.

`PartyMemberState.Inventory` goes from 3 slots to 6 (`PartyMemberState.cs:27`),
and `DungeonHud`'s inventory panel has to grow with it. Slot 0 stays the
equipped weapon; the swap keys currently hardcode slots 2 and 3, so the input
handling generalises.

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

# Phase 6 — Between runs: wear, the enchanter, the hub

The dungeon resets every time you leave (§4.1). Your party and your gear do
not. Phase 6 is the layer that turns that asymmetry into progression: the
dungeon is the renewable resource, and a weapon is the thread running through
a whole campaign.

```
descend → fight (weapons accrue wear), gather, survive
        → leave; the dungeon resets
        → hub: deposit a worn weapon with the enchanter, re-equip from the stable
        → descend again; the deposited weapon works off its time
```

## 6.1 Wear is earned, not suffered

**Nothing breaks.** Wear is not damage to repair — it is proof of use, and it
is the enchanter's raw material. Swinging a weapon accrues wear; the enchanter
consumes it to attach an enchantment.

That matches the philosophy the rest of the game already runs on: health levels
from being healed, mana from being spent, proficiency from damage dealt. A
weapon becomes enchantable by being *used*, not by being found.

Consequence worth stating plainly: **you cannot enchant a weapon you have not
fought with.** A fresh unique straight off the floor is inert until it has done
some work.

## 6.2 The enchanter takes time, measured in runs

Depositing a weapon starts a service clock. Service time is
**current enchantment count + 1**, so each enchantment costs more downtime than
the last:

| Adding enchantment # | Runs in service | Cumulative |
| --- | --- | --- |
| 1st | 1 | 1 |
| 2nd | 2 | 3 |
| 3rd | 3 | 6 |
| 4th | 4 | 10 |
| 5th | 5 | 15 |

Fifteen runs to a fully enchanted weapon makes it a long-term project, and
makes a 5-enchantment weapon rare **by construction** rather than by drop rate.

This is the second, independent cost on enchantment power. Max mana says *how
much you can carry at once* (§3.3); service time says *and it is in the shop
while you carry it*. Your best weapon is routinely unavailable.

### It forces a stable

You cannot run one weapon. Every drop you would have vendored becomes rotation
depth, which is what finally justifies the loot volume.

Proficiency makes the rotation cheap in the right way: it is per **class** per
character (§2.2), not per weapon, so a same-class backup swings at your full
skill. The rotation costs you the *item*, never your progress — and it creates
a real stable-building decision between hoarding same-class backups for
continuity and diversifying at the price of swinging at low proficiency.

### Only the newest work gets worked on

The enchanter progresses the **two most recently deposited** weapons. Depositing
a third stalls the oldest.

That is a soft cap that enforces itself: dumping the whole stable achieves
nothing, so you choose what matters. It stays legible in a way a pure
last-in-only rule would not — "the smith is working on these two" reads
correctly at a glance, where a silently stalled queue reads as a bug.

## 6.3 What counts as a run

**Entering the dungeon ticks nothing.** Otherwise the optimal play is a stack
of twenty-second entries to burn the service clock without ever fighting.

**A run is successful when you kill the boss** (§4.3) and fight your way back
out (§4.4). Nothing else counts as a win.

The tick and the spoils come apart, and they should:

| Outcome | Service tick | Drops |
| --- | --- | --- |
| Entered, left above floor 2 | **None** | — |
| Reached floor 2, left without the boss | **1 (mercy)** | — |
| Killed the boss, died on the way out | **1** | **None** |
| Killed the boss and extracted | **1** | **Everything you killed climbing out** |

The tick rewards the achievement; the drops require the extraction. Beating the
boss and then dying in the stairwell still moves your weapons along at the
enchanter — you just walk away with nothing to put on them.

The mercy tick exists for exactly one purpose: **a losing streak must not
freeze the workshop.** Several failed runs in a row would otherwise stall every
weapon in service at the moment you most need them back.

It is deliberately not a consolation prize — a mercy tick advances the service
clock and nothing else. You still lose the run's spoils. Succeeding is strictly
better; failing merely is not compounding.

Reaching floor 2 is the bar because it cannot be cleared by walking in and
turning around, but it also does not demand a good run.

Note the mercy tick and the success tick are currently worth the same. Since a
boss can sit anywhere from floor 5 to floor 10, that is deliberate for now —
see the open questions.

In the **tutorial** the two nearly coincide, since its boss is on floor 2
(§4.3) — reaching the mercy bar means standing in front of the golem. That is
intentional: a first run should not be able to come away with nothing.

## 6.4 Service can improve the weapon

Working a weapon has a **low chance of adding a modifier stack** — sometimes
you make a thing better while working on it.

This is the mechanism that most directly delivers cross-run weapon progression.
Enchantments are attachments; a modifier is the weapon itself getting better.
An heirloom that has been through twenty services can genuinely out-roll a
fresh drop, which is the payoff for loyalty and the answer to the upgrade
treadmill — your investment is not stranded when a better base drops.

Rules that keep it coherent:

- **The chance scales with wear brought in.** The weapon you actually fought
  with improves; the one you carried does not. Same principle as everywhere
  else in the game.
- **It adds a stack to a modifier the weapon already carries**, biased toward
  the class modifier. A spear gets more spear-like; it does not sprout
  `Charges`. Weapon identity survives.
- **The §1.1 caps still bind.** A maxed modifier cannot be improved, and if
  every modifier is at ×5 the roll simply does not happen.

## 6.5 Transferring an enchantment

Enchantments can be moved to a new weapon at the enchanter, costing the same
service time as attaching one and dropping the enchantment a tier. Without
this, a lucky late drop would strand everything you invested — with it, the
body is replaceable and the soul is the thing you built.

## 6.6 Code impact

New `Logic/Wear.cs`, `Logic/Enchanter.cs`, and a `CampaignState` that lives
*outside* `DungeonState` (§4.3) — party, stable, enchanter queue, run counter.
The existing distinction does the work for us: `DungeonState` is discarded on
leaving, `CampaignState` is not.

`Weapon` gains `Wear`; the unified attack resolver from Phase 0 is the single
place it accrues.

Phase 5's save format grows a campaign section, and it becomes the *outer*
document — a save with no dungeon in progress is now a valid state, which it
is not today.

`TurnSystem` needs a run-outcome signal. `GameOver` already fires on a party
wipe (`TurnSystem.cs:620`); leaving the dungeon and reaching a new floor are
new events on the floor-transit path from Phase 4.

# Open questions

- **Ranged and wand enemies.** `EnemyAi.PlanMove` only knows how to close.
  Those classes need kiting — hold range, back off when approached — plus the
  placement scorer for wands (§1.4). Until both exist, `EnemyPlacer` should
  roll only the six close-range classes, and the friendly-fire constant stays
  off. Half-damage friendly fire is what lets the first version of that scorer
  be merely adequate rather than finished.
- **STR has four classes to DEX's three.** STR covers axe, spear, throwing and
  sword; DEX covers dagger, bow and sword. Both have close and far answers, so
  nothing is *broken*, but STR simply has more room to move. Fixing it means
  either a ninth class on DEX, or moving throwing to DEX (thrown knives are
  plausibly dexterous, though it costs the STR-throws-heavy-things read), or
  accepting that STR is the martial-breadth stat and DEX the precision one.
- **What are consumables, actually?** §3.4 fixes that they cost movement and
  occupy slots; their contents, where they come from (dungeon drops or the
  hub), and whether they are craftable at the enchanter are all open.
- **Is half the right fraction?** Half damage is the forgiveness knob that
  makes a simple scorer shippable; once the scorer is good, full friendly fire
  may be the better game. Worth revisiting rather than treating as final. Note
  the 1.5× scoring weight is a separate dial and does not have to move with it.
- **Should the ally weight vary per target?** A flat 1.5× ignores that clipping
  a nearly-dead ally, or a healer, costs more than clipping a fresh dummy.
  Weighting by remaining HP or by role would be more accurate — and more
  expensive, and harder to predict when baiting. Flat first.
- **Overwatch and enemy-turn reactions.** Overwatch fires during the enemy
  phase, as braces already do. Whether a character can hold *both* an overwatch
  shot and a brace in the same turn needs a ruling before Phase 1 codes it.
- **Does a shallow boss pay the same as a deep one?** The boss floor rolls 5–10
  on entry and a win ticks service once either way, so a floor-5 dungeon is
  strictly cheaper than a floor-10 one for the same reward. Not disclosing the
  roll stops players re-entering to scum for a shallow dungeon — you cannot
  tell without descending — but if a hint ever surfaces the depth, ticks should
  scale with boss depth instead.
- **The golem's statline.** Themed drops are settled (§4.3) but the fight is
  not: HP, movement budget, whether it rolls a weapon like a dummy does, and
  how many `Block` stacks it carries innately. A slow, heavily armoured
  construct is the obvious shape, but slow enemies are trivially kited once
  ranged weapons exist (§1.2) — worth checking the tutorial boss does not
  become a joke in Phase 1.
- **What does the hub look like?** Dungeon selection now definitely exists
  (§4.3): a list of themed dungeons, the tutorial present until beaten. Nothing
  else about the hub is specified — how dungeons are discovered, whether the
  list grows, whether themes repeat. Phase 6 scope.
- **Which debuff staff does D carry?** Blight (Poison) is the more legible
  demonstration; Mire (movement tax) is the more on-theme one for this game.
- **Can you re-enter after killing the boss?** Leaving resets the dungeon
  (§4.1), which re-rolls the boss floor and revives everything. So a cleared
  dungeon cannot be returned to — clearing it is worth doing only for what you
  can carry out in that visit.
- **Is 24 the right carry limit?** The structure is settled — unified with the
  loadout, no haul bag — but the number is a guess. Too high and the extraction
  stops forcing choices; too low and a deep farm is mostly wasted. Needs play.
- **Does wear cap?** If it accumulates without limit, a long-serving weapon
  eventually has enough for any enchantment forever and wear stops being a
  gate. A ceiling — or wear being fully consumed per attachment — needs
  deciding.
- **Two enchanter slots, or a strict last-in-only queue?** Phase 6 specs the
  two-newest rule as the legible middle ground, but a hard slot count is
  simpler and a pure last-in rule is harsher.
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
- Modifiers are **stacks, not values**. A Purity weapon is a second stack of
  the class signature; uniques are arbitrary stacks. One mechanism.
- **Enchantments are a separate system**, not modifier stacks — own dials
  (lock, trigger cost, condition, potency, tier), potency scaling with INT, and
  max mana as the budget. That is what lets a wizard enchant a dagger into
  close-range damage without touching their dagger proficiency.
- **Modifiers are rolled at drop; enchantments are applied at the enchanter.**
  The dungeon supplies bodies, the hub supplies souls.
- **Wear is a resource, not damage.** Nothing breaks; using a weapon is what
  makes it enchantable.
- **Service costs runs, not gold** — `enchantments + 1` runs per attachment, so
  power and availability trade off directly.
- **Entering does not tick service.** A successful run ticks; reaching floor 2
  and then failing ticks once as mercy, so a losing streak cannot freeze the
  workshop.
- **A run is successful when you kill the boss.** One boss per dungeon, on a
  floor rolled 5–10 at entry and not disclosed; the boss floor is the bottom.
- **Each dungeon is themed on one modifier.** Its boss carries that modifier
  innately whatever it wields, and every weapon it drops carries it too — the
  only off-class modifiers in the game, and the reason to choose one dungeon
  over another.
- **The shipped dungeon is the tutorial**, themed on Block, with a stone golem
  for a boss.
- **A class has a baseline plus four role weapons** — Efficiency (`Light`),
  Purity (more of the signature), Control (degrade the enemy), Support (help
  the party). The old universal "crit-specced" slot is gone; a crit class puts
  crit in its baseline instead.
- **Crit riders are per-weapon modifiers**, `CritWeaken` and `CritSunder`, not
  a per-class field — so a stack stays a count rather than a payload.
- **The dagger's baseline is `CritWindow ×1, CritMultiplier ×1`**, with its
  four weapons adding `Light`, another `CritWindow`, `CritWeaken`, and
  `CritSunder`.
- **All six martial classes are specced** — sword (`Riposte`/`BlockWeaken`), spear
  (`Push`/`Pin`), axe (`Splitting`/`Rout`), ranged (`Pin`/`Overwatch`),
  throwing (`Drag`/`Softening`). Caster classes keep their own frame: baseline
  `Cast ×1` plus four effects or four shapes, since role decomposition means
  nothing when the signature *is* which effect you cast.
- **`Pin` appears on both spear and ranged**, delivered by a brace and by a hit
  respectively — the two halves of a kiting pair.
- **Throws are cheap (15) and hard-capped by `Charges`**, not free. The cap is
  the binding constraint rather than the budget, so the class fights *and*
  keeps movement — which banks and regenerates mana, making low-Charges an
  economy weapon and high-Charges a damage one.
- **Attack cost tracks weight**: throwing 15, dagger and bow 30, sword 50,
  spear 55, axe 60. The spear was cheaper than the bow, which had a polearm
  swinging faster than an archer looses.
- **The bow is deliberately weak up close.** Damage 5 stays; the answer to an
  enemy in your face is swapping weapons, not a stronger baseline. Every stat
  owns both a close and a ranged answer, so swapping never costs progression
  rate.
- **INT solves range by borrowing.** No martial classes of its own — a high-INT
  character enchants a dagger or a bow and delivers INT-scaled damage through
  it, since enchantment potency ignores weapon proficiency.
- **Swapping costs 20 movement in combat, free out of it**, gated on the
  existing `AnyLiveEnemySeenThisTurn` signal. Free swapping would make every
  weapon's downside optional rather than only the bow's.
- **Consumables cost movement and occupy inventory slots**, making the 24-slot
  budget a three-way trade between weapons, potions and haul.
- **The starting party is dagger, sword, axe, staff** — both spears go, one to
  an axe on C and one to a staff on D, with a debuff staff in D's bag. Brace is
  taught by enemy spear dummies instead of a party spear.
- **The tutorial is consumed by beating it**; every other dungeon is picked
  from the hub and repeatable. Rushing its boss permanently forfeits its themed
  drops, which is the game's central decision delivered once in miniature.
- **The tutorial is two floors deep**, boss on floor 2 — the only hardcoded
  depth in the game. Shortest run that demonstrates every dungeon mechanic, and
  a fast turnaround into the hub since it cannot be replayed.
- **Modifiers can live on an actor, not just a weapon.** `ActorState` carries
  its own `ModifierSet`; resolution reads weapon plus innate.
- **Killing the boss stops resurrection**, turning the dungeon from an infinite
  farm into a finite clear — so winning ends your farming, and choosing when to
  stop farming is the run's central decision.
- **You fight your way out.** The boss turns the party around; you climb back
  through every floor killing everything a final time.
- **`DefeatCount` is the drop's quality, the last kill collects it.** Farming
  banks value into a dummy; you harvest it on the way out. Die in the
  stairwell and you keep the service tick but none of the spoils.
- **Carry limit is unified with the loadout** — 6 slots per character, 24
  party-wide, no separate haul bag. Loot competes with weapons, so hauling
  deep makes you weaker for the fight you are hauling through.
- **Floors can be skipped on the way out.** No gate is needed: brace taxes
  running through, skipping forfeits that floor's drops, and wear gating means
  a rushed run earns service time it cannot spend.
- **Service has a low chance of adding a modifier stack**, scaled by wear
  brought in and bounded by the §1.1 caps — this is how a weapon improves
  across runs rather than merely accumulating attachments.
- **Max 5 stacks per modifier type**, hardcoded, with types independent — no
  shared budget across a weapon. The cap is on the stack count, not the
  resolved value, so per-stack value is the only dial.
- `CritWindow` is **+1 per stack**: ×1 crits on 19–20, ×2 on 18–20, and so on.
- **The dagger's baseline is `CritWindow ×1` and its crit specialist `×2`** —
  a deliberate break from the prototype's 16+. Crit *frequency* stays inside
  10–30% game-wide so the d20 keeps mattering; `CritMultiplier` (×2 to ×7) is
  the investment axis, so crit builds are rare-and-catastrophic rather than
  constant.
- **Wand friendly fire is on, at half damage, on both sides.** Half is what
  makes a simple placement scorer shippable — a mediocre wand enemy is
  inefficient rather than suicidal. Ships behind a constant, flipped on when
  the scorer lands.
- **The scorer weights allies at 1.5×, not at the half they actually take.**
  The weight is a policy, not an EV calculation: it refuses even trades, so
  baiting a caster into its own line takes real positioning instead of just
  standing nearby.
- **If it needs a cap, it is an enchantment.** A weapon modifier has to be safe
  at ×5 by construction; anything needing a bespoke ceiling goes to the
  enchantment layer, where the mana lock and trigger cost bound it organically.
  `Momentum` moved there for exactly this reason.
- **A modifier on a value that varies across classes is proportional.** `Light`
  is −10% of the weapon's cost per stack, not a flat subtraction — attack costs
  span 20–60, and flat would zero out a dagger while barely touching an axe.

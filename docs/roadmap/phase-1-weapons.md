# Phase 1 — The martial and caster classes

> §1.2–1.5. Stack rules in [phase-1-modifiers](phase-1-modifiers.md); index in [ROADMAP.md](../../ROADMAP.md).

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

### Crit frequency is narrow in practice, not by rule

`CritWindow` scales like everything else — `forged + 5`, no cap (§1.1). The full
range it can reach:

| Weapon | Forged | Crits on | Rate |
| --- | --- | --- | --- |
| Any non-dagger | — | 20 | 5% |
| Dagger baseline | `×1` | 19–20 | 10% |
| Assassin's Fang | `×2` | 18–20 | 15% |
| Widowmaker | `×3` | 17–20 | 20% |
| Assassin's Fang, fully worked | `×6` | 14+ | 35% |
| **Widowmaker, fully worked** | `×8` | 12+ | **45%** |

**The band that matters is the top half of that table, and almost nobody sees
it.** Every stack past the forged spread has to be won one at a time from the
enchanter: a low chance per service, scaled by wear, landing on `CritWindow`
rather than on the two or three other modifiers a crit weapon carries. Five of
those in a row, on the same weapon, kept in rotation across dozens of runs.

So the 45% dagger is not a build you pick — it is an **artefact of a campaign**,
and one weapon's worth of one. For essentially the whole game a crit is a 10–20%
event and the d20 keeps mattering exactly as intended. Capping the window would
have bought that guarantee at the price of telling a player who ground for forty
runs that the thing they were grinding toward was not allowed to exist.

The investment axis is still `CritMultiplier`, which scales the same way. Every
dagger is forged `CritMultiplier ×1` and so ceilings at a ×8 multiplier; only a
unique that raised `CritMultiplier` rather than `CritWindow` — a Kris or a
Stiletto taken to `×3` (§1.5) — reaches ×10. **Frequency and magnitude are
therefore separate uniques**, and no single weapon is forged deep in both.

So a crit build is not "crit constantly" but **"crit rarely and
catastrophically"** — and the two dagger specialists split exactly along that
line — Assassin's Fang buys frequency, the baseline `CritMultiplier` carries
magnitude.

Starting at `×1` rather than the prototype's `×4` also leaves the dagger seven
stacks of headroom for depth, grafts and services to work with, instead of one.

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

**Both have the same blind spot.** Crits bypass Block entirely (§1.6), so a crit
is not a successful block and neither `Riposte` nor `BlockWeaken` triggers on
one. A crit-heavy attacker beats the whole class rather than just its
mitigation, which is the price the sword pays for scaling to `Block ×8`.

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
  shields, and it is measured stack for stack against the thing it counters
  rather than against an arbitrary number — though the two no longer share a
  ceiling now that caps scale with the forged spread (see open questions). This
  is the weapon the tutorial dungeon exists to teach you to want (§4.3).
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
that: it grants **one throw, plus one per stack**, so `×1` permits 2. Raising it
is genuinely the class feature, because the cap rather than the budget is what
binds.

Getting this backwards is easy and worth stating plainly: at a *normal* attack
cost the budget already caps you at four or five swings, so a "throws per turn"
limit would be a restriction rather than a feature. Cheap throws are what make
the cap the interesting number.

| Charges | Throws | Movement spent | Damage | Movement left |
| --- | --- | --- | --- | --- |
| ×1 | 2 | 30 | 18 | 130 |
| ×2 | 3 | 45 | 27 | 115 |
| ×5 | 6 | 90 | 54 | 70 |
| ×7 — a Bandolier at its ceiling | 8 | 120 | 72 | 40 |
| ×8 — Feathered Death at its ceiling, the deepest `Charges` in the game | 9 | 135 | 81 | 25 |

**The per-stack value is chosen so the cap stays inside the budget at every
reachable stack count** (§1.1). At the old `+2` per stack, the deepest spread
would have permitted 16 throws for 240 movement against a budget of 160 — five
of them unthrowable, which would have made the last three stacks of the class's
own signature worthless. `+1` keeps the shipped baseline of 2 throws and lands
the deepest weapon in the game at 9 throws for 135, just inside.

That is deliberate rather than lucky: `Charges` is the one modifier measured
directly against the movement budget, so if the budget or the throw cost ever
moves, this number moves with them.

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
| Purity | Bandolier | 190 | 9 | 15 | `Charges ×1` → 3 throws |
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

**A unique is a variant with exactly one of its modifiers raised to `×3`.**

That is the whole rule. Not an arbitrary spread, not several deep modifiers —
take one of the thirty-two weapons above, pick one modifier it is already
forged with, and take it to three. Everything else about the weapon is
unchanged.

| Unique | Class | Built from | Modifiers | Reads as |
| --- | --- | --- | --- | --- |
| The Bulwark | Sword | Tower Guard | `Block ×3` | Absorbs 9; nothing else |
| Widowmaker | Dagger | Assassin's Fang | `CritWindow ×3, CritMultiplier ×1` | Finds the gap on 17+ |
| Hoplite's Wall | Spear | Phalanx Spear | `Brace ×3` | Three retaliations a turn |
| Stormcrow | Ranged | Longbow | `Longshot ×3` | +3 damage a tile; lethal across a room |
| Feathered Death | Throwing | Bandolier | `Charges ×3` | Four throws, and the movement to reposition after |
| Shieldbreaker | Axe | Reaver | `Cleave ×1, Splitting ×3` | Ignores 9 Block on *everything* the swing catches |

### Why derive them rather than author them freely

**It bounds the forge without a second rule.** "One modifier at `×3`" already
implies "nothing forged past `×3`", which is what makes `×8` the universal
ceiling that every per-stack value in §1.1 is priced against. One authoring
constraint, not two.

**It keeps uniques legible.** A unique built off a variant is *a weapon you
already know, more so* — Widowmaker is a Fang that crits harder, Shieldbreaker
is a Reaver that actually splits shields. The player recognises what they picked
up. A freely-authored spread like `Brace ×3, Pin ×2` reads as a stat block; a
derived one reads as a weapon.

**It stops "more special" meaning "more modifiers."** The temptation with
uniques is always to add another line. Deriving them means the only axis
available is depth on something the weapon already had, so a unique is
recognisably of its class and its role rather than a grab bag.

### Four shapes, one per variant

Because each class has four variants, each class has four possible uniques, and
they are not interchangeable:

| Derived from | The raised modifier | Result |
| --- | --- | --- |
| **Purity** | The class signature (already `×2`) | One deep modifier and nothing else — the purest expression the class has |
| **Control** | Either the signature or the control modifier | Signature-deep with a rider, or shallow with a *brutal* rider |
| **Support** | Same, on the support modifier | The party-facing version of the above |
| **Efficiency** | The signature — **never `Light`** | Deep *and* cheap, the only shape that gets both |

Efficiency uniques cannot raise their own added modifier, because `Light` is
forge-limited to `×1` (§1.1). That turns out to be the more interesting
outcome: an Efficiency unique is forced onto the class signature and comes out
as `signature ×3, Light ×1` — the only weapons in the game that are both deep
and cheap to swing.

The Control and Support derivations are the richest, since they keep a second
modifier. A Purity unique collapses to a single number, which is right for The
Bulwark but would be dull four times over.

### Uniques are the deepest ceilings in the game

A unique's whole spread is **forged** (§1.1), so its raised modifier caps at
`×8` with a full five-stack acquired budget still unspent above it. A unique is
therefore not just the best weapon you can find — it is the best weapon you can
*keep building*, which is what earns it the deep end of the repeat-kill ladder
(§3.2).

Because they are just stacks, a unique that turns out overtuned is a data edit,
not a code change. And since ceilings scale with the forged spread rather than
sitting at a flat cap, the balance envelope is held by the derivation rule plus
hand-authoring: no roll, graft or service can reach a spread nobody wrote down.
# Phase 1 — Crits, riders and the damage pipeline

> §1.6. Stack rules in [phase-1-modifiers](phase-1-modifiers.md); index in [ROADMAP.md](../../ROADMAP.md).

## 1.6 Crit riders — a crit leaves a mark

A crit today just doubles damage — a number occasionally getting bigger, with
nothing left behind. Crits should *land an effect*, which is what lets them
fill the Control and Support roles (§1.2) rather than being pure damage
variance.

Two new status effects carry it, both using the existing decaying model —
level N, ticks down one per turn, removed at zero, so they always self-clear:

| Effect | Per level | Notes |
| --- | --- | --- |
| **Sundered** | Target takes **+1 damage** from every source | Getting crit opens you up |
| **Weakened** | Target deals **1 less damage**, floored at 1 | Getting crit rattles your swing |

The `Weakened` floor mirrors Block's existing "never below 1 taken" rule
(`CombatRules.cs:58`), so nothing can be reduced to harmlessness.

### Riders accumulate, and there is no level cap

**Re-applying a rider adds levels. It does not refresh a timer.** A
`CritSunder ×4` weapon that crits three times has applied twelve levels, and
the target takes `+12` from everyone until it decays off.

That is the point of building a rider weapon, and any ceiling on the level
would take it away. A cap would also reintroduce the exact failure §1.1 exists
to forbid: a Weakspot Stiletto ceilings at `CritSunder ×6` and a
Stiletto-derived unique at `×8`, so a cap of 4 would mean the last four stacks
of the thing you spent a campaign deepening apply nothing at all. **Investment
should read as a bigger number, not as the same number lasting longer.**

Per level is `1` rather than `2` precisely because nothing clamps it. With
accumulation and no cap, the per-level value is the only dial the effect has,
and the coarser one made a moderate crit streak swing the maths too hard.

Three things bound it instead, none of them a cap:

- **Decay.** One level per turn, always, so a rider only stays deep while you
  keep landing crits.
- **The fight ends.** `Sundered` makes the target die faster, which is the
  thing that stops `Sundered` — it is a win-more that closes out its own
  window rather than a runaway.
- **`Weakened` floors at 1.** However deep it goes, an attacker still hits for
  something, so no enemy is ever fully neutralised.

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
party — a dagger dummy with `CritWindow ×1` crits on 10% of swings. Three things
keep that from spiralling:

- Riders decay one level a round like every status (§1.5), and an enemy critting
  on 10% cannot outrun that decay for long.
- `Sundered` is a flat `+1` per level added **after** the multiplier in the
  pipeline below, not a multiplier itself, so it cannot compound with the crit
  spike into a one-shot.
- Enemy weapons are rolled, not built. Nothing on the enemy side accumulates
  `CritSunder` stacks across runs the way a serviced party weapon does, so the
  deep rider builds are the player's alone.

### A crit is not blocked

**Block is skipped entirely on a crit.** Not reduced, not halved — skipped.

This is what lets `Block` scale with the forged spread like everything else
(§1.1). The Bulwark ceilings at `Block ×8`, 24 absorbed, against weapons that
deal 5 to 18: without an exception, a maxed shield would simply stop taking
damage, and the minimum-1 rule would turn every fight against one into a
hundred-turn arithmetic exercise.

Capping `Block` would have fixed that by making the shield worse. Letting crits
through fixes it by making armour *answerable*, which is the more interesting
version — the shield stays genuinely excellent, and there is always a way in:

| Answer to heavy armour | Who has it | How reliable |
| --- | --- | --- |
| Crit through it | Dagger, at `CritWindow ×1–8` | 10–45% of swings |
| Crit through it | Everyone else, on a natural 20 | 5% |
| `Splitting` | Axe | Every swing, 3 Block per stack |
| `Softening` | Throwing | Every hit, for everyone, one turn |

So the dagger becomes the **burst** answer to armour and the axe the **grind**
answer, which is a better division than both of them wanting the same modifier.
It also puts a floor under every other class: nobody is ever fully locked out,
they just need the 20.

Two consequences worth stating:

- **`Riposte` and `BlockWeaken` do not fire on a crit**, since both trigger on a
  *successful block* and a bypassed one never happens. A crit-heavy attacker is
  therefore the counter to the sword's whole Control/Support half, not just to
  its mitigation.
- **It runs both ways.** Enemy crits go straight through party shields too, so
  a shield-bearer is not a solution to dagger dummies — which is the same
  symmetry the riders already have.

### Damage pipeline

`CombatRules.ResolveAttack` becomes an ordered chain rather than a calculation
— it is the canonical handler order for `DamageTaken` (§1.7), and every step
below is a handler that transforms the payload and returns it. `Block` is not
last, but it is the last thing that *reduces* damage, which is what its
minimum-1 guarantee needs: everything after it either moves damage somewhere
other than HP or reacts to the hit:

```
1. roll d20   → base damage    (× CritMultiplier, or halved on a natural 1)
2. attacker's Weakened          → subtract
3. defender's Sundered          → add
   ── this is the WEAPON's damage, and only this ──────────────────
4. attached enchantments fire    → add, tracked separately — §2.2
5. defender's Block             → absorb, off the weapon's share first,
                                  never below 1 taken
                                  SKIPPED ENTIRELY on a crit
   ── the DEALT amount is fixed here ──────────────────────────────
6. defender's Ward              → spend 1 per point taken; temporary HP,
                                  so NOT skipped on a crit — §3.3
   ── the amount reaching HP is fixed here ────────────────────────
7. on a crit, apply `CritWeaken` / `CritSunder` stacks to the defender
```

### The pipeline has two outputs, and the design already needed both

**`Dealt` is the number after mitigation and before absorption; `Taken` is what
actually reached hit points.** They differ by whatever `Ward` swallowed, and the
distinction is not bookkeeping — it falls straight out of §3.3's rule that
**`Ward` is temporary health, not armour**:

| | Is | So a hit into it |
| --- | --- | --- |
| **`Block`** | Mitigation | Was never dealt. It is *prevented* |
| **`Ward`** | Temporary hit points | **Was** dealt. It landed on a pool that is not HP |

Four things read one or the other, and getting them from the same step would be
wrong in three of them:

| Reads | Which | Why |
| --- | --- | --- |
| Weapon XP (§2.2) | **`Dealt`** | You did that damage; where it landed is the defender's business |
| `Serrated` levels (§3.3) | **`Dealt`** | The wound is as deep as the blow that made it |
| The clean-kill test (§3.2) | **`Dealt`** | Already specified as *after mitigation* |
| Death, and `Sturdy` | **`Taken`** | Only HP kills you |

### And it tracks two *sources*, because they mean different things

`Dealt` is the sum of the weapon's own damage and whatever its attached
enchantments added, and the payload keeps them apart the whole way down. **Only
the weapon's share teaches you the weapon** (§2.2) — a knife that hurts because
of the `Arcane` on it has not made anyone better with knives.

**Block comes off the weapon's share first.** Armour stops blows; it is not
obvious it should stop the lightning riding one, and taking it off the weapon's
share keeps `Block`'s minimum-1 rule operating on the thing `Block` was written
against.

The consequence is real: **enchantment damage is better into armour than a raw
swing is**, so there is a build in stacking damage enchantments against heavily
blocking targets. That is intended, and if it proves too strong the answer is
**a shielding enchantment rather than a change to this line.**

### Answer a strong build with a counter, not a nerf

This is worth stating as a rule, because it is the third time the design has
reached for it. §1.1 re-prices a modifier rather than capping it; §6.4 limits at
the forge rather than forbidding a graft; and here, an over-strong damage
enchantment is met by **something a player can go and get**.

| | Costs | Who it reaches |
| --- | --- | --- |
| **Nerfing the attribution** | One line | Everyone, silently, including builds that were fine |
| **A shielding enchantment** | A slot, a service, a lock, a trigger | Only the fight where someone chose to bring it |

The shape it would take needs nothing new: a catalogue enchantment whose
`EffectPerLevel` absorbs a point of **enchantment** damage, sitting beside
`Block` as the second armour for the second damage source (§2.2). It works on
both sides of the fight, exactly as `Block` and the crit riders do — so a party
that leans on `Arcane` will eventually meet dummies that shrug it off, which is
a better lesson than the number quietly having been smaller all along.

**Keeping the arms race in content rather than in constants is also what keeps
it in the data files** (§5.5). A counter is an entry someone adds; a rebalance
is a recompile and an argument.

**This is what forces the attack resolver to return a result rather than write
one.** Damage is not a number the attacker computes and applies; it is a value
that passes through the defender's handlers and **comes back**, because the
attacker cannot credit XP or apply a bleed until it knows what the defender did
to it. That requirement is the whole reason §1.7 states the handler contract the
way it does.
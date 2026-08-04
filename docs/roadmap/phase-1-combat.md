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

`CombatRules.ResolveAttack` gains two steps, ordered so Block stays last and
its minimum-1 guarantee holds:

```
1. roll d20   → base damage    (× CritMultiplier, or halved on a natural 1)
2. attacker's Weakened          → subtract
3. defender's Sundered          → add
4. defender's Block             → absorb, never below 1 taken
                                  SKIPPED ENTIRELY on a crit
5. on a crit, apply `CritWeaken` / `CritSunder` stacks to the defender
```
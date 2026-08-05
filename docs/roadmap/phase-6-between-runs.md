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

**An innate enchantment counts.** A weapon that dropped already enchanted
(§3.1) starts the table on its second row, so a caster's first service costs two
runs and the full climb costs fourteen more. Casters are handed the first
enchantment and pay for it in downtime, which keeps a free head start from being
a free fifteen-run shortcut.

**Raising a tier is priced by the same rule.** Service time is always **the
number the work arrives at** — attaching the 3rd enchantment costs 3 runs, and
lifting an enchantment to tier 3 also costs 3. `count + 1` was only ever that
rule stated for the breadth case.

| Work | Runs |
| --- | --- |
| Attach enchantment #*n* | *n* |
| Raise an enchantment to tier *t* | *t* |

That is what lets tier be uncapped (§3.3) without the deep end being cheap. The
climb to tier 6 costs 2+3+4+5+6 = **20 runs**, more than a fully enchanted
weapon, and tier 10 costs 54. There is no ceiling and there does not need to be
one: the curve is quadratic and the currency is the only thing in the game you
cannot farm.

It also puts depth and breadth in direct competition on identical terms. Four
runs buys your 4th enchantment or your 4th tier on one you already have, and
the mana pool (§3.3) is what decides which was correct.

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

### Two outcomes: deepen, or graft

| Outcome | What happens | Weighting |
| --- | --- | --- |
| Nothing | The weapon comes back as it went in | **Most services** |
| **Deepen** | `+1` stack on a modifier the weapon already carries | Uncommon, biased toward the class signature |
| **Graft** | `+1` stack of a modifier it has never carried | **Rare** |

**The rarity is load-bearing, not flavour.** It is the only thing standing
between a weapon and its ceiling, and §1.1 deliberately caps nothing — so the
distance to `×8` is measured entirely in services survived. A modifier climbing
five stacks means five separate low-chance rolls landing on that same modifier,
on a weapon kept in rotation across dozens of runs while it is repeatedly out of
your hands in the shop (§6.2).

That is what makes the deep end an **artefact of a campaign** rather than a
build you assemble — a fully worked Widowmaker crits on 12+ for ×8, and the
answer to "is that too strong" is that almost no weapon ever gets there, and the
one that does was carried the whole way.

Grafting is also the surprise in an itemisation system that is otherwise
entirely determined — drops are class-locked and variant-rolled, enchantments
are chosen, boss themes are announced. A Disarming Kris that comes back from
service carrying `Block` is a story, and it is the only place the game tells you
one you did not ask for.

**Identity survives by ceiling, not by restriction.** The obvious guard would be
to forbid grafts outright — a spear should not sprout `Charges` — but §1.1
already handles it more gracefully. A grafted modifier has `forged = 0` and so
caps at 5, while everything the weapon was forged with caps at 6 to 8. The graft
is always the shallowest thing on the weapon, permanently. So a spear *can*
sprout `Charges`, and it will never be a throwing weapon.

Rules that keep it coherent:

- **The chance scales with wear brought in.** The weapon you actually fought
  with improves; the one you carried does not. Same principle as everywhere
  else in the game.
- **The §1.1 caps still bind.** A modifier at its ceiling cannot be deepened,
  and a weapon with every modifier capped rolls nothing at all.
- **It competes with farming.** Deepening spends the same acquired budget
  repeat-kills spend (§3.2), so a heavily farmed weapon has less room left to
  work with. The forge and the farm are alternatives, not a stack: buy the
  depth now, or climb to it over twenty runs.
- **A finished weapon is not inert.** Even with its signature capped, a weapon
  can still gain *breadth* through grafts at 5 apiece — which is what keeps a
  max-farmed drop worth depositing at all.

## 6.5 Transferring an enchantment

Enchantments can be moved to a new weapon at the enchanter, **dropping a tier**
and costing the service time of the tier it lands on (§6.2). Without this, a
lucky late drop would strand everything you invested — with it, the body is
replaceable and the soul is the thing you built. **Tier 1 is the floor**: a
tier-1 enchantment moves intact for one run, because there is nothing left to
take.

The tier loss is what stops transfer being free re-rolling, and it gets sharper
the deeper the enchantment is: moving a tier-6 enchantment costs 5 runs and
throws away the 6 runs that bought the sixth tier. A shallow soul travels
cheaply; a deep one is most of the reason you keep the body.

**This is also the answer to a bad innate roll.** A staff that drops with
Warding when you wanted Arcane Edge is not a dead weapon — it is a body with the
wrong soul in it, and the enchanter moves souls. The roll sets what you start
with, never what you end with.

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


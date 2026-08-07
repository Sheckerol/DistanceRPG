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
is the enchanter's raw material. Swinging a weapon accrues wear at
**`WearPerHit = 1`**; the enchanter consumes it to attach an enchantment.

One per hit rather than per swing, per turn or scaled by damage. It makes the
pool a plain count of work done, it costs a multi-hit weapon nothing and grants
it nothing, and it is the only version a player can hold in their head while
deciding whether to deposit.

That matches the philosophy the rest of the game already runs on: health levels
from being healed, mana from being spent, proficiency from damage dealt. A
weapon becomes enchantable by being *used*, not by being found.

Consequence worth stating plainly: **you cannot enchant a weapon you have not
fought with.** A fresh unique straight off the floor is inert until it has done
some work.

### A deposit spends the whole pool

Wear does not part-pay and it does not carry over. **Depositing empties the
weapon's wear**, whatever the service bought — so the question is never
*whether* to spend it, but **when**, and **on what** (§6.4).

That turns the pool from a counter into a decision. A weapon kept in rotation
three runs longer walks into the shop worth more than the same weapon deposited
at the first opportunity, and it is the player who decides which.

### The pool has a ceiling, and the ceiling is the durability system

Wear is bounded by **`WearCapacity`**, and that bound is the same number a
durability system would track — read from the other end:

| | Counts | Starts at | Ends at |
| --- | --- | --- | --- |
| **Durability** | Down, as you fight | Full | Worn out |
| **Wear** | Up, as you fight | Empty | Full |

`durability = WearCapacity − wear`. They are one number, and the design keeps
the **wear** direction as the vocabulary so there is one counter going one way.
Service is the repair, and it always restores the weapon completely — which is
the same event as emptying the pool, seen from the other side. **A deposit
repairs and spends in one action, because those were never two things.**

### Bottoming out costs nothing but opportunity

**A fully worn weapon is not broken, damaged, or worse in any way.** It swings
for identical damage, at identical range, with every stack and every enchantment
intact. What it stops doing is *banking*: further hits accrue nothing, because
there is nowhere to put them.

That is the whole penalty, and it is the right one. This design does not have
punishment mechanics — nothing in it makes you weaker for having played — and a
degradation penalty would be a death spiral in a game where the way out of
trouble is to fight (§4.3). So the cost of running a weapon dry is **the work
you did for free**, which is a cost the player can choose to pay and can always
see coming.

It also does something the design wanted anyway: **it is the bound on hoarding.**
§6.4 makes waiting before a deposit genuinely worth it, and without a ceiling the
optimal play would be to wait forever. The pool answers that without a rule — you
cannot hoard past full, and a weapon sitting at its cap is telling you plainly
that it is time to rotate it into the shop.

### Every repair makes the weapon hold more

**`WearCapacity` grows with every service**, and grows faster under refinement:

| Service | Capacity gained |
| --- | --- |
| **Enchanting** | A little |
| **Refinement** | **More** |

A worked weapon is a tempered one. This is the third thing loyalty compounds —
proficiency is per class (§2.2), stacks are per weapon (§6.4), and capacity is
per weapon too, so an heirloom out-performs a fresh drop in three separate ways
that all took the same route to get there.

Two consequences worth naming, because they change what the two services mean:

- **Refinement is not only a gamble; it is an investment.** Declining a soul
  buys better odds *now* and a bigger pool *forever*. That is a genuinely
  different shape from enchanting, which pays out immediately and compounds
  slowly — and it is what makes the fork in §6.4 a strategy rather than a mood.
- **A fresh drop cannot be refined into an heirloom quickly.** Its pool is
  small, so it fills fast, empties fast, and rolls badly whichever service it
  buys. Capacity is the part of a weapon that cannot be shortcut, which is
  exactly what the "your investment is not stranded when a better base drops"
  claim in §6.4 needs in order to be true.

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

**The wear cost climbs with the clock.** Attaching enchantment #n costs `n`
runs of downtime *and* `n × EnchantmentWearCost` out of the pool, for one
reason: the two prices should say the same thing. A fifth enchantment being five
times the wait but the same material would make the material irrelevant by the
end.

That is what ties §6.1's growing capacity to breadth rather than leaving it a
side benefit. A fifth enchantment demands a pool no fresh weapon could hold, so
**capacity is the gate on breadth** and refinement — which grows it fastest — is
the road to the next soul rather than a detour from it. Enchanting also grows
the pool, just slower, so the gate is a gradient and never a wall.

**The enchanter does not sell tiers.** Tier is earned by casting (§3.3), so
service time prices **breadth only** — how many enchantments a weapon carries,
never how deep any of them runs.

That split is what keeps both halves honest, and it lines up with every other
pair of ladders in the game:

| | Bought with | Costs |
| --- | --- | --- |
| **Breadth** — another enchantment | Service time | Runs of downtime |
| **Depth** — another tier | Use | Mana, and the pool the next tier locks |

You cannot grind your way to breadth and you cannot pay your way to depth. A
player who wants a deep enchantment has to *cast with it*, which means carrying
it down rather than leaving it in the shop — and the shop is where breadth
comes from, so the two ambitions want the weapon in different places. That is
the same tension §6.2 already builds the stable around, now running inside a
single weapon.

### You choose what gets attached

The enchanter is not a gamble. **You pick the enchantment**, from everything
your campaign has seen — anything currently on a weapon in the stable, and
anything that has ever passed through your hands.

This is the one place in itemisation that is deliberately not a roll, and it is
what makes every other roll bearable. Drops are class-locked and variant-rolled,
grafts are a rare surprise (§6.4), innate enchantments come up random (§3.1) —
so the system needs exactly one lever the player operates directly, or a build
is something that happens to you. Enchantments are that lever, which is why they
are the half of itemisation the design calls *chosen*.

**Random drops are how the catalogue grows.** A wand that dropped with a damage
type you did not want is not waste — it is that type, permanently added to what
the enchanter can give you, on any weapon, forever after. So the rolls feed the
choice rather than competing with it, and a scrapped drop still moves the
campaign somewhere.

The trade against a free innate is clean: **free but random, or chosen but
costly.** A caster's staff arrives enchanted at no cost and no say; anything you
actually want costs runs of downtime.

**And you can decline it.** A deposit does not have to buy an enchantment — the
alternative is to spend the same wear and the same downtime gambling on the
weapon itself (§6.4). The chosen lever is always *available*; it is not always
what you want.

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
| Failed above floor 3 | **None** | — |
| **Failed at floor 3 or deeper** — died, or retreated | **1 (mercy)** | — |
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

### The mercy tick is for failing deep, not for leaving early

The bar is **three floors down, and the run is over without a boss kill** —
however it ended. Dying on floor 6 earns it; so does turning around on floor 4
and getting home. What does *not* earn it is entering, seeing what is on the
first floor, and walking back out.

That framing matters more than the number. A rule that paid out for *leaving*
would make a short exit a strategy — descend two floors, turn around, bank a
tick, repeat. A rule that pays out for **getting deep and failing anyway** pays
for the same thing every other system here pays for: the attempt.

**Retreating is not the safe option.** Withdrawing without the boss means
climbing back through floors where nothing stayed dead — resurrection only stops
when the boss does (§4.3), so every dummy you killed on the way down is up again,
stronger for having died, and coming back faster the more often you killed it
(§3.2). The way out is *the same dungeon after you have made it worse*.

So the three failure shapes are genuinely different, and none of them is free:

| | What it costs |
| --- | --- |
| **Turn back early** | The run, and you were never deep enough for the tick |
| **Turn back deep** | A fighting retreat through everything you farmed |
| **Die** | The run and the haul, but the attempt still counted |

Three floors is the bar because it cannot be cleared by walking in and turning
around, and because by the third floor a retreat is already a real fight rather
than a stroll. It still does not demand a good run.

Note the mercy tick and the success tick are currently worth the same. Since a
boss can sit anywhere from floor 5 to floor 10, that is deliberate for now —
see the open questions.

**The tutorial has no mercy tick at all**, since its boss sits on floor 2 (§4.3)
and the bar is three. It needs no exception to say so: **the enchanter is not
open during the tutorial**, because he is the one running it (§4.3). Powering
two floors of golems is everything his mana will do, so he cannot be servicing
weapons at the same time — there is no clock to advance, mercifully or
otherwise.

The workshop opens when the training ends. Beating the golem frees him, and the
first thing he can do is take a weapon off you — which is why a single tutorial
run lands exactly on the one-run cost of a first enchantment (§6.2). The pacing
is not a coincidence to be tuned; it is the same event seen twice.

The tutorial's answer to failure is that you can simply walk back in. It never
locks until it is beaten, and then it is gone for good.

It also means the mercy rule never has to be explained to a new player. They
meet it later, in a real dungeon, at the moment it first does something — which
is the right time to learn any rule.

## 6.4 A deposit buys one of two things

Working a weapon has a chance of **adding a modifier stack** — sometimes you
make a thing better while working on it.

This is the mechanism that most directly delivers cross-run weapon progression.
Enchantments are attachments; a modifier is the weapon itself getting better.
An heirloom that has been through twenty services can genuinely out-roll a
fresh drop, which is the payoff for loyalty and the answer to the upgrade
treadmill — your investment is not stranded when a better base drops.

**Which is why it is a choice rather than a side effect.** Wear is the currency
(§6.1) and the deposit is where it is spent; what it buys is up to you:

| | You get | The improvement roll | `WearCapacity` |
| --- | --- | --- | --- |
| **Enchanting** | A **chosen** enchantment, guaranteed (§6.2) | **Really low** | `+` a little |
| **Refinement** | Nothing attached at all | **Much better** | **`+` more** |

Refinement is the gamble *and* the investment: you give up the one guaranteed
thing in itemisation for a better shot at the one thing that cannot be bought,
and a permanently bigger pool to try again from (§6.1).

### It is one mechanism, not two rates

The odds do not differ because a rule says so. **Attaching an enchantment
consumes wear** — that is what §6.1 has always meant by raw material — and the
improvement roll is paid for out of whatever is left:

```
attachmentCost    = EnchantmentWearCost × (currentEnchantments + 1)   // §6.2
improvementChance = min(MaxImprovementChance,
                        (wear − attachmentCost) / WearPerImprovementRoll)
```

Enchanting takes `attachmentCost` off the top, so a routine deposit has little
remainder and rolls at a token chance. Refinement pays for nothing, so
the entire pool pushes the roll. **The gap between the two is exactly the price
of the enchantment**, quoted in the same units as everything else rather than as
a second tuning knob.

Three things fall out that nothing had to state:

- **A fresh drop refines for nothing.** No wear, no roll. §6.1 already said you
  cannot enchant a weapon you have not fought with; it turns out you cannot
  improve one either, and for the same reason.
- **Hoarding wear becomes a real decision.** Deposit the moment you can afford
  the enchantment and the improvement roll is a rounding error; fight several
  more runs first and the *same* enchantment arrives with a genuine chance of
  the weapon itself getting better. Breadth sooner, or breadth and a lottery
  ticket later — and the weapon has to stay in rotation to earn the difference,
  which is the behaviour every other system here rewards.
- **Refinement cannot be spammed.** The clock is not what limits it; the pool
  is. Two refinements back to back on an empty weapon achieve precisely
  nothing, so the gamble needs no cooldown, no per-weapon limit and no guard of
  its own.
- **The odds cannot run away from the costs.** Capacity growth would otherwise
  push both services to the cap and dissolve the fork — but `attachmentCost`
  climbs with breadth (§6.2), so a weapon that grows its pool is *keeping pace*
  with what its next soul costs rather than outrunning the roll. The gap between
  the two services survives to the end of a campaign.

**The chance is capped.** A deep enough pool must never make a graft certain —
the rarity below is load-bearing, and an uncapped curve would let a patient
player *purchase* the one thing the design refuses to sell.

### The clock is the same either way

Refinement costs the downtime of the enchantment you declined —
**current enchantment count + 1** runs (§6.2). He has the weapon for the same
time; what differs is what he does with it. That keeps the fork about what you
want rather than about scheduling, and it needs no anti-abuse rule because wear
already decides how often refining is worth asking for.

**Refinement does not advance the rung.** Nothing was attached, so the next
enchantment costs what it would have cost anyway — the gamble delays breadth
without ever making it more expensive.

### Two outcomes: deepen, or graft

| Outcome | What happens | Weighting |
| --- | --- | --- |
| Nothing | The weapon comes back as it went in | **Most services** |
| **Deepen** | `+1` stack on a modifier the weapon already carries | Uncommon, biased toward the class signature |
| **Graft** | `+1` stack of a modifier it has never carried | **Rare** |

Refinement raises the chance that the roll *lands*; it does not re-weight what
lands. Deepen stays the common result and a graft stays the surprise, so a
player who wants a specific graft is buying more attempts rather than better
odds on the one they are after — which keeps §1.1's ceilings, not a drop table,
as the thing standing between a weapon and its shape.

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
to forbid grafts outright — a spear should not sprout `Block` — but §1.1 handles
it more gracefully. A grafted modifier has `forged = 0` and so caps at 5, while
everything the weapon was forged with caps at 6 to 8. The graft is always the
shallowest thing on the weapon, permanently. So a spear *can* sprout `Block`,
and it will never be a shield.

**`Charges` is the exception, and it proves the rule rather than bending it.**
It is ForgedOnly (§1.1) — deepenable if the weapon has it, never grantable if it
does not. A ceiling cannot make it safe, because `Charges` is a *cap* rather
than a bonus: a first stack grafted onto a bow would hold it to 2 shots where it
already had 5. Restriction is reserved for grafts that would be **negative**,
not merely off-class, which is why it applies to exactly one modifier and the
other twenty are handed out freely.

Rules that keep it coherent:

- **The §1.1 caps still bind.** A modifier at its ceiling cannot be deepened,
  and a weapon with every modifier capped rolls nothing at all.
- **It competes with farming.** Deepening spends the same acquired budget
  repeat-kills spend (§3.2), so a heavily farmed weapon has less room left to
  work with. The forge and the farm are alternatives, not a stack: buy the
  depth now, or climb to it over twenty runs.
- **A finished weapon is not inert.** Even with its signature capped, a weapon
  can still gain *breadth* through grafts at 5 apiece — which is what keeps a
  max-farmed drop worth depositing at all. A weapon carrying all five
  enchantments has nothing left to attach, so refinement is simply the only
  service it can still buy: the fork closes itself at the top end without a
  rule.

## 6.5 Transferring an enchantment

Enchantments can be moved to a new weapon at the enchanter, **dropping a tier**
and costing one attachment's service time. Without this, a lucky late drop would
strand everything you invested — with it, the body is replaceable and the soul
is the thing you built. **Tier 1 is the floor**: a tier-1 enchantment moves
intact, because there is nothing left to take.

The tier loss is what stops transfer being free re-rolling, and it is now the
only place a tier can go *backwards*. It also bites hardest exactly where it
should: a tier-6 enchantment represents hundreds of points of mana spent through
it, and a move throws away the last and most expensive of those tiers. A shallow
soul travels cheaply; a deep one is most of the reason you keep the body.

**This is also the answer to a bad innate roll.** A wand that drops Cold when
you wanted Flaming is not a dead weapon — it is a body with the
wrong soul in it, and the enchanter moves souls. The roll sets what you start
with, never what you end with.

## 6.6 Code impact

New `Logic/Wear.cs`, `Logic/Enchanter.cs`, and a `CampaignState` that lives
*outside* `DungeonState` (§4.3) — party, stable, enchanter queue, run counter.
The existing distinction does the work for us: `DungeonState` is discarded on
leaving, `CampaignState` is not.

`Weapon` gains `Wear` and `WearCapacity`; the unified attack resolver from Phase 0 is the single
place it accrues, and `Enchanter` is the single place it is spent (§6.4) — a
deposit carries the chosen service with it, so the queue entry is
`(weapon, service, wearAtDeposit)` rather than a weapon alone.

Phase 5's save format grows a campaign section, and it becomes the *outer*
document — a save with no dungeon in progress is now a valid state, which it
is not today.

`TurnSystem` needs a run-outcome signal. `GameOver` already fires on a party
wipe (`TurnSystem.cs:620`); leaving the dungeon and reaching a new floor are
new events on the floor-transit path from Phase 4.


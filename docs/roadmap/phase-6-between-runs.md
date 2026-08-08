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

**`WearCapacity` climbs an authored ladder**, one step per full bar cashed. A
weapon has no stat to divide its threshold by (§2.1), so it needs the *whole*
pool for every step:

| Step | Capacity | Wear needed to reach the next |
| --- | --- | --- |
| Fresh | **25** | 25 |
| Second | **30** | 30 |
| Third | **40** | — |

**Three steps and no fourth**, deliberately. A fourth is a number invented
against a game nobody has played; these three are enough to see whether the
curve feels like anything, and the ladder is authored rather than computed
precisely so a fourth can be appended the moment play asks for one.

The steps widen because the bar does. Reaching the third step means cashing 25
and then 30 — 55 hits' worth of fighting on one weapon (§6.1), across at least
two services and the downtime between them.

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

### Wear is what makes a weapon workable, and refinement is maintenance

The mechanic below is easier to hold if it is named for what it is — and the
name is not *experience*. **People grow. Things are built and repaired**, and
the distinction is the whole of it:

| | Improves by | Which is |
| --- | --- | --- |
| **The wielder** | Proficiency, CON, INT — doing the thing | Growth |
| **The weapon** | **Wear, cashed at the enchanter** | **Work done to it** |

A weapon does not learn. It gets **worn**, and a worn weapon is one a craftsman
can do something with: material can be worked into the places it has thinned,
and there is somewhere for a new magic circle to be cut. Refinement is
maintenance that happens to be magical.

That is also why wear gates *both* services and why the price of a soul climbs
(§6.2). A circle needs to have **ingrained** itself before another can be laid
beside it without erasing it — so a weapon carrying four is harder to add a
fifth to, in material and in the smith's time, and the two prices climbing
together stops being a balance decision and starts being the same fact stated
twice.

**And it answers the unworn-weapon question without a metaphor**, because it is
simply true: *a pristine blade has nothing to repair.* There is no 0% to explain
and no rule to write. A craftsman handed a weapon that has never been swung has
no work to do on it, which every player already understands about every craft.

The rest of §6.1 follows the same way. Bottoming out costs nothing but
opportunity because a weapon can only be *so* worn before further use tells the
smith nothing new; capacity growing per service is the weapon becoming
better-made and so able to take more work before it needs him again; and *a
deposit spends the whole pool* is what finishing a repair means.

### The graft is not luck; it is the craftsman's eye

§6.4 keeps its roll, and the reason is better than "the design needs one
surprise."

**A craftsman looking at his own work sees the mistakes.** Every time — the
weak spot, the thing missed, the shortcut taken under time pressure. What comes
back changed is not a lottery win; it is *what he happened to notice this pass*,
and what he noticed depends on the weapon, on where it wore, and on how long he
had with it.

So the Disarming Kris that returns carrying `Block` has a cause: he found a flaw
in the metal and reinforced it, and reinforced it well enough that the blade now
turns a blow. Nobody asked for that. He was fixing a mistake.

That reading earns the randomness rather than merely permitting it, and it lands
on the right side of a fork this document had open:

| | Would mean | Rejected because |
| --- | --- | --- |
| **A threshold** — bank enough, get a stack | The weapon is levelling | It makes the weapon a character, and things do not grow |
| **A roll** — what he saw this time | The weapon is being worked on | *This* — and it keeps the one outcome the player did not choose |

It also explains the deepening-versus-grafting split with no extra rule.
Deepening is the same flaw seen again and worked further; a graft is a *new*
one, spotted for the first time — which is exactly why grafts are the rarer of
the two and why they cap shallower (§6.4). A fault he has been correcting for
twenty services is one he knows; a fault he has just found is one he has only
begun on.

### A base rate, and a multiplier refinement earns

**Enchanting carries a flat 3% chance** that the weapon improves anyway — the
smith noticing something while he had it open, and nothing more than that.

**Refinement multiplies that base by how much of a bar was cashed:**

```
GraftBaseChance   = 3%
improvementChance = GraftBaseChance × (1 + wear ÷ StartingWearCapacity)
```

`StartingWearCapacity` is 25 (§6.1), so a full fresh bar doubles the base to
**6%**, and a weapon two steps up the ladder cashing 40 reaches **7.8%**.

**Proportional rather than thresholded, and that is the point.** A rule that
paid out on *whether* you refined would make cashing a single point of wear
worth the same as bringing in a practically broken blade. Multiplying by the
fraction of a bar means the reward tracks the work — which is the same principle
§6.1 uses when it says a deposit's value is the wear you brought, not the fact
that you showed up.

Note what this is **not**. The two services no longer differ because one of them
spends the pool on a soul and leaves a remainder; they differ because refinement
is the smith working on the weapon *rather than* on something attached to it, and
gets his full attention for it. Enchanting's 3% does not move with wear at all.

Three things fall out that nothing had to state:

- **A fresh drop refines for nothing.** No wear, no roll — nothing to repair.
  §6.1 already said you cannot enchant a weapon you have not fought with; it
  turns out you cannot improve one either, and for the same reason.
- **Hoarding wear is a decision, and it belongs to refinement.** A full bar
  doubles the graft chance and a nearly-empty one barely moves it, so the
  weapon has to stay in rotation to be worth refining. Enchanting is unaffected,
  which sharpens the fork rather than blurring it: **bring a worn weapon to
  refine, bring any weapon to enchant.**
- **Refinement cannot be spammed.** The clock is not what limits it; the pool
  is. Two refinements back to back on an empty weapon achieve precisely
  nothing, so the gamble needs no cooldown, no per-weapon limit and no guard of
  its own.
- **The odds cannot run away.** A weapon three steps up the ladder cashes 40
  against a divisor of 25, so the multiplier reaches 2.6 and stops there — the
  ladder is authored and short (§6.1), so there is no curve for it to climb.
  A graft stays rare at every depth without needing a cap to say so.

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

**The tier loss has a cause rather than a justification.** Depth is the circle
burned deeper by the mana driven through it, finding as it goes the path it most
naturally follows through *that* weapon (§3.3). Cut the same figure into a
different body and the deepest part of the burn — the part that was the old
weapon's grain rather than the figure itself — has nothing to correspond to. It
is the weapon-specific part of the work that is lost, which is exactly the part
that took longest to earn.

**Tier 1 travels intact for the same reason**: a circle that shallow has not yet
found any path in particular, so there is nothing about it that belonged to the
body it came from.

Mechanically it does the two things it needs to. It stops transfer being free
re-rolling, and it is the only place a tier can go *backwards*. And it bites
hardest exactly where it should: a tier-6 enchantment represents hundreds of
points of mana spent through it, and a move throws away the last and most
expensive of those tiers. A shallow soul travels cheaply; a deep one is most of
the reason you keep the body.

**This is also the answer to a bad innate roll.** A wand that drops Cold when
you wanted Flaming is not a dead weapon — it is a body with the
wrong soul in it, and the enchanter moves souls. The roll sets what you start
with, never what you end with.

## 6.6 The farm — where potions come from

**Every first-time clear adds someone to the hub**, and the hub makes potions
while you are away. Not a shop and not a crafting bench: a **farm** that
produces at a rate set by how many dungeons you have beaten *once*.

```
potionsProduced per run = FarmYieldPerClear × non-tutorial dungeons first-cleared
```

### You split it yourself, and what you leave behind spoils

**Talking to the alchemist is where the batch becomes potions.** You divide the
run's yield between health and mana in whatever proportion you want — choice
rather than a roll, for the same reason enchantments are chosen (§6.2): the
system needs levers the player operates directly, and an emergency supply that
arrives random is an emergency you were handed rather than one you prepared for.

**What you do not pick up is gone.** It does not wait on a shelf, it is not
there when you climb back out, and next run's batch is a fresh one. A mixed
potion keeps exactly as long as the trip it was mixed for.

So **the farm is a flow, not a stock**, and that single rule does most of the
work in this section:

| | If the yield banked | As it actually is |
| --- | --- | --- |
| The decision | Made once, in the late campaign, forever | **Every run, before every descent** |
| Growing the farm | Compounds without limit | Raises the *ceiling* on one trip |
| A cautious player | Hoards, then never has to choose | Cannot hoard, so always chooses |

It also means the carry limit and the yield are the same conversation rather
than two. §3.4 makes a potion cost an inventory slot against weapons and haul;
now that trade is decided at the moment the potions are handed to you, with the
run's whole supply in front of you and nothing recoverable afterwards.

### The alchemist arrives with the first real clear, and the tutorial does not count

**Nobody mixes potions until you have beaten a dungeon that is not the
tutorial.** The alchemist is one of the people a first clear brings back, and
until then the category does not exist — no potions on the shelf and no shelf.

That is the same shape §6.3 already uses for the enchanter, one step later:

| Opens | On | Because |
| --- | --- | --- |
| **The workshop** | Beating the tutorial | It frees the enchanter, who was powering the golems (§6.3) |
| **The farm** | Beating a *real* dungeon | It brings back the first people to work it |

**The tutorial not counting is the same exclusion it already gets everywhere
else** — no mercy tick, no cooldown, no return visit (§6.3) — and it needs no
new rule to say so, because the tutorial cannot be cleared *twice* and the farm
counts first clears of dungeons you can go back to.

The effect on pacing is worth having deliberately: **the early campaign has no
potions at all.** A party learns to survive on positioning, the healer's mana
and the movement budget before it ever learns to drink, so first aid arrives as
a thing that changes how you fight rather than as a thing you always had. And
the first real clear — already the hardest gate in the game — hands over an
entire category rather than a number going up.

### It pays for the one thing nothing else pays for

This design is **depth-biased everywhere**. Farm one dummy, deepen one weapon,
level one enchantment, work one heirloom for twenty services — every ladder in
it rewards going *further into* something you already have.

A first clear is the exception that had no reward attached:

| | Pays | With |
| --- | --- | --- |
| Repeat clears | Service ticks, drops, farmed stacks | §6.3, §3.2 |
| Depth in a weapon | Stacks, tiers, capacity | §3.2, §6.4 |
| **A dungeon's *first* clear** | **Nothing, until now** | — |

And the design was already *pushing* toward breadth without paying for it. A
cleared dungeon goes on cooldown for `bossFloor` runs (§4.2), so the correct
play is to go somewhere new — a pressure with no reward on the other side of it.
**The farm attaches the reward to the thing the cooldown already forces.**

### It is production, not purchase, so the invariant holds

§3.4 rests on there being **no currency in this game**: every cost is something
you do — runs of downtime, accumulated wear, turns spent camping, mana spent
casting. A shop would have been the first exception.

A farm is not. It is priced in *dungeons beaten*, which is the most expensive
thing on the list, and it is paid before the goods arrive rather than after. The
rule survives intact, and the awkward question §3.4 left open — what a potion
even drops off, given the drop table is class-locked to weapons (§3.1) — simply
disappears. **Nothing drops potions.** No non-weapon outcome in the loot table,
no floor containers, no new system.

### Production ticks per run, not per turn and not per minute

This is the one condition worth insisting on. **A run is the unit**, exactly as
it is for the enchanter's service clock (§6.2), and for the same reason: the hub
working while you are away is a rhythm this design already has, and a hub
working while you are *idle* would be the first reward in the game that is not
paid for by playing.

It also means the farm and the enchanter tick together. A run advances your
weapons and stocks your shelf in one event, which is one fewer clock for a
player to hold.

### Quality is what surplus yield buys, so it does not saturate

Because nothing banks, yield above what the party can carry would be wasted —
which would put a ceiling on what clearing more dungeons is worth, and a
saturating reward stops paying for the breadth this section was built to pay
for.

**So yield buys strength as well as count.** A potion is mixed at a **quality**,
and quality is simply how many units of the batch went into it:

```
quality        = units of yield spent on this potion
healthRestored = HealthPotionPercent × quality × maxHp     // §3.4
```

No exchange table and no named tiers — one unit makes an ordinary potion, three
units make one that is three times the drink. The alchemist's question is
therefore two-dimensional: **how many, of what type, at what strength**, out of
a fixed batch.

That is the shape the constraint asked for. §3.4 makes the **slot** the scarce
thing, not the potion, so the way to grow past a full inventory is to make each
slot worth more. A late-campaign party does not carry more potions than an early
one — it carries the same six, and each is worth five of the ones it started
with.

### Neither strength nor count dominates, which is why it is a choice

Quality is deliberately **linear**: three units make a potion three times as
large, not four times. So it buys no raw value at all — it buys *slot
efficiency*, and it costs something real in exchange:

| | Good for | Costs you |
| --- | --- | --- |
| **Many small** | Several separate crises; a long fight of attrition | Slots, one per drink |
| **Few large** | One enormous save; the boss room | Granularity, and overheal (below) |

**And a big potion wastes what it overshoots**, with no rule needed: healing is
capped at missing HP (§3.4), so a quality-5 potion drunk at half health throws
away most of itself. Large potions are lossier exactly when they are least
needed, which is the counterweight that keeps small ones worth mixing.

A party carrying `Overheal` (§3.3) is the exception, and pleasingly so: the
overshoot becomes `Ward` instead of vanishing, so the one build that wants
enormous potions is the one that has solved overhealing. Nothing was written for
that; it falls out of two rules meeting.

### It asymptotes rather than saturating, and the ceiling is legible

There is still an end state, but it is a long way out and it is one a player can
name: **a potion that heals you completely from 1 HP is the best potion there
is.** Past that, more quality is pure overshoot.

At `HealthPotionPercent` 20% that is quality 5, so a full loadout of six
maximum-value potions costs thirty units of yield — a farm many first clears
deep. The reward curve flattens toward something recognisable instead of
stopping dead at "your bag is full", and every clear before that point buys
something you can feel.

### Tune the total, not the rate — the dungeon count is not settled yet

`FarmYieldPerClear` cannot be picked in isolation, because what a fully explored
campaign actually produces is that number **times however many dungeons exist**,
and how many exist is a content decision nobody has made. Pick the rate first
and every dungeon added afterwards silently inflates the whole system.

So state the target the way §1.3 states `MovementUnitsPerMana` — as the rule
rather than the number:

> **A campaign that has cleared everything should produce about one full loadout
> of maximum-quality potions per run.**

```
FarmYieldPerClear = (PartyCarrySlots-worth of potions ÷ HealthPotionPercent)
                    ÷ number of non-tutorial dungeons
```

Then the count can change without retuning. Six dungeons or sixteen, the
end state is the same and only the pace of arrival differs — which is the right
thing for the count to control, since it is also how much *game* there is.

**`HealthPotionPercent` is the half that decides the span**, not the height. It
sets how many units a maximum potion costs, and therefore how much of a campaign
passes before the farm stops mattering. Low, and early potions feel like
nothing; high, and the ceiling arrives while there are still dungeons left to
find.

### Mana and health are equivalent until play says otherwise

`ManaPotionPercent` starts **equal** to `HealthPotionPercent` — one unit of yield
buys the same share of either pool, and the split is a pure preference.

There is a real argument that they should not be. The two resources are spent
differently: **health is spent by being attacked and mana by choosing to cast**
(§1.3). A caster can decline to run dry in a way a front-liner cannot decline to
be hit, which makes mana the more *avoidable* emergency and so arguably the more
expensive unit.

The counter is that the caster's shape is a sprinter's (§1.3) — the dry spell is
not a mistake, it is the class working as designed — and charging extra to
shorten it taxes the thing the design deliberately built. Equal is the honest
starting point, and the split being a free choice is what will make the answer
obvious: if every party mixes the same ratio every run, the prices are wrong.

### The presentation is an art question, not a design one

Whether the hub visibly gains a person per clear, or the farm simply reports a
bigger number, changes nothing mechanical. It changes a great deal about whether
a first clear *reads* as a reward — but that is a matter for how the hub is
drawn, and it does not belong in this document.

## 6.7 Code impact

New `Logic/Wear.cs`, `Logic/Enchanter.cs`, and a `CampaignState` that lives
*outside* `DungeonState` (§4.3) — party, stable, enchanter queue, run counter,
the set of dungeons cleared at least once, and the farm's stock.
The existing distinction does the work for us: `DungeonState` is discarded on
leaving, `CampaignState` is not.

`Weapon` gains `Wear` and `WearCapacity`; the unified attack resolver from Phase 0 is the single
place it accrues, and `Enchanter` is the single place it is spent (§6.4) — a
deposit carries the chosen service with it, so the queue entry is
`(weapon, service, wearAtDeposit)` rather than a weapon alone.

`Enchanter` needs the two services as one call taking which was chosen, because
they share everything except what the wear buys:

```csharp
record Deposit(Weapon Weapon, Service Service, int WearAtDeposit);
enum Service { Enchant, Refine }
```

The graft roll is `GraftBaseChance` for `Enchant` and
`GraftBaseChance * (1 + wear / StartingWearCapacity)` for `Refine` (§6.4) — one
expression, one branch, and the capacity ladder (§6.1) is a `int[] {25, 30, 40}`
rather than a formula so a fourth step is an edit and not a redesign.

`Logic/Farm.cs` is small enough to question whether it is a file: a clear set, a
yield computed from its count, and a split the player chooses. It earns one
anyway, because the *spoilage* rule (§6.6) is the kind of thing that gets
quietly broken by a later change unless there is somewhere obvious for it to
live — the batch must never reach `CampaignState`.

Phase 5's save format grows a campaign section, and it becomes the *outer*
document — a save with no dungeon in progress is now a valid state, which it
is not today.

`TurnSystem` needs a run-outcome signal. `GameOver` already fires on a party
wipe (`TurnSystem.cs:620`); leaving the dungeon and reaching a new floor are
new events on the floor-transit path from Phase 4.


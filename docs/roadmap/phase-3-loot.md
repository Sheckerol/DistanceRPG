# Phase 3 — Loot and enchantments

## 3.1 Drops are class-locked, variant-rolled

An enemy drops a weapon of **its own class**, rolled uniformly among that
class's four variants. `EnemyPlacer` already assigns each dummy a weapon from a
seeded stream (`EnemyPlacer.cs:49`); the drop reads that class back. Placement
rolls over the eight **classes**, with the variant rolled at drop time on the
loot stream.

### A drop can arrive already enchanted

A dropped weapon rolls for an enchantment (§3.3) alongside its variant:

| Class | Enchantment on drop |
| --- | --- |
| **Staff** | **Always, and fixed** — its effect *is* its enchantment (§1.3). A Staff of Blight carries Poison, every time |
| **Wand** | **Always, and rolled** — its damage type (§1.4), uniformly among the four |
| Everything else | **Rare** — an uncommon roll on the loot stream, uniformly among the general set |

Always **exactly one**, always **tier 1**. Breadth stays the enchanter's
product (§6.4) and depth past the first tier stays a project; what the dungeon
hands you is a *seed*, and the fifteen-run climb to a fully enchanted weapon
(§6.2) is untouched.

**The two casters are asymmetric on purpose.** A staff's identity is the effect
it casts, so fixing it keeps a Staff of Mire recognisably a Staff of Mire. A
wand's identity is its *shape*, which leaves the damage free to roll — so a wand
is a shape you picked crossed with an element you did not, and the same four
wands play differently every campaign. Neither class gives up its identity;
they just keep it in different fields.

The innate enchantment is **forged** in the §1.1 sense — it is part of what the
weapon is, not something piled on afterwards. That matters in exactly two
places: farming can deepen it (§3.2), and it counts toward service time (§6.2),
so a caster's first visit to the enchanter already costs two runs. Casters come
pre-loaded and pay for it in downtime.

**A weapon you cannot afford is not a weapon you cannot use.** A staff dropping
with a 30-lock enchantment would otherwise be unequippable by a fighter with a
small pool. Instead the lock goes unpaid and the enchantment sits **dormant** —
the same non-event as a trigger you cannot afford (§3.3). The weapon is a
weapon; the enchantment wakes up if your pool ever grows to cover it, or when it
moves to someone whose pool already does (§6.5).

## 3.2 Repeat kills deepen the drop

Dummies resurrect after 10 turns (`GameConstants.DummyResurrectTurns`) and
already record `DefeatedAtTurn`. Add `DefeatCount` to `EnemyState`: the *n*-th
defeat of the same dummy deepens the weapon it is carrying.

Repeat kills never add a **new** enchantment. Farming buys depth in what the
weapon already is — that rule holds for enchantments exactly as it holds for
modifiers, so a farm can raise the tier of an innate enchantment (§3.1) and can
never attach a second. Breadth remains the thing you only get by choosing it.

This makes the resurrection timer a deliberate farming rhythm rather than
flavour — camp a dummy to deepen its drop, at the cost of the turns you spend
waiting. That cost shrinks as you farm: the timer shortens with `DefeatCount`
(below), so the waiting stops being the expensive part and the fighting starts.

**Nothing is handed over at the time.** `DefeatCount` is the quality of the
drop that dummy is *carrying*; you collect it only by killing it permanently on
the way out, after the boss has stopped resurrection (§4.4).

### A clean kill counts twice

**A killing blow that deals at least the target's *max* HP advances
`DefeatCount` by 2.** Everything else advances it by 1.

The test is the swing against the enemy's constitution, not against whatever is
left of it — *could this have killed it outright?* A dummy already down to 3 HP
takes 2 from a dagger and dies; that is a kill, not a clean one. The same dagger
crits for 40 into a 24-HP dummy and it is clean whether the dummy was full or
not.

The problem it solves is that fights are not all the same length, and the short
ones were paying the same as the long ones. An axe at proficiency, swinging into
a dummy that has not been farmed up, simply deletes it — that is a fight the
party won before it started, and the ladder had no way to say so. Now the
trivial fight is *worth* something, and it is worth exactly what it looks like:
twice as much progress for half as much fight.

**Measuring against max HP rather than current is what makes it uncheesable.**
The bar does not move, so nothing you do to a target beforehand brings a clean
kill closer — softening it up makes the kill easier and the *clean* kill no more
likely. It is purely a statement about your weapon against their constitution,
which is exactly the thing the rule is trying to reward.

It is also one comparison rather than a piece of tracked state. "From full HP to
zero in one hit" was the first draft, and it is the same rule stated worse: at
full HP, killing in one blow *means* dealing at least max HP. The general
version subsumes it, needs no `wasAtFullHp` flag, and stops caring whether the
enemy had already acted.

**It accelerates the reward and the risk together, which is why it needs no
counterweight.** `DefeatCount` already means two things at once (below) — the
quality of the drop *and* how dangerous the dummy has become. Advancing it by 2
buys two cycles of drop quality and hands the dummy two cycles of statline,
including two steps of the revival speed-up. You are not skipping the cost; you
are paying it faster.

**And it puts itself out of business**, now by the most direct route available:
the bar *is* max HP, and revival scaling raises max HP (§3.2). Every cycle that
rolls Health lifts the threshold your swing has to clear, so a dummy you could
clean-kill at `DefeatCount 2` is one you cannot at 10. The bonus front-loads a
farm and then stops — early cycles blur past while the fight is trivial, late
ones arrive one at a time exactly when each is a real fight. That is the shape
the ladder wanted anyway, and it falls out of a rule written for another reason.

It also makes **burst the farming build**, which is a real distinction the
classes did not have. An axe or a crit dagger clears the early cycles at double
rate; a grind weapon does not, and catches up only because the dummy eventually
outgrows everyone's biggest hit. Two ways to farm, differing in *where* on the
curve they are fast.

### The accidental clean kill is the best thing about it

Because the bar is measured **after mitigation** and crits bypass `Block`
entirely (§1.6), a crit can clear a threshold the same weapon's ordinary swing
cannot. That is not a leak to be closed. It is the rule's best moment, and it is
worth being explicit about why.

**A clean kill you did not plan is a consequence you did not choose.**
`DefeatCount` is bidirectional — it is the drop *and* the threat — so a lucky
natural 20 on a dummy you were already struggling with does not hand you a
reward. It hands you **two cycles of statline on the thing that was already
beating you**, plus two steps of the revival speed-up, and it comes back
angrier in three turns instead of eight.

Nothing else in the design produces that shape. Every other reward is something
you decided to pursue: you chose the dungeon, chose the weapon to farm, chose
when to stop. This one arrives on a d20 and immediately makes the room worse.
The player who rolls it laughs and then has to deal with it, which is a better
memory than any planned outcome.

**So the variance is the point, and it needs no correction.** The usual worry
about RNG driving a progression rate — that it rewards luck over decision — does
not apply when the thing luck advances is *also* the thing that kills you. There
is no version of this where a player farms crits for free value; there is only a
version where they get further, faster, into something they have to survive.

### The ladder has no top; the danger curve is the top

Two things accrue, on different shapes. **Stacks are rolled and run out; unique
chance is a curve and never does.**

### Stacks are rolled twice: whether, and onto what

Each defeat rolls **50% to add one stack**. On a success it rolls again for
*which* modifier receives it, uniformly among the modifiers the weapon is
already forged with, skipping any that have reached their `forged + 5` ceiling
(§1.1). An **innate enchantment is one of the entries in that roll** — landing
on it banks a tier's worth of enchantment XP (§3.3) instead of a modifier stack.

XP rather than a tier outright, because tier is something a weapon *earns* and
the farm has an obvious story for how: the dummy carrying it has been casting
with it, over and over, for as many cycles as you have made it come back. The
drop arrives with the levelling already done, which is the same thing farming
does for everything else.

**A farm grants at most 10 stacks**, and §1.1's per-modifier cap of 5 is what
forces those across **at least two modifiers**. There is no new rule doing that
— five is simply as much as one modifier can take, so the eleventh point of
investment has nowhere to go but sideways.

Enchantment tiers are **not** capped (§3.3), so they take the same allowance of
five as a rule of the farm rather than a property of the enchantment: farming
can bank five tiers' worth and no more. Beyond that the enchantment levels the
way every other one does — by being cast with, by you. Farming buys a head
start, never the climb.

The allowance is never wasted, because **no weapon is forged with fewer than two
axes** (§1.2). Every class baseline carries a second modifier for exactly this
reason; the casters carry an innate enchantment instead, and it does the same
job. Without one, a Purity variant would hold a single modifier, absorb five
stacks, and be the one weapon in the game you could not fully invest in.

A fully farmed Assassin's Fang therefore comes out `CritWindow ×2+5`,
`CritMultiplier ×1+5` — both dials of the crit build maxed, from one weapon that
was carried long enough.

A clean kill (above) advances two rows of this table at once, so the columns
below are cycles rather than swings — a burst party reaches `DefeatCount 10` in
five fights and a grind party in ten, and both arrive at the same drop.

| `DefeatCount` | Stacks, expected | Unique chance |
| --- | --- | --- |
| 5 | ~2.5 | 1% |
| 10 | ~5 | 3.5% |
| 15 | ~7.5 | 11% |
| **20** | **10 — the farm's allowance, spent** | **25%** |
| 30+ | 10 | 47% |

At 50% a defeat, the allowance runs out around `DefeatCount 20` — which is
exactly where the unique curve below is steepest. **The weapon stops improving
at the moment the gamble gets interesting**, so the deep farm is unambiguously a
lottery from that point rather than a mix of two rewards.

### Which weapon you farm matters more than how long

The roll picks among what the weapon *already carries*, so its forged spread
decides where a farm can go:

| Weapon | Modifiers | Where 10 stacks land |
| --- | --- | --- |
| Tower Guard | `Block ×2`, `Push ×1` | `Block ×7`, `Push ×6` — **both maxed** |
| Assassin's Fang | `CritWindow ×2`, `CritMultiplier ×1` | `CritWindow ×7`, `CritMultiplier ×6` — both maxed |
| Disarming Kris | `CritWindow`, `CritMultiplier`, `CritWeaken` | 10 spread across three, **none maxed** |
| Staff of Blight | `Resonant ×1`, innate enchantment | `Resonant ×6` and **five tiers** — the only farm that buys potency |

**A two-modifier weapon can be finished; a three-modifier weapon cannot.** Ten
stacks fill two ceilings exactly and leave nothing over, so a Purity variant
farmed to the end is *complete* — every dial at its cap. A three-modifier weapon
has fifteen stacks of room and only ten to fill it, so it comes out good at
three things rather than perfect at two, with five slots left for the enchanter.

That is a genuine trade rather than a strict ordering. Purity concentrates and
finishes; Control and Support spread and stay open. And it reverses cleanly
against §6.4 — the weapon farming can complete is the one the enchanter has
nothing left to do with.

Choosing the weapon to farm is therefore choosing the *shape* of what you get
out, not just how much.

### The unique chance accelerates toward an asymptote

A flat `+1%` per cycle made deep farming a novelty rather than a strategy — at
`DefeatCount 20` it was still colder than simply killing a boss. It should
instead start negligible, get genuinely hot in the middle, and then flatten
without ever reaching certainty. That is a logistic:

```
uniqueChance(n) = Ceiling / (1 + e^(-K × (n − Midpoint)))

Ceiling  = 0.50     never certain, however deep you go
Midpoint = 20       the steepest point, at half the ceiling
K        = 0.26     tuned so DefeatCount 5 lands on ~1%
```

| `DefeatCount` | 5 | 10 | 12 | 15 | 18 | **20** | 25 | 30 | 40 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Chance | 1% | 3.5% | 5.6% | 11% | 19% | **25%** | 39% | 47% | 50% |

Three things this buys that a flat rate did not:

- **A hot zone.** Between 12 and 25 the odds roughly quadruple, so there is a
  stretch where each additional cycle visibly matters — and it lands exactly
  where revival scaling has made the dummy genuinely dangerous. The interesting
  decision and the interesting fight are the same cycles.
- **A real reason to overcommit.** At 20 it is a coin-flip's worth of a coin
  flip, against an enemy that has come back twenty times. That is a story.
- **No certainty, ever.** The 50% ceiling means no amount of grinding
  *guarantees* a unique, so the deep farm stays a gamble rather than becoming a
  long, safe purchase. This is the asymptote doing the work a hard cap used to.

All three constants are knobs. `Ceiling` sets how much grinding can ever be
worth, `Midpoint` moves the hot zone, and `K` controls how sharply it arrives.

The chance is rolled **once, on the permanent kill** (§4.4), not per defeat —
`DefeatCount` sets the odds the drop is carrying, and the extraction is where
you find out.

Tuning targets, but the *shape* is the decision, and three things fix it.

**Depth, never breadth.** The roll only ever picks a modifier the weapon is
already forged with — it deepens what is there and never adds something new.
Breadth is the enchanter's product (§6.4); if farming produced it too, the two
ladders would collapse back into one. Kept apart, each gives you something the
other cannot: **farming buys depth in what the weapon already is, the enchanter
buys breadth and the slow climb.**

Because the stacks land on forged modifiers, they inherit the *forged* ceiling —
6, 7 or 8 rather than the bare 5 an off-class graft would hit (§1.1).

**Farming spends the weapon's future.** Those stacks are **acquired**, so they
come out of the same per-modifier budget the enchanter would otherwise fill. A
`DefeatCount 10` dagger has spent five of them somewhere, and a fully farmed one
has spent ten — which on a two-modifier weapon means the enchanter can never
deepen it again at all. Farming consumes the weapon's long-term potential, and
that is the cost the repeat-kill ladder was otherwise missing, since turns are
the cheapest thing a patient player has.

**Nothing stops it, and that is now safe.** §4.3 rests on "deciding when you
have farmed enough is the run's real decision, and it is entirely the player's
to make" — and an earlier draft put a hard top at five to guarantee that
decision existed, on the reasoning that farming's only cost was turns and turns
are the cheapest thing a patient player has.

**Revival scaling makes that reasoning obsolete.** Every cycle now hands the
dummy damage or health, so the tenth kill is a materially harder fight than the
first, and the eighteenth is harder still. Farming is self-limiting through
*risk* rather than through a number in a table, and a limit the player feels is
worth more than one they read.

That also turns the deep end into the right shape. Past ten the drop cannot
improve at all — only the odds can — so a player pushing to fifteen is buying
lottery tickets with escalating danger, in a run they still have to climb out
of. Knowing when to walk away from that is a genuine judgement rather than a
lookup.

### What comes back is stronger than what died

**Every revival makes the dummy more dangerous.** On each resurrection it rolls
one of three outcomes:

| Roll | Gains |
| --- | --- |
| Damage | **+5 damage** |
| Health | **+5 max HP**, restored full |
| Both | **+5 damage and +5 max HP** |

One third each. **`+5` is a placeholder** — the step is unlikely to survive
contact, since against a party dealing 10–18 a swing `+5` HP is small and `+5`
damage is large, and a flat number means far more on a dagger dummy than an axe
one. What matters here is the *shape*: a fixed step, rolled between two axes,
compounding every cycle without limit.

At that placeholder, five cycles is roughly `+17` damage and `+17` HP in
expectation — a dummy that opened the run as scenery finishes it as a genuine
threat, and at fifteen cycles it is something else entirely.

**This is the cost the ladder was missing.** §3.2 above prices farming in the
weapon's long-term potential, which is real but abstract. This prices it in the
fight itself, immediately and visibly: the tenth cycle is not the first cycle
nine more times, it is a harder fight for a better drop. Turns stop being the
only currency, and turns were the cheapest thing a patient player had.

**`DefeatCount` now means two things at once**, and they are deliberately the
same number. It is the quality of the drop the dummy is carrying *and* how
dangerous it has become. You cannot bank value into a dummy without arming it.
The nameplate (§4.5) has to read as a threat level and a reward tier
simultaneously — which is honest, because that is exactly what it is.

### And it comes back sooner

The roll decides *what* a revival grants; the count decides **how fast it
happens**. Resurrection is 10 turns today (`GameConstants.DummyResurrectTurns`),
and it shortens with every defeat:

```
resurrectTurns(n) = max(3, 10 − n)
```

| `DefeatCount` | 0 | 2 | 4 | 6 | **7+** |
| --- | --- | --- | --- | --- | --- |
| Turns to revive | 10 | 8 | 6 | 4 | **3** |

**The two pressures arrive at different stages, which is the point.** Speed
front-loads: it bites in the first handful of cycles, turning a farm from a
leisurely rhythm into something that keeps interrupting you, and it bottoms out
at three before the ladder is halfway up. The statline back-loads: `+5` a cycle
is negligible early and lethal by fifteen. So the early farm gets *busy* and the
late farm gets *dangerous*, and neither stage feels like the other one repeated.

Three is a floor rather than a curve because a two-turn revival is not a fight,
it is a treadmill — the party would spend every turn re-killing the same thing
and never get to decide anything. At three there is still room to reposition,
swing at something else, or leave.

**It is what makes retreat expensive.** Resurrection only stops when the boss
dies (§4.3), so a party that turns back without one has to climb through every
floor it farmed — and the dummies there are now stronger, and getting up in
three turns instead of ten. Farming does not merely arm the dummy in front of
you; **it arms the corridor behind you.** That cost lands precisely on the
player who farmed deepest and then lost their nerve, which is the right person
to charge for it.

It also sharpens the boss kill into the thing the run is actually *for*. Before
it, the dungeon is an infinite accelerating farm; after it, everything stays
down and the climb out is finite (§4.4). The boss is not the last obstacle
between you and the exit — it is what makes an exit exist.

**Both numbers are knobs**, and they are coupled: shortening the floor or
steepening the slope makes deep farming untenable long before the statline does,
which would waste the ladder §3.2 builds.

**The bonuses land on the actor, not on the weapon.** A dummy hitting for `+15`
does not drop a weapon with `+15` on it; drop quality comes from the ladder
above and nothing else. Otherwise farming would pay twice for one investment,
and the flat bonus would leak into a modifier system that has no place to put
it. Store the accumulated values on `EnemyState` alongside `DefeatCount` so
saves round-trip without replaying rolls (§5.1), and roll on the loot stream's
sibling — `mapSeed ^ ReviveSalt` — never a continuation of an existing one.

**The clean-kill check needs the *unclamped* damage**, not the amount actually
subtracted — a 40-damage crit into a 24-HP dummy has to read as 40. The unified
attack resolver (Phase 0) knows both; the defeat handler currently knows
neither, since `EnemyDefeated` fires after the fact. Pass the post-mitigation,
pre-clamp figure through with the defeat and compare it to `MaxHp` there.

Two revival rolls fire on a clean kill rather than one, so the statline and the
timer both advance twice — and since one of those rolls may add max HP, the two
must resolve **in order** rather than both reading the pre-kill statline.

**The scaling never stops, and that is what lets the drop ladder run forever
too.** The reward curve flattens into pure probability once the stack allowance
is spent around `DefeatCount 20`, while the danger curve keeps climbing at the
same rate it always did. Farming therefore prices itself — every further cycle
is strictly more dangerous for strictly less, and the player is the only one who
decides where that stops being worth it. No table needs to tell them.

### The unique is the only thing that undoes the cost

Every stack the farm grants trades the weapon's future for power now, out of the
budget the enchanter would have filled. By the time the allowance is spent, a
two-modifier weapon has nothing left anywhere: both its dials sit at
`forged + 5` and **the enchanter can never deepen it again**. You bought a
finished weapon with everything it could have become.

The unique roll is the one outcome that undoes that. A unique's spread is
**forged** (§1.5), so winning the roll lands you on a deeper base *with the
acquired budget untouched* — Widowmaker at `CritWindow ×3`, ceiling 8, five
stacks still to spend. Not a better version of the same weapon: the opposite
kind of object.

So the deep farm is a genuine gamble rather than a grind, and it sharpens as it
goes. Early cycles buy stacks cheaply. Late cycles buy nothing but odds, against
an enemy that has come back twenty times, in a run you still have to climb out
of. Lose the roll and you carry out the most finished weapon in the game; win it
and you carry out the best **project** in the game.

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
| **Tier** | Uncapped; **earned by use** — mana it spends is its XP — lifting lock *and* potency together |

Insufficient mana means it simply **does not fire** — no failure state, no
penalty, just a resource gate. The same is true one level up: a lock you cannot
afford leaves the enchantment **dormant** rather than making the weapon
unequippable (§3.1).

### Every trigger costs mana, without exception

**No enchantment may have a trigger cost of zero.** Whatever fires it — a hit, a
crit, a kill, being hit, taking lethal damage, anything invented later — firing
costs mana. Siphon is the case that had to move: it restores mana on a kill, and
it now pays 5 to do it. Net positive, still the engine that sustains everything
else, but no longer free.

This is a rule about the *system's* integrity rather than about any one
enchantment, and it holds three things up at once:

- **The resource gate stays real.** A free trigger is a passive, and a passive
  does not participate in the budget that §3.3 uses to balance everything. One
  zero-cost enchantment would be strictly better than every priced one at equal
  effect, and the temptation to add a second is exactly how a mana system stops
  mattering.
- **Levelling works on the same rule everywhere.** Tier is earned from mana
  spent, so a free trigger would be an enchantment that never levels — the
  single dead end in a design that forbids dead stacks. Guaranteeing a cost
  guarantees a path.
- **`Resonant` always has something to discount.** A weapon whose enchantments
  fired free would gain nothing from mana efficiency, so `Resonant` would be
  inert on exactly the build it exists for.

**It also simplifies the XP rule.** Tier used to be earned from mana *moved*
rather than spent, purely so a zero-cost Siphon could still level. With no
zero-cost triggers left, the special case goes:

```
enchantXp[enchantment] += manaSpent * rate(INT)
```

Mana **spent**, which is the same quantity §2.2 already credits to max mana. One
number, two ladders, no second definition.

### Tier is earned by use, not bought

```
enchantXp[enchantment] += manaSpent * rate(INT)
```

**Mana is an enchantment's experience.** Every trigger it pays for feeds its own
pool — the same `manaSpent` credit that already
grows max mana (§2.2), counted a second time against the thing that spent it.
An enchantment you fire constantly gets better at what it does, and one you
carry does not.

That is the pattern the whole game already runs on: health levels from being
healed, mana from being spent, weapon proficiency from damage dealt, wear from
swinging. An enchantment levelling from the mana it spends is the same rule
reaching the last system that lacked one.

Every enchantment can level, because **every trigger costs something** (above).
There is no enchantment in the game that fires for free, so there is none that
sits at tier 1 forever.

### Tier is uncapped, because the pool is the cap

Lock and potency both scale linearly with tier — `lock × tier`,
`potency × tier` — so a tier-3 Arcane locks 90 and a tier-6 locks 180. The
enchantment never stops improving and never needs a ceiling, because every tier
it earns is a tier's worth of pool you can no longer spend on anything else.

**The brake is built into the fuel.** Levelling an enchantment means spending
mana; the tier that spending buys locks more of the pool that mana comes from.
So the deeper an enchantment gets, the smaller the remainder available to feed
it, and the climb slows on its own with no curve authored anywhere. A tier-6
Arcane on a 200-pool wizard has 20 mana left to fire with, which is two
more triggers and then nothing — the enchantment has very nearly eaten the
character that grew it.

And the mana itself only comes back from **movement left unspent at end of
turn** (`PartyMemberState.RegenManaFromUnusedMovement`). So the deep enchantment
is ultimately paid for in the game's real currency, by a caster standing still
to afford the thing that makes standing still worthwhile.

This is the same principle §1.1 states for modifiers, arriving at a different
place because the constraint is different. Modifiers cap at `forged + 5` because
nothing else limits them; enchantments cap nowhere because **max mana already
limits them, continuously and painfully**. A capped tier would be a second
limit on a thing that is already the most constrained system in the game.

What that buys is a genuine build fork with no right answer:

- **Deep and few.** One enchantment at tier 6 on a wizard with a large pool: a
  single overwhelming effect, most of the pool locked, very little left to fire
  with. Wants an on-kill condition, since those fire rarely and pay out.
- **Shallow and many.** Five tier-1 enchantments: a weapon that does something
  on every condition in the game, and a spendable remainder large enough to
  actually trigger them.

Both cost the same pool. Neither is a strictly better use of it, and INT is what
raises the ceiling on both.

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

The one place class enters is the **drop roll** (§3.1), and even there it only
sets the odds of arriving with one, never which one. A staff is guaranteed an
enchantment; it is not guaranteed a *caster's* enchantment, and Ward on a
Staff of Mire is a perfectly ordinary drop.

### Starting set

Potency values are before INT scaling.

| Enchantment | Lock | Trigger | Fires on | Effect |
| --- | --- | --- | --- | --- |
| **Arcane** | 30 | 8 | Hit | Bonus damage — **the wizard-DPS core** |
| **Vampiric** | 20 | 10 | Crit | Heal the wielder for damage dealt |
| **Echoing** | 20 | 15 | Attack | The weapon's class feature triggers once more |
| **Shattering** | 25 | 10 | Crit | Crit riders (§1.6) land one level deeper |
| **Flaming** | 20 | 5 | Hit | Flaming damage; the type chart applies (§1.4) |
| **Shocking** | 20 | 5 | Hit | Shocking damage |
| **Acidic** | 20 | 5 | Hit | Acidic damage |
| **Cold** | 20 | 5 | Hit | Cold damage |
| **Regeneration** | 15 | 5 | Hit | Heals per turn, decaying — Staff of Renewal's |
| **Ward** | 20 | 5 | Hit | Absorbs damage until spent — the Staff of Warding's |
| **Poison** | 20 | 5 | Hit | Damage per turn, decaying — Staff of Blight's |
| **Mire** | 25 | 5 | Hit | Cuts the target's movement budget — Staff of Mire's |

**`Arcane` was `Arcane Edge`.** An edge is a thing a blade has, and the
enchantment goes on wands — where there is no edge and the whole point is that
it works anyway. The shorter name says what it is: raw magical damage, no
element, and therefore nothing the type chart can resist or amplify.

That last part makes `Arcane` the **safe** damage enchantment and the four
elements the **situational** ones. Arcane never gets halved and never gets the
`×1.5`; an element is better in half the dungeons and worse in a quarter of
them. Which to carry is a real question rather than a strictly-ordered one.

### Only opposed enchantments exclude each other

A weapon may carry **any combination of enchantments that do not oppose**. The
only exclusion in the system is the type chart's own pairing:

| | Can share a weapon? |
| --- | --- |
| `Flaming` + `Cold`, `Shocking` + `Acidic` | **No** — opposed |
| `Flaming` + `Shocking` (any non-opposing elements) | Yes |
| `Arcane` + anything | Yes — it opposes nothing |
| Everything else | Yes |

This is the same rule §1.1's displacement group runs on, arriving in the other
system for the same reason: one hit cannot be two contradictory things. It needs
no relation table of its own, because `opposite(t)` already exists for the chart
and this is that function read once more.

**The lock budget is what actually limits breadth**, not this rule. A weapon
carrying four enchantments has locked most of a pool and has nothing left to
fire them with (§3.3), so "any non-opposing combination" is permissive on
purpose — the constraint that matters is already doing its job elsewhere, and a
second one would just be a cap by another name.

### Unique enchantments

Four of these are **not in the catalogue** — they exist only on uniques (§1.5),
cannot be chosen at the enchanter, and cannot be copied:

| Enchantment | Lock | Trigger | Fires on | Effect |
| --- | --- | --- | --- | --- |
| **Siphon** | 20 | 5 | Kill | Restore mana, net positive |
| **Weightless** | 25 | 5 | Attack | Attacks cost less movement |
| **Sturdy** | 30 | 40 | Lethal damage | Survive at 1 HP instead |
| **Momentum** | 30 | 10 | Kill | Refund part of the swing's movement cost |
| **Overheal** | 25 | 8 | Healing above full | Convert the excess into `Ward` at `OverhealPerWard` to 1 |

Look at what they have in common: **every one of them bends a rule the rest of
the game is built on.** `Siphon` breaks the mana economy's dependence on unspent
movement. `Weightless` and `Momentum` refund the movement that *is* the game's
currency. `Sturdy` denies death. `Overheal` un-wastes the one resource the
design deliberately throws away. None of them is merely a larger number, and
none of them belongs in a list the enchanter can hand out on request.

That is the test for whether something is unique-level: **not "is it strong" but
"does it break the frame."** An enchantment that does more damage is a catalogue
entry however much damage it does. One that gives movement back is not.

`Momentum` arrived here by a different road and confirms the rule — §1.1 sent it
to the enchantment layer because as a weapon modifier a movement refund loops
(refunded movement buys the next swing, which refunds again). Making it unique
closes the last of that: it loops on *one weapon in the game* rather than on any
weapon the graft roll touches.

### `Overheal` and the one resource the game throws away

`TickStatusEffects` caps healing at missing HP (`TurnSystem.cs:589`), so healing
a healthy target does nothing at all. That is deliberate and load-bearing —
§2.2 rests "constitution grows by getting hurt and then healed" on it, and a
party that never takes a scratch never gains HP.

**`Overheal` converts the excess into `Ward` instead of discarding it.** Heal a
full-HP ally for 12 and they gain 12 shield. It is the healing analogue of
`Siphon` and `Momentum`: all three take something the design explicitly wastes
and hand it back, which is exactly why all three are unique-level rather than
catalogue.

**It grants no HP XP, and that matters more than it looks.** The converted
overflow is `Ward`, not healing — `hpXp` still counts only what closed an actual
wound. Without that guard a healer parked next to a full-HP party would farm CON
forever off a resource that costs nothing, and §2.2's emergent rule would
collapse into a stat you grind by standing still. So the overflow stops being
wasted for *survival* while staying wasted for *progression*, and the sentence
"constitution grows by getting hurt and then healed" survives intact.

**It is the one unique enchantment that wants a second enchantment to matter**,
and that requirement decides where it drops. `Overheal` on a weapon with no
healing source is inert, so it can only be handed out on a shape that carries
two souls — which is the `Light` artifact and nothing else (§1.5).

**So it has exactly one home: the Efficiency dagger**, paired with `Vampiric`.
Needing a partner is not a drawback worked around; it is a requirement, and
`Overheal` goes where its dependency can be satisfied rather than where its
effect is easiest to explain.

**A staff version exists, and you build it.** Transfer (§6.5) is the only route
to a second copy — every unique enchantment is `neverRolled` and sits on one
weapon — so moving `Overheal` onto a Staff of Renewal costs a tier and a
service, and turns a healer's routine overflow into a party-wide shield. That
is the transfer rule paying off on the enchantment least obviously portable, and
it only works *because* the staff is not where it starts. An `Overheal` that
dropped on the obvious weapon would have nothing left to reward.

The two builds are genuinely different, which is what makes the move worth
paying for:

| | Source of surplus | Who gets the `Ward` |
| --- | --- | --- |
| **The dagger** (where it drops) | Its own crits, via `Vampiric` | The wielder, mid-fight |
| **A healing staff** (where you move it) | Every overheal on a healthy party | Whoever was topped up

**The conversion is lossy, and that is the first of its two brakes.**
`OverhealPerWard` — start at **5** — means 20 points of surplus healing becomes
4 points of `Ward`, and 4 points of `Ward` is 4 HP it will later save you. A
ratio rather than 1:1 keeps it a salvage mechanism rather than a second healing
pool: you are recovering something that was going to be thrown away, at a
discount, which is the honest shape for it.

### `Ward` is a pool that decays, and that is the second brake

`Ward` was going to be a pool that never decays, which was fine only while a
staff cast was its single source. With `Overheal` producing it continuously, an
undecaying pool accumulates until the fight ends.

So `Ward` keeps being a pool — points of damage it will absorb — and gains the
one thing every other status already has:

| | |
| --- | --- |
| **Absorbing** | Spends **one point per HP saved**, one for one |
| **The floor** | Never reduces a hit below **1 taken**, exactly as `Block` does (§1.1) |
| **Re-application** | Accumulates |
| **Decay** | **One point per turn**, at the start of the wielder's turn |

So a 20-damage hit into 20 `Ward` leaves **1 damage through and 1 point
remaining** — 19 absorbed, 19 spent. And that last point is gone at the start of
your next turn.

**Decay is slow against a fight and fast against a run**, which is the whole
reason 1 a turn is the right number rather than a percentage. A shield survives
the engagement it was raised in and bleeds away on the walk to the next one, so
`Ward` is something you carry *through* a fight and never something you arrive
with. Nobody stockpiles a shield in the hub and cashes it on floor 9.

**Two drains on one pool is what makes `Overheal` self-limiting**, and it needs
no ceiling to do it. Levels arrive at `healing / 5` a turn, time removes one a
turn regardless, and being hit removes them far faster than either. The
equilibrium moves with how hard you are being hit — which is the §1.1 answer
rather than the bespoke max-HP cap this was heading toward.

**It also makes `Overheal` and a Staff of Renewal a rhythm rather than a
stockpile.** Regeneration is itself a decaying level count, so a long heal on a
healthy target grants `Ward` slowly and the earliest points bleed off before the
last ticks land. You get the shield you had *while* the healing was happening.
Overhealing someone is a thing you do during a fight.

### Party-stacked `Ward` accumulates, and it is allowed to be silly

Four characters can pile `Ward` onto one front-liner. It **accumulates** rather
than overwriting, and that is deliberate — an ally's temporary health is still
temporary health, and a rule that made the second caster's contribution vanish
would be a bespoke exception to the one thing this status does.

Which means a party that spends long enough at it can walk into a boss room
carrying an absurd pool and **gum an overlevelled boss to death** — soaking
enormous hits one point at a time while chipping it down at whatever damage a
party of healers manages. That is a legitimate strategy and it should stay one.
It is also very funny, which is not nothing.

**Three things bound it, and none of them is a rule written for this.**

- **Mana throughput.** A cast is 20 mana and regen is a trickle from unspent
  movement (§1.3), so a large pool costs hundreds of turns of standing still.
  The cost is paid in the game's actual currency.
- **The dungeon is not safe while you do it.** Resurrection runs until the boss
  dies (§4.3), and every cycle makes the dummies stronger and quicker to return
  (§3.2). Stockpiling in a corridor for two hundred turns means fighting
  everything on that floor several times over, at escalating difficulty, for
  free. **You cannot bank in peace before the boss, and after the boss there is
  nothing left to spend it on.**
- **Decay charges you for the walk.** One point a turn means the pool shrinks
  every step between where you built it and where you need it.

And the 1-damage floor means it is never immortality — every hit still lands for
at least 1, so a big enough pool buys a *long* fight rather than an unlosable
one. Which is exactly the shape "gum it to death" should have: you do not become
invincible, you become extremely difficult to finish, and you still have to do
the finishing.

### `Ward` is temporary health, not armour — so crits do not bypass it

`Block` is skipped entirely on a crit (§1.6): that is the universal way through
armour and the reason a natural 20 matters. **`Ward` is not skipped**, and the
reason is categorical rather than a balance decision.

**`Ward` is temporary hit points that decay.** It is not mitigation — it does
not reduce a hit, it *takes* it, one point per point, and is consumed doing so.
A crit finding the gap in armour is a coherent thing to say; a crit finding the
gap in *being alive* is not. There is nothing for it to bypass.

That reading settles several questions at once, which is the sign it is the
right one:

| Question | Answer, from "it is temporary HP" |
| --- | --- |
| Does a crit bypass it? | No. Crits bypass armour; HP is not armour |
| Why one point per HP saved? | Because that is what a hit point is |
| Why the 1-damage floor? | It is `Block`'s floor, and the pipeline applies it once at the end (§1.6) |
| Why does it decay? | Because it is *temporary* — the word is doing the work |
| Does it stack with `Block`? | Yes, and in that order: armour reduces the hit, then the hit spends HP |

So the party does end up with a crit counter, but it was never designed as one.
It is what happens when a pool of extra health meets an attack that ignores
armour: the armour does nothing and the health does exactly what health does. A
Bulwark carrying `Block ×8` still eats a natural 20 at full force; the same
party with a staff up takes it on the temporary health and has none left
afterwards.

That is a better shape than making armour crit-proof, which would have taken the
natural 20 away again — and it arrived without a rule.

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

`PartyMemberState` gains `UsableMaxMana = MaxMana − Σ *paid* locks`. Equipping
never rejects: locks are paid in order until the pool runs out, and the
remainder are dormant. Dormancy is therefore derived state, recomputed whenever
max mana or the equipped set changes — never stored on the enchantment, which
is shared and immutable.

`Tier` is an `int` with no upper bound; `EffectiveLock` and `EffectivePotency`
are `Base × Tier`. Nothing in the type should express a maximum.

**Tier is derived from XP, not stored.** An enchantment instance carries `Xp`
and computes `Tier` from a curve in `Progression.cs`, exactly as
`WeaponXp`/`HpXp`/`ManaXp` already work (§2.2). That keeps one levelling
mechanism in the game rather than two, and makes the farm's contribution (§3.2)
a plain XP grant instead of a special case.

`TurnSystem` credits it wherever mana moves: `TryCast` and the trigger dispatch
point both already exist for §2.2's `manaXp`, so this is a second credit at the
same call sites, against the enchantment that moved the mana rather than the
member that owns it.

**Damage types are enchantments, but they resolve in `CombatRules`.** The
opposition table (§1.4) is a static four-entry map; a `DamageType?` on the
incoming attack and an `Attunement` on `EnemyState`, populated by the floor
theme (§4.3), are what it reads. It applies **after** `Block` and follows the
same rule crits do about ordering — worth pinning in a test, since a halved
attack that is then blocked and a blocked attack that is then halved are
different numbers.

**The catalogue is campaign state.** §6.4's "choose from what you have seen"
means `CampaignState` holds a `HashSet<EnchantmentId> Seen`, added to whenever
a weapon carrying one enters your inventory. It is small, but it is save data
(§5.1) and it is the one collection that only ever grows.

`TurnSystem` needs a **trigger dispatch point** per condition — on hit, on
crit, on kill, on being hit, on cast. Phase 0's unified attack resolver is
where hit/crit/kill all pass through, so this is one call site rather than the
several it would have been before.

Note `Weapon` is a `record` shared by reference from `GameConstants.Weapons`
today, so dropped instances must be **copies**, never mutations of the shared
table — both `ModifierSet` and the enchantment list should be immutable.

`PartyMemberState.MaxMana` subtracts the equipped item's total lock.


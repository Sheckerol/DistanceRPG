# Phase 3 — Loot and enchantments

## 3.1 Drops are class-locked, variant-rolled

An enemy drops a weapon of **its own class**, rolled uniformly among that
class's four variants. `EnemyPlacer` already assigns each dummy a weapon from a
seeded stream (`EnemyPlacer.cs:49`); the drop reads that class back. Placement
rolls over the eight **classes**, with the variant rolled at drop time on the
loot stream.

## 3.2 Repeat kills deepen the drop

Dummies resurrect after 10 turns (`GameConstants.DummyResurrectTurns`) and
already record `DefeatedAtTurn`. Add `DefeatCount` to `EnemyState`: the *n*-th
defeat of the same dummy deepens the weapon it is carrying.

Repeat kills do **not** drop enchantments. Enchantments are applied at the
enchanter between runs (Phase 6), not found in the dungeon — they are the
chosen half of itemisation, and finding them at random is what would make them
feel farmed rather than built.

This makes the resurrection timer a deliberate farming rhythm rather than
flavour — camp a dummy to deepen its drop, at the cost of the turns you spend
waiting.

**Nothing is handed over at the time.** `DefeatCount` is the quality of the
drop that dummy is *carrying*; you collect it only by killing it permanently on
the way out, after the boss has stopped resurrection (§4.4).

### The ladder has no top; the danger curve is the top

Two things accrue, on different shapes. **Stacks are rolled and run out; unique
chance is a curve and never does.**

### Stacks are rolled twice: whether, and onto what

Each defeat rolls **50% to add one stack**. On a success it rolls again for
*which* modifier receives it, uniformly among the modifiers the weapon is
already forged with, skipping any that have reached their `forged + 5` ceiling
(§1.1).

**A farm grants at most 10 stacks**, and §1.1's per-modifier cap of 5 is what
forces those across **at least two modifiers**. There is no new rule doing that
— five is simply as much as one modifier can take, so the eleventh point of
investment has nowhere to go but sideways.

The allowance is never wasted, because **no weapon is forged with fewer than two
modifiers** (§1.2). That is the whole reason every class baseline carries a
second: without it a Purity variant would hold one modifier, absorb five stacks,
and be the one weapon in the game you could not fully invest in.

A fully farmed Assassin's Fang therefore comes out `CritWindow ×2+5`,
`CritMultiplier ×1+5` — both dials of the crit build maxed, from one weapon that
was carried long enough.

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
| Tower Guard | `Block ×2`, `CritMultiplier ×1` | `Block ×7`, `CritMultiplier ×6` — **both maxed** |
| Assassin's Fang | `CritWindow ×2`, `CritMultiplier ×1` | `CritWindow ×7`, `CritMultiplier ×6` — both maxed |
| Disarming Kris | `CritWindow`, `CritMultiplier`, `CritWeaken` | 10 spread across three, **none maxed** |

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

**The bonuses land on the actor, not on the weapon.** A dummy hitting for `+15`
does not drop a weapon with `+15` on it; drop quality comes from the ladder
above and nothing else. Otherwise farming would pay twice for one investment,
and the flat bonus would leak into a modifier system that has no place to put
it. Store the accumulated values on `EnemyState` alongside `DefeatCount` so
saves round-trip without replaying rolls (§5.1), and roll on the loot stream's
sibling — `mapSeed ^ ReviveSalt` — never a continuation of an existing one.

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


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

### The ladder is short and it has a top

| `DefeatCount` | The drop becomes |
| --- | --- |
| 0 | The class variant, rolled |
| 1–2 | Variant `+1` acquired stack on the class signature |
| 3–4 | Variant `+2` |
| **5** | A roll on that class's **unique** table (§1.5) |
| 6+ | Nothing further |

Tier 5 is the only place a unique is **rolled**. A boss can also hand you one,
but only as arithmetic rather than a roll — see §4.3.

Tuning targets, but the *shape* is the decision, and three things fix it.

**Depth, not breadth.** The stacks land on the class signature, never on a
modifier the weapon has no claim to. Breadth is the enchanter's product (§6.4);
if farming produced it too, the two ladders would collapse back into one. Kept
apart, each gives you something the other cannot: **farming buys depth in what
the weapon already is, the enchanter buys breadth and the slow climb.**

Because the stacks land on the signature, they inherit the *forged* ceiling —
6 or 7 rather than the bare 5 an off-class graft would hit (§1.1).

**Farming spends the weapon's future.** Those stacks are **acquired**, so they
come out of the same five-stack budget the enchanter would otherwise fill. A
`DefeatCount 3` dagger arrives at `CritWindow` forged 1 + acquired 2, ceiling 6,
with three of its five acquired slots already gone. Farming a weapon deep
partly consumes its long-term potential — which is the cost the repeat-kill
ladder was otherwise missing, since turns are the cheapest thing a patient
player has.

**It stops at five, and stopping is the point.** §4.3 rests on "deciding when
you have farmed enough is the run's real decision, and it is entirely the
player's to make" — and that is only a decision if farming demonstrably stops
paying. Unbounded, even on a diminishing curve, it becomes a grind-tolerance
test instead. A visible top also has to fit on a nameplate, which §4.5 requires:
`3/5` reads at a glance, an asymptote does not.

### Hitting five inverts the ladder

Tiers 1–4 trade future depth for power now. Tier 5 does the opposite: a unique's
spread is **forged**, so you land on a deeper base *with the acquired budget
untouched* — Widowmaker at `CritWindow ×3`, ceiling 8, five stacks still to
spend.

So pushing to five is not more of the same, it is the thing that resets the
ladder in your favour, and the last two cycles are where the farm stops being
incremental. A max-farmed dummy hands you the best **project** in the game
rather than the most finished weapon.

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


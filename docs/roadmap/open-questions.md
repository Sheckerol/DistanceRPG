# Open questions

Everything else has been resolved into [settled](settled.md), either as a firm
decision, a provisional/tunable number (`tuning.json`, §5.3), or an
accepted-for-now item flagged to watch during playtest rather than pre-solve on
paper. Append here rather than hedging in place if a new one comes up during
implementation.

## What a deposit costs (§6.1, §6.2, §6.4)

Three passages price a deposit differently, and none of them is wrong on its own
terms:

- **§6.1** — a deposit spends the *whole pool*, wear never part-pays and never
  carries over, and `WearCapacity` climbs one step per **full bar cashed**.
- **§6.2** — attaching enchantment #n costs `n × EnchantmentWearCost` out of the
  pool. That constant is listed in §5.3's tuning table and has no value anywhere
  in these documents.
- **§6.4** — capacity grows "`+` a little" when enchanting and "`+` more" when
  refining, which is service-dependent growth rather than §6.1's single authored
  ladder.

The reading that collapses all three is that **the full bar is the price, and
the rising capacity is itself the escalating cost of the next enchantment** —
cash a full 30, get an enchantment, and the next one wants a full 40. That would
delete `EnchantmentWearCost` and §6.4's per-service growth, leaving one number
doing the work. It also raises its own question: a weapon may carry five
enchantments (§6.2) but the ladder has three rungs, so either the ladder grows
rungs or the top rung repeats for the fourth and fifth.

**Deliberately left open until the enchanter can be played.** Nothing is blocked
— wear is Phase 6a and no code exists yet — and the difference between the
readings is a pacing question that a spreadsheet answers worse than an evening
with the game. Phase 6a must not resolve it by accident: whoever builds the
enchanter should surface the choice rather than pick a passage.

## Where `CampaignState` lives (§3.3, §4.1, §5.1, §6.4)

`Logic/CampaignState.cs` holds what the campaign has *met* — the enchantment ids
a weapon has brought into the party's hands — and Phase 3 shipped it as a field
of `DungeonScene`, seeded from the starting loadout in `SpawnParty` and added to
at every pickup. **One scene is one floor, so as written the set starts empty on
every floor**, which is exactly not what "campaign" means.

Nothing reads it before §6.4, so nothing is wrong in play today. But it is a seam
with no owner yet, and it wants one before the enchanter can be built on it:

- **Phase 4 (§4.1)** decides what survives a floor transition. The seen set is
  campaign-wide, not per-floor: it must outlive the scene, and §4.1's "reset when
  you leave the dungeon" does **not** apply to it — §6.4's enchanter is the
  permanent half of the game, and an entry met on floor 2 is still met after the
  party walks out.
- **Phase 5 (§5.1)** saves it as `SeenInOrder`, the ordinal-sorted view, never the
  `HashSet` (a set's enumeration order is not stable across launches).

Recorded here rather than left in a commit message so that whoever lifts the
party out of `DungeonScene` lifts this with it, instead of a Phase 6a implementer
discovering that the enchanter will not offer what the party carried in on
floor 1.

## What `Echoing` does, and who raises `AttackDeclared` (§3.3, §1.7)

`Echoing` is in §3.3's starting set — lock 20, trigger 15, fires on Attack — with
one line of definition: "the weapon's class feature triggers once more." Phase 3
shipped the entry and no behaviour, the `Weightless`/`Momentum` shape, because
two separate things are missing and neither is Phase 3's to invent:

- **The effect has no meaning for six of the eight classes.** A dagger's feature
  is `CritWindow`, a bow's is `Longshot`, a throwing weapon's is `Charges`:
  "triggers once more" reads on `Brace`, `Opportunist` and `Overwatch` — a
  reaction budget, which a second use is obviously a second of — and reads on
  nothing else. A crit window does not trigger; a per-tile damage curve does not
  trigger. Inventing a meaning per class would be eight design decisions taken to
  make one enchantment work, and they would be taken here rather than in §1.1,
  where the class features live.
- **Nothing raises the event it fires on.** `GameEvent.AttackDeclared` is on the
  enum and is raised nowhere: the only place a swing is declared is
  `TurnSystem.ResolveAttackOn`, and every condition the enchantments actually use
  — hit, crit, kill, being hit, cast — is a later event on the same path, which
  is why Phase 1 never needed it. `Weightless` ("attacks cost less movement") is
  blocked on exactly the same thing, so a phase that raises it unblocks two
  entries at once.

Nothing is wrong in play: the entry validates, attaches, reserves its lock and
fires nothing, and `LootTable.DropOrder` keeps it out of the drop pool so a
martial weapon's one guaranteed enchantment can never be it. Whoever raises
`AttackDeclared` — the movement rules around a swing are Phase 4's most likely
owner — should settle both entries then, and should settle what a class feature
"triggering once more" means in §1.1's terms rather than in the enchantment's.

That filter is `DropOrder`'s own, and it is the only one of its kind in the
tree: **`EnchantmentCatalogue.Rollable` is not a grantable pool.** Rollable is
everything outside `neverRolled`, in file order, so both `echoing` and `aegis`
sit in it — and `neverRolled` cannot absorb them, since
`ContentValidator.ValidateNeverRolled` cross-checks that set against the
`unique` flag and neither entry is a unique soul. §6.4's enchanter, specified as
offering `CampaignState.SeenInOrder` filtered against `NeverRolled`, would
therefore offer `Echoing` — declared inert, reserving a lock of 20, able to fire
nothing — and would be the only thing in the game that could. **It must apply
the same live-behaviour test `LootTable.DropOrder` applies** rather than
`NeverRolled` alone. The other half of that filter is why **`aegis` has no
source at all today**: it is kept out of every drop because its behaviour is a
compiled step on the defender rather than a row in any loop, so until the
enchanter exists nothing can put a shield on a weapon that did not ship with
one. Both are filter gaps rather than content gaps, and both are resolved by
whoever writes §6.4's offer.

## What decides a `Serrated`/`Arcane` pair (§3.3)

§3.3's worked example on attachment order reads: "A dagger that leads with
`Serrated` and trails with `Arcane` bleeds reliably and adds damage when it can
afford to; reverse them and it is a damage weapon that sometimes bleeds."
**The shipped architecture cannot do that, and the passage is wrong about why.**

The two entries fire on different events of the same swing: Arcane at step 4 of
`DamageTaken`, Serrated on `DamageDealt` — and `TurnSystem.ApplyDamage` spends
the hit's `ManaToSpend` as one mana record *before* it enqueues `DamageDealt`.
So Arcane is served first in **both** orders, and what Serrated sees is whatever
Arcane left of the pool either way: **event order decides that pair, and
attachment order decides nothing about it.** Both orders read identically, and
`CatalogueEntryTests.SerratedAndArcaneReadTheSameEitherWay_BecauseTheyFireOnDifferentEvents`
pins that they do.

The claim the passage is making is real; it is a claim about entries firing on
the *same* event, which is the only place the loop reads attachment order at
all. `CatalogueEntryTests.AttachmentOrderDecidesWhatFiresWhenThePoolIsShort` is
that claim, pinned with an Arcane/Shattering pair on a pool that covers one of
them. Whoever next edits §3.3 should change the example's pair rather than the
paragraph around it.

**The open question underneath it**: should an entry that fires on `DamageDealt`
see the pool as it stood *before* the hit's step-4 payments? Deferring
`ApplyDamage`'s spend until `DamageDealt` has settled would put both events on
one budget spent in attachment order, and would make the passage true as
written. It is a real choice with real costs — everything queued between the two
events would read a pool that has not yet paid for what it is about to pay for,
and `Spent`/`Wanted` scaling (§3.3's enchantment XP) would have to be taken
across two events rather than one — so it wants deciding rather than
discovering. Nothing is wrong in play today: the entries fire, they pay, and
they are credited what they paid; only the doc's example is unbuildable.

## What "and then nothing" means for a locked-out pool (§3.3)

§3.3's worked case for the enchantment ladder's brake reads: "A tier-6 `Arcane`
on a 200-pool wizard has 20 mana left to fire with, which is two more triggers
and then nothing — the enchantment has very nearly eaten the character that grew
it." The arithmetic is right — 180 of the 200 is locked, 20 is left, the trigger
is 8 — but **what the shipped rule gives is two whole fires, a partial fire on
the 4 that remain, and then nothing.**

Nothing is wrong in play; two settled rules simply meet here. §3.1's
`PartialFire` is what an entry does when the pool cannot cover its trigger: it
fires at the fraction it could pay rather than refusing, so 4 of a trigger of 8
buys half the effect — 12 of `Arcane`'s damage where a whole fire adds 24 — and
is credited the 4 it actually paid (§3.3's enchantment XP). The doc's sentence is
a claim about the brake, and the brake is exactly as tight either way: the budget
is spent to its last point, and the swing after that adds nothing at all.

`EnchantmentLockTests.LockingCompetesWithFiring` pins the shipped reading in the
doc's own numbers — 200 earned, 180 locked, 20 spendable; spends of 8, 8 and 4;
enchantment shares of 24, 24 and 12; then a swing that pays nothing and adds
nothing. Whoever next edits §3.3 should write "two more triggers, a fraction of a
third, and then nothing" rather than move the code toward the shorter sentence:
an entry that refused to fire at all below its trigger would be a new rule, and
it would contradict the one §3.1 already settled.

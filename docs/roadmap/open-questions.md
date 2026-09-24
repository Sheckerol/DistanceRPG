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

## What happens when every dungeon is locked (§4.3, §6.3)

settled.md fixes the lock: "clearing a dungeon locks it for `bossFloor` runs",
counted "by the same events that tick the enchanter, so there is no second
clock". **With every dungeon that is not retired locked, that rule has no way
out**: no dungeon can be entered, so no run can end, so nothing ticks, so no
lock ever lifts. The game would be stuck on the between-runs screen for good.

**The cause is Phase 4's four dungeons, not the rule.** settled.md's first
playable hub has six non-tutorial dungeons; Phase 4 ships four, because the
other two need content that does not exist yet (a Great Axe unique for a
Cleave dungeon, or a repeated theme — Phase 6b completes the roster). With four
and a lock of at least five runs, four straight clears lock every one of them,
certainly: the first lock outlasts the three ticks after it. At six the dead end
needs long locks to line up and is rare; at four it is one ordinary streak away.

**Shipped provisionally, as the least rule that prevents it**
(`CampaignState.EarlyOffer`): with every non-retired dungeon locked, the one
whose lock lifts soonest — ties in `dungeons.json` file order — is offered early.
The next run goes there by default (`CampaignState.DefaultNext`), and entering
it sets its `LockedUntilRun` to the current run counter, so it stands as a lock
that has just lifted; nothing else moves (the cleared set, the other locks, the
counter). It is the only place a lock is ever shortened.

**This changes a settled rule in one corner, so it is the user's to decide.**
The alternatives worth weighing: keep the early offer; let a locked dungeon's
lock tick down on an entry that is not a run (a second clock, which settled.md
rules out); shorten every lock while all are locked; or make the hub's sixth
dungeon a Phase 4 requirement so the corner is rare rather than certain. Phase
6b's full roster makes it rare whichever is chosen — but not impossible, so some
rule has to exist. settled.md's Phase 4 implementation pass records the ruling.

## What a dungeon's theme attunes (§1.4, §4.3, §5.8)

settled.md's entry on theme reads: "A themed dungeon attunes every floor it has;
an unthemed dungeon attunes none of them." §4.3 says each dungeon is themed, so
taken word for word, every dungeon attunes every floor. But the theme §4.3
means is a modifier (`Block`, `Brace`), which names no element to attune
anything to, and §5.8 gives `dungeons.json` an `attunement` field of its own: "a
damage type, or none". The entry cites §1.4, whose type chart speaks of "a
Flaming floor" and "a Cold dungeon", and read there "themed" means
element-themed.

**Shipped provisionally: attunement is its own field.** A dungeon's attunement
is `DungeonDef.Attunement`, apart from its modifier theme, and every enemy the
dungeon spawns carries it. None of the five shipped dungeons sets one, so
nothing in play is attuned yet and the type chart (the same element halved, the
opposed one ×1.5) changes no hit. The mechanism is built and tested on a
fixture dungeon. The tutorial's golem teaches `Block`, not the chart.

**This reads a settled entry one way rather than the other, so it is the user's
to decide.** The alternatives: keep it, and attune a dungeon only where content
says so; take the entry word for word and attune every dungeon, which needs an
element chosen for each of the five; or attune the four ordinary dungeons and
leave the tutorial unattuned, so the golem still teaches `Block` alone. The
last two are content edits, one field a dungeon, with no code to change.
settled.md's Phase 4 implementation pass records the ruling.

## What class a boss drops (§3.1, §4.3, §5.8)

§3.1 says "an enemy drops a weapon of its own class": the drop reads back the
class the enemy holds. §4.3 says the boss's drop is a guarantee, so "its drop
table bends to it rather than the other way round", and that "the golem's table
is all eight classes"; §5.8 computes that table by running `Allowed` over the
eight. The two meet at the boss, because an enemy holds only the close-range
classes `EnemyPlacer` fields (settled.md's `EnemyPlacer` entry): a staff in an
enemy's hands flees, and a wand has no enemy AI. Read off the weapon it holds,
the golem's table would shrink from all eight classes to those, and the Long
Gallery (`Longshot`, a bow's class feature) could never drop a bow — so its
unique, Stormcrow, could never drop at all.

**Shipped provisionally: what a boss holds and what it drops are two draws.** It
holds a class from its theme's table narrowed to what an enemy fields
(`DungeonCatalogue.WieldClasses`), and drops a class drawn over the whole table
(`BossLoot.Roll`, which never reads the held weapon). So a golem swinging a
hatchet can drop a bow, a staff or a wand, each forged with `Block`.

**This departs from §3.1 for the one enemy whose table bends to the guarantee,
so it is the user's to decide.** The alternatives: keep it; read the drop off
the held weapon as §3.1 says, which shrinks every table to the classes an enemy
fields, so the Long Gallery drops neither a bow nor its unique until enemies
can use one; or let the boss hold any class in its table, so one draw serves
both, which waits on the kiting AI and wand placement that the `EnemyPlacer`
entry waits on. settled.md's Phase 4 implementation pass records the ruling.

## What the golem holds, and the Block it stands behind (§4.3)

§4.3 gives the golem `Block ×3` innately, 9 absorbed, "whatever it happens to be
holding", and has resolution read weapon plus innate. It then works the lesson
through as though the weapon added nothing: the sword's 10 lands for 1, the
dagger's 15 for 6, the axe's 18 for 9. But the golem holds a class an enemy
fields, drawn like any boss's, and one of those five is the sword, whose every
variant forges `Block`. So about one tutorial in five the golem stands behind
more than 9: 12 behind an Arming Sword, a Riposte Blade or a Warden's Shield
(hits of 1, 3 and 6), 15 behind a Tower Guard (1, 1 and 3).

**Shipped provisionally: weapon plus innate is literal.** The golem adds its
sword's Block, its plate shows the Block it stands behind, and the dagger's crit
still lands 45. The lesson holds either way (small hits are terrible against
flat Block, large ones are fine, a crit ignores it), but the numbers are §4.3's
only when the golem holds no sword.

**This bends §4.3's worked numbers in those runs, so it is the user's to
decide.** The alternatives: keep it; or keep the golem's hands off any class
that already forges its theme, so it always stands behind 9. That cannot be the
rule for every boss as things stand: the Quiver Pits' boss can hold only
throwing weapons, and every one of them forges `Charges`, which would leave it
nothing to hold. So it would be the tutorial's rule alone, or a preference that
falls back to the whole table where nothing else remains. Either way the
tutorial's boss rolls change, and the seeds its tests pin move with them.
settled.md's Phase 4 implementation pass records the ruling.

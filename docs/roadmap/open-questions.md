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
everything outside `neverRolled`, in the catalogue's order, so both `echoing`
and `aegis` sit in it — and `neverRolled` cannot absorb them, since
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

## Whether a content file's numbers have bounds (§5.4, §5.6, §5.7)

Phase 5.2 put every enchantment's numbers in `enchantments.json`, a file a
playtester edited by hand. Since the user's ruling of 2026-09-25 they sit in the
enchantment catalogue, which is code (`EnchantmentDefinitions`, §5.7) and still
validated at load, first and on its own, by
`ContentValidator.ValidateEnchantments` — which checks only a few of them: a
`lock` of at least 1 (`RuleEnchantmentLockIsPositive`), a `trigger` that is
never 0 or less (`RuleZeroTrigger`), and a `potency` of at least 1 for the three
kinds whose whole magnitude is their potency — bonus damage, Shattering and
Shielding (`RuleMagnitudeNamesAPotency`). **Nothing else is bounded.** Phase
5.2's review loaded `vampiric` with `"potency": -5`, `"applyPercent": -5`,
`"potency": 2147483647` or `"lock": 2147483647`, and each played without a word;
the same numbers written into the catalogue's row would too.

Nothing is wrong in play today: every shipped number is sensible. The risk is the
edit — by hand in a file, or to a row of the catalogue. `Enchantment.LevelsFor`
multiplies `applyPercent`, the source and the tier straight through, so a sign
typo lands negative status levels or negative healing; and
`Enchantment.EffectiveLock` is `Lock x Tier` in unchecked arithmetic, so a huge
lock wraps negative, and so do the mana sums built on it. The dialled numbers —
`ApplyPercent` and `ArcanePotency` — are `tuning.json`'s alone, clamped to
0–1000, so the catalogue's rows and the clamped dials disagree about how far
such a number is trusted.

**`weapons.json` has the same gap** (Phase 5.3). A weapon's forged spread is
bounded — every stack at least 1 (`RuleForgedStackCount`) and at most its
`MaxForged` (`RuleMaxForged`) — but nothing checks a statline's `range`,
`damage`, `cost` or `manaCost` — a class row's, since the ruling gave the classes
a section of their own, or a variant's own — or a wand shape's dimensions, and
an enchantment reference's `tier` answers only to the unique shapes' own rules,
none of which sets a ceiling. Phase 5.3's review loaded the Halberd with a
`range`, `damage`, `cost` or `manaCost` of -1, 0 or -2147483648, the Staff of
Mire's `manaCost` and the Wand of the Blast's `radius` and `targetReach` at the
same three, and the Nameless Knife's `vampiric` at tier -1, 0 or 2147483647, and
each loaded without a word.

These fail more quietly than the enchantment numbers, because play floors some
of them. A `tier` below 1 plays as 1 (`WeaponCatalogue.Instantiate` floors it),
so the file names a tier the game never plays; a huge one goes through
`Lock x Tier` unchecked, so `vampiric`'s lock of 20 at tier 2147483647 wraps
to -20. A `cost` below 1 is charged the one-unit minimum wherever movement is
paid (`Progression.MovementCost`), while the HUD's `COST` still shows a number
below 1. A `manaCost` below 0 leaves `ResolvedManaCost` at 0 or below, so the
cast gate (`Mana >= ResolvedManaCost`) passes on an empty pool. `damage`,
`range` and a shape's dimensions are read as written. `dungeons.json` already
bounds its numbers — a boss's statline and innate stacks at least 1
(`RuleBossStatline`, `RuleBossInnateStackable`), a boss floor range that starts
at 1 and never inverts (`RuleBossFloorRange`) — and the starting party's stats,
code since the ruling, are still checked at load as a permutation of 1 to 4
(`RulePartyStatPermutation`); so the enchantment catalogue and `weapons.json`
are the two whose numbers the load mostly takes on trust.

Two readings, and the choice wants a ruling rather than a guess — one ruling
for both, the catalogue and `weapons.json`, since the question is the same:

- **Range rules.** `ContentValidator.ValidateEnchantments` refuses a negative
  `potency`, an `applyPercent` outside 0 to a ceiling, and a `lock` or `trigger`
  above a ceiling; `ContentValidator.ValidateWeapons` refuses a statline number
  (a class row's or a variant's own), a shape dimension or a `tier` outside a
  range of its own — per field, since a staff's `damage` and a martial weapon's
  `manaCost` are 0 today. Each is a startup failure naming the entry and the
  rule — content is "validated rather than clamped" (§5.4), so never a clamp.
  The ceilings are numbers someone must pick; `tuning.json`'s 1000 is the
  nearest precedent.
- **Trusted fields.** These numbers are the designer's, and the load refuses only
  what makes an entry mean nothing: a free lock, a free trigger, a magnitude of
  zero, a spread the relations refuse. [Settled](settled.md) then says so for
  both, and a negative potency or cost is a choice the catalogue or the file
  permits rather than an oversight.

Phase 5 brings the content files without adding a new kind of content rule, so
it ships neither, and the content rework after the ruling ships neither too:
whoever rules on this should add the rules, or the settled line, then.

## What the d20 does across a reload (§5.2)

§5.2 asks that "a fixed sequence of turns produces an identical world state"
after a save and a reload, and says nothing of the dice. Every other random
stream in `Logic/` is drawn fresh per use off the map seed and a counter the
save already keeps (a dummy's revivals rolled, its defeats), so it replays by
construction. The d20 is the one stream consumed a draw at a time, and until
Phase 5.7 it was `Random.Shared` in the scene: a reload rerolled every die still
to come.

**Shipped provisionally: the visit's own die, seeded and saved.** Each draw is
a stream of its own, keyed by the visit's map seed and how many the visit has
rolled (`DungeonState.RollD20`, `DiceSalt`, `DiceMix`), and the count is saved
(`diceRolled`). So a reload rolls exactly the dice play without one would have
rolled, a reload is never a reroll, and §5.2's identical world holds in the
shipped game and not only under a test's die (`ReloadDeterminismTests`).

**The trade-off, stated.** With one counter for the whole visit, a player who
kills the process after a turn's autosave replays that turn knowing the draws
to come, and can reorder the turn's actions to route a known natural 20 to the
swing that matters. The mitigation not taken is keying each draw by the turn,
the actor and the attack's ordinal rather than one counter, which pins a roll to
whoever makes it — a new stream family and a key per draw, for foresight it
narrows but does not remove.

**This fills a silence with a choice the player can feel, so it is the user's
to decide.** The alternatives: keep it; key each draw by turn, actor and ordinal;
or go back to `Random.Shared` — one line in `DungeonScene.RollD20`, the visit's
die then serving the tests alone — so a reload is a reroll. settled.md's Phase 5
implementation pass records the ruling.

## How the campaign is saved (§5.1, §5.2)

The phase doc says what a save holds and in what format, and nothing of where
it lives, how many there are, when one is taken, or how a player gets back.

**Shipped provisionally:** one campaign and one slot, in the player's data
folder, with no manual save: the game saves itself at every safe point — a
run's entry, every player turn's start, every floor arrival, a run's end, NEW
CAMPAIGN — and on every quit. A quit that finds the enemies taking their turn
plays it out to the next player turn first, on the game's fixed logic step, and
saves that. A launch resumes the campaign where it stood, between runs or in the
dungeon mid-run, guarded by a `campaign.resuming` marker that sets aside a save
whose resume crashed the game; a save that fails is said at once, and a quit
that cannot save leaves a `campaign.unsaved` marker, so the next launch says
that what it resumes is from before that quit. NEW CAMPAIGN is N, then N,
between runs, and `--new-campaign` sets the old campaign aside at launch. The
dev overrides (`DISTANCERPG_DUNGEON`, `DISTANCERPG_SEED`) play in a scratch slot
of their own and never touch the campaign.

**This is a design the doc left open, so it is the user's to decide.** The
alternatives: manual save slots beside the autosave; a quit during the enemies'
turn saving the turn's start instead, which loses the moves the player made that
turn; a title screen with CONTINUE and NEW in place of resuming at launch; and
dev overrides that apply only when no save exists. settled.md's Phase 5
implementation pass records the ruling.

## Whether a variant may name its own statline (§5.6) — REWORK DECISION 1

The user's ruling of 2026-09-25 made the weapon types a section of
`weapons.json`, and each class row carries the statline its variants share:
§1.2, §1.3 and §1.4 give a class one statline, but for §1.3's staves, which
differ in mana. Before the ruling every weapon row stated its whole statline,
so any of its numbers could differ from its siblings'.

**Shipped provisionally: a variant may name its own statline, field by field.**
Any of `range`, `damage`, `cost` and `manaCost` given on a variant plays for it
instead of its class's, and a unique takes its variant's. The shipped file uses
it only for the Staff of Renewal's 15 mana and the Staff of Mire's 25; the
Staves of Warding and Blight take the Staff row's 20.

**This decides what the file lets a designer say, so it is the user's to
decide.** The alternative: the statline is the class's alone, and only a caster
variant may name its `manaCost` — stricter to the docs' tables, at the cost of a
per-variant tweak becoming a new class. settled.md's Phase 5 implementation pass
records the ruling.

## Where the caster drop order lives (§3.5, §5.6) — REWORK DECISION 2

The ruling makes `weapons.json` the master of every variant, with no copy in
code. `LootTable.CasterVariants` is one: a C# list of eight of its variant ids —
which staves and wands a caster drop rolls between, and in what order. So a
caster variant added to the JSON never drops without a code edit.

**Shipped provisionally: kept in code.** The drop's draw indexes the list, and
§3.5 wants a saved, not-yet-collected dummy's drop never to move under a content
edit: with the order held in code, a reorder of `weapons.json` re-rolls nobody.
It is cross-checked at load — a caster class must hold every variant the list
names (`RuleCasterVariantPool`) — and settled.md's ruling subsection names it as
the one weapon-id list the code keeps.

**This keeps a copy the ruling removes everywhere else, so it is the user's to
decide.** The alternative: an explicit, append-only drop order carried in
`weapons.json` — a `dropOrder` list on each caster class's row — cross-checked
at load: every id a variant of that class, none twice, and a copy beside the exe
keeping the built-in copy's order as its prefix, so an append re-rolls nobody. A
new caster variant then drops with no code edit, at the cost of a stream order a
hand edit can break — and an edit that reorders the master itself is caught by
no load rule, only by review. settled.md's Phase 5 implementation pass records
the ruling.

## Which of the masters' values the tests pin (§5.3–§5.8) — REWORK DECISION 3

The ruling leaves no copy of a master's value in code, and a test that pins a
shipped number as a literal is one: a designer's edit to the JSON fails it.

**Shipped provisionally: only the values a design doc fixes stay literal**, each
pinned where `ShippedDataTests`' summary lists it beside the doc line that fixes
it, so an edit that moves one fails by name and its doc moves with it:
`MovementBudget` 160 and `MovementUnitsPerMana` 10 derived from it
(phase-1-weapons.md's `(160 − 40) / (8 + 3)`); §1.2–§1.4's class rows,
statlines, spreads and wand shapes; §1.5's unique table; and §5.8's tutorial
row, its golem and its boss floor. Every other test reads the master, or stands
on numbers of its own, and the README says which master edits also fail a test.
Three facts come with the choice, so that it is ruled on knowingly:

- **The `Premise` scope.** The suite did not only learn to read the master: 45
  scenario suites run on `GameEngine.DistanceRPG.Tests/Premise.cs`, a frozen
  snapshot — the 113 values of `TestData/saves/pre-rework-tuning.json`, the
  shipped tuning from before the rework, and, as C# literals, §1.2–§1.4's
  statlines, the staves' mana, the wands' shapes and the bosses' statlines, the
  four provisional bosses' 200 and 70 among them (`VisitSaveTests` still asserts
  the Spear Warden's 200 and 70 under it). Those suites no longer exercise the
  shipped numbers, and the premise can drift from the master unseen: the two
  were equal key for key when it was written, and nothing compares them. The
  alternative for this part: those suites read the master, and a master edit
  can fail them.
- **§1.1's modifier table** (phase-1-modifiers.md: `Block` +3 absorbed a stack,
  `Splitting` +3, `Light` −10% of the weapon's cost, `CritMultiplier`'s base ×2
  …) is as doc-fixed as §1.2–§1.4, yet sits on neither side of the list: it is
  pinned nowhere against the master, since `ModifierRulesTests` checks only that
  the rules read and sum the tuning's tables. A candidate addition.
- **The friendly-fire switch** — `FriendlyFireEnabled` false, which
  phase-1-weapons.md L1062–1063 and settled.md L134–136 fix until the wand
  placement scorer exists — is a doc-fixed value that the numbers-only audit
  never moved. It is pinned against the master in
  `WandTests.FriendlyFire_OffByDefault_HalfWhenOn`, and listed with the pins in
  `ShippedDataTests` and the README.

**This decides what a master edit can break, so it is the user's to decide.**
The alternative: no literal at all — every test reads the master, the docs alone
quote their numbers, and no master edit fails the suite. settled.md's Phase 5
implementation pass records the ruling.

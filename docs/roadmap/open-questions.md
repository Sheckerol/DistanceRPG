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

## Whether a content file's numbers have bounds (§5.4, §5.6, §5.7)

Phase 5.2 put every enchantment's numbers in `enchantments.json`, a file a
playtester edits by hand, and the load checks only a few of them: a `lock` of at
least 1 (`RuleEnchantmentLockIsPositive`), a `trigger` that is never 0 or less
(`RuleZeroTrigger`), and a `potency` of at least 1 for the three kinds whose
whole magnitude is their potency — bonus damage, Shattering and Shielding
(`RuleMagnitudeNamesAPotency`). **Nothing else is bounded.** Phase 5.2's review
loaded `vampiric` with `"potency": -5`, `"applyPercent": -5`,
`"potency": 2147483647` or `"lock": 2147483647`, and each played without a word.

Nothing is wrong in play today: every shipped number is sensible. The risk is the
hand edit. `Enchantment.LevelsFor` multiplies `applyPercent`, the source and the
tier straight through, so a sign typo lands negative status levels or negative
healing; and `Enchantment.EffectiveLock` is `Lock x Tier` in unchecked
arithmetic, so a huge lock wraps negative, and so do the mana sums built on it.
Where `tuning.json` owns the same numbers — `ApplyPercent` and `ArcanePotency` —
they are clamped to 0–1000, so the two files disagree about how far such a
number is trusted.

**`weapons.json` has the same gap** (Phase 5.3). A weapon's forged spread is
bounded — every stack at least 1 (`RuleForgedStackCount`) and at most its
`MaxForged` (`RuleMaxForged`) — but nothing checks a statline's `range`,
`damage`, `cost` or `manaCost` or a wand shape's dimensions, and an enchantment
reference's `tier` answers only to the unique shapes' own rules, none of which
sets a ceiling. Phase 5.3's review loaded the Halberd with a `range`, `damage`,
`cost` or `manaCost` of -1, 0 or -2147483648, the Staff of Mire's `manaCost` and
the Wand of the Blast's `radius` and `targetReach` at the same three, and the
Nameless Knife's `vampiric` at tier -1, 0 or 2147483647, and each loaded without
a word.

These fail more quietly than the enchantment numbers, because play floors some
of them. A `tier` below 1 plays as 1 (`WeaponCatalogue.Instantiate` floors it),
so the file names a tier the game never plays; a huge one goes through
`Lock x Tier` unchecked, so `vampiric`'s lock of 20 at tier 2147483647 wraps
to -20. A `cost` below 1 is charged the one-unit minimum wherever movement is
paid (`Progression.MovementCost`), while the HUD's `COST` still shows a number
below 1. A `manaCost` below 0 leaves `ResolvedManaCost` at 0 or below, so the
cast gate (`Mana >= ResolvedManaCost`) passes on an empty pool. `damage`,
`range` and a shape's dimensions are read as written. The other content files
already bound theirs — a boss's statline and innate stacks at least 1
(`RuleBossStatline`, `RuleBossInnateStackable`), a boss floor range that starts
at 1 and never inverts (`RuleBossFloorRange`), a member's stats a permutation
of 1 to 4 (`RulePartyStatPermutation`) — so `enchantments.json` and
`weapons.json` are the two whose numbers the load mostly takes on trust.

Two readings, and the choice wants a ruling rather than a guess — one ruling
for both files, since the question is the same:

- **Range rules.** `ContentValidator.ValidateEnchantments` refuses a negative
  `potency`, an `applyPercent` outside 0 to a ceiling, and a `lock` or `trigger`
  above a ceiling; `ContentValidator.ValidateWeapons` refuses a statline number,
  a shape dimension or a `tier` outside a range of its own — per field, since a
  staff's `damage` and a martial weapon's `manaCost` are 0 today. Each is a
  startup failure naming the entry and the rule — content is "validated rather
  than clamped" (§5.4), so never a clamp. The ceilings are numbers someone must
  pick; `tuning.json`'s 1000 is the nearest precedent.
- **Trusted fields.** These numbers are the designer's, and the load refuses only
  what makes an entry mean nothing: a free lock, a free trigger, a magnitude of
  zero, a spread the relations refuse. [Settled](settled.md) then says so for
  both files, and a negative potency or cost is a choice the file permits
  rather than an oversight.

Phase 5 brings the content files without adding a new kind of content rule, so
it ships neither: whoever rules on this should add the rules, or the settled
line, then.

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

## Where content's truth lives (§5.4)

§5.4 moves the content into five files and §5.3 the numbers into `tuning.json`,
and neither says whether the files or the compiled content are the source the
other follows. Something has to be, or the two drift.

**Shipped provisionally: the compiled content (`ContentDefaults.cs`) is the
source, and the six files are generated from it** (`--write-data`) and pinned
byte for byte by `ShippedDataTests`. A designer edits the copies beside the exe
to playtest, and a change worth keeping is ported into `ContentDefaults` and
regenerated.

**This decides who owns the data, so it is the user's to decide.** The
alternative: the JSON as the source, embedded in the assembly as the compiled
fallback, with `ContentDefaults` reading it — one source a designer edits
directly, at the cost of the C# builders and the comments that derive every
unique from its variant (`ContentDefaults.cs`). settled.md's Phase 5
implementation pass records the ruling.

## Whether class baselines are a section of `weapons.json` (§5.6)

§5.6 opens: "The 32 weapons, the uniques, and the class baselines leave
`Weapons.cs`." They did leave it, in Phase 1, for `ContentDefaults`; the
question is the file's shape.

**Shipped provisionally: derived, never a section.** Every variant row carries
its whole forged spread, and a class's baseline is what its four variants share,
checked at load (`RuleClassBaseline`, `RuleVariantDelta`). A spread has one
source, so nothing can disagree with it — the reason §5.8 gives for computing a
dungeon's drop table rather than writing one.

**This bends a line of the phase doc, so it is the user's to decide.** The
alternative: a `baselines` section, class to spread, with each variant written as
its one added modifier — re-basing a class becomes one edit, at the cost of a
second shape for a spread and a reader that composes the two. settled.md's Phase
5 implementation pass records the ruling.

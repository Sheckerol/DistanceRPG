# Open questions

Everything else has been resolved into [settled](settled.md), either as a firm
decision, a provisional/tunable number (`tuning.json`, §5.3), or an
accepted-for-now item flagged to watch during playtest rather than pre-solve on
paper. Append here rather than hedging in place if a new one comes up during
implementation.

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
and `aegis` sit in it — and the shipped `neverRolled` does not absorb them: it
is the economy rule about souls, and neither entry is a unique soul. (Since the
rework's audit the file could list them — `neverRolled` is `restricted.json`'s
alone, no longer held to the `unique` flag — but "never copied" is not what an
inert entry needs.) §6.4's enchanter, specified as
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

## Whether an edit to the caster drop order may move a saved drop (§3.5, §5.6) — REWORK DECISION 2

Where the order lives is ruled: the ruling makes `weapons.json` the master of
every variant, with no copy in code, so since the rework's audit the order a
caster drop rolls its variants in is the file's — each caster class's row names
its `dropOrder` (the Staff's four staves, the Wand's four wands). It had shipped
as `LootTable.CasterVariants`, a C# list of the eight ids, so a caster variant
added to the file never dropped without a code edit. The load checks the order:
a caster class that fields variants names one, every id a variant of that class
and none twice, and a martial class names none (`RuleCasterVariantPool`). A
variant the order leaves out never drops; one appended to it drops.

What is left is what an edit to the order may do. The draw is uniform over the
order's length, and §3.5 wants a saved, not-yet-collected dummy's drop never to
move under an ordinary content edit. Reordering the file's `variants` moves
nothing, since the draw never reads the file's order. An edit to `dropOrder`
itself — appending a staff, reordering the four, taking one out — does move
the drops of that class's saved caster dummies.

**Shipped provisionally: any edit to the order plays**, a copy beside the exe's
included, as the deliberate, rare change §5.6 already accepts for a fifth
variant: the list says what it is for, beside the class it orders, and the
header of `weapons.json` says what an edit to it moves.

**This decides what a hand edit can do to a saved campaign, so it is the user's
to decide.** The alternative: a copy beside the exe must keep the built-in
copy's order as its prefix — refusing a reorder or a removal there, which moves
the saved drops with nothing new to show for it, while still allowing an
append. It narrows the hazard rather than closing it: an appended variant must
take its share of a uniform draw from the others, so an append moves some saved
drops too, and an edit to the master itself is caught by no load rule, only by
review. (The rework's plan had offered that prefix as keeping an append from
re-rolling anybody; no uniform draw can.) settled.md's Phase 5 implementation
pass records the ruling.

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

## Whether the gameplay numbers still compiled are tuning numbers (§5.3)

The user's ruling puts "numbers driving tuning" fully in JSON, and every
`tuning.json` key now is: the `Tuning` record holds no value, and no key's
number is written in C#. But some gameplay numbers never were keys, and are
still compiled:

- `GameConstants.DummyHp` 50 — a rank-and-file dummy's hit points (a boss's are
  `dungeons.json`'s);
- `GameConstants.MaxMana` 100 — the mana pool of an actor with no progression,
  which is every enemy's (a member's is `StartingPool` and INT's);
- `GameConstants.EnemyMove` 100 — an enemy's movement a turn, the counterpart of
  the `MovementBudget` key, and the numerator of the scale an enemy's costs are
  priced at (`EnemyMove / MovementBudget`);
- `GameConstants.MinEnemiesForStaffHealer` 3 and `StaffHealerChance` 0.5 — when
  a crowded room turns a dummy into a staff healer;
- `CombatRules.OpposedDamagePercent` 150 — the attunement chart's ×1.5
  (phase-1-weapons.md §1.4), beside its halving, the cast crit's doubling and
  the fumble's, which `CombatRules` compiles the same way;
- `Progression.DamagePerLevels` 2 and `MovementDiscountPerLevels` 3 — a
  weapon level's `+floor(L / 2)` damage and `-1` movement per 3 levels
  (phase-2-progression.md);
- `EnemyPlacer.MaxEnemiesPerRoom` 4 — the most dummies a room is placed with.

No document or ruling says whether these are tuning numbers or rules of the
game. What bears on it:

- **The docs fix some of them as rules**: the chart's ×1.5 and the level
  effects are written in the design as the rule itself, not as a first guess to
  tune, as `tuning.json`'s keys are.
- **Three shape the placement stream.** `MaxEnemiesPerRoom` is on the suite's
  list of seed-parity names (`TuningSchemaTests.NothingTheGoldenTestsPinIsAKey`
  treats every constant of `EnemyPlacer` as one), and "nothing the golden tests
  pin may ever go in" `tuning.json` (ROADMAP). `MinEnemiesForStaffHealer` and
  `StaffHealerChance` decide whether a room's healer draw is taken at all, so
  moving either shifts every draw after it on the floor. The golden file itself
  pins no placement — the map generator's stream is what it pins — so this is
  the suite's reading of the rule, not the golden data's.
- **`StaffHealerChance` is a fraction**, and `tuning.json` holds whole numbers:
  as a key it would be a percentage.
- **A new key changes every save's tuning hash** once, so the first resume of
  every campaign saved before it says `THE NUMBERS HAVE CHANGED`.

**Shipped provisionally: they stay compiled**, as rules of the game rather than
numbers a playtest turns, until ruled.

**This decides what "numbers driving tuning" covers, so it is the user's to
decide.** The alternatives: move them into `tuning.json` as keys, each with a
range — all of them, or all but the placer's three, which stay compiled as seed
parity (or move with the parity list amended, the golden file not pinning
them); or move only the ones no design doc fixes — the enemy's hit points, mana
and movement, and the healer's odds. settled.md's Phase 5 implementation pass
records the ruling.

## Whether the deposit's numbers are six keys or three (§6.1, §6.2) — USER DECISION 1

The user's ruling of 2026-09-25 on what a deposit costs (R4, settled.md) fixed
the rule — a weapon's wear capacity starts at 30 and rises a point for every
full bar of wear cashed in, an enchantment slot opens every 5 capacity, and
attaching enchantment *n* needs a free slot and a deposit of at least
30 + 5(*n* − 1) — and that its numbers live in `tuning.json`. How many keys hold
them the ruling left to the build.

**Shipped provisionally: each number its own key**, in the Enchanter group and
read by nothing until Phase 6a builds the enchanter — `WearCapacityPerBar`,
`FirstSlotCapacity`, `SlotCapacityStep`, `EnchantmentSlots`,
`FirstEnchantmentWear` and `EnchantmentWearStep` — and a weapon's starting
capacity `StartingPool`, the Phase 2 pass's one key for every pool's first bar,
not split from it. So any one number can move alone.

**This decides what one edit to `tuning.json` can move, so it is the user's to
decide.** The alternatives: tie them — slot *n* opens at, and attaching
enchantment *n* costs, `StartingPool + SlotCapacityStep × (n − 1)`: three keys,
so the slots and the price cannot drift apart, nor from the capacity a weapon
starts at; or split a weapon's starting capacity into a key of its own,
un-ruling the Phase 2 pass's one key. settled.md's Phase 5 implementation pass
records the ruling.

## Whether a slot above the count may be played in place (the save slots, R5) — SLOT DECISION 1

The user's ruling of 2026-09-25 on the save slots (R5, settled.md) binds a
campaign to the slot it was loaded from or begun in: "you choose a save slot and
any saving happens on that save slot". A slot above `tuning.json`'s `SaveSlots`
— one that holds a save from before the count was lowered — was listed for
loading and never saved into (R2). Loading it is choosing it, so under R5 a
campaign loaded from it saves into it: that follows from the ruling, and no
second choice of a slot is offered. What the words leave open is whether such a
slot may be played at all.

**Shipped provisionally: yes.** It is loaded and continued like any slot, and
LOAD GAME marks it `BEYOND THE 3 SLOTS`, no longer `- LOAD ONLY` and no longer
dimmed. NEW CAMPAIGN never begins a campaign there, so the count limits where
campaigns begin, and lowering the count strands nothing.

**This decides what the slot count limits, so it is the user's to decide.** The
alternative: it stays load-only, as R2's folder had it — loading it first moves
its campaign into the lowest empty slot within the count, which is then its
slot, and with none empty it does not load, saying `SLOT 5 IS BEYOND THE 3
SLOTS`. settled.md's Phase 5 implementation pass records the ruling.

## What NEW CAMPAIGN does to a slot in use (the save slots, R5) — SLOT DECISION 2

The user's ruling of 2026-09-25 on the save slots (R5, settled.md) has NEW
CAMPAIGN choose the slot a campaign begins in — "you choose a save slot and any
saving happens on that save slot" — from 1 to `tuning.json`'s `SaveSlots`. The
save model's ruling (R2) asks NEW CAMPAIGN to "confirm before it can overwrite
anything, and say what it will do". Neither says what becomes of the campaign in
the slot chosen: USER DECISION 3's question of the autosave, asked now of a slot.

**Shipped provisionally: it asks, then sets that slot's save and backup
aside.** An empty slot begins at once. Over a slot in use it asks, naming the
campaign there as the old games ask before they overwrite a file: `SLOT 2 HOLDS
THE SPEAR HALL FLOOR 4 - 3 RUNS` / `BEGIN HERE - WHAT IT HOLDS LEAVES LOAD GAME,
KEPT AS FILES, NEVER DELETED - ENTER: YES - ESC: NO`. On ENTER the slot's backup
and save become `slot-2/campaign.abandoned-N.json`, never deleted: moved into an
empty slot folder as `campaign.json`, the old campaign loads and plays again
(renamed back where it lies, it would take the new campaign's save's name).
Every other slot is untouched, and the list starts on the lowest empty slot, so
an ENTER pressed without looking sets nothing aside while a slot is free.

**This decides whether starting over can lose a campaign, so it is the user's
to decide.** The alternative: it asks, then saves over it, as the old games
overwrite a file — the old save kept one save deep as the new campaign's
backup, and gone at its next save. settled.md's Phase 5 implementation pass
records the ruling.

## What becomes of play in a slot whose writes keep failing (the save slots, R5) — SLOT DECISION 3

The user's ruling of 2026-09-25 on the save slots (R5, settled.md) puts every
save of a campaign into the slot it came from, SAVE GAME's too: "any saving
happens on that save slot instead of choosing a save slot everytime you save".
Under R2 a player whose slot would not take a save — a full disk, a
`campaign.json` another program holds, a folder with no rights — could save by
hand into another slot meanwhile. R5 removes that way out, and says nothing of
what replaces it.

**Shipped provisionally: nothing more than today's words.** Each failed save
says `SLOT 2 COULD NOT BE SAVED (...) - WHAT IT HELD IS KEPT - SEE THE LOG` over
the floor and on the next screen, and a later save that goes through takes it
back. A quit that cannot save leaves the unsaved marker, and the title says
`SLOT 2 COULD NOT BE SAVED WHEN YOU LAST LEFT PLAY - WHAT IT HOLDS IS FROM BEFORE
THAT, BEHIND WHERE YOU LEFT IT`. What was played since the last save that went
through is lost at the quit. A slot that refuses its writes before play begins
brings no campaign into play, so that case loses nothing.

**This decides whether a slot that stops taking saves can cost play, so it is
the user's to decide.** The alternatives: (a) at the quit, when the slot's write
fails for anything but a full disk, what could not be saved is written into the
same slot folder as a new file, `campaign.stranded-N.json`, overwriting nothing,
and the title's unsaved word names it, with `TO PLAY IT AGAIN, MOVE IT INTO AN
EMPTY SLOT FOLDER AS CAMPAIGN.JSON`; or (b) while the slot's writes are failing,
and only then, SAVE GAME offers an empty slot to move the campaign into, which
becomes its slot. settled.md's Phase 5 implementation pass records the ruling.

## Where the old autosave goes (the save slots, R5) — SLOT DECISION 4

The user's ruling of 2026-09-25 on the save slots (R5, settled.md) binds every
campaign to a slot and every save of it to that slot, so there is no autosave
slot apart from the others, and nothing new is saved in the save folder itself.
A build before the ruling saved its campaign there, as the autosave, and a
player's campaign may still stand there. The ruling says nothing of what becomes
of it.

**Shipped provisionally: every launch moves it, without asking, into the lowest
empty slot** — `SLOT 1` to the count, or else the lowest empty slot above it,
listed `BEYOND THE 3 SLOTS` and played like any (SLOT DECISION 1) — and the
title says `THE AUTOSAVE MOVED INTO SLOT 4 - IT SAVES THERE FROM NOW ON`. Its
save, backup and two markers move byte for byte, all or nothing, and nothing is
written over or deleted. With slots 1 to 9 all holding something it
stays, listed as the `OLD AUTOSAVE`, `NOT YET MOVED INTO A SLOT`, and is played
in place. A move that stops is undone, and when no slot's try goes through the
old autosave takes no saves that session, until a launch can move it.

**This decides whether the game moves a player's campaign without asking, so it
is the user's to decide.** The alternatives: (a) the first title after the
update asks which slot, on NEW CAMPAIGN's list, setting a slot in use aside as
NEW CAMPAIGN does; or (b) it stays listed as the `OLD AUTOSAVE` until it is
loaded, and its load then asks for a slot to move it into. settled.md's Phase 5
implementation pass records the ruling.

## What counts as a hit for wear (§6.1) — PHASE 6a DECISION 1

A weapon banks `WearPerHit` "per hit rather than per swing, per turn or scaled by
damage" (settled.md's `WearPerHit = 1` entry; phase-6-between-runs.md §6.1). That
much is settled, so every body a hit lands on counts: a swing, each body a cleave
fans to, a shot carried on, a brace, a counter, an ally caught by friendly fire,
and each body a wand's area cast strikes. A status's tick and an enchantment's own
discharge are no hits (the one names no weapon, the other rides the hit that
counted), and an enemy carries no pools. Two cases the words leave open: whether a
staff's cast — which deals no damage, and may heal or ward an ally — is a hit, and
what a wipe does to the run's wear.

**Shipped provisionally: a staff's landed cast wears it once, on an ally or an
enemy alike** (§1.3, "a cast is a hit"), a wand's Cast being no hit of its own —
its hits are the bodies its shape strikes; **and a wipe keeps the run's wear**:
"a wipe forfeits spoils, not gear" (settled.md), and the claw-back is the member
pools' alone. The log says when a member's weapon fills
(`[Wear] A's Weakspot Stiletto is full: wear 30 of 30`), and the weapon line
reads `WEAR 30/30 FULL` in green.

**What the pick lets a player do.** A staff whose cast lands on its own side — a
heal, a ward — can fill its wear with no enemy in play: cast on itself or an
ally, end the turn, cast again as the mana comes back, on a cleared floor or
anywhere safe. A blade or a wand earns only by striking a body. Wear is what
every service at the enchanter is paid in (R4), so this loop is what the pick
rules on; alternative (a)'s second half, only a staff's casts on enemies count,
closes it.

**This decides how a caster and a wipe earn toward the enchanter, so it is the
user's to decide.** The alternatives: (a) staves accrue nothing — so a staff is
never enchanted or refined, against §6.2's caster cases — or only a staff's casts
on enemies count; or (b) a wipe claws back `DeathXpLossPercent` of the run's wear,
as it does XP. settled.md's Phase 6a implementation pass records the ruling.

## How a weapon's capacity is kept (§6.1, §6.7) — PHASE 6a DECISION 2

The user's ruling of 2026-09-25 on what a deposit costs (R4, settled.md) gives a
weapon's `WearCapacity` a start, 30 (`StartingPool`), a growth of
`WearCapacityPerBar` a full bar of wear cashed, the bar the capacity at the time,
and a progress that carries from one deposit to the next. The v2 hand-off
(settled.md) words the save as holding "`wear` and `wearCapacity` … and beside
them the capacity's progress toward its next point"; how the capacity is kept is
the build's.

**Shipped provisionally: the save keeps `wear` and `wearCashed`, the wear the
weapon has ever cashed**, and its capacity and the progress toward the next
point are replayed from `StartingPool` on read (`Wear.Capacity`), as a member's
maximum HP and mana are from its XP (settled.md: "Max HP and wear capacity move
with it"; `Progression`'s "one integer is the whole pool"). So a `StartingPool` or
`WearCapacityPerBar` edit re-derives every weapon's capacity at once, and the
numbers-changed notice says so. Wear over a capacity a tuning lowered since is
kept, never taken away, and the weapon reads full (`WEAR 30/20 FULL`). A save's
wear is held to a ceiling no tuning in range can pass, `10000 + 1000 ×
wearCashed` (`StartingPool` and `WearCapacityPerBar` at their clamps' tops).

**This decides whether a tuning edit moves a weapon's grown capacity, so it is
the user's to decide.** The alternative: keep the capacity and its progress as
numbers, as the hand-off words it; the migration then writes a capacity that
reads as `StartingPool`, and a grown capacity stays where it grew whatever the
tuning says later. settled.md's Phase 6a implementation pass records the ruling.

## The tuning hash of a save from before Phase 6a (§5.3) — PHASE 6a DECISION 9

A save records the hash of the `tuning.json` numbers a rule of play reads
(settled.md, the hash entry), and a load under other numbers says `THE NUMBERS
HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED` once. Phase 6a starts to play keys a
phase-5 save was never played under — `WearPerHit` and `WearCapacityPerBar` from
6a.1, the rest of the Enchanter group as the enchanter reads them — so a hash
over the current keys would differ from every phase-5 save's, though no number
it was played under moved: the cost the entry on the gameplay numbers still
compiled accepts for a new key.

**Shipped provisionally: a save's hash is compared on the keys played at the
version that wrote it.** Each key names the save version whose hash first covers
it (`HashedFrom`: 1 for Phase 5's, 2 for Phase 6a's); a save names the version
its hash is of (`tuningHashVersion`), which the step that lifts a version-1
document writes as 1; and a load compares on that version's keys. A phase-5 save
therefore resumes with nothing to say unless a number it was played under moved,
and its next save is hashed on version 2's keys. The log's numbers-changed line
prints the game's hash on the same keys and names the version. **During the
Phase 6a steps** version 2's key set grows — at the enchanter's queue (6a.3a) and
at the improvement roll (6a.4) — so a save written on an earlier 6a build shows
the notice once under a later one, though no number moved; a released version 2
never does.

**This decides what a phase-5 save says on its first resume, so it is the user's
to decide.** The alternative: accept one `THE NUMBERS HAVE CHANGED SINCE THIS
CAMPAIGN WAS SAVED` on the first resume of every phase-5 save, as a new key costs
(the entry on the gameplay numbers still compiled), with no new member at the
save's root. settled.md's Phase 6a implementation pass records the ruling.

## The stable (§6.2) — PHASE 6a DECISION 7

§6.2 puts a deposited weapon in the shop, out of the party's hands, and says the
campaign keeps "the stable": the weapons the party owns and is not carrying
(phase-6-between-runs.md §6.2, §6.7; ROADMAP's "Needs `CampaignState` and the
stable"). No ruling gives the stable a size, says where a weapon goes when its
service is done, or says whether a member may be left with nothing in hand; and
the bags are six slots a member (settled.md, "Carry limit is unified with the
loadout").

**Shipped provisionally: the stable is the campaign's, unbounded, in the order
weapons were put there**, and it is the screen between runs' WEAPONS page (TAB):
a weapon is taken up and put down in another member's hand or bag, or in the
stable, and back out again — onto a weapon it swaps, onto a hand it equips.
Spoils carried home stay in the bags, as ever. **No move leaves a member
carrying no weapon**: it is refused, `EACH MEMBER KEEPS A WEAPON - [A] WOULD
CARRY NONE`, and the weapon stays taken up. A hand a move empties is armed from
the bag as the rest before a dungeon arms it. When the enchanter's queue arrives
(Phase 6a.3a), a finished service will return its weapon to the stable's end, and
the deposit will be refused on the same rule — a transfer asked with both of its
weapons gone.

**This decides where the party's spare weapons live and whether a member can be
left unarmed, so it is the user's to decide.** The alternatives: (a) a capped
stable; (b) a finished weapon returns to the bag it came from, or to the stable
when that bag is full; or (c) a member may be left unarmed, as a wipe that takes
every spoil it held already can leave one. settled.md's Phase 6a implementation
pass records the ruling.

## Autosaving between runs (§5.1's safe points) — PHASE 6a DECISION 10

A save is taken only at a safe point, and between runs is always one (settled.md,
the save's safe points); SAVE GAME saves there by hand. The list of when the game
saves on its own — a run's entry, every player turn's start, every floor arrival,
a run's end, NEW CAMPAIGN and every quit — names nothing between runs, since
nothing changed the campaign there before Phase 6a: the screen between runs only
chose a dungeon. From Phase 6a it moves weapons between the bags and the stable,
and the enchanter's deposits and take-backs follow.

**Shipped provisionally: every change the hub makes to the campaign is saved**,
into the campaign's own slot, at the end of the frame it was made in — a move
(Phase 6a.2), and a deposit and a take-back when they arrive. The screen asks
through its host (`IBetweenRunsHost.RequestSave`), which the game answers with
the same request the dungeon's autosaves make, written through the one door
every save of the campaign goes through; the log says `Saving the campaign
between runs, after a change on the hub`. A save that fails is said at once under
the hub's title, in orange, as one over a floor is, and on the next screen until
a later save goes through.

**This decides whether a crash between runs can cost the changes made there, so
it is the user's to decide.** The alternative: only the run's end, SAVE GAME and
the quit save, as the list stands, so a crash between runs loses whatever the hub
changed since the last of them. settled.md's Phase 6a implementation pass records
the ruling.

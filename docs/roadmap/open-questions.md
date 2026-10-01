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

**Phase 6a.3b wrote the offer, and it closes the enchanter's half of the
`Echoing` gap.** `Enchanter.Offers` is what the campaign has met,
`CampaignState.SeenInOrder`, less `NeverRolled` and less the kinds declared inert
— the inert half of the test `LootTable.DropOrder` applies, asked through the one
predicate both now share (`LootTable.IsInert`) — so `Echoing` is never offered,
and a save whose shop names it is refused as one no play wrote. What `Echoing`
does, and who raises `AttackDeclared`, are untouched and as open as above. Of the
`aegis` half, the offer does what a filter can: `aegis` passes it, and would be
offered with nothing against it — a defender's step fires on any weapon. But the
offer lists only what a campaign has met, and nothing puts `aegis` there, so
where it comes from is **PHASE 6a DECISION 4** below.

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
read by the enchanter since Phase 6a — `WearCapacityPerBar`,
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
the build's, and a pick other than those words bends the hand-off — settled.md
marks it where the hand-off stands, the one of its two bends that is the user's
to rule.

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
the bag as the rest before a dungeon arms it. A finished service at the enchanter
returns its weapons to the stable's end (Phase 6a.3a), and a deposit is refused
on the same rule — `[A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE
FIRST` — and a transfer (Phase 6a.6) is asked with both of its weapons gone, its
reason shown on the source's row.

**This decides where the party's spare weapons live and whether a member can be
left unarmed, so it is the user's to decide.** The alternatives: (a) a capped
stable; (b) a finished weapon returns to the bag it came from, or to the stable
when that bag is full; or (c) a member may be left unarmed, as a wipe that takes
every spoil it held already can leave one. settled.md's Phase 6a implementation
pass records the ruling.

From Phase 6b.1 a bag slot can hold a potion, which is no weapon: a hand a move,
a deposit or the rest before a dungeon empties takes the first *weapon* in the bag,
passing a potion over; every rule that keeps a member armed counts the weapons it
carries, never its full slots, so a member with one weapon and a bag of potions
carries one weapon; and the stable holds weapons alone — no potion is ever put
there (PHASE 6b DECISION 1, below).

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
(Phase 6a.2), a deposit and a take-back (Phase 6a.5). The screen asks
through its host (`IBetweenRunsHost.RequestSave`), which the game answers with
the same request the dungeon's autosaves make, written through the one door
every save of the campaign goes through; the log says `Saving the campaign
between runs, after a change on the hub`. A save that fails is said at once under
the hub's title, in orange, as one over a floor is — but where a floor's notice
goes after a few seconds, the hub's stays, and the next screen says it too, until
a later save goes through, which takes it back from both. Phase 6b.4 grows the
list with the alchemist's two changes, a potion mixed into a bag and one poured
back, each saved the same way, under the same log line; so a quit and a load after
a mix bring the potion back and never refill the batch (PHASE 6b DECISION 6).

**This decides whether a crash between runs can cost the changes made there, so
it is the user's to decide.** The alternative: only the run's end, SAVE GAME and
the quit save, as the list stands, so a crash between runs loses whatever the hub
changed since the last of them. settled.md's Phase 6a implementation pass records
the ruling.

## When a service's enchantment attaches (§6.2, §6.4) — PHASE 6a DECISION 3

A deposit cashes the weapon's whole pool, and its capacity grows on the cash:
that much is the user's ruling on what a deposit costs (R4, settled.md: "+1 per
full bar … cashed in", "the pool itself still empties at every deposit"), so the
wear leaves and the capacity grows as the weapon goes in. What the words leave
open is when the service's purchase lands: the enchantment an Enchant buys, and
the improvement roll either service may win (§6.4, Phase 6a.4).

**Shipped provisionally: the chosen enchantment is attached when the service's
last run is worked**, inside the end of the run that works it — last, at tier 1
with no XP — and the weapon comes back to the stable then. Until then it is in the
shop, out of the party's hands, and its enchantments are what they were. The
improvement roll is made at the same moment (since Phase 6a.4), on a stream of the
completing run's map seed and the deposit's ordinal, both saved, so a reload never
rerolls it (R1).

**This decides when a deposit's purchase lands, so it is the user's to decide.**
The alternatives: (a) attach and roll at the deposit, on a seed the campaign saves
for the purpose — the weapon then waits out its runs already enchanted; or (b)
grow the capacity when the service finishes, the wear still leaving the pool at
the deposit ("cashed in" read as the service's return). settled.md's Phase 6a
implementation pass records the ruling.

## The `+ 1` in a service's time (§6.2) — PHASE 6a DECISION 6

§6.2 has a service cost runs, "the current enchantment count + 1", for every
deposit, a refinement too (settled.md, "Service costs runs"). That the enchanter
works on the two newest deposits is the doc's too ("the enchanter keeps its
two-newest-slots rule"), and that its two is a `tuning.json` key,
`EnchanterWorksOn`, follows the user's standing rule that a number they may
revisit lives in the file. The `+ 1` is the one number of the rule not yet asked
of.

**Shipped provisionally: the `+ 1` stays a rule**, never a key: a service takes
the weapon's count, what it arrived with included, plus one runs that count. So the
tutorial's one run keeps landing on a first enchantment's one run, and attaching
five in a row takes 1, 3, 6, 10 and 15 runs.

**This decides whether the base of every service's time is a number to turn, so
it is the user's to decide.** The alternative: a key beside the count
(`count + ServiceBaseRuns`, 1 shipped), read with the rest of the Enchanter
group's. settled.md's Phase 6a implementation pass records the ruling.

## Taking a weapon back from the enchanter early (§6.2) — PHASE 6a DECISION 8

§6.2 puts a deposited weapon in the shop until its service is done, and says
nothing of wanting it back sooner. A deposit cashes the weapon's pool and grows
its capacity at once (R4; DECISION 3's context).

**Shipped provisionally: any queued weapon, worked or stalled, can be taken back
between runs**, to the end of the stable. Its service is forfeit — nothing
attached, nothing rolled — and the runs worked are lost, the ordinal never reused.
**The deposit's cash is reversed exactly**: the wear goes back into the pool and
off what the weapon has cashed, so its capacity and the progress toward the next
point are as they were before it went in. The take-back is the enchanter's since
Phase 6a.3a, and since Phase 6a.5 T on a weapon's row in the shop asks for it and
says what it costs — `TAKE BACK THE ARMING SWORD - ITS SERVICE IS LOST - ITS 36 WEAR
COMES BACK` — and only Enter takes it back.

*Why not keep the cash spent:* capacity grows at the deposit. A take-back that
kept the growth would let a player deposit to refine, take the weapon back at once,
and bank a point of capacity a full bar with no downtime, forfeiting only a 6 to 9
in 100 roll — every bar between two slots a free action, which R4's consequences 1
("the bars that open a slot are refinements") and 3 (71 runs of downtime, 31 at
the least) rule out, as does §6.2's "dumping the whole stable achieves nothing".

**This decides what a deposit commits a weapon to, so it is the user's to
decide.** The alternatives: (a) no taking back — a deposit is final until its
service is done; (b) a take-back keeps the cash spent and the capacity grown,
**opening the loop above**; or (c) the capacity grows when the service finishes
(DECISION 3's (b)), which closes the loop with the take-back unchanged.
settled.md's Phase 6a implementation pass records the ruling.

## What an earlier Phase 6a build does with a later step's save (§5.2) — PHASE 6a DECISION 14

Every Phase 6a step reads a version-2 save as its own, and the reader passes over
a member it does not know (the save's header rule), so an earlier step's build
loads a later step's save and its next save drops what it did not read. From Phase
6a.2 that next save comes at the first weapon moved between runs. From Phase 6a.3a
what it drops holds weapons: a 6a.1 or 6a.2 build that opens a 6a.3a save loses the
enchanter's queue — every weapon in the shop — with no notice; a 6a.1 build loses
the stable the same way. Only builds between released versions can do this, and a
playtester playing each step is who meets it.

**Shipped provisionally: the save stays at version 2**, as every Phase 6a step
has it, and the README's Saves section and each step's commit say not to open a
slot a later Phase 6a build saved with an earlier one. This reads the standing
instruction that new persistent state goes in "with a version bump and a
migration so phase-5 saves still load" as met by Phase 6a.1's one bump, from
which every Phase 5 save migrates; the 6a.3a review read it per step, and asked
that the choice be put to the user as it stands.

**This decides whether an older development build can lose a playtester's weapons,
so it is the user's to decide.** The alternative: lift the save to version 3 with a
step that writes nothing but the version (and removes a stray member a hand gave an
older document), so a 6a.1 or 6a.2 build sets a 6a.3a save aside as a newer
build's, kept and never deleted — and bump again at every later step that adds
weapons to the save (6a.6's transfer), the tuning hash's version following or
staying 2. settled.md's Phase 6a implementation pass records the ruling.

Phase 6a.6 stayed at version 2 too, its transfer's members appended. A 6a.3a,
6a.4 or 6a.5 build reads the shop and the last run's reports, and a `Transfer` is
no service it knows, so it refuses a save with a transfer in either and sets it
aside, kept and never deleted. A 6a.1 or 6a.2 build passes over the shop unread,
as above, and its next save loses both of a transfer's weapons with the rest of
the shop.

Phase 6b lifts the save to version 3 from its first step, 6b.1, which gives a
member potions in its bag (a member's `potions`, appended with its default). The
step from version 2 writes nothing but the version — the reader stamps it — and
removes a stray `potions` a hand gave a version-2 member, which the version-2
reader ignored; the tuning hash's version stays as the save wrote it. So a Phase
6a build sets every Phase 6b save aside as a newer build's, kept and never
deleted, rather than reading it and saving it again without its potions: do not
open a Phase 6b slot with a Phase 6a checkout. Within Phase 6b the version stays
3 at every step, as it stayed 2 within Phase 6a, so this entry's question asks
itself again there: a later 6b step that appends members (the farm's, at 6b.3) is
read by an earlier 6b build that passes over them, and that build's next save
drops them. Phase 6b.3 appends the farm's four to the last run's record
(`farmYield`, `farmClears`, `firstClear` and `spoiled`), so a 6b.1 or 6b.2 build
passes over them and its next save drops them; if that save holds potions, a
6b.3-or-later build then refuses it — the potions carried are more than the yield
the dropped record says the last run made (check 32) — and sets it aside, kept and
never deleted.

## Where `aegis` comes from (§6.2's offer; the `Echoing` entry above) — PHASE 6a DECISION 4

The entry on `Echoing` above calls `aegis`'s lack of any source a filter gap
rather than a content gap, "resolved by whoever writes §6.4's offer". Phase 6a.3b
wrote it (`Enchanter.Offers`): what the campaign has met, less `neverRolled` and
the kinds declared inert. `aegis` passes that — it is neither, and its step on the
defender fires on any weapon, so it is offered with no refusal and no note. But
nothing puts it among what a campaign has met: `LootTable.DropOrder` leaves it out
of every class's drops (its behaviour is a compiled step on the defender rather
than a row in a loop, so the class-event test finds nothing for it), and no weapon
the content ships carries it. So the offer alone can never contain it.

**Shipped provisionally: `aegis` stays out of reach until content carries it.**
The offer lists only what the campaign has met, as §6.2 words it ("from
everything your campaign has seen"), so a playtester never sees `aegis` at the
enchanter. It stays in the catalogue, checked at load, and it fires on any weapon
that carries it.

**This decides whether a shield can ever be put on a weapon, so it is the user's
to decide.** The alternatives: (a) the offer also lists the catalogue's
defender-step kinds (`aegis`) unmet, as a standing offer; or (b) a drop may roll
`aegis`, as a class-agnostic arrival — the drop pool taught that a defender's step
fires on any class, which moves what drops — and the offer then takes it as it
takes any entry met. settled.md's Phase 6a implementation pass records the ruling.

## What the enchanter refuses, and how a refusal shows (§6.2, §6.5) — PHASE 6a DECISION 12

§6.2 has you pick the enchantment "from everything your campaign has seen", and
§3.3 that "any enchantment goes on any weapon … nothing keys off `WeaponClass`"
(phase-3-loot.md); settled.md that "only opposed enchantments exclude each other
… a second rule would be a cap by another name", and that a lock the wielder
cannot afford leaves the enchantment dormant. None says what the enchanter does
with an entry the relations would refuse on the weapon, or with one the weapon
could carry and never fire, or whether a refused choice is shown.

**Shipped provisionally** (Phase 6a.3b, `Enchanter.Offers`; the deposit panel lists
the offer since Phase 6a.5, a refused entry dimmed with its reason, and a refused
service — ENCHANT, REFINE, or MOVE since Phase 6a.6 — dimmed the same way, its
number key saying why; MOVE's list of souls since Phase 6a.6, a refused soul
dimmed with its reason):
- the enchanter refuses what `restricted.json` refuses: an entry an exclusion
  group pairs with one the weapon carries (`COLD - CANNOT SIT WITH FLAMING`, the
  weapon's entries asked in the order they were attached), and an entry whose
  prerequisite is not on the weapon ahead of where it would land, last (`BURNING -
  NEEDS FLAMING AHEAD OF IT`), the very question content validation asks of every
  weapon;
- it refuses a status applier onto a wand (`REGENERATION - A WAND CASTS NO
  STATUS`), the content rule that no wand carries one: an area cast names its
  caster as its own target, so the status would land on the wielder. **This bends
  the phase docs**: §3.3's "any enchantment goes on any weapon … nothing keys off
  `WeaponClass`", §6.2's "on any weapon, forever after", and settled.md's "a
  second rule would be a cap by another name". The rule is Phase 1.8's content
  rule, not a ruling;
- it refuses a staff's status for one side onto a staff whose cast is for the
  other (`MIRE - THIS STAFF WOULD CAST IT ON AN ALLY`): a staff's first entry
  picks whom it casts on, and the cast lands every status its entries apply on
  that one body, so Mire on a Staff of Renewal would mire the ally it casts on,
  and Regeneration on a Staff of Mire would heal every enemy it mires. The same
  bend of the phase docs as the wand's, for the same hazard — a status landing on
  the wrong body — but with no content rule behind it: no shipped staff mixes
  sides and a staff's drop rolls no second entry, so the enchanter is the first
  way to make one (Phase 6a.3b's review);
- everything else attaches: the same entry again, an entry whose lock the wielder
  cannot pay (it sleeps), and an entry that never fires on the weapon, which the
  offer notes (`NEVER FIRES ON A SWORD`, a staff's status on a blade; `NEVER FIRES
  ON A STAFF`, anything that rides a strike or a kill, since a staff casts and
  never strikes). An element on a staff is offered with no note: it fires on the
  cast, paying its trigger, and types a cast that lands no damage, so it spends
  mana for nothing;
- a refused choice is listed with its reason, never hidden — and, since Phase 6a's
  audit, chosen like the rest: W/S stop on it, dimmed and marked, and Enter on it
  says why and chooses nothing, since a list that passed over its refused lines
  never showed a run of them longer than its window past either end of what could
  be chosen. An Enchant with nothing left to choose — every offer refused, or
  none left (nothing met, or only what the enchanter never offers) — is refused
  as such (`NOTHING TO OFFER - NO ENCHANTMENT MET YET CAN GO ON IT`), and
  refining is left;
- a transfer (Phase 6a.6; `Enchanter.TransferRefusal`) asks the moved soul the
  same relations, the wand's rule and the staff's side on the weapon it lands on
  — its own entries, less the soul for a reorder — and refuses one that would
  strand an entry on the weapon it leaves, whose prerequisite was ahead of it and
  would no longer be (`BURNING NEEDS FLAMING AHEAD OF IT`: settled.md's "refuses a
  transfer that would strand a unique's lingering effect", the `requires` row
  asked of what the source keeps; an entry that lacked its prerequisite already is
  not the move's doing, and is not asked);
- **the caster clause**: it refuses a transfer after which a staff's or a wand's
  first entry — the one it comes off, or the one it lands on — is one it cannot
  cast, or none: a staff's must apply a status, a wand's must be an element, the
  dungeon's own two gates (`Weapon.CastsWith`; `THE ROTWOOD WOULD CAST NOTHING`). A
  caster left with neither could neither cast nor strike, so it could never earn
  wear again. **This narrows settled.md's "that effect can be transferred onto a
  dagger"**: a staff arrives with one soul, so it gives its effect away only once
  it has grown to capacity 35 and been given a second status to cast. A wand's
  Cold becomes Flaming through a third element, since the two cannot sit together.
  **Tightened at Phase 6a.6's review** for a staff whose first entry the move
  changes: that entry picks whom the cast lands every status on, and the dungeon
  asks only that it apply one. Serrated does — Bleeding, on enemies — but on a
  strike, never a cast, and nothing kept it from coming first: moved onto a Staff
  of Renewal behind its Regeneration, then a Ward enchanted on, then the
  Regeneration moved away, the staff cast on enemies and landed its Ward on them.
  So the new first must be a staff's cast as content makes one, a status it casts
  (content's `RuleCasterInnate`; `THE STAFF OF RENEWAL WOULD CAST NOTHING`), and a
  status the staff keeps that its cast landed on the side the old first chose
  must land on the same side after (`THE STAFF OF RENEWAL WOULD CAST WARD ON AN
  ENEMY`: no staff play makes can meet it, but a unique `weapons.json` gives both
  sides can). A staff wrong already is not the move's doing, and is not asked, as
  the stranding rule reads a prerequisite.

**This decides what the enchanter will attach, and whether §3.3's "any weapon"
holds, so it is the user's to decide.** The alternatives: (a) hide refused
choices; (b) refuse never-firing entries too, as the drop table does; (c) also ask
content's arrival rules (Overheal beside a healer, a dependency only on a
currency weapon); (d) **keep settled.md's dagger whole**: allow a caster left
with no castable first entry, which casts nothing until it is given one — the
WEAPONS page already says so of such a weapon (`CASTS NOTHING - ITS FIRST
ENCHANTMENT IS ITS CAST`); (e) **keep §3.3**: a status applier attaches to a wand, the
wand's area cast skips it, and the offer notes it `NEVER FIRES ON A WAND`, as it
notes any entry that never fires. For the staff: (f) attach a status for the
other side with a note instead, the cast landing it where it lands today; (g) have
a staff's cast skip an entry for the other side — a change to how a cast plays —
and note it `NEVER FIRES ON A STAFF`; or (h) give `weapons.json` the rule too, so
a unique staff cannot ship what the enchanter refuses, as the wand's rule is
shared. And for an element on a staff: (i) note it as never firing, or refuse it.
And for how a list's refused lines are walked (Phase 6a's audit): (j) W/S pass
over a refused line, as the game's other lists pass over what cannot be chosen,
the window pulling in the refused lines past either end of what can be as far as
it has room — quicker to walk, but a run of them longer than the window at either
end stays out of view: a crossbow with every slot filled, whose reorders are all a
MOVE may choose, listed 37 refused souls over them in a window of 25. settled.md's
Phase 6a implementation pass records the ruling.

## How a landed improvement chooses (§6.4) — PHASE 6a DECISION 5

§6.4 gives a service coming back from the enchanter a chance to improve the
weapon anyway, and settles the chance: `GraftBaseChance`, 3 in 100, flat when it
enchants, and times `1 + wear ÷ StartingPool` when it refines (settled.md;
R4's consequence 2). What a roll that lands does, its table gives in words alone:
**deepen**, +1 stack on a modifier the weapon already carries, "uncommon, biased
toward the class signature"; **graft**, +1 stack of a modifier it has never
carried, "rare" — "deepen stays the common result and a graft stays the surprise",
and refinement moves whether the roll lands, "not what it lands on". It gives no
number for either. What a roll does when the outcome it drew has nothing to take
— every modifier the weapon carries at its forged-plus-five ceiling, or nothing
left it may graft — it speaks to twice, and the two sentences pull apart: "A
modifier at its ceiling cannot be deepened, and a weapon with every modifier
capped rolls nothing at all" (§6.4's rules), and "A finished weapon is not inert.
Even with its signature capped, a weapon can still gain *breadth* through grafts
at 5 apiece — which is what keeps a max-farmed drop worth depositing at all" (the
next rule). A max-farmed drop has every modifier it carries capped, so read
literally — every modifier it carries — the first says that drop's landed roll is
nothing, and the second says it grafts.

**Shipped provisionally** (Phase 6a.4, `Enchanter.Improve`):
- of the rolls that land, `GraftSharePercent` (25) in 100 graft, and the rest
  deepen — the second draw off the roll's stream, which neither service moves;
- a deepen weighs the class's signature `DeepenSignatureWeight` (3) against 1 for
  every other modifier the weapon carries below its ceiling, so on a Tower Guard
  (Block x2, Push x1) Block deepens three times in four; a staff or a wand has no
  signature, so it has no bias;
- a graft is drawn evenly among the modifiers the weapon has never carried that
  the relations let it take (the kind, the exclusions, the prerequisites, and
  never a forged-only one, so never `Charges`) — the relations and nothing else.
  So it may land on one the weapon never reads, such as `CRIT MULTIPLIER` on a
  staff, whose cast crits at a fixed x2 (a wand's area cast does read it), and
  the summary's green line names it as the story. The record pulls both ways on
  that. For it: "every other modifier is at worst inert on the wrong weapon"
  (settled.md's relations entry, `Charges`, the negative one, alone kept out),
  and "restriction is reserved for grafts that would be negative, not merely
  off-class" (§6.4). Against it: the same relations entry sharpens §1.1's rule to
  "no stack may be dead"; the caster crit ruling is made "without reopening
  `CritMultiplier`, which stays a martial-only stat" (settled.md); and "a dead
  stack is a farm cycle or a service roll that bought nothing" (settled.md's
  "nothing is capped at the ceiling" entry);
- an outcome with nothing to take falls through to the other. §6.4's "every
  modifier capped" is read as every modifier the weapon may hold, so the roll
  that changes nothing is the one with nothing to deepen and nothing to graft,
  and a weapon whose carried modifiers are all capped still grafts, "a finished
  weapon is not inert". With neither, the roll that landed changes nothing, which
  the summary says as it says a roll that missed: `NO CHANGE` (the log tells the
  two apart: `landed, nothing to deepen or graft`).

Both numbers are `tuning.json` keys, each comment starting `Provisional:` (the
file's marker), by the user's standing rule that a number they may revisit lives
in the file; `GraftBaseChance`, the doc's own 3, is a key beside them and not
provisional.

**This decides what a landed roll gives, and how often each, so it is the user's
to decide.** The alternatives: (a) no fall-through: an outcome drawn with nothing
to take gives nothing, so a capped weapon's deepens are lost rolls — which is
§6.4's "rolls nothing at all" read literally, for the deepens; read so for the
grafts too, a weapon whose every carried modifier is capped rolls nothing at all;
(b) other numbers — the share and the weight are guesses, §6.4 saying only
"uncommon" and "rare"; (c) the split as a rule of the code rather than two keys;
or (d) a graft only of what the weapon reads: the candidates narrowed to the
modifiers its attack or its cast reads, so a staff is never grafted `CRIT
MULTIPLIER` — a rule of the code beside the modifiers no weapon stacks, keyed on
a staff's cast, or a `kind` row in `restricted.json`, though a row for casters
would keep it off a wand too, whose cast reads it.
settled.md's Phase 6a implementation pass records the ruling.

## Reordering a weapon's own enchantments (§6.5, §3.3) — PHASE 6a DECISION 11

settled.md says "reordering means detaching and re-attaching, which is a
transfer: a tier and a service", and §6.5 that a move costs "one attachment's
service time". Two texts disagree on the weapon whose every slot is filled:
§6.2 ("You choose what gets attached"), beside settled.md's words, calls
changing the order of a five-enchantment weapon "one of the more expensive
things you can do", and settled.md elsewhere says "a weapon at five enchantments
therefore has refinement as its only remaining service".

**Shipped provisionally** (Phase 6a.6; `Enchanter.TransferRuns`,
`Enchanter.TransferRefusal`): a reorder is a transfer whose source is the weapon
itself — MOVE's list offers the weapon's own souls last; on an Arming Sword
carrying Flaming and Vampiric, `FLAMING - ITS OWN - MOVES TO THE END - ARRIVES AT
T1 - 2 RUNS`:
- it takes the **count** in runs, the service time of attaching the soul again as
  the last of the same count ("one attachment's service time"), not the count plus
  one every other deposit takes; and its gates are the same attachment's — slot n
  open and a deposit of its price, n the count;
- so it is **allowed at five**, where a move from another weapon is refused
  (`ALL 5 SLOTS ARE FILLED`), and the WEAPONS page's price line names the one move
  left there, `ALL 5 SLOTS ARE FILLED - A REORDER NEEDS A DEPOSIT OF 50 WEAR`. With
  no soul to choose at five, MOVE is dimmed with the reorder's own slot and price
  (`ENCHANTMENT 5 NEEDS A DEPOSIT OF 50 WEAR - IT CARRIES 20`), never with the
  slots the other moves are refused by (Phase 6a.6's review);
- the entry already last is not moved (`IT IS LAST ALREADY`): the move would
  change nothing but its tier. **So a reorder needs two souls, and costs 2 runs at
  the least**: a weapon with one soul has nothing to reorder, and its row says so;
- **a weapon forged whole keeps every forged mark** — a unique reordered, its souls
  all forged — since the forged ones are still a prefix with the soul last. **A
  weapon forged in part loses the moved soul's mark**: a forged entry cannot
  stand behind an acquired one, so it lands acquired, as it would on another
  weapon (DECISION 13), and its damage share or cast levels no longer teach the
  weapon. So a staff given a Ward that reorders its Regeneration behind it casts
  its acquired Ward and levels no staff proficiency; a wand variant given a second
  element that reorders its first behind it has no forged entry left. The screen
  names no entry forged or acquired anywhere, and does not say this either
  (Phase 6a.6's review).

**This decides between texts that disagree, so it is the user's to decide.** The
alternatives: (a) a reorder costs the count plus one runs, as every deposit does
in §6.2 and settled.md's "service costs runs … `enchantments + 1`"; (b) at five
enchantments a reorder is refused, and refining is the only service left, as
settled.md's "refinement as its only remaining service" has it; (c) the entry
already last may be "moved" too, a tier and the runs spent for nothing but the
roll — a one-soul weapon could then reorder, for 1 run; and for the forged mark
on a weapon forged in part: (d) refuse a reorder that would put a forged entry
behind an acquired one; (e) keep the mark with the entry wherever it stands, a
mark on each entry rather than a count of the first ones — a change to the save;
or (f) say the loss on the MOVE row or the confirm. settled.md's Phase 6a
implementation pass records the ruling.

## How a transfer works (§6.5, §6.7) — PHASE 6a DECISION 13

§6.5 moves an enchantment from one weapon to another at the enchanter, a tier
down and "one attachment's service time", and says no more of how: whether the
weapon it lands on needs a slot and a deposit, what happens to the weapon it
leaves while the move is made, what the queue counts it as, and what it rolls.
§6.7's `Deposit` record names one weapon and no enchantment.

**Shipped provisionally** (Phase 6a.6; `Service.Transfer`, `Enchanter.Deposit`):
- a transfer is the Enchant service with the soul brought from a weapon the party
  owns rather than from the offer: the weapon it lands on needs the free slot and
  the deposit of attaching it as its next (R4), its whole pool is cashed, it takes
  the runs of that attachment, and it gets enchanting's flat odds of improving (3
  in 100); a reorder's are DECISION 11's;
- the soul may come from any weapon the party owns out of the shop — a member's or
  the stable's — whatever it is: a forged innate, a unique's soul, one of a kind
  that does nothing yet, the source list noting that one `DOES NOTHING YET` as it
  notes one that never fires (Phase 6a's audit). The enchanter moves what it never
  offers to copy;
- **the source's wear is left in its pool**, uncashed, and comes back with it;
- **both weapons stay in the shop until it is done, as one deposit**, which holds
  one of the `EnchanterWorksOn` newest places, not two; the WEAPONS page lists the
  source `HELD` under the weapon, and T on either takes both back, the weapon's
  cash reversed (DECISION 8);
- when it comes back, the soul drops a tier and starts that tier at no XP; tier 1,
  and every unique soul, pinned there, moves intact, its XP with it; it lands
  last, and **acquired**: a forged innate's mark stays with the weapon forged with
  it, whose forged count drops — so a staff given a new cast this way no longer
  levels staff proficiency on it, and a dagger given a staff's effect levels none.
  A reorder, which moves the soul on the weapon forged with it, keeps the mark
  where the weapon is forged whole and loses it where it is not (DECISION 11);
- the deposit record gains the soul by its place — its source, and its index there
  — and `Service` gains `Transfer` (§6.7 named two); a run's report of one names
  its soul, its source and the tier it landed at, and the save keeps both.

**This decides what a move costs and what it holds, so it is the user's to
decide.** The alternatives: (a) a transfer needs no wear, since the soul was paid
for once; (b) only the weapon is held, the soul leaving its source at the deposit;
(c) a caster's first entry counts as forged whatever its origin; (d) a transfer
holds two of the worked places, since §6.2 works "the two most recently deposited
weapons" and a transfer deposits two; (e) a transfer rolls nothing; or (f) the
source's wear is cashed too, growing its capacity as any deposit does.
settled.md's Phase 6a implementation pass records the ruling.

## Where a potion may be (§3.4, §6.6) — PHASE 6b DECISION 1

§3.5 puts a potion in a bag slot: "Potions live in inventory slots as ordinary
items, one per slot, so `PartyMemberState.Inventory` needs to hold something
other than a `Weapon`" (phase-3-loot.md). Nothing says whether a potion may sit in
the hand, go to the stable, be put down on a floor, or move from one member's bag
to another's; the hand keeps a weapon (PHASE 6a DECISION 7), and the farm's batch
is "a flow, not a stock" (settled.md).

**Shipped provisionally (Phase 6b.1): a potion lies only in a member's bag — slots
2 to 6, as the bag panel numbers them — never in the hand**, one a slot. It is
never in the stable, and never moved between bags: the WEAPONS page lists it
under its member, grey, and never chooses it nor puts a weapon down on it, and the
campaign's door for a move refuses a potion's slot either way. The alchemist
(Phase 6b.4) mixes one into the first empty bag slot of the member chosen, and a
potion in the wrong bag is poured back there and mixed again (DECISION 9). In a
dungeon, X then a potion's number pours it out — `POURED OUT THE HEALTH POTION
Q2` over the member — and it is gone, not laid on the floor, so a floor and its
save hold weapons alone; it costs nothing, as putting a weapon down costs nothing.
A slot a potion fills is full: a pickup finds no room there, and a bag of weapons
and potions refuses a drop as a bag of weapons does.

**What the pick lets a player do.** Make room in a full bag in the dungeon by
pouring a potion out, as a weapon is put down to make room; and never lose a
potion to a move between runs, since none is made.

**This decides where a potion can be carried and how one is got rid of, so it is
the user's to decide.** The alternatives: (a) the WEAPONS page may move a potion
between bags, as it moves a weapon; (b) a potion is put down on a floor as a
weapon is, and taken back with G (a floor's `LooseItem` and its save row would
hold either); or (c) a potion cannot be put down at all, only drunk. settled.md's
Phase 6b implementation pass records the ruling.

## What a drink costs out of combat (§3.4, §1.2) — PHASE 6b DECISION 2

§3.4 prices a drink at `PotionUseCost`, 40, "on the same principle as swapping
(§1.2): anything that changes your situation mid-turn comes out of the same
budget as moving and swinging" (phase-3-loot.md). A swap is free out of combat —
it costs `WeaponSwapCost` only once a live enemy has been seen this turn, so
loadout management between fights stays free (settled.md) — and §3.4 states the
drink's cost with no such condition.

**Shipped provisionally (Phase 6b.2): a drink always costs `PotionUseCost`, in
combat and out.** The rule is one line, and topping up after every fight still
costs a turn's walking: a party that drinks between fights spends 40 of a 160
budget each time, as it would in one. The cost is paid whole or the drink is
refused, as a swap short of its cost is, and the bag says which before the key is
pressed — `A DRINK COSTS 40 MOVE`, or `... - NOT ENOUGH LEFT` in red — while a
potion's number short of it says `NOT ENOUGH MOVE TO DRINK - IT COSTS 40` over
the member and drinks nothing. One thing the drink makes easier to meet, and this
entry does not change: the MOVE readout rounds the movement left up, as the
prototype's did (`Math.ceil`), so after walking it can read `MOVE: 40` at 39.6,
where a 40 drink is refused (and a 20 swap at 19.6, the same way). The bag's red
line and the log, which rounds down (`39 movement left, a drink costs 40`), say the
truth; whether the readout should round down too is a question of the readout's,
asked here so it is not lost.

**This decides whether a potion is cheaper outside a fight, so it is the user's to
decide.** The alternative: free out of combat, priced as a swap is — the cost only
once a live enemy has been seen this turn (`TurnSystem.SwapCost`'s condition),
so a party tops up for nothing between fights and pays 40 only when the drink is
a mid-fight decision. settled.md's Phase 6b implementation pass records the ruling.

## How a potion's share rounds, and of which pool (§3.4) — PHASE 6b DECISION 3

§3.4 has a potion restore "a share of max HP" or "a share of max mana", per unit
of quality (`HealthPotionPercent`, `ManaPotionPercent`, 20 each; §6.6's
"healthRestored = HealthPotionPercent × quality × maxHp"). Nothing says how a
share that is not whole rounds — 20 percent of 33 is 6.6 — or which maximum a
mana potion is a share of: the pool the member has earned (`MaxMana`, what the
ladder grows, "the mana pool" §3.4 names) or what a spend can reach
(`UsableMaxMana`, the earned pool less what the equipped enchantments' locks
reserve).

**Shipped provisionally (Phase 6b.2): rounded down, the house's truncation, with
no minimum**, so quality 1 on 33 HP restores 6, quality 3 restores 19, and quality
5 restores all 33 — the ceiling §6.6 names, "a potion that heals you completely
from 1 HP", which rounding down never weakens, since five units are then 100
percent or more. **Health is a share of `MaxHp`; mana is a share of `MaxMana`, the
earned pool, and what it restores is capped at `UsableMaxMana`**, as Siphon's
refund is: so at quality 5 a mana potion refills the spendable pool whatever the
locks reserve. The bag says what a potion offers of the member's maximum as it
stands — `RESTORES 12 OF 30 HP`, `RESTORES ALL 30 HP` at exactly a whole pool,
`RESTORES ALL 30 HP - 6 OVER` past it — and a drink reports what the pool took:
for health the heal it applied, the HP XP it teaches, never the point of max HP
that XP may buy, which arrives filled as it does after any heal.

**This decides how much a potion restores at the margins, so it is the user's to
decide.** The alternatives: (a) round to the nearest, or up, or restore at least
1, so a quality-1 potion on a small pool always restores something; or (b) a mana
potion is a share of `UsableMaxMana`, so a heavily locked caster's potion restores
less, and quality 5 still refills exactly what is spendable. settled.md's Phase 6b
implementation pass records the ruling.

## Which runs yield a batch (§6.6, §6.3) — PHASE 6b DECISION 4

§6.6 has the farm tick per run, with the enchanter: "Production ticks per run ...
the farm and the enchanter tick together. A run advances your weapons and stocks
your shelf in one event" (phase-6-between-runs.md). But the enchanter's clock does
not tick on every run: a run counts for its boss, or for §6.3's mercy — "a mercy
tick advances the service clock and nothing else" — and "Entering the dungeon ticks
nothing".
Nothing says whether "per run" is every run that counts, or every run's end, or the
boss kills alone.

**Shipped provisionally (Phase 6b.3): every run that counts yields — a boss kill,
or §6.3's mercy for failing deep — exactly the runs that tick the enchanter**, in
the same event at the run's end (`CampaignState.EndRun`, `Farm.Yield`). A run that
does not count yields nothing, and the run's summary says so: `THE FARM YIELDED
NOTHING - THE RUN DID NOT COUNT`. A boss killed and then a wipe is a clear, and
counts, so it yields (§6.3's "Killed the boss, died on the way out", a run that
ticks; settled.md's "Every boss kill adds its dungeon").

**What the pick lets a player do.** A losing streak deep in a dungeon still sends
the party down with potions, as it still works the enchanter's shop; a run in and
straight out again stocks nothing.

**This decides how often the shelf is stocked, so it is the user's to decide.**
The alternatives: (a) only a boss kill yields — the mercy tick "advances the
service clock and nothing else", read literally; or (b) every run's end yields,
counted or not — since the batch never banks (DECISION 6), a twenty-second entry
gains nothing there but a fresh batch to mix. settled.md's Phase 6b implementation
pass records the ruling.

## When the first batch comes (§6.6) — PHASE 6b DECISION 5

§6.6 has the first real clear "hand over an entire category rather than a number
going up", and the hub "makes potions while you are away" (phase-6-between-runs.md).
A run's end records the clear and makes the farm's yield in one event: nothing says
which comes first — whether the dungeon a run beats for the first time is already a
worker when that run's yield is made.

**Shipped provisionally (Phase 6b.3): the clearing run's own end yields at the new
count.** The clear is recorded first, then the yield is worked out, so the first
clear of a dungeon besides the tutorial brings the alchemist and a batch at once —
`A FIRST CLEAR - THE ALCHEMIST HAS ARRIVED - THE FARM YIELDED 7 UNITS` — even on a
wipe after the boss fell, and each later first clear yields at the count it brings.

**This decides whether the first real clear pays at once, so it is the user's to
decide.** The alternative: a new worker produces from the next run on, so the
first batch comes a counted run after the first real clear, and the run that beat
a dungeon yields at the count it began with. settled.md's Phase 6b implementation
pass records the ruling.

## Where the batch lives, and what is saved (§6.6, §6.7, §3.5) — PHASE 6b DECISION 6

§6.7 says "the batch must never reach `CampaignState`"; §3.5 says "The batch is not
saved between runs ... computed at the hub and discarded" (phase-3-loot.md); and
§6.7 also has the campaign hold "the farm's stock". The hub saves every change
(PHASE 6a DECISION 10), and the user's ruling R1 says a bad roll is never reloaded
away: a batch recomputed on every load would be mixed again after a quit and a load.

**Shipped provisionally (Phase 6b.3): the run's yield is saved as a fact of the
run's result** (`lastResult.farmYield`, beside the clears it was made at, whether
the run was a first clear, and the potions that spoiled as it ended). **The batch is
that yield less every potion's quality the bags hold, worked out (`Farm.Batch`),
never a field of the campaign.** A mix (Phase 6b.4) writes only a potion into a
bag, which is saved; so quitting and loading never refills the batch, a held
visit reads it as 0, and the units left unmixed spoil on the descent with no code to
spoil them. The next run's end replaces the result, and the yield with it: nothing
is ever added to a batch. The save refuses bags holding more units than the last
run yielded (check 32).

**This bends §3.5**, whose batch is not saved at all, **and keeps §6.7**: no batch
in `CampaignState` (pinned: `FarmTests.TheBatchNeverReachesCampaignState`).

**This decides what a reload can do to the batch, so it is the user's to decide.**
The alternatives: (a) §3.5 as written — nothing saved, the batch recomputed at the
hub from the clear set — where a mix, a quit and a load mixes it again; or (b) the
units left as a campaign field, set at the run's end, spent by mixing and zeroed on
entry — which bends §6.7's "never reach `CampaignState`". settled.md's Phase 6b
implementation pass records the ruling.

## `FarmYieldPerClear` and "tune the total" (§6.6) — PHASE 6b DECISION 7

The user's ruling states the farm's target "as a total, not a rate, because the
dungeon count is an unmade content decision and a per-clear number would inflate
silently with every dungeon added" (settled.md), and §6.6 adds "Then the count can
change without retuning". The target: "a campaign that has cleared everything
should produce about one full loadout of maximum-quality potions per run", by
`FarmYieldPerClear = (PartyCarrySlots-worth of potions ÷ HealthPotionPercent) ÷
number of non-tutorial dungeons` — stated "the way §1.3 states
`MovementUnitsPerMana`", a key "re-derived by hand, never computed from the budget"
(settled.md). A loadout is "six" elsewhere in
§6.6 ("it carries the same six", "a full loadout of six maximum-value potions costs
thirty units"); `PartyCarrySlots` is 24. And the hand-off names the key
`FarmYieldPerClear` (settled.md's Phase 5 hand-off; phase-5-save.md §5.3's table).

**Shipped provisionally (Phase 6b.3): the hand-off's per-clear key, re-derived by
hand.** A loadout is six maximum potions, each at the quality that restores a
whole pool — 100 ÷ `HealthPotionPercent` rounded up, 5 at 20 — so 6 × 5 = 30
units, over the four dungeons beside the tutorial that ship: 7.5, rounded down to
7, so a full clear yields 28 a run. Its comment in `tuning.json` carries the
derivation and says to re-derive it, and the suite fails whenever re-deriving
would give a rate other than the master's — the master's roster moved, or
`HealthPotionPercent` moved far enough to change the quality for a whole pool (21
to 24 still ask 5) — or when the rate is not that rounding down, 6 or 8 at four
dungeons (`FarmTests.TheShippedYieldIsAboutOneLoadoutOfMaximumPotions`).

**What the pick costs, plainly.** It is a rate. A dungeon added through
`dungeons.json` — beside the game in a playtest, or in the master without a test
run — raises a full clear's yield by 7 a dungeon with no test and no warning: the
silent inflation the ruling rules against. The test catches it only in the master,
and only when the suite runs.

**This decides how the farm's number is kept, so it is the user's to decide.** The
alternatives: (a) a loadout read as `PartyCarrySlots`-worth — 24 × 5 = 120 units,
30 a dungeon, the ceiling arriving after the second clear; (b) **the form that
keeps both rules**: a `tuning.json` key for the total — the full-clear yield, 30,
or the loadout, 6 — with the per-clear yield computed as the content loads, from the
loaded count of dungeons that do not retire on their clear, so a dungeon added
anywhere re-derives it and the number is still tuned without a rebuild; its cost is
the hand-off's key name, since the key would not be `FarmYieldPerClear`, and
settled.md's hand-off and §5.3's table would move with it; or (c) rounded up to 8,
32 a run. Whatever the form, the six-or-24 reading of "a loadout" and the 7-or-8
rounding are this decision's too. settled.md's Phase 6b implementation pass records
the ruling.

## The roster stays at four (§6.6; settled.md's "Phase 6b completes the roster") — PHASE 6b DECISION 8

settled.md has the first playable hub ship "6 non-tutorial dungeons", then records
"Four dungeons ship beside the tutorial, not the six above — provisional. The
missing two need content that does not exist yet (a Great Axe unique, or a repeated
theme), and Phase 6b completes the roster." A dungeon's name and its boss are
setting, which is kept outside this repository, and the Great Axe unique for Cleave
does not exist.

**Shipped provisionally (Phase 6b.3): deferred.** No dungeon is added, and
`FarmYieldPerClear` is derived against the four that ship (DECISION 7).
`dungeons.json`'s header, which said "Phase 6b completes the roster", now says the
roster stays at four for now, deferred here, and that `FarmYieldPerClear` is
re-derived by hand when a dungeon is added. **This bends the settled hand-off.**

**This decides how much hub Phase 6b leaves, so it is the user's to decide.** The
alternative: two placeholder rows, each a repeated theme with provisional names,
marked `/* provisional */` as the four are, and `FarmYieldPerClear` re-derived to 5
(30 over six). settled.md's Phase 6b implementation pass records the ruling.

## Whether the alchemist can leave (§6.6) — PHASE 6b DECISION 11

§6.6 has the alchemist arrive with the first real clear: "nobody mixes potions until
you have beaten a dungeon that is not the tutorial", and "the tutorial does not
count" (phase-6-between-runs.md). Phase 6b.3 derives his arrival from the campaign's
own records: he is there once the farm has a worker, a dungeon cleared and not
retired (`Farm.AlchemistHasArrived`), asked of the retired list and never of which
dungeon today's `dungeons.json` retires on its clear (check 30's precedent). Nothing
says whether he can go again. Under the shipped files he cannot: only the tutorial
retires, and it is never a worker. But a `dungeons.json` beside the game may move
`retiresOnClear` onto a dungeon already beaten (check 3, withdrawn). If that is the
farm's one worker, the hub lists it alone, as the content's tutorial now, until it is
beaten again, and that clear retires it. The farm has no worker, so the alchemist is
not there, and the next first clear of a dungeon that does not retire brings him
again.

**Shipped provisionally (the Phase 6b.3 review): he is there while the farm has a
worker**, derived as built, with nothing saved. So under that file he goes with the
farm's one worker, and the run that brings him back says so as the first did: `A
FIRST CLEAR - THE ALCHEMIST HAS ARRIVED - THE FARM YIELDED 7 UNITS`. No potion can
be in a bag while he is gone: those carried into the re-clear spoiled as it ended,
and nothing mixes one until he is back. So the save's check that a potion needs a
worker (31) refuses nothing play writes. The log's arrival line says only what is
true both times, `[Farm] The alchemist has arrived: The Long Gallery, beaten for the
first time, is the one dungeon the farm counts`, never "the first dungeon beaten
besides the tutorial", which the Spear Hall was. The ALCHEMIST page (Phase 6b.4)
opens and closes on the same question, so it follows this reading: under that file
it says when he arrives again, naming the content's tutorial — `THE ALCHEMIST
ARRIVES WITH THE FIRST CLEAR BEYOND THE SPEAR HALL` — until the next first clear.

**This decides whether the hub can lose a service it has opened, so it is the
user's to decide.** The alternatives: (a) he stays for good once any dungeon besides
the tutorial has been beaten. That needs the tutorial told apart from a worker a
moved flag retired: either asked of today's content, against check 30's precedent,
or saved, as a version-3 member such as the run he arrived on, with a check of its
own. With no worker the farm still yields nothing, so his page would stay open over
an empty batch until the next first clear, whose summary would say the farm grows.
(b) He counts every dungeon ever beaten but the first, taking the tutorial to come
first. A dev-override launch (`DISTANCERPG_DUNGEON`) beats a real dungeon first, so
(b) would miscount it. settled.md's Phase 6b implementation pass records the ruling.

## Pouring a potion back before the descent (§6.6) — PHASE 6b DECISION 9

§6.6 has the split made once: "that trade is decided at the moment the potions are
handed to you, with the run's whole supply in front of you and nothing recoverable
afterwards" (phase-6-between-runs.md), and §3.4 "you split it between health and
mana *and strength* when you collect it, and anything you leave with him has
spoiled by the time you climb back out" (phase-3-loot.md). The doc's most literal
shape is a single hand-over: the whole split set at once, and what is left spoiling
there and then. Nothing says whether a potion, once mixed, can be undone before the
party descends.

**Shipped provisionally (Phase 6b.4): potions are mixed one at a time, and each is
reversible until the descent.** On the ALCHEMIST page, H or M mixes a health or a
mana potion at the quality A/D set into the chosen member's bag, and T on any
potion in a bag pours it back, its units back in the batch exactly — the batch is
the last run's yield less what the bags hold, so a pour-back needs no number of its
own (DECISION 6). Each mix and each pour-back is saved at once (PHASE 6a DECISION
10). So a misplaced mix — the wrong bag, the wrong strength — costs nothing before
the descent, and nothing can be recovered after it: what is left unmixed spoils as
the party descends, which DUNGEONS says beforehand (`THE ALCHEMIST HOLDS 3 UNMIXED
UNITS - THEY SPOIL WHEN YOU DESCEND`), and every potion carried spoils as the run
ends. The descent logs the run's split — `[Alchemist] Descending with 3 health
potions (9 units) and 2 mana potions (4 units); 1 of 14 units unmixed, spoiled` —
the numbers §6.6 asks a playtest for ("if every party mixes the same ratio every
run, the prices are wrong").

**What the pick lets a player do.** Try a split, read what each potion would
restore each member on the page's right, and change it, for nothing, until the
party goes down; and move a potion to another bag by pouring it back and mixing it
again.

**This decides whether the split is a commitment before the descent, so it is the
user's to decide.** The alternatives: (a) a mix is final, made one at a time — no
pour-back, so a misplaced potion is carried or poured out in the dungeon; or (b)
**a single hand-over**, the doc's literal shape: the whole split set on the page
and confirmed once, what is left spoiling at that moment rather than at the
descent, and nothing mixed or poured back after it until the next batch.
settled.md's Phase 6b implementation pass records the ruling.

## The ALCHEMIST page before the alchemist arrives (§6.6) — PHASE 6b DECISION 10

§6.6: before the first real clear there are "no potions on the shelf and no shelf"
(phase-6-between-runs.md). The enchanter, shut until the tutorial is beaten, says
so where he would be — `THE ENCHANTER OPENS WHEN THE TUTORIAL IS BEATEN` — and the
presentation backlog asks for what is unavailable to be shown as unavailable
"rather than simply absent" (settled.md).

**Shipped provisionally (Phase 6b.4): the third page is there from the start.**
TAB goes DUNGEONS, WEAPONS, ALCHEMIST and round on every campaign, and until the
first clear of a dungeon besides the tutorial the ALCHEMIST page says `THE
ALCHEMIST ARRIVES WITH THE FIRST CLEAR BEYOND THE TUTORIAL`, naming the tutorial
off its row as the enchanter's gate does, with nothing to choose; H and M say it
over the keys, every other key but TAB and ESC does nothing, and its keys line is
`TAB DUNGEONS - ESC MENU`. It opens with his arrival, and closes again if he goes
(DECISION 11).

**This decides whether the hub shows a service before it has one, so it is the
user's to decide.** The alternative: the page is absent until he arrives — TAB goes
between DUNGEONS and WEAPONS alone until then — and the run's summary that brings
him (`A FIRST CLEAR - THE ALCHEMIST HAS ARRIVED - ...`) announces the third page.
settled.md's Phase 6b implementation pass records the ruling.

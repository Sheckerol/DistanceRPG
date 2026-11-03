# Phase 5 — Save / load

Lands last, once the state model above is final.

## 5.1 What is stored

| Group | Contents |
| --- | --- |
| Run | Map seed, current floor index, turn count, **party XP snapshot taken at dungeon entry** (§4.4 — on a party wipe, each pool rolls back `DeathXpLossPercent` of its gain since this snapshot; discarded untouched on any other exit) |
| Party | Position, HP, mana, innate stats, all three XP pools, inventory (each weapon carrying **both** its forged and its live modifier sets, plus enchantments, wear and `WearCapacity` — built in Phase 6a as `wear` and `wearCashed`, the wear ever cashed, from which the capacity and its progress are replayed as HP is from its XP: settled.md's Phase 6a pass, PHASE 6a DECISION 2), active status effects, the hidden `Overheal` remainder, carried consumables |
| Per visited floor | Enemy states (position, HP, alive, `DefeatedAtTurn`, `DefeatCount`, **accumulated revival damage and HP bonuses**, weapon, enchantments, status effects) and the explored fog grid |

Maps are **never serialized** — they regenerate from `floorSeed(n)`. This is
exactly the design `EnemyPlacer`'s doc comment anticipated: regenerate the
dungeon from the seed on every entry, place enemies only on the first visit,
restore saved states afterwards.

## 5.2 Format

`System.Text.Json` with an explicit version field and a DTO layer in
`Logic/Save/` — the gameplay types must not carry serialization attributes, or
`Logic/` stops being clean. Version 1 was Phase 5's; **version 2 is Phase 6a's**
(a weapon's `wear` and `wearCashed`, and the root's `tuningHashVersion`, the
version whose played keys the tuning hash covers: PHASE 6a DECISION 9), and a
version-1 document is lifted to it as it is read — every weapon unworn, its hash
of version 1's keys — by the one migration step the codec ships.

Where the saves live, how many there are, when one is taken and how a player
gets back to one is settled.md's (the user's rulings of 2026-09-25 on the save
model, R2, and on the save slots, R5): a title screen offering CONTINUE, NEW
CAMPAIGN, LOAD GAME and QUIT; save slots whose count is `tuning.json`'s
`SaveSlots`; and a campaign bound to the slot it was begun in or loaded from,
into which every save of it goes — the game's own at every safe point, and SAVE
GAME's.

Round-trip test: save, reload, and assert that a fixed sequence of turns
produces an identical world state — in the shipped game too, since its dice are
the visit's own, seeded and saved (the user's ruling of 2026-09-25, settled.md).

A save is raw JSON, so none of its fields is trusted (the user's ruling of
2026-09-25: "since we are storing in raw json we should probably not trust the
fields"). Each is read against the content and its range — a count past what
play can reach, where a larger one would wrap the arithmetic that reads it, is
refused at its path as a count below 0 is — and then the fields against each
other: **a save that contradicts itself is refused whole** (a retired dungeon
never cleared, a last run's summary that disagrees with the campaign it ended),
and set aside like any save that cannot be read (settled.md, R3(b), which lists
the ceilings and the checks).

## 5.3 `tuning.json` — the numbers, editable between loads

Almost every open question in these documents is a **number nobody can pick on
paper**: the mana divisor (§1.3), the three Searing constants (§1.5), the
revival step (§3.2), fight-length-dependent decay. Recompiling to try `8`
instead of `16` is the difference between tuning a game and not tuning it.

**`DotManaPerDamage` is the clearest case in the file.** It is a single
exchange rate governing what the entire damage-over-time layer costs — every
lingering element, every bleed, on every target in a shape (§1.5) — so it is
both the number with the widest blast radius and the one with the least chance
of being right first time. Nothing about it can be reasoned to a value; it needs
a session, a wand, and a floor of enemies. A constant like that being compiled
in would be the single worst hardcode in the project.

So: a **`tuning.json` beside the executable**, read once at startup into the
`Tuning` record (settled.md, Phase 5 pass), overriding the shipped file's
numbers, which the game carries built in.

### What it must never contain

**Nothing the golden tests pin.** `MapGenerator`, `Mulberry32` and `Pathfinder`
constants stay compiled, permanently and without exception — a tunable RNG
constant is a tunable *parity* constant, and `TestData/distancerpg-golden.json`
stops meaning anything the moment one is editable.

That wants to be a test rather than a comment: assert the tuning schema's key
set is disjoint from a hardcoded list of parity-critical names, so adding one by
accident fails the build rather than silently invalidating the goldens.

The same applies to anything **structural** — the eight classes, the four
variants each, the modifier relations of §1.1. Those are data, but they are data
with invariants (`Allowed`, `MaxForged`), and a JSON file that can violate them
is a crash waiting on a typo. `tuning.json` holds **scalars only**.

### Rules that keep it honest

- **Read once, at startup.** Never hot-reloaded mid-run. A run has to resolve
  under one set of numbers or its own save stops reproducing.
- **Missing keys fall back to the shipped file's number, which the game carries
  built in.** A partial file is valid, so the player edits the three lines they
  care about — and adding a constant later never invalidates an existing file.
- **Ratios are integer divisors, not floats.** `MovementUnitsPerMana: 10`, not
  `ManaPerMovementUnit: 0.0625`. It reads correctly, it cannot drift, and it
  makes the units explicit in the name — which is the exact error §1.3 already
  made once by thinking in tiles while the code counted units.
- **Out-of-range values clamp and warn** rather than throwing. This is a file a
  human edits by hand; a typo should cost a log line, not a session.

### The starting set

Everything currently flagged as a knob, gathered in one place:

| Group | Keys |
| --- | --- |
| Economy | `MovementUnitsPerMana`, `MovementBudget`, `StartingPool` (30 — HP, mana and a weapon's starting wear capacity alike; 25 before settled.md's Phase 2 pass) |
| Statuses | `EffectPerLevel` per status (`Searing` 1, `Mire` 10%, `Ward` 1), `OverhealPerWard`, `DotManaPerDamage`. **No decay constants** — one level a round, universally (§1.5) |
| Appliers | `ApplyPercent` per enchantment — the one number that decides how many levels a source grants |
| Modifiers | `AcquiredHeadroom`, per-stack values, `MaxForged` overrides |
| Farming | `DefeatStackChance`, `ReviveStepPercent`, `ResurrectTurnsBase`, `ResurrectTurnsFloor`, `CleanKillBonus` |
| Uniques | `UniqueChanceCeiling`, `UniqueChanceMidpoint`, `UniqueChanceK` |
| Dungeon | `BossFloorMin`, `BossFloorMax`, `BossStackOffset`, `CooldownRunsPerFloor`, `MercyFloor`, `DeathXpLossPercent` |
| Enchanter | `WearPerHit`, `GraftBaseChance`, and what a deposit costs by the user's ruling of 2026-09-25 (settled.md): `WearCapacityPerBar`, `FirstSlotCapacity`, `SlotCapacityStep`, `EnchantmentSlots`, `FirstEnchantmentWear`, `EnchantmentWearStep` — superseding `WearCapacityLadder` (30/40/55) and `EnchantmentWearCost` |
| Inventory | `PartyCarrySlots` |
| Consumables | `PotionUseCost`, `HealthPotionPercent`, `ManaPotionPercent`, `FarmYieldPerClear` |

Collecting them is worth as much as making them editable. Half the open
questions in these documents are "this number, against that number" — the mana
divisor against cast costs, Searing decay against fight length, the farm
allowance against `forged + 5`. Seeing them on one screen is how those get
resolved.

### Saves have to notice

A save records the **hash of the tuning values it was played under**. Loading
under different numbers warns rather than refuses — retuning mid-campaign is a
thing a designer does on purpose, and silently changing a saved game's rules
underneath the player is the failure to avoid.

The full values are not stored. The hash costs nothing and answers the only
question that matters: *are these the same numbers?*

## 5.4 The content files, and what they share

Three files hold content: **`restricted.json`**, **`weapons.json`** and
**`dungeons.json`**. They are a different kind of thing from `tuning.json`,
which is scalars — these carry **invariants**, so they are validated rather
than clamped. Beside them, the reference data the content names is code: the
modifiers, the enchantment catalogue and its unique souls (§5.7), and the
starting party (§5.9).

### The reason, and it is not that numbers become editable

Numbers becoming editable is a convenience. The real gain is that **adding a
mechanic stops being a content change.** The work splits five ways, and only
two parts are code — the rule, and the reference data the rule is addressed by:

| Question | Answered by | Why there |
| --- | --- | --- |
| **What does it do?** | Code | It is a rule. `Brace` fires a retaliation; that has to run |
| **What is it?** — the modifiers, the enchantments and their souls, the starting party | Code | Reference data: what behaviour is addressed by, by a stable id; it changes when behaviour does |
| **What number?** | `tuning.json` | It is a scalar |
| **What may it coexist with, and can it be rolled?** | `restricted.json` | It is a relation, and relations change every time a mechanic is added |
| **Which weapons and dungeons use it** — the weapon types, the weapons, the dungeons? | `weapons.json`, `dungeons.json` | It is content |

So adding a modifier is: implement the behaviour, price it, declare its
relations — **and none of that last part is a recompile.** Every existing weapon
can adopt it in the same edit. That is the difference between a modifier
*system* and a modifier *list*, and it is §1.1's uniformity finally paying out
in the workflow rather than only in the design.

Relations were originally going to stay in code, on the reasoning that they are
what validates the content and so cannot themselves be content. That was wrong,
and specifically it was wrong in the way that mattered most: **if declaring a
relation needs a recompile, then adding a modifier needs a recompile**, and the
additive property the whole split exists to buy is not real. The validation
concern is genuine but it is answered by validating the relations too (§5.5),
not by freezing them.

### The user's ruling (2026-09-25): reference data in code, the JSON the master copy

Phase 5 first shipped `tuning.json` and five content files — these three,
`enchantments.json` and `party.json` — all generated from a compiled copy
of every number, relation, weapon and dungeon, with a test holding each
file to that copy byte for byte. The user ruled that **reference data is
code and everything else is JSON, with no C# duplicate**: "behaviour,
modifiers, and enchantments living in the code due to them being reference
data, while weapon types, weapon modifiers, rolled weapons and numbers
driving tuning are fully in json". The modifiers, the enchantment catalogue
and its unique souls, and the starting party are what behaviour is
addressed by: they change when behaviour does, so they live beside it. The
numbers, the relations, the weapon types, the weapons and the dungeons are
the files' alone, because "if it lives in the code then changing the json
loses all purpose" — a number or a relation with a second home in code is
one whose file edit may not be the one that plays.

So the shipped files in `GameEngine.DistanceRPG/Data` are the **master copy**,
edited by hand, and the build carries them: each is the fallback for its copy
beside the executable, where a playtest edits it. A copy that is missing or
cannot be read plays the built-in one and says so, and a `tuning.json` key left
out plays the built-in number — so the game runs with nothing beside it, and
says so. The weapons the party owns are no content at all: they live in the
save (§5.1).

### The shared contract

All three follow the same rules, and it is worth stating once:

- **Every entry has a stable string id.** Saves, drops, enemy states and the
  enchanter's seen-catalogue all store ids, never indices or positions.
  Reordering a file is then free and appending to one costs nothing.
- **Validated at load against the predicates that already exist** — `Allowed`,
  `MaxForged`, the type-opposition table. No new validation logic for content:
  the loader asks the same questions the graft roll and the boss drop table ask.
- **Invalid content aborts startup**, naming the entry and the rule it broke.
  Not clamped, not skipped. A bad scalar in `tuning.json` is a bad balance
  number and the game still runs; a broken invariant is something every
  downstream caller assumes holds, and limping past it turns a typo into a
  crash somewhere unrelated.
- **Every number has a range, checked as the invariants are** (the user's ruling
  of 2026-09-25: "since we are storing in raw json we should probably not trust
  the fields"). A statline, a stack, a tier, a shape's dimension, a boss's floor
  and statline: each lies in the range the game plays it in, its top read off
  `tuning.json`'s clamps or a rule and never off the file, and one outside it
  aborts startup like any broken invariant, naming the entry, the field, the
  number and the range (settled.md, R3(a), which lists them).
- **Nothing here touches the golden tests.** `TestData/distancerpg-golden.json`
  pins map generation, fog and pathing — never statlines, relations or drop
  tables. Worth saying plainly, because "the content is data now" sounds like a
  parity concern and is not.

The code catalogues keep the same contract: every entry has a stable string id,
and each is validated at load — the enchantment catalogue on its own and against
`restricted.json`, the starting party against `weapons.json` — so a code edit
that breaks a rule stops the game as a broken file does, and the tests catch it
first.

### Load order is a real constraint

The files reference each other, and the code catalogues, in one direction, so
they load in one order:

```
the code catalogues → tuning.json → restricted.json → weapons.json → dungeons.json → the starting party
```

The modifiers and the enchantment catalogue are what every id is checked
against, so they come first, and the catalogue is checked on its own before
anything reads it. `restricted.json` is the predicates everything after it is
checked against, and the catalogue is checked against it in turn — its
elements' opposition, its unique souls. `weapons.json` names enchantment ids for
caster innates and unique souls; `dungeons.json` names a modifier for its theme,
and its **drop table is computed from the weapon list** rather than read. The
starting party is checked last because it is the only reference data that names
*specific weapon ids* rather than classes or variants — a starting loadout is a
concrete item, not a roll. A cycle here would be a design error rather than a
loader problem — if content ever needs to reference forward, the thing it is
reaching for probably belongs in code.

## 5.5 `restricted.json` — what may coexist, and what may be rolled

The relations from §1.1, for modifiers and enchantments alike, in one file —
and nowhere else: the game keeps no copy of a relation in its code (§5.4):

| Key | Holds | Shape |
| --- | --- | --- |
| `excludes` | The exclusion groups — threat zone, displacement, block response, crit rider, currency, and the opposed damage-type pairs | Arrays of ids; every member excludes every other |
| `requires` | Dependencies — `Riposte` needs `Block`, a lingering element needs its element | id → array of ids |
| `kind` | Weapon-type restrictions — `Brace` melee, `Overwatch` ranged | id → melee / ranged / caster |
| `forgedOnly` | Deepenable if already present, **never added from zero** — `Charges` | Array of ids |
| `neverRolled` | Never granted by any roll at all — the unique enchantments | Array of ids |

**Groups are arrays rather than pairs**, which is what makes them readable. The
threat zone is `["Brace", "Opportunist", "Overwatch"]` and the loader expands it
to the six directed exclusions — so a group is one line, cannot be
half-declared, and adding a fourth member to it is adding a word. That is the
`Symmetric()` closure §1.7 already describes, moved to load time.

The last two are deliberately separate, because they say different things.
`forgedOnly` is about a modifier that would be *harmful* granted from nothing —
a `Charges` on a bow is a cap where there was none (§1.1). `neverRolled` is
about something that must stay scarce — a unique enchantment the enchanter
cannot copy (§3.3). One is a safety rule, the other is an economy rule, and
collapsing them would lose the reason either exists.

### The file that validates everything else is validated first

The original objection to relations-as-data was that editable relations cannot
validate editable content. The answer is that `restricted.json` has its own
well-formedness rules, checked before anything reads it:

| Check | Why |
| --- | --- |
| Every id resolves to a real modifier or an enchantment in the game's catalogue | A typo in a relation is a rule that silently does not apply |
| No id excludes itself | Unsatisfiable, and always a mistake |
| `excludes` closes symmetrically | Half-declared exclusions produce order-dependent bugs (§1.7) |
| `requires` is acyclic | `A` needs `B` needs `A` can never be satisfied |
| Nothing both requires and excludes the same id | That modifier can never legally exist |
| Every `forgedOnly` id is forged on at least one weapon | Otherwise it is unreachable — a rule for nothing |

Then the ordering does the rest. **Relations load before content, so a relations
edit that invalidates an existing weapon aborts startup naming both** — the
weapon that is now illegal *and* the rule that made it so. That is strictly
better than freezing either: the pair is checked together, and the error tells
you which of the two you meant to change.

## 5.6 `weapons.json`

The 32 weapons, the uniques, and the class baselines leave `Weapons.cs` — the
baselines for a section of their own, since the weapon types are a section of
`weapons.json` (the user's ruling of 2026-09-25, §5.4). Statline, forged spread,
area shape, innate enchantment id, in three sections:

| Section | Holds |
| --- | --- |
| `classes` | Each of the eight classes once, by name: its `kind` (melee, ranged or caster), the `stats` it levels on, its `signature` (none for a caster), its `baseline` — the modifiers every weapon of the class is forged with — and the statline its weapons share: `range`, `damage`, `cost`, `manaCost`; and, for the Staff and the Wand, its `dropOrder` — the variants a drop of the class rolls between, in the order the draw indexes them |
| `variants` | The 32 weapons: each one's class, its role, the modifier it **adds** to its class's baseline (a caster variant has no role and adds none), the enchantments it arrives with (a staff's innate, by id) and a wand's area shape. A statline field a variant names plays instead of its class's |
| `uniques` | Each artifact as the variant it derives from — whose class, statline and shape it takes — the modifier it **raises**, set to the count given (a caster unique raises none), and its enchantments |

So re-basing a class is one edit, to its row: a variant is its class plus what
it adds, and a unique is its variant with what it raises — both nothing, for a
caster. The loader composes each into the one weapon every other system reads.

| Check | Rule | From |
| --- | --- | --- |
| Each of the eight classes has one row | The code names every class: the placer draws five, the Staff and the Wand are cast with, a save keys weapon XP by class | §1.2 |
| A class casts exactly when it is the Staff or the Wand | The code casts with those two: a staff's innate is its cast, a wand's element and shape arrive when it drops | §1.3, §1.4 |
| A class levels on one or more of STR, DEX and INT | CON levels no weapon: it is the body's pool, not a craft | §1.2, §2.2 |
| No two classes share a signature | A dungeon's theme finds its class by its signature | §4.3 |
| A martial class's baseline is its signature plus a second modifier; a caster class has no signature and a baseline of one modifier at least — the shipped rows' `Resonant ×1` — and every staff and wand is forged with it alone | No weapon starts with fewer than two forged axes, and a caster's second is its enchantment; which modifier is the row's, not the load's | §1.2, §1.3 |
| A caster class orders the variants its drop rolls between — each its own, none twice — and a martial class orders none | A martial drop draws a role; a caster's draw indexes the order, so reordering `variants` moves no saved drop, and a variant the order leaves out never drops | §3.1, §3.5 |
| Every forged spread is legal | `Allowed(t, kind, present)` | §1.1 |
| Nothing forged too deep | `MaxForged` — `×3`, or `×1` for `Light`/`Resonant` | §1.1 |
| No weapon has fewer than two forged axes | The farm allowance has somewhere to go | §1.2 |
| A unique derives from a variant | Exactly one modifier at `×3`, plus its unique enchantment | §1.5 |
| Casters carry exactly one innate enchantment | Staff fixed by variant, wand a damage type | §3.1 |
| A `Light`-forged unique carries `signature ×n` with **`5 − n` enchantments**, `n` ≥ 2, one of them unique or tier 3 | A currency cannot carry an artifact's identity, so the souls do — at one soul per stack given up | §1.5 |
| Every unique enchantment that is a **magnitude** names the source it takes a percentage of, and quotes **no flat trigger cost** | The tier-1 pin gives it no ladder of its own; a borrowed ladder must lift the price as well as the effect | §1.5 |
| A unique enchantment with a dependency appears only on weapons carrying two enchantments | `Overheal` alone is inert; the check catches it being placed somewhere it cannot fire | §1.5 |

§1.7 already lists "weapon data validation" as a caller of `Allowed`. This is
that caller, and the point is that **no new validation code exists**.

**The index trap is sharpest here.** `EnemyPlacer` assigns weapons from a seeded
stream and saves record what every enemy carries, so a positional reference
means adding one weapon silently re-rolls every enemy in every existing save and
breaks seed reproducibility for no visible reason.

§3.1's placement roll is already right for the same reason — it rolls over the
**eight classes** with the variant rolled later, so the stream depends on a count
fixed in code rather than on the file's length. Adding a fifth variant to a class
would still shift it; that is a deliberate, rare change rather than an ordinary
edit.

## 5.7 The enchantment catalogue (code)

The catalogue and the unique souls: lock, trigger cost, condition, base potency,
effect reference, `unique` flag, and a damage type where the entry is elemental.
They are **code** (`Logic/Enchantments/EnchantmentDefinitions.cs`), not a file:
the user's ruling of 2026-09-25 (§5.4). An enchantment is reference data, like a
modifier — the effect it names is code, and the entry is what that behaviour is
addressed by, through the stable id the relations, the weapons, the drop pool
and a save name it by. A change to the catalogue is a rebuild, made when
behaviour changes.

**The effect itself stays code.** "Heal the wielder 1 per tier on damage dealt" is a rule,
so an entry names an `EffectKind` and supplies parameters — the same shape as a
modifier naming a behaviour it does not implement.

**The numbers `tuning.json` owns are not written in the catalogue at all.** An
element's and `Serrated`'s `ApplyPercent` and `Arcane`'s potency are
`tuning.json`'s (`ApplyPercent`, `ArcanePotency`, §5.3): the catalogue marks each
as set by tuning and states no number of its own, and the shipped `tuning.json`
must name every one, or the game does not start.

**The `unique` flag says one thing only: the tier is pinned at 1** (§3.3). Its
*roll-eligibility* lives in `restricted.json` under `neverRolled`, because that
is the same question asked of `Charges` and belongs in the same place — and
there alone: the loader checks only that `neverRolled` names the catalogue's
enchantments, never that it matches the flag. (It cross-checked the two until
the content rework's audit of 2026-09-25 found the cross-check a second copy of
the relation in code, against the user's ruling — "if it lives in the code then
changing the json loses all purpose": any edit to the list alone stopped the
game.) The shipped file lists the nine souls; a soul it leaves out may be
copied, and still drops nowhere, since the drop pool names none.

**It is still validated at load**, first and on its own, as the file was: a code
edit that breaks a rule stops the game on its screen, and the tests catch it
before a playtester can. The checks that meet `restricted.json` — `neverRolled`
naming the catalogue's enchantments, the elements' opposition — are that file's
to fix, since it is the one a playtest edits, and a failure there names it.

| Check | Rule | From |
| --- | --- | --- |
| Trigger cost is **never zero** | No enchantment fires free, ever | §3.3 |
| An entry quotes a flat trigger cost **or** applies a status, never both | A status applier prices off its level count, so a flat cost would decay into free | §3.5 |
| Every innate stat spread is a permutation of 1–4 | Free to check, silently wrong if it drifts | §2.1 |
| `neverRolled` names the catalogue's enchantments alone | Only an enchantment is rolled or copied: a modifier there is a rule that silently does not apply | §5.5 |
| Damage-type opposition is symmetric and total | Four types, two pairs, every type opposed by exactly one | §1.4 |
| A lingering element names an element that exists | Searing is nothing without its `Flaming` | §1.5 |
| Every effect reference resolves to an `EffectKind` — by construction: an entry's effect is typed | A typo is not a silent no-op | — |

The first is the one to enforce hardest. §3.3 spends a section on *every trigger
costs mana* holding up three separate arguments — the resource gate, the
levelling path, and `Resonant` having something to discount — and all three fail
quietly if a single entry ships with `0`.

**The id trap is sharper here than for weapons**, because the enchanter's
catalogue is a `HashSet` of enchantment ids in `CampaignState` (§3.5). If ids
shift, a player's accumulated catalogue does not merely mis-sort — it silently
becomes a set of different enchantments, or of nothing.

## 5.8 `dungeons.json`

Name, theme modifier, attunement, boss definition, floor range, and the flags
the tutorial needs.

| Field | Notes |
| --- | --- |
| `theme` | A `ModifierType`. Forged on the boss's own drop, added to the roll pool for every other drop in the dungeon (§4.3) |
| `attunement` | A damage type, or none. Applies to everything the dungeon spawns (§1.4) |
| `bossFloorMin` / `bossFloorMax` | `5`–`10` for ordinary dungeons; the tutorial pins both to `2` |
| `boss` | Statline plus innate modifiers. The golem: **80 HP, `MovementBudget 40`** (slow, in a game where the party moves on a ~160-unit budget), innate `Block ×3` whatever it holds, and it **does roll a weapon like a dummy does** — forged with the theme, same as every other drop in the dungeon (§4.3) |
| `retiresOnClear` | Tutorial only. Everything else takes the `bossFloor`-run cooldown (§4.3) |

**The drop table is computed, not authored.** §4.3 says a themed boss cannot
drop a class that will not take its theme, and that is `Allowed` run over the
eight classes at load — so the file names a theme and the loader derives which
classes remain. Authoring it by hand would let the two disagree, and the
disagreement would look exactly like a drop-rate bug.

It is also the clearest payoff from relations being data. A `restricted.json`
edit that makes `Brace` legal on axes changes the Brace dungeon's drop table
**with no other edit anywhere** — because the table was never written down.

| Check | Rule | From |
| --- | --- | --- |
| At least one class can carry the theme | Otherwise the boss drops nothing at all | §4.3 |
| The theme is a **class signature**, as `weapons.json`'s class rows name them | So each themed dungeon can drop exactly one named unique, via the Purity coincidence | Settled |
| `retiresOnClear` implies a fixed boss floor | The tutorial's two floors are the enchanter's mana budget, not a roll | §4.3 |
| Only one dungeon retires on clear | Two un-repeatable dungeons is a content decision nobody has made | §4.3 |

The second is the one that will get broken by accident, because a theme that is
*not* a class signature still works — it just quietly means that dungeon can
never produce its unique, and nothing else in the game announces the difference.

## 5.9 The starting party (code)

The four-character starting roster: name, innate stat spread (§2.1, a
permutation of 1–4), and starting weapon **by id**, referencing an entry in
`weapons.json`. It is **code** (`Logic/Content/StartingParty.cs`), not a file.
This section first gave it a file "for the same reason weapons and dungeons do
— a starting loadout is content, not a constant", with the identities "decided
in this file, not hardcoded"; the user's ruling of 2026-09-25 reverses both
(§5.4): the roster and its loadouts are reference data. A change to them is a
rebuild, and plays from the next campaign started — a saved campaign keeps the
party it started with (§5.1).

| Field | Notes |
| --- | --- |
| `id`, `name` | Stable id for saves; display name |
| `stats` | `{ STR, DEX, CON, INT }`, one permutation of 1–4 (§2.1, validated per §5.7's checklist) |
| `startingWeaponId` | A `weapons.json` id. Must exist, and the class must be one the roster wants represented |

**It is still validated at load, last, against `weapons.json`** — the only
reference data that names specific weapon ids. A `weapons.json` that renames a
weapon only the party names stops the game naming the member, the rule and
`weapons.json`, the file a playtest can fix. The roster's own rules — four
members, each spread a permutation, a caster in hand — no file can break: only
a code edit can, and the tests catch it first.

Party B/C/D's own identities (which pair splits which stats, which debuff
staff D carries) are therefore the code's: changing the tutorial's fourth
character to try a different stat pairing is an edit to `StartingParty` and a
rebuild.

# Phase 7 — Death, revives and the wipe

**Shipped 2026-11-09** (steps 7.1–7.6, each with an *As built* note below where
it departed from the plan; save version 4).

A new plan on top of the shipped Phases 0–6b. It **replaces the death rules
§4.4 shipped** — a fallen member stays down for the rest of the run, and only a
whole-party wipe costs XP (`DeathXpLossPercent` of the run's gain) — with a
death that costs the fight you are in and a share of the run's XP, and a wipe
that costs all of it. The decision and its reasons are recorded in
[settled](settled.md) under *Death, revives and the wipe*, with the user's
rulings on it (PHASE 7 DECISIONS 1–5, 2026-11-03, and 6, 2026-11-09).

The older documents keep their reasoning, with a note at each rule this phase
changed saying what shipped (§4.4, §5.1, §5.3, §6.1, §6.3).

| | Shipped (Phase 4) | Phase 7 |
| --- | --- | --- |
| A member falls | Down for the rest of the run, carried down the stairs; stands again only at the next entry's rest | Down for the **encounter**; revives when it ends, losing **half their XP gained this run** |
| Repeat deaths | — | Each halves what is left |
| Whole party down | Run ends; each pool loses `DeathXpLossPercent` (50) of its gain | Run ends; every pool **resets to the entry snapshot** (100%, not tuned) |
| Loot, `DefeatCount`, mercy tick, wear on a wipe | Forfeit, forfeit, floor 3+, kept | Unchanged |

## 7.1 A fallen member gets back up when the fight ends

A party member reduced to 0 HP is **down for the rest of the encounter** — out
of the turn order, unable to act, a body on the floor, exactly as today. A
death mid-fight stays a real tactical loss: the fight you are in has to be
finished, or escaped, three-handed.

**When the encounter ends, they revive** at **1 HP** (PHASE 7 DECISION 6) — mana
as it was when they fell, capped at their new maximum (PHASE 7 DECISION 4) — and
pay for it in XP:

```
runGain    = currentXp − snapshotXp        # per pool, per member
currentXp -= runGain × DeathXpLossPercent  # 50 by default, truncated
```

That is half of what this member has gained since entering the dungeon, in
every pool — each weapon class's proficiency, HP XP and mana XP (§2.2). Levels
and maxima are already derived from XP, so a death visibly costs levels rather
than an invisible number. Pre-dungeon progress is never touched: the entry
snapshot is the floor. `DeathXpLossPercent` stays in `tuning.json` (§5.3) at 50,
and its meaning changes from "a wipe's share" to "one death's share".

Only the member who fell pays. The rest of the party keeps everything.

**Why:** "dead for the rest of the run" was a death spiral the player could not
climb out of — one member down makes every later fight harder, which makes the
next death likelier. A cost in XP keeps the party whole and puts the cost where
§4.4 already puts its risk: the deeper and longer the run, the more a death
costs, because there is more run gain to halve.

### Getting up at 1 HP

A revived member stands, but barely (PHASE 7 DECISION 6). Death isn't a free
heal: the party has to spend something to bring them back up — a Renewal cast, a
health potion, or the next entry's rest — before the next fight, or walk into it
with a member one hit from going down again. Two things follow from the rules
already in place:

- **Healing them back feeds HP XP** (§2.2: `hpXp += hpRestored`), so part of
  what the death cut took is earned back by doing the healing. That's a small
  consolation, and it goes to the member who needed the healing.
- **A second death in a row costs less** because the first cut already halved
  the run gain (§7.1, below). A member left at 1 HP who falls again pays half of
  what is left, not half of the original.

### Repeat deaths get cheaper, and that is accepted

Each death halves what is left, so a member who keeps dying converges on their
entry baseline and each further death costs less. The reward for continuing has
shrunk with the penalty — a member with no run gain left is carrying no
progress out either. One character knocked back to baseline across a whole run
is a weakness the player can see and plan around, not a lost body.

No death counter is needed: the cut is always against the current XP and the
snapshot, so "halves what is left" falls out of the formula.

## 7.2 An encounter is combat mode to movement mode

**An encounter ends the moment the party drops out of combat back into
movement mode** — the turn-start check that today grants marching
(`DungeonScene.EvaluateMarchingForTurn`): no live enemy in view. Revives fire on
that transition, **at the start of the player turn** and not the instant the last
enemy in view falls mid-turn (PHASE 7 DECISION 5): one rule, one moment, the same
one marching already uses.

Breaking line of sight therefore counts. A party that runs, gets out of view
and falls back into movement mode has ended the encounter, and its fallen get
up. That is deliberate, not an exploit to close: escaping was the hard part, and
the dungeon you escaped into is still full of the enemies you ran from.

The check **ignores the marching spacing test** (every member within their own
budget of the leader). That test decides whether the line can form, not whether
the fight is over, and a revived member lands beside the leader anyway.

## 7.3 A revived member gets up beside the party

A revived member does **not** get up where they fell. They appear on the nearest
free tile beside the active character (the march leader), preferring a point
*behind* the leader along the trail just walked (`MarchingLine.PointBehind`),
spiralling out to the nearest tile that clears `GridCollision` if that is
blocked.

*As built (step 3):* the march trail is the scene's and restarts the moment
marching does, so after a fight there is no trail to read. A revived member gets
up on the nearest free floor tile to the leader instead: breadth-first from the
leader's tile, crossing floor only, so it is never through a wall. Members
revived together fill tiles in roster order. The march's "tuck in behind"
closes the line up from there.

This matters most for a retreat: the bodies are back in the fight the party ran
from. Reviving in place would put a member there, and marching would drag the
other three back after them to keep the line. Reviving at the leader means the
formation is whole the moment movement mode starts. They rejoin at their normal
party-order rank; the trail resets when movement mode begins, and the existing
"tuck in behind the previous marcher" fallback covers a member who appears
mid-line.

## 7.4 A party wipe is the whole party down in one encounter

Deaths cannot accumulate across encounters, because everyone is back up between
them — so the existing all-dead check (`TurnSystem.OnCharacterDied`) already
*is* the wipe check and needs no encounter bookkeeping.

A wipe **resets every member's XP to the entry snapshot** — the full run's gain,
levels included, fixed at 100% rather than tuned — and ejects the party, as a
wipe already does today (`DungeonState.Wiped` → `CampaignState.EndRun`),
forfeiting loot and `DefeatCount` and taking the mercy tick if deep enough.
That is what makes a wipe distinct from four unlucky deaths: it costs everything
the run earned and the haul with it. Wear is kept (PHASE 6a DECISION 1): gear
never punishes you.

## 7.5 Leaving sets the new baseline

**Leaving ends the encounter, so a fallen member pays on the way out**
(PHASE 7 DECISION 1). Taking the way out with a body carried revives them and
takes the death cut *before* the run ends, so a body cannot be carried home to
dodge it. Taking the stairs down or up is the same rule seen from the other
side: the party arrives, the arrival check runs, and the fallen revive on the
new floor if no live enemy is in view there — otherwise they stay down until
that floor's encounter ends.

Every run-ending path is a way of leaving the dungeon, and whatever XP each
member walks out with — after any death penalties paid along the way — is what
the next entry's snapshot captures. This is already how the code behaves (the
snapshot is taken fresh in `DungeonState.Enter`); Phase 7 only makes it a stated
rule.

## 7.6 What the player sees

- On revive, the HUD floats `REVIVED` over the member with the levels lost
  (e.g. `REVIVED  -1 SWORD  -2 HP`), or `REVIVED` alone when they had no run gain
  left to lose.
- The run summary gains a line per member who died: deaths and the XP they cost.
- The wipe's summary says the run's XP is **reset**, not clawed back by a share,
  and the outcome reads `PARTY WIPE` rather than `DEATH`.

## 7.7 Code impact

The shipped pieces this rests on: `XpSnapshot.Of` / `ClawBack` (`Logic/RunResult.cs`),
`DungeonState.EntrySnapshot`, `TurnSystem.OnCharacterDied` and `GameOver`,
`DungeonState.Wiped` → `CampaignState.EndRun`, `MarchingLine.PointBehind`, and the
save's `ActorDto.Alive`, which already records a member down mid-encounter.

- **"The encounter ended" moves into `Logic/`.** `TurnSystem` learns which live
  enemies are in view *now*, not just seen this turn: `NotifyEnemyVisible` keeps
  an in-view set on both edges. At player-turn start (after resurrections,
  before members' `StartTurn` so a revived member gets a full budget), if no live
  enemy is in view and any member is down, it raises **`CombatEnded`**. The
  same check runs on floor arrival. The scene subscribes to grant marching and
  play the revive, instead of deciding "no enemy in view" itself.
- **The revive** (`TurnSystem`, on `CombatEnded`): for each fallen member — HP to
  1 (never above the new maximum, which is always at least 1), `Alive = true`, mana clamped to `UsableMaxMana`, status effects
  already shed at death, XP cut through a per-member `XpSnapshot.ClawBack`
  overload at `DeathXpLossPercent`, position from `MarchingLine.PointBehind` (the
  scene hands the leader's trail point in, or Logic finds the nearest free tile
  behind the leader with `GridCollision`), then a `CharacterRevived(member,
  clawbacks)` event.
- **The wipe**: `CampaignState.EndRun` calls `ClawBack(_party, 100)` instead of
  `ClawBack(_party, tuning.DeathXpLossPercent)`.
- **The way out**: `CampaignState.EndRun(RunEnding.Exited)` revives every
  fallen member through the same per-death cut before anything else it does,
  so the result's clawbacks and the next baseline both see it.
- **`RunOutcome.Death` becomes `RunOutcome.PartyWipe`** (PHASE 7 DECISION 2),
  in code, on screen (`PARTY WIPE`) and in the save. The save writes the outcome
  by name, so this is **save version 4**: a step in `SaveCodec.Migrations`
  rewrites a stored `"Death"` outcome to `"PartyWipe"` wherever a run result is
  kept, pure and by name, never throwing on a hand edit. Every older save still
  loads (the pinned `TestData/saves` are never edited), and a Phase 6b build
  refuses a version 4 save rather than misread its outcome.
- **The run's death record**: `DungeonState` accumulates each revive's clawbacks
  for the summary; it goes in the save's `Run` group in version 4, written
  `null` while empty, with its checks in `SaveConsistency` in the same change.
- **Save consistency**: every member's XP stays at or above the entry snapshot
  (`SaveConsistency` check 16 already compares the two, and a per-death cut never goes below the snapshot); the death record's totals must
  match the gap. A member saved down is valid, and is revived by the next check
  after the load (PHASE 7 DECISION 3). No new field is needed for it:
  `ActorDto.Alive` already says who is down.

### Implementation steps

Each step lands with its tests and keeps the golden tests green. Nothing here
touches `MapGenerator`, `Mulberry32` or `Pathfinder`, and no RNG stream is added.

1. **7.1 — The encounter's end in Logic.** In-view tracking on `TurnSystem`;
   `CombatEnded` raised at player-turn start and floor arrival; the scene's
   marching grant reads it (spacing test kept for marching only). No revive yet:
   behaviour is unchanged, and existing marching tests still pass.
2. **7.2 — The per-death cut.** Per-member `XpSnapshot.ClawBack`; the revive on
   `CombatEnded` (HP to 1, alive, mana clamp, XP cut); `CharacterRevived`. Tests: revived at exactly 1 HP; half
   the gain per pool, truncation, repeat deaths halving what is left, nothing
   below the snapshot, only the fallen pay, `DeathXpLossPercent` 0 and 100.
3. **7.3 — Where they get up.** Revive position behind the leader with the
   `GridCollision` spiral fallback; marching rank on rejoin. Tests: blocked trail,
   corridor, revive after taking the stairs.
4. **7.4 — The party wipe resets, and the way out charges.** `EndRun` resets to
   the snapshot on a wipe and revives-with-the-cut on the way out;
   `RunOutcome.PartyWipe`; the summary's reset line. Tests: a wipe after earlier
   deaths ends at the snapshot exactly; boss killed then wipe still ticks and
   locks; carrying a body out costs the cut and the next entry snapshots after it.
5. **7.5 — Save version 4.** The outcome rename's migration step; the run's death
   record in the `Run` group and the summary; consistency checks; a mid-encounter
   save with a fallen member round-trips and revives on the next turn start. The
   pinned pre-rework and Phase 6a saves, and a version 3 save with a `"Death"`
   result, still load.
   *As built (step 5):* every death goes through one door, `DungeonState.PayDeath`
   (the encounter's end through `TurnSystem.DeathCost`, and a body carried out
   through `CampaignState.EndRun`), which cuts and records a `RunDeath` — the
   member and its pool cuts — on `DungeonState.Deaths`; the run's result carries
   them all (`RunResult.Deaths`, a wipe's reset staying `Clawbacks`). Saved as
   `deaths` on the visit and the last result, each `{ memberId, cuts: [{ pool,
   before, after }] }`, null while empty. `SaveConsistency` check 36 holds the
   visit's to the door and the pools now, and check 37 the last result's to
   themselves; both chain pool by pool (a member's next death finds at least what
   its last left). The summary tells a line a member who died
   (`B DIED 2 TIMES: SWORD -10, HP -4`), and the wipe's headline is `PARTY WIPE`.
   *As built (step 6):* the revive floats `REVIVED` and the levels the cut took
   (`DungeonHud.RevivedText`: a class's ladder levels and the pools' points,
   each replayed before and after the cut), or `REVIVED` alone when it crossed
   none; settled.md's Phase 7 section is marked shipped, and §4.4, §5.1, §5.3,
   §6.1 and §6.3 say what shipped.
6. **7.6 — HUD and docs.** The `REVIVED` float text; settled.md's PHASE 7
   decisions marked shipped; the pointers in §4.4, §5.1, §5.3, §6.1, §6.3 updated
   from "Phase 7 replaces this" to the shipped wording.

# Phase 8 — Exploring, resting and combat

**Planned 2026-11-09.** A new plan on top of the shipped Phases 0–7. It gives the
dungeon the mode Phase 7 (§7.2) already speaks of and no code holds: three modes
— **exploring**, **resting** and **combat** — with turns kept as the world's clock
in all three. The decisions and their reasons are recorded in
[settled](settled.md) under *Exploring, resting and combat*, with the user's
rulings (PHASE 8 DECISIONS 1–7). It replaces the open question *One
combat/movement mode, and exploring without the pause* in
[open-questions](open-questions.md).

**Why.** Three checks stand in for a mode today, and they can disagree:
`DungeonScene.EvaluateMarchingForTurn` (is the party marching?),
`TurnSystem.AnyLiveEnemySeenThisTurn` (does a swap cost movement? is the reach
overlay up?) and each enemy's `_mayReact` (may it react?). The frozen dummy found
after Phase 7 was one such disagreement: an enemy in plain view counted as unseen.
And exploring pauses every couple of spaces: `AutoEndTurnWhenDry` ends the turn
whenever the slowest member runs dry, and the enemies' whole phase plays out
before the party walks on. A later addition — enemy patrol routes — needs enemies
moving *while* the party moves, so turns cannot simply be removed.

| Mode | Entered | Ends | The clock | The party | The enemies |
| --- | --- | --- | --- | --- | --- |
| **Exploring** | Arriving on a floor with no live enemy in view; combat ending; a rest ending without a sighting | A live enemy comes into view; the player rests | One turn per `MovementBudget` the leader walks; still while nobody walks | Walks freely as a marching line, no budget, no pause | Awake ones move at the same time, at their budget's share of the party's speed |
| **Resting** | Chosen by the player while exploring | Every living member at full HP and mana, or a live enemy comes into view, or the player stops it | Runs on its own, fast-forwarded | Stands still, regains HP and mana, earns no XP | Move as in exploring |
| **Combat** | A live enemy comes into view in either mode above | The start of a party turn with no live enemy in view (PHASE 7 DECISION 5) | One turn per side's phase, as today | Turn-based, as today; opens on a fresh party turn | Turn-based, as today |

## Decisions

- **PHASE 8 DECISION 1 — three modes (2026-11-09).** Exploring, resting and
  combat. Exactly one holds at any moment, and `Logic/` holds it: the swap cost,
  the reach overlay, marching and the turn's pace all read the mode instead of
  each asking its own question.
- **PHASE 8 DECISION 2 — turns stay the world's clock in every mode
  (2026-11-09).** Resurrection timers (`TurnCount`), status ticks (the turn
  boundaries) and `TurnsSinceSeen` keep counting turns. Patrol routes are a later
  addition, and they need enemies that move while the party moves.
- **PHASE 8 DECISION 3 — exploring is simultaneous (2026-11-09).** Everyone moves
  at the same time. An enemy's speed is the party's walking speed times its
  movement budget over the party's (`EnemyState.MoveBudget / Tuning.MovementBudget`):
  a dummy's 100 against the party's 160 walks at 62.5% of the party's pace, the
  golem's 40 at 25%. More movement, faster; the slow ones at a relaxed pace.
- **PHASE 8 DECISION 4 — the clock runs only while the party walks
  (2026-11-09).** One turn passes for every `MovementBudget` (160) of ground the
  leader walks — a second of walking at `GameConstants.Speed` (160 a second).
  Standing still stops the world, so nothing happens while the player thinks.
  Resting is how time passes on purpose. Deterministic, as every clock in the game
  is (the logic step, 1c).
- **PHASE 8 DECISION 5 — resting (2026-11-09).** Chosen while exploring. The
  party regains HP and mana while the enemies move. It ends when every living
  member is at full HP and full mana (`UsableMaxMana`), or a live enemy comes into
  view — whichever is first — and the party is exploring or fighting again.
- **PHASE 8 DECISION 6 — resting teaches nothing (2026-11-09).** HP and mana
  regained by resting earn no XP, so taking damage and resting is never an HP-XP
  farm. Healing by a cast or a potion still earns HP XP (§2.2).
- **PHASE 8 DECISION 7 — the party opens every fight (2026-11-09).** Combat begins
  on a fresh party turn with every member's full movement, wherever the party
  stands when the enemy comes into view. Spotting the enemy is the party's
  advantage.
- *Ruled before (open-questions, 2026-11-08):* **leaving combat is checked at the
  start of the party's turn only** — any live enemy in view keeps the party in
  combat; none drops it to exploring. Killing the last enemy in view mid-turn
  changes nothing until then. It is the moment Phase 7 ends an encounter and
  revives the fallen (PHASE 7 DECISION 5).

## 8.1 The mode, in Logic

`TurnSystem` gains `Mode` (`Exploring`, `Resting`, `Combat`) and a `ModeChanged`
event, and becomes the one place the mode changes:

- **Into combat:** a live enemy enters the in-view set (`NotifyEnemyInView`, Phase
  7.1) while exploring or resting. Combat starts at once on a fresh party turn
  (DECISION 7).
- **Out of combat:** `CheckEncounterEnd` at the party turn's start (and on a floor
  arrival) finds no live enemy in view — as Phase 7 built it — revives the fallen
  and switches to exploring.
- **What reads it:** the swap cost (free outside combat), the reach overlay and
  the attack-cost ruler (combat only), and marching (exploring only), replacing
  `EvaluateMarchingForTurn`, `_marchingThisTurn` and the overlay's
  `ReachOverlayOn` guess. Reactions stay per enemy — "no ambushes from the fog" —
  with an enemy in view counting as seen, as the fix after Phase 7 made it for
  `TurnsSinceSeen`: `_mayReact` reads the in-view set too.

This step changes no pace: exploring still plays in turns, so it can land and be
played before the clock changes under it.

## 8.2 The exploring clock

While exploring, the party has no movement budget and no turn to end: it walks as
far as the player likes, and `AutoEndTurnWhenDry` goes. `TurnSystem` counts the
ground the leader walks, and each `MovementBudget` of it is one turn of the world:

- the turn boundaries the party and the enemies already raise — status ticks
  (`TurnEnd`, `RoundEnd`), held shots lapsing, reaction uses restored;
- `TurnsSinceSeen`, counted as at an enemy turn's start;
- `TurnCount` moves on, and every dummy whose wait has run out gets up
  (resurrection timers count walking turns, as they counted ended turns).

What a walking turn does **not** do: no attack beats (no enemy is in view, or it
would be combat), and no mana from unspent movement — nothing is left unspent.
Resting is where mana comes back outside combat.

## 8.3 Enemies moving at the same time

While the party walks, every awake enemy — one not yet passive, `TurnsSinceSeen`
below 2: a pursuer from a fight the party broke sight of — moves at its share of
the party's speed (DECISION 3) along the path `EnemyAi` plans for it, re-planned
each world turn. An enemy that walks into the party's view starts combat (8.1),
the scene's sight check running on enemy tile crossings as it does in the enemy
phase today. Patrol routes plug in here later — a route is another plan for an
enemy that is not passive — and are out of this phase.

## 8.4 Resting

A key while exploring starts a rest, and the party stands where it is:

- **The clock runs on its own,** fast-forwarded so a rest plays out in a few
  seconds, and every rest turn is a world turn as in 8.2: statuses tick, timers
  run, enemies move as in 8.3.
- **Each rest turn restores** HP and mana to every living member — through a door
  that credits no XP (DECISION 6), never the heal and mana appliers that do. How
  much is open (below).
- **It ends** when every living member is at full HP and `UsableMaxMana`; when a
  live enemy comes into view (combat, the party first); or when the player stops
  it. A dummy whose timer runs out during a rest may get up in view and end it.
- A member Phase 7 revived at 1 HP is who a rest is for: the fight over, the
  party rests back to full before walking on.

## 8.5 Save version 5

The mode is saved: combat outlasts the sight that started it (until a party
turn's start), so it cannot be derived from the in-view set. So is the walking
clock's progress into the current turn. `ViewDto.MarchingThisTurn` is replaced:
version 5's migration step writes the mode from it — a version-4 save, always
taken in the party's phase, was exploring when it marched and in combat when it
did not. Every older save still loads (the pinned saves are never edited), and a
Phase 7 build refuses a version-5 save. A rest is a safe point or it is not (open
below).

## 8.6 What the player sees

A mode readout (EXPLORING, RESTING, COMBAT); `SPOTTED!` over the enemy that
starts a fight; a rest's progress — every member's HP and mana filling — and how
to stop it; the reach overlay and the ruler only in combat. The older documents
(§1.2's turns, §6.1, the marching rules) gain a note saying what Phase 8 changes.

## 8.7 Code impact

- `TurnSystem`: `Mode`, `ModeChanged`, the transitions (8.1); the walking clock —
  an `AdvanceExploring(walked)` the scene feeds the leader's distance, raising
  each world turn's boundaries (8.2); enemy movement on that clock (8.3); the rest
  (8.4). `CheckEncounterEnd` becomes the combat-to-exploring transition.
- `DungeonScene`: movement without a ledger while exploring; marching followers as
  today; `EvaluateMarchingForTurn`, `_marchingThisTurn` and `AutoEndTurnWhenDry`
  removed; the overlay and swap cost read the mode.
- `EnemyAi`: a walking plan sized to world time rather than a budget.
- Save: version 5 (8.5), with its consistency checks.
- Tuning: whatever the rest's rates are (below), each a `tuning.json` key with its
  reader, a range and a `ContentBounds` row where it is a content number.

### Implementation steps

Each step lands with its tests and keeps the golden tests green.

1. **8.1 — The mode in Logic.** `Mode` and its transitions; the swap cost,
   overlay, marching and reactions read it. Exploring still plays in turns.
2. **8.2 — The walking clock.** No budget and no pause while exploring; a world
   turn per `MovementBudget` walked, with its boundaries. Tests: a turn passes at
   exactly 160 walked; standing still passes none; a dummy's timer runs on walking
   turns; poison ticks on the walk.
3. **8.3 — Enemies move with the party.** Awake enemies at their budget's share of
   the party's speed; walking into view starts combat with the party first.
4. **8.4 — Resting.** The key, the rest turns, the restore without XP, the three
   ways it ends.
5. **8.5 — Save version 5.** The mode and the clock saved; the migration from
   `MarchingThisTurn`; older saves load.
6. **8.6 — HUD and docs.**

## Open questions

1. **How much a rest turn restores.** Mana: an idle combat turn already banks
   unspent movement as mana (`MovementUnitsPerMana`: a whole budget is 16) — reuse
   it, or a share of the pool? HP: a new `tuning.json` key — a flat amount, or a
   percentage of max HP per turn like a potion's (§3.4)?
2. **How fast a rest plays.** Turns a second while fast-forwarding, and whether
   enemies are drawn moving or jump to where the rest leaves them.
3. **Moving one member while exploring.** Today only the active member walks and
   the others follow in line. Does exploring keep the march's spacing test, or do
   followers simply walk into formation behind the leader, however split they
   were when the fight ended?
4. **Which enemies move while exploring.** Awake ones only, until patrols exist?
   And does a pursuer walk to where it last saw the party, or towards the party
   wherever it is?
5. **The fight's first turn.** Does combat starting mid-walk begin a new world turn
   (`TurnCount` moves on), or does it continue the walking turn in progress?
6. **Mana from unspent movement.** Combat only from now on, with resting as the
   other way back?
7. **Stopping a rest.** A key to stop it early, and can the party rest with a
   status still ticking (poison would keep it from ever being full)?
8. **Resting next to a farm.** Rest turns run the dummies' resurrection timers — a
   rest is a fast way to wait for a farm to come back. Accepted, or does resting
   pause the timers?
9. **Saving during a rest.** A safe point that resumes the rest, or does a save
   end it first?
10. **Taking the stairs while exploring.** Still only with the party gathered on
    them, or does the marching line take them together?
11. **The action bar and talent tree** (open-questions) are queued after Phase 7
    too. A rest button belongs on the bar if the bar comes first.

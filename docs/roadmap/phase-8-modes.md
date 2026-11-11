# Phase 8 — Exploring, resting and combat

**Planned 2026-11-09.** A new plan on top of the shipped Phases 0–7. It gives the
dungeon the mode Phase 7 (§7.2) already speaks of and no code holds: three modes
— **exploring**, **resting** and **combat** — with turns kept as the world's clock
in all three. The decisions and their reasons are recorded in
[settled](settled.md) under *Exploring, resting and combat*, with the user's
rulings (PHASE 8 DECISIONS 1–7; 8–19, which answered the plan's open questions
on 2026-11-11 and added the stop-resting button; and 20–21, Space's one-turn rest
and free actions without XP outside combat). It replaces the open question *One
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
| **Exploring** | Arriving on a floor with no live enemy in view; combat ending; a rest ending without a sighting | A live enemy comes into view; the player rests | One turn per `MovementBudget` the leader walks; still while nobody walks | Walks freely as a marching line, no budget, no pause; acts without spending movement, earning no XP; Space rests one turn | Awake ones move at the same time, at their budget's share of the party's speed |
| **Resting** | Chosen by the player while exploring (`R`, or the REST button) | Every living member at full HP and mana with no negative status, or a live enemy comes into view, or the player stops it (`R`, or the STOP RESTING button), or the pause menu opens | Runs on its own, fast-forwarded | Stands still, regains HP and mana, earns no XP | Move as in exploring |
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
  farm. Healing by a cast or a potion still earns HP XP (§2.2) — in combat only,
  since DECISION 21.
- **PHASE 8 DECISION 7 — the party opens every fight (2026-11-09).** Combat begins
  on a fresh party turn with every member's full movement, wherever the party
  stands when the enemy comes into view. Spotting the enemy is the party's
  advantage.
- **PHASE 8 DECISION 8 — a button stops a rest (2026-11-11).** Added by the
  user: while resting the HUD shows a clickable **STOP RESTING  R** button, and
  while exploring a **REST  R** button. `R` toggles the rest, and clicking a
  button does exactly what its key does (`ClickZones.Press`, as the inventory's
  rows do). Only the key, the button, Space (which finishes the rest turn in
  progress first, DECISION 20) and the pause menu (DECISION 18) stop a rest: the
  walking keys do nothing while resting. The buttons are the HUD's own for now
  and move onto the action bar when that phase lands (open question 11).
- **PHASE 8 DECISION 9 — a rest waits out negative statuses (2026-11-11).** A
  rest is how the party gets rid of them: it ends on its own only when every
  living member is at full HP, full mana and has no negative status — Poison,
  Bleeding, Searing, Mire, Sundered, Weakened or Softened. (`StatusEffectType`
  has no such split yet; 8.4 adds it.) The statuses tick on every rest turn as on
  any world turn, harm and all: poison still deals its damage while the rest heals
  through it, and each status decays a level a round as in combat, so every rest
  ends. Regeneration, Ward and the Overheal pool don't hold a rest open. This
  narrows DECISION 5's end.
- **PHASE 8 DECISION 10 — a rest turn restores a share of max HP (2026-11-11).**
  `RestHealthPercent` (new `tuning.json` key, provisionally 10): each rest turn
  every living member regains that percentage of its max HP, rounded down and
  never less than 1, as a potion's share is (§3.4). It grows with the pool, so a
  rest from 1 HP takes about the same number of turns at any level.
- **PHASE 8 DECISION 11 — a rest turn banks a whole idle turn's mana
  (2026-11-11).** No new key: each rest turn converts a whole turn's movement
  (`MovementBudget`, Mire's cut included, as an idle combat turn would bank) into
  mana through `RegenManaFromUnusedMovement` — 160 / `MovementUnitsPerMana` 10 =
  16 a turn at today's tuning, capped at `UsableMaxMana`. That conversion credits
  no XP already (DECISION 6).
- **PHASE 8 DECISION 12 — a rest plays at a tuned pace, enemies drawn moving
  (2026-11-11).** `RestTurnsPerSecond` (new `tuning.json` key, provisionally 4)
  sets how fast rest turns play; awake enemies are drawn walking at that sped-up
  pace, so one coming into view is seen arriving. It changes wall time only, never
  what a rest does, so it is `Played = false` for good, like `SaveSlots`.
- **PHASE 8 DECISION 13 — followers walk into formation (2026-11-11).** While
  exploring the other members path into line behind the leader, however split up
  the fight left them; the march's spacing test goes.
- **PHASE 8 DECISION 14 — only awake enemies move, a pursuer to where it last saw
  the party (2026-11-11).** Until patrol routes exist, only an enemy with
  `TurnsSinceSeen` below 2 moves while exploring or resting. A pursuer walks to
  the spot where it last saw the party and stops there: no tracking through the
  fog. Each enemy keeps that spot (new state, saved, 8.5).
- **PHASE 8 DECISION 15 — a fight starts a new world turn (2026-11-11).** Combat
  starting mid-walk moves `TurnCount` on and drops the walking turn in progress:
  the fight opens on a clean boundary, matching DECISION 7's fresh party turn.
- **PHASE 8 DECISION 16 — mana from unspent movement is combat's alone
  (2026-11-11).** Only a combat turn's end converts unspent movement into mana,
  for the party and the enemies alike; a walking turn converts none. Outside
  combat, a rest (DECISION 11) and potions are how mana comes back.
- **PHASE 8 DECISION 17 — resting near a farm is accepted (2026-11-11).** Rest
  turns are world turns and run the dummies' resurrection timers. A dummy that
  gets up in view ends the rest and starts a fight, which is the limit.
- **PHASE 8 DECISION 18 — a save ends a rest (2026-11-11).** Opening the pause
  menu stops the rest, so a save is always taken while exploring or in combat;
  save version 5 holds no rest state.
- **PHASE 8 DECISION 19 — the leader takes the line down the stairs
  (2026-11-11).** While exploring, `E` with the leader on the stairs takes the
  whole marching line; the members need not be gathered on them. In combat the
  rule stays as today.
- **PHASE 8 DECISION 20 — Space rests one turn outside combat (2026-11-11).**
  Added by the user: while exploring, Space — End Turn in combat, as today — plays
  exactly one rest turn (the restore, the status ticks, the enemies moving, as in
  8.4) and stops, so the party can act between turns. When a status deals more
  than a rest restores, the player steps turn by turn and heals or drinks in
  between to save a member. With every member full and clear it still passes one
  world turn, restoring nothing: a way to wait a turn. Pressed during a full rest,
  it finishes the rest turn in progress and stops there.
- **PHASE 8 DECISION 21 — actions outside combat are free and teach nothing
  (2026-11-11).** While exploring a cast or a potion spends no movement (there is
  no budget, 8.2); a cast still pays its mana and a potion is still used up. So
  that free casts and the rest's mana never make an XP farm (DECISION 6), nothing
  done outside combat credits XP of any kind — mana, HP or a weapon's — and no
  cast banks wear. This narrows DECISION 6's cast and potion healing to combat.
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
would be combat), and no mana from unspent movement, for anyone (DECISION 16) —
nothing is left unspent. Resting is where mana comes back outside combat.

The other members walk into formation behind the leader (DECISION 13), and `E`
with the leader on the stairs takes the whole line (DECISION 19).

When a fight starts mid-walk, the walking turn in progress is dropped and
`TurnCount` moves on: combat opens on a new turn (DECISION 15).

The party may act while exploring — heal, drink, swap — without spending
movement, and nothing it does credits XP or banks wear (DECISION 21). Space plays
one rest turn (8.4, DECISION 20); time otherwise passes only by walking.

## 8.3 Enemies moving at the same time

While the party walks, every awake enemy — one not yet passive, `TurnsSinceSeen`
below 2: a pursuer from a fight the party broke sight of — moves at its share of
the party's speed (DECISION 3) along the path `EnemyAi` plans for it, re-planned
each world turn. A pursuer heads for the spot where it last saw the party and
stops there (DECISION 14); passive enemies stay put until patrols exist. An enemy that walks into the party's view starts combat (8.1),
the scene's sight check running on enemy tile crossings as it does in the enemy
phase today. Patrol routes plug in here later — a route is another plan for an
enemy that is not passive — and are out of this phase.

## 8.4 Resting

`R` (or the REST button) while exploring starts a rest, and the party stands
where it is. A party already full and clear of negative statuses ends the rest
before its first turn.

- **The clock runs on its own,** `RestTurnsPerSecond` turns a second (DECISION
  12), and every rest turn is a world turn as in 8.2: statuses tick, harm and all —
  a rest runs poisoned and waits the poison out (DECISION 9) — timers run (a farm included, DECISION 17), and awake
  enemies move as in 8.3, drawn walking.
- **Each rest turn restores** to every living member `RestHealthPercent` of its
  max HP, rounded down and at least 1 (DECISION 10), and a whole turn's movement
  as mana through `RegenManaFromUnusedMovement` (DECISION 11) — through a door
  that credits no XP (DECISION 6), never the heal applier that does.
- **It ends** when every living member is at full HP and `UsableMaxMana` and
  carries no negative status (DECISION 9); when a
  live enemy comes into view (combat, the party first, on a new turn); when the
  player stops it — `R` again, or the **STOP RESTING** button (DECISION 8); when
  the pause menu opens (DECISION 18); or on Space, which finishes the rest turn in
  progress and then stops (DECISION 20). Stopping by `R` or the button drops the
  rest turn in progress: what a rest restores comes in whole turns. A dummy whose timer runs
  out during a rest may get up in view and end it. The walking keys do nothing
  while resting.
- **Space rests one turn** while exploring (DECISION 20): one rest turn, then the
  party is exploring again and can act — a heal cast, a potion — before the next.
  It is how a member is saved from a status that deals more than a rest restores.
  With the party full and clear it passes one world turn anyway.
- A member Phase 7 revived at 1 HP is who a rest is for: the fight over, the
  party rests back to full before walking on.

## 8.5 Save version 5

The mode is saved: combat outlasts the sight that started it (until a party
turn's start), so it cannot be derived from the in-view set. So is the walking
clock's progress into the current turn. `ViewDto.MarchingThisTurn` is replaced:
version 5's migration step writes the mode from it — a version-4 save, always
taken in the party's phase, was exploring when it marched and in combat when it
did not. Every older save still loads (the pinned saves are never edited), and a
Phase 7 build refuses a version-5 save. Each enemy's last-seen spot (DECISION 14)
is saved too, written empty for every enemy of an older save. A save never holds a
rest: the pause menu ends it first (DECISION 18).

## 8.6 What the player sees

A mode readout (EXPLORING, RESTING, COMBAT); `SPOTTED!` over the enemy that
starts a fight; a clickable **REST  R** button while exploring and **STOP RESTING
R** while resting (DECISION 8), each doing what its key does; a **SPACE: REST 1
TURN** hint while exploring (DECISION 20); a rest's progress —
every member's HP and mana filling; the reach overlay and the ruler only in
combat. The older documents
(§1.2's turns, §6.1, the marching rules) gain a note saying what Phase 8 changes.

## 8.7 Code impact

- `TurnSystem`: `Mode`, `ModeChanged`, the transitions (8.1); the walking clock —
  an `AdvanceExploring(walked)` the scene feeds the leader's distance, raising
  each world turn's boundaries (8.2); enemy movement on that clock (8.3); the rest
  (8.4) and its stop. `CheckEncounterEnd` becomes the combat-to-exploring
  transition. Unspent movement banks mana only at a combat turn's end.
- `DungeonScene`: movement without a ledger while exploring; marching followers as
  today; `EvaluateMarchingForTurn`, `_marchingThisTurn` and `AutoEndTurnWhenDry`
  removed; the overlay and swap cost read the mode; followers path into
  formation; `R` toggles a rest; Space rests one turn outside combat; the pause
  menu ends a rest; the leader takes the line down the stairs.
- `DungeonHud`: the mode readout, and the REST / STOP RESTING buttons through
  `ClickZones`.
- `EnemyState`: the spot where it last saw the party.
- `StatusRules`: which statuses are negative (DECISION 9), the rest's end check
  reading it.
- XP and wear: `CreditXp` and the wear a cast banks read the mode, crediting
  nothing outside combat (DECISION 21); casts and drinks spend no movement while
  exploring.
- `EnemyAi`: a walking plan sized to world time rather than a budget, a pursuer's
  to its last-seen spot.
- Save: version 5 (8.5) — the mode, the walking clock, each enemy's last-seen
  spot — with its consistency checks.
- Tuning: `RestHealthPercent` (played, hashed from version 5) and
  `RestTurnsPerSecond` (`Played = false` for good), each with its reader and
  `TuningSchema` clamp. The rest's mana needs no key (DECISION 11).

### Implementation steps

Each step lands with its tests and keeps the golden tests green.

1. **8.1 — The mode in Logic.** `Mode` and its transitions; the swap cost,
   overlay, marching and reactions read it. Exploring still plays in turns.
2. **8.2 — The walking clock.** No budget and no pause while exploring; a world
   turn per `MovementBudget` walked, with its boundaries. Tests: a turn passes at
   exactly 160 walked; standing still passes none; a dummy's timer runs on walking
   turns; poison ticks on the walk.
3. **8.3 — Enemies move with the party.** Awake enemies at their budget's share of
   the party's speed, a pursuer to its last-seen spot; walking into view starts
   combat with the party first, on a new turn.
4. **8.4 — Resting.** `R` and the REST / STOP RESTING buttons, the rest turns, the
   restore without XP, the ways it ends. Tests: a rest stops on `R` and on the
   button alike; it stops on a sighting; it ends when all are full and clear;
   poisoned, it takes the poison's damage and runs on past full HP until the
   poison has worn off; no XP is credited. Space plays exactly one rest turn, and
   one world turn at full; during a full rest it stops after the turn in
   progress; a heal cast or a potion while exploring spends no movement and
   credits no XP or wear.
5. **8.5 — Save version 5.** The mode and the clock saved; the migration from
   `MarchingThisTurn`; older saves load.
6. **8.6 — HUD and docs.**

## Open questions

None. The eleven the plan was written with were ruled on 2026-11-11 (PHASE 8
DECISIONS 9–19, with DECISION 8 added by the user):

| # | Question | Ruling |
| --- | --- | --- |
| 1 | How much a rest turn restores | HP: `RestHealthPercent` of max HP (DECISION 10). Mana: a whole idle turn's banking, no new key (DECISION 11) |
| 2 | How fast a rest plays | `RestTurnsPerSecond`, enemies drawn moving (DECISION 12) |
| 3 | Moving one member while exploring | Followers walk into formation (DECISION 13) |
| 4 | Which enemies move while exploring | Awake ones; a pursuer to its last-seen spot (DECISION 14) |
| 5 | The fight's first turn | A new world turn (DECISION 15) |
| 6 | Mana from unspent movement | Combat only (DECISION 16) |
| 7 | Stopping a rest; resting with a status | `R` and a STOP RESTING button (DECISION 8); a rest waits out negative statuses, harm and all, and ends only once they are gone (DECISION 9) |
| 8 | Resting next to a farm | Accepted (DECISION 17) |
| 9 | Saving during a rest | The pause menu ends the rest (DECISION 18) |
| 10 | Taking the stairs while exploring | The leader takes the line (DECISION 19) |
| 11 | The action bar | The rest buttons are the HUD's own until the bar lands (DECISION 8) |

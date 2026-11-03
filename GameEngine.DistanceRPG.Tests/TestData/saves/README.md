# Saves from before the content rework

These files exist to prove one thing: a campaign saved before the Phase 5
content rework (the user's ruling of 2026-09-25 — reference data in code, the
JSON files the master copy) still loads after it, and at every step of it.

**Never regenerate them, and never edit them by hand.** A fixture rewritten by a
later build proves nothing about the build that came before it.

| File | What it is |
| --- | --- |
| `pre-rework-mid-run.json` | A campaign saved in the middle of a run: `VisitSaveTests.Flagship()` — the Spear Hall on seed 4, five floors deep, saved on floor 4 (index 3) at turn 10 in the player phase. A dagger dummy farmed on floor 2, the boss's own grafted drop in B's bag, a spoil lying loose on floor 4, a corpse lying on its drop, Poison on A, Mire on a halberd, two attacks spent this turn, and fog explored on every floor. |
| `pre-rework-between-runs.json` | A campaign saved between runs: the tutorial cleared and retired; the Spear Hall cleared and locked, its result the last one, its boss's grafted drop (a Riposte Blade forged with Brace) in B's bag; weapon XP in two classes; a seen catalogue; and in B's bag the Nameless Knife (three souls, `vampiric` at tier 3), a Wand of the Cone made with the Cold element, and Rotwood (`poison` at tier 3). |
| `pre-rework-tuning.json` | `GameEngine.DistanceRPG/Data/tuning.json` at commit `febe036`, byte for byte: the tuning both saves were played under. Its hash is both saves' `tuningHash`. |

## Where they came from

A throwaway test, run once against the code at commit `febe036` (Phases 0–5,
before the rework changed anything) and never committed, wrote both saves with
`SaveCodec.Write(SaveGame.Capture(campaign))`, under the content a player's game
played: the shipped data files, loaded as the game loads them
(`ContentFiles.Load(DataFiles.Read(...))`) and made current for the capture.

At `febe036` that content is the compiled content exactly. The shipped files
were pinned byte for byte to the generator's output (`ShippedDataTests`); they
loaded with nothing to say — no notice, no number fitted or overridden — and
hashed as the compiled tuning; and the same run checked that the content they
load dumps to the same text as the compiled content (`ContentDump`, retired with
`ContentEquivalenceTests` once the rework was done, at its R5).

## What reads them

`PreReworkSaveTests`: each save is read at version 1 and lifted through both
steps this build ships — to 2 (Phase 6a.1: the step adds exactly an unworn `wear`
and `wearCashed` to every weapon row and `"tuningHashVersion": 1` at the root,
and keeps every other member at its value and in its order), then to 3 (Phase
6b.1: a step that writes nothing) — restores whole under the current content,
and — its floor opened as a resume opens it — captured again it writes the same
document as the migration writes it, but for the `tuningHash` and
`tuningHashVersion` (those are the current tuning's, on the current version's
keys), the `savedAt` a capture writes, and the members appended since, which the
saves lack and the capture writes at their defaults: the stable (Phase 6a.2), the
enchanter's queue, `depositsMade` and the last result's `services` (Phase 6a.3a),
each member's `potions` (Phase 6b.1), and the last result's `farmYield`,
`farmClears`, `firstClear` and `spoiled` (Phase 6b.3). The saves' hash is the
hash of `pre-rework-tuning.json` on version 1's keys, read by the current reader
over the master copy, as a copy beside the game is read: the file is frozen, so
it lacks the keys added since, and the test asserts those are exactly the keys
no version-1 hash covers — what a hit wears and what a deposit costs, the user's
ruling of 2026-09-25, either unplayed or played only from version 2; the
Consumables group's four, what a drink costs, what a potion restores and what the
farm yields, played only from version 3; and the launcher's `SaveSlots` — so
every number the hash covers is the file's own. The
game says the numbers have changed exactly when the numbers it ships differ from
that file's. And the launch resumes each one — the mid-run one in the dungeon, on
its floor and turn, where its visit plays on.

`pre-rework-tuning.json` has one more reader: the suite's `Premise`, the numbers
the worked examples were written against. A suite whose scenarios stand on the
tuning — a push that moves a tile, three Block a stack, a dummy back up in nine
turns — reads this file over the master copy and makes it current, so a
designer's edit to `tuning.json` moves the game and fails none of them.

# A save from Phase 6a

The same proof one version on: a campaign the Phase 6a build saved (version 2)
still loads after Phase 6b lifts the save to version 3, and at every 6b step.

| File | What it is |
| --- | --- |
| `phase-6a-between-runs.json` | A campaign saved between runs, version 2, its `tuningHash` of version 2's keys: `pre-rework-between-runs.json` played on — the Spear Hall and the tutorial cleared, the tutorial retired — with the Riposte Blade moved from B's bag to the stable, B's Wand of the Cone deposited at the enchanter to be refined (an unworn Refine, 0 of 2 runs), and so one deposit made. |
| `phase-6a-tuning.json` | `GameEngine.DistanceRPG/Data/tuning.json` at commit `69d7bee`, byte for byte: the tuning the save was played under. Its hash on version 2's keys is the save's `tuningHash`. |

## Where they came from

The Phase 6a build itself wrote the save, by play, never a test and never a
hand: the untouched tree at `69d7bee` built in place (a worktree does not build:
the engine is a sibling checkout), its `tuning.json` beside the exe checked to be
the master's byte for byte, and launched on a scratch save folder
(`DISTANCERPG_SAVE_DIR`) holding `pre-rework-between-runs.json` as slot 1. LOAD
GAME 1, TAB to WEAPONS, B's Riposte Blade taken up and put down at the stable's
end; then D on B's Wand of the Cone, 2 (REFINE — every weapon a version-1 save
lifts is unworn, so ENCHANT is refused for want of wear, and a refine is not),
ENTER; ESC, QUIT TO TITLE. The slot's `campaign.json` is this file, as written —
each change on the hub saves the campaign at once (PHASE 6a DECISION 10), and the
quit found the slot already holding it, so its `savedAt` is the deposit's. The
tuning is the master at that commit, `git show 69d7bee:GameEngine.DistanceRPG/Data/tuning.json`.

## What reads them

`Phase6aSaveTests`: the save is read at version 2 and lifted to 3 by a step that
writes nothing, restores whole under `phase-6a-tuning.json` with nothing to say,
and captured again is the document it was — every member at its value and in its
order — but for the tuning hash and its version, the time, each member's
potions, written `null` (Phase 6b.1), and the last result's `farmYield`,
`farmClears`, `firstClear` and `spoiled`, written at their defaults — 0, 0,
`false` and `null` — since a run of version 2 made no batch (Phase 6b.3). Its
hash is the frozen tuning's on version 2's keys, read by the current reader over
the master copy, as a copy beside the game is read; and the game says the
numbers have changed exactly when a number version 2 hashes differs from that
file's — so a later retune of `tuning.json` fails nothing.

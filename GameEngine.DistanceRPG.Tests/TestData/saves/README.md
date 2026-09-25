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
load dumps to the same text as the compiled content (`ContentDump`).

## What reads them

`PreReworkSaveTests`: each save reads (version 1) and restores whole under the
current content, and — its floor opened as a resume opens it — captured again it
writes the same document, its `tuningHash` aside (that is the current tuning's).
The saves' hash is the hash of `pre-rework-tuning.json`, read by the current
reader. The game says the numbers have changed exactly when the numbers it ships
differ from that file's. And the launch resumes each one — the mid-run one in
the dungeon, on its floor and turn, where its visit plays on.

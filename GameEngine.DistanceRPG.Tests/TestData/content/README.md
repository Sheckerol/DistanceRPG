# The content from before the content rework

`pre-rework-content.txt` is the game's content as it stood at commit `febe036`
(Phases 0–5, before the Phase 5 content rework), written as one canonical text
by `ContentDump.Of`: one `<section> <id>.<field>=<value>` line per field.

- every weapon row, in the catalogue's order — each field, the forged spread in
  `ModifierType` order, the enchantments it arrives with and their tiers, the
  shape;
- every enchantment row as the loaded catalogue plays it — after the tuning's
  dials are laid over it, so `applyPercent` and `potency` are the numbers that
  play;
- every relation of the shipped `restricted.json`, as read;
- every dungeon row and its boss, and every starting party member;
- the tuning the game played, as `TuningValues.Of` lists it — every number the
  tuning's hash covers.

**Never regenerate it, and never edit it by hand.** It exists so that nothing
the rework retypes, moves or restructures — the enchantment rows moved into
code, the compiled copies deleted, `weapons.json` rewritten into class rows —
can drift unseen: `ContentEquivalenceTests.TheShippedContentIsThePreReworkContent`
dumps the content the game loads from the shipped files the same way and
compares the two texts, at every step of the rework.

## Where it came from

The same throwaway run that wrote the saves in `../saves` (never committed) wrote
it against the code at `febe036`, from the shipped data files loaded as the game
loads them. At `febe036` that content is the compiled content exactly: the
shipped files were pinned byte for byte to the generator's output, and the run
checked that the content they load dumps to the same text as the compiled
content does.

## When it goes

It is retired with `ContentEquivalenceTests` and `ContentDump` once the rework
has moved everything it moves (Phase 5.R5). From then on the JSON files are the
master copy, and a designer's edit to them must not fail a test.

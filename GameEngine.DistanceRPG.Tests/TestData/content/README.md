# The suite's frozen configuration

The four files here are copies of the game's data files —
`GameEngine.DistanceRPG/Data/tuning.json`, `restricted.json`, `weapons.json` and
`dungeons.json` — as they shipped on 2026-11-20. The test suite runs under these
and never under the game's own, so the game's files (and its starting party, in
`Logic/Content/StartingParty.cs`) can be changed for gameplay reasons without a
test failing. `FixtureContent.cs` is the suite's one door to them, and holds the
frozen starting party.

They are a fixture, not a record:

- **Do not sync them to the game's files.** A gameplay change belongs in
  `GameEngine.DistanceRPG/Data` only.
- **Do edit them when the code needs it.** A `tuning.json` key the code gains
  must be added to `tuning.json` here too, or the suite stops and says so. A test
  that needs a row or a number these lack may add it here.
- Changing a value here will fail the tests whose worked examples stand on it.

`ShippedDataTests` and `ShippedFilesTests` are the only suites that read the
game's real files, and only for whether they load and a campaign can start on
them — never for what they say.

The saves in `../saves` are a different thing: records, never edited.

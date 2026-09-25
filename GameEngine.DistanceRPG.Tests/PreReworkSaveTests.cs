using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// A campaign saved before the Phase 5 content rework still loads, at every
/// step of it (1i). The two saves in <c>TestData/saves</c> were written once by
/// the code at <c>febe036</c>, under the content a player's game played, and are
/// never regenerated: one in the middle of a run (the
/// <see cref="VisitSaveTests.Flagship"/> visit), one between runs (a campaign
/// with the tutorial retired, the Spear Hall locked, and in B's bag the unique,
/// wand and caster unique the rework's weapon rows touch). Each reads at version 1,
/// restores whole under the current content — every id and name it holds
/// resolves — and, opened as a resume opens it, writes back the same document;
/// its tuning hash is the hash of the tuning it was played under, read by the
/// current reader; the game says the numbers changed exactly when the numbers
/// it ships are not those; and a launch resumes each, the mid-run one in the
/// dungeon, where its visit plays on. In the content collection, since a restore
/// runs under the current content and the tuning cases swap it.
/// </summary>
[Collection(TestContent.Collection)]
public class PreReworkSaveTests
{
    /// <summary>The run in progress: the Spear Hall on seed 4, saved on floor 4 (index 3) at turn 10, in the player phase.</summary>
    private const string MidRun = "pre-rework-mid-run.json";

    /// <summary>The campaign between runs: the tutorial retired, the Spear Hall cleared and locked, its result the last.</summary>
    private const string BetweenRuns = "pre-rework-between-runs.json";

    /// <summary><c>Data/tuning.json</c> at <c>febe036</c>, byte for byte: the tuning both saves were played under.</summary>
    private const string PlayedTuning = "pre-rework-tuning.json";

    /// <summary>The document's member holding the hash of the tuning it was saved under: the one member a capture under other numbers writes differently.</summary>
    private const string TuningHashMember = "tuningHash";

    private static readonly string[] Saves = [MidRun, BetweenRuns];

    private static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "saves", name);

    private static string TextOf(string name) => File.ReadAllText(PathOf(name));

    /// <summary>
    /// The tuning the saves were played under, as the current reader reads
    /// <see cref="PlayedTuning"/>: strictly, as the game reads its built-in copy —
    /// the file names every key, as the master copy does, so nothing under it is
    /// taken from anywhere else.
    /// </summary>
    private static TuningRead ReadPlayedTuning() => TuningJson.ReadBuiltIn(TextOf(PlayedTuning));

    /// <summary>Whether any number <paramref name="now"/> holds is not the number the saves were played under.</summary>
    private static bool NumbersMoved(Tuning now) => TuningValues.Moved(ReadPlayedTuning().Tuning, now).Length > 0;

    /// <summary>
    /// The first place the two documents differ, as a path from the root —
    /// <c>$.campaign.party[1].inventory[3].enchantments[0].tier</c> — with what
    /// each holds there; null when they are the same document, member order
    /// included. The root's <see cref="TuningHashMember"/> is left out: a capture
    /// writes the current tuning's hash there.
    /// </summary>
    private static string? FirstDifference(JsonNode? expected, JsonNode? actual, string path, bool root = false)
    {
        switch (expected, actual)
        {
            case (null, null):
                return null;
            case (JsonObject was, JsonObject now):
            {
                var wasNames = was.Select(member => member.Key).Where(name => !(root && name == TuningHashMember)).ToList();
                var nowNames = now.Select(member => member.Key).Where(name => !(root && name == TuningHashMember)).ToList();
                foreach (string name in wasNames)
                {
                    if (!now.ContainsKey(name))
                        return $"{path}.{name}: in the saved document, not in the capture";
                    if (FirstDifference(was[name], now[name], $"{path}.{name}") is { } found)
                        return found;
                }
                foreach (string name in nowNames)
                    if (!was.ContainsKey(name))
                        return $"{path}.{name}: in the capture, not in the saved document";
                return wasNames.SequenceEqual(nowNames, StringComparer.Ordinal)
                    ? null
                    : $"{path}: members in the order [{string.Join(", ", wasNames)}] saved, [{string.Join(", ", nowNames)}] captured";
            }
            case (JsonArray was, JsonArray now):
            {
                for (int i = 0; i < Math.Min(was.Count, now.Count); i++)
                    if (FirstDifference(was[i], now[i], $"{path}[{i}]") is { } found)
                        return found;
                return was.Count == now.Count ? null : $"{path}: {was.Count} items saved, {now.Count} captured";
            }
            case (JsonValue was, JsonValue now):
                return was.ToJsonString() == now.ToJsonString() ? null : $"{path}: {was.ToJsonString()} saved, {now.ToJsonString()} captured";
            default:
                return $"{path}: {expected?.ToJsonString() ?? "null"} saved, {actual?.ToJsonString() ?? "null"} captured";
        }
    }

    [Fact]
    public void ASaveFromBeforeTheReworkRestoresWhole()
    {
        foreach (string name in Saves)
        {
            // The save reads at version 1 — no bump, no migration — and restores under
            // the content loaded now: every weapon, enchantment, dungeon, status and
            // member it names resolves, or the restore refuses it, naming where.
            string text = TextOf(name);
            var file = SaveCodec.Read(text);
            Assert.Equal(1, file.Version);
            var loaded = SaveGame.Restore(file, GameContent.Current);
            var campaign = loaded.Campaign;
            Assert.Equal(name == MidRun, campaign.Visit is not null);

            // A run in progress is opened as the game's resume opens it — on the
            // visit's own die — and captured again it is the same document, every
            // weapon row field by field: nothing lost, nothing re-derived differently.
            // The tuning hash aside, which is the current tuning's.
            campaign.Visit?.OpenTurns(campaign.Party);
            string again = SaveCodec.Write(SaveGame.Capture(campaign, loaded.View));
            string hash = TuningJson.Hash(GameContent.Current.Tuning);
            Assert.Equal(hash, SaveCodec.Read(again).TuningHash);
            if (FirstDifference(JsonNode.Parse(text), JsonNode.Parse(again), $"{name} $", root: true) is { } difference)
                Assert.Fail($"captured again, the document is not the save: {difference}");

            // And as text, whitespace and number formatting included.
            string savedHash = $"\"{TuningHashMember}\": \"{file.TuningHash}\"";
            Assert.True(text.Replace(savedHash, $"\"{TuningHashMember}\": \"{hash}\"", StringComparison.Ordinal) == again,
                $"{name}: captured again, the document's text is not the save's, though every value and member order is");
        }
    }

    [Fact]
    public void ItsHashIsItsTuningsHash()
    {
        // The hash each save carries is the hash of the tuning it was played under,
        // as the current reader reads that file: the hash covers the same numbers
        // it did before the rework, so a save written then is recognised as played
        // under exactly those numbers.
        var read = ReadPlayedTuning();
        Assert.Empty(read.Warnings);
        string hash = TuningJson.Hash(read.Tuning);
        foreach (string name in Saves)
            Assert.Equal(hash, SaveCodec.Read(TextOf(name)).TuningHash);
    }

    [Fact]
    public void TheNumbersHaveChangedExactlyWhenTheyHave()
    {
        // A save is loaded under changed numbers exactly when the game's numbers are
        // not the ones it was played under — under the shipped tuning, as the game
        // resumes it, that is whether any shipped number differs from the save's
        // tuning; under the save's own tuning, never; and under it with one number
        // moved, always.
        var played = ReadPlayedTuning().Tuning;
        bool moved = NumbersMoved(ShippedContent.Tuning);
        foreach (string name in Saves)
        {
            var file = SaveCodec.Read(TextOf(name));
            using (TestContent.Use())
                Assert.Equal(moved, SaveGame.Restore(file, GameContent.Current).TuningChanged);
            using (TestContent.Use(tuning: played))
                Assert.False(SaveGame.Restore(file, GameContent.Current).TuningChanged);
            using (TestContent.Use(tuning: played with { MovementBudget = played.MovementBudget + 1 }))
                Assert.True(SaveGame.Restore(file, GameContent.Current).TuningChanged);
        }
    }

    [Fact]
    public void ItResumesThroughTheBoot_AndPlaysOn()
    {
        foreach (string name in Saves)
        {
            var folder = Directory.CreateTempSubdirectory("distancerpg-");
            try
            {
                // The save as the game finds it at launch: the campaign's file in the
                // save folder. The launch resumes it and says so — and says the numbers
                // have changed only while they have.
                var store = new SaveStore(folder.FullName);
                File.Copy(PathOf(name), store.SavePath);
                var saved = SaveCodec.Read(TextOf(name)).Campaign;
                var boot = CampaignBoot.Boot(store, GameContent.Current, new BootRequest());
                Assert.True(boot.Resumed);
                string[] notices = NumbersMoved(GameContent.Current.Tuning)
                    ? [BetweenRunsHud.Resumed, CampaignBoot.TuningChanged]
                    : [BetweenRunsHud.Resumed];
                Assert.Equal(notices, boot.Notices);
                Assert.True(store.WasResuming);
                store.ClearResuming();

                var campaign = boot.Campaign;
                Assert.Equal(saved.RunCounter, campaign.RunCounter);
                if (name == MidRun)
                {
                    // In the dungeon, on the floor and turn it was saved on — the Spear
                    // Hall on seed 4, floor 4, turn 10 — with no floor open until the
                    // game opens it. Opened as the game opens it, a turn ended plays out
                    // on the visit's own die and settles to the next player turn.
                    var visit = campaign.Visit;
                    Assert.NotNull(visit);
                    Assert.Equal(("spear_hall", 4L, 3, 10), (saved.Visit!.DungeonId, saved.Visit.MapSeed, saved.Visit.FloorIndex, saved.Visit.Turn));
                    Assert.Equal((saved.Visit.DungeonId, saved.Visit.MapSeed, saved.Visit.FloorIndex, saved.Visit.Turn),
                        (visit.Def.Id, visit.MapSeed, visit.FloorIndex, visit.Turn));
                    Assert.Null(visit.Turns);
                    var turns = visit.OpenTurns(campaign.Party);
                    Assert.Equal(TurnPhase.Player, turns.Phase);
                    turns.EndTurn();
                    Assert.True(SaveGame.SettleToSafePoint(campaign));
                    Assert.Same(visit, campaign.Visit);
                    Assert.Equal((TurnPhase.Player, saved.Visit.Turn + 1), (turns.Phase, visit.Turn));
                }
                else
                {
                    // Between runs, the screen it left; and the next run — into the
                    // dungeon the screen offers first — goes three floors down and
                    // back out like any other, and counts. The run itself stands on
                    // the numbers the run helpers were written against — the seed's
                    // depth, the mercy floor (Premise) — so it is walked under them;
                    // the resume before it and the save after it are the game's own.
                    Assert.Null(saved.Visit);
                    Assert.Null(campaign.Visit);
                    using (TestContent.UsePremise())
                        CampaignRuns.DeepRetreat(campaign, campaign.DefaultNext());
                    Assert.Equal(saved.RunCounter + 1, campaign.RunCounter);
                }

                // Played on, it saves as any campaign does — under the numbers played
                // now — and the next launch resumes that, with nothing more to say.
                Assert.Null(CampaignBoot.Save(store, campaign));
                Assert.Equal([BetweenRunsHud.Resumed], CampaignBoot.Boot(store, GameContent.Current, new BootRequest()).Notices);
                store.ClearResuming();
            }
            finally
            {
                folder.Delete(recursive: true);
            }
        }
    }
}

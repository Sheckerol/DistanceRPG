using System.Text.Json;
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
/// it ships are not those; and a launch resumes each — the save folder itself
/// being the old autosave, where the listing finds it the most recent save, with
/// no time — the mid-run one in the dungeon, where its visit plays on. In the
/// content collection, since a restore runs under the current content and the
/// tuning cases swap it.
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

    /// <summary>The document's member holding when it was saved: appended since the saves were written, so a capture writes it (null, with no time handed in) and the saves have none.</summary>
    private const string SavedAtMember = "savedAt";

    private static readonly string[] Saves = [MidRun, BetweenRuns];

    private static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "saves", name);

    private static string TextOf(string name) => File.ReadAllText(PathOf(name));

    /// <summary>
    /// The keys added since <c>febe036</c>, which <see cref="PlayedTuning"/> is
    /// frozen before and so lacks — each one no rule of play reads, and so none a
    /// save's hash covers: what a deposit costs, the user's ruling of 2026-09-25,
    /// declared ahead of Phase 6a's enchanter; and how many save slots there are,
    /// the ruling of the same day on the save model, the launcher's.
    /// </summary>
    private static readonly string[] KeysSinceTheSaves =
    [
        nameof(Tuning.WearCapacityPerBar), nameof(Tuning.FirstSlotCapacity), nameof(Tuning.SlotCapacityStep),
        nameof(Tuning.EnchantmentSlots), nameof(Tuning.FirstEnchantmentWear), nameof(Tuning.EnchantmentWearStep),
        nameof(Tuning.SaveSlots),
    ];

    /// <summary>
    /// The tuning the saves were played under, as the current reader reads
    /// <see cref="PlayedTuning"/>: over the shipped tuning, as the game reads a
    /// copy beside it, since the file is frozen and predates the keys no rule of
    /// play reads (<see cref="TuningKey.Played"/>). Those are the only keys it
    /// lacks — asserted on every read — so every number a save's hash covers is
    /// the file's own, and nothing under it is taken from anywhere else.
    /// </summary>
    private static TuningRead ReadPlayedTuning()
    {
        string text = TextOf(PlayedTuning);
        using (var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
        {
            var named = document.RootElement.EnumerateObject().Select(key => key.Name).ToHashSet(StringComparer.Ordinal);
            var lacking = TuningSchema.Keys.Where(key => !named.Contains(key.Name)).ToList();
            Assert.Equal(KeysSinceTheSaves, lacking.Select(key => key.Name));
            Assert.All(lacking, key => Assert.False(key.Played, $"{key.Name} is played, and the saves' tuning has no number for it"));
        }
        return TuningJson.Read(text, ShippedContent.Tuning);
    }

    /// <summary>Whether any number <paramref name="now"/> holds is not the number the saves were played under.</summary>
    private static bool NumbersMoved(Tuning now) => TuningValues.Moved(ReadPlayedTuning().Tuning, now).Length > 0;

    /// <summary>
    /// The first place the two documents differ, as a path from the root —
    /// <c>$.campaign.party[1].inventory[3].enchantments[0].tier</c> — with what
    /// each holds there; null when they are the same document, member order
    /// included. The root's <see cref="TuningHashMember"/> is left out: a capture
    /// writes the current tuning's hash there. So is its <see cref="SavedAtMember"/>,
    /// which a capture writes and the saves, older than it, do not.
    /// </summary>
    private static string? FirstDifference(JsonNode? expected, JsonNode? actual, string path, bool root = false)
    {
        switch (expected, actual)
        {
            case (null, null):
                return null;
            case (JsonObject was, JsonObject now):
            {
                bool Compared(string name) => !(root && name is TuningHashMember or SavedAtMember);
                var wasNames = was.Select(member => member.Key).Where(Compared).ToList();
                var nowNames = now.Select(member => member.Key).Where(Compared).ToList();
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

            // And as text, whitespace and number formatting included — but for the
            // time a capture writes last, null with none handed in, which the save,
            // older than the member, never had.
            string untimed = again.Replace($",\n  \"{SavedAtMember}\": null\n}}", "\n}", StringComparison.Ordinal);
            Assert.NotEqual(again, untimed);
            Assert.Null(file.SavedAt);
            string savedHash = $"\"{TuningHashMember}\": \"{file.TuningHash}\"";
            Assert.True(text.Replace(savedHash, $"\"{TuningHashMember}\": \"{hash}\"", StringComparison.Ordinal) == untimed,
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
                // save folder itself — the old autosave, where a build before the
                // slots saved it — and no file moves. The listing finds it there, first,
                // with no time recorded and nothing else saved: the most recent save.
                // Loaded, it resumes and says so, by the old autosave's name — and says
                // the numbers have changed only while they have.
                var slots = new SaveSlots(folder.FullName, GameContent.Current.Tuning.SaveSlots);
                var store = slots.Store(SaveSlots.OldAutosave);
                Assert.Equal(Path.GetFullPath(folder.FullName), store.Folder);
                File.Copy(PathOf(name), store.SavePath);
                var saved = SaveCodec.Read(TextOf(name)).Campaign;
                var listing = SaveListing.Read(slots, new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
                var old = listing.Slots.Single(slot => slot.Number == SaveSlots.OldAutosave);
                Assert.Same(old, listing.Slots[0]);
                Assert.Equal(SlotState.Readable, old.State);
                Assert.Equal(new SlotHeader(null, saved.RunCounter, saved.Visit?.DungeonId, saved.Visit?.FloorIndex + 1, saved.Visit?.Turn), old.Header);
                Assert.Same(old, listing.Newest);
                Assert.All(listing.Slots.Skip(1), slot => Assert.Equal(SlotState.Empty, slot.State));
                Assert.Empty(listing.Notices);

                var boot = CampaignBoot.Load(slots.Slot(SaveSlots.OldAutosave), GameContent.Current);
                Assert.True(boot.Loaded);
                const string resumed = "CAMPAIGN RESUMED FROM THE OLD AUTOSAVE";
                string[] notices = NumbersMoved(GameContent.Current.Tuning)
                    ? [resumed, CampaignBoot.TuningChanged]
                    : [resumed];
                Assert.Equal(notices, boot.Notices);
                Assert.True(store.WasResuming);
                store.ClearResuming();
                Assert.Equal([SaveStore.FileName], Directory.GetFiles(folder.FullName).Select(path => Path.GetFileName(path)!));   // nothing moved

                var campaign = boot.Campaign!;
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
                Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.OldAutosave), campaign));
                Assert.Equal([resumed], CampaignBoot.Load(slots.Slot(SaveSlots.OldAutosave), GameContent.Current).Notices);
                store.ClearResuming();
            }
            finally
            {
                folder.Delete(recursive: true);
            }
        }
    }
}

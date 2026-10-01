using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// A campaign saved by Phase 6a still loads at version 3 (Phase 6b.1), as
/// <c>PreReworkSaveTests</c> holds the saves from before the content rework. The
/// save in <c>TestData/saves</c> was written once by the Phase 6a build at
/// <c>69d7bee</c>, by play — the pre-rework between-runs save loaded, a weapon
/// moved to the stable and another deposited to be refined, and quit to the title
/// (the folder's README) — and is never regenerated: version 2, its hash of version
/// 2's keys, a stable, a queue and a deposit made; the Spear Hall and the tutorial
/// cleared, the tutorial retired. It reads at version 3 through a step that writes
/// nothing, restores whole under the tuning it was played under — frozen beside it,
/// <c>phase-6a-tuning.json</c>, as <c>pre-rework-tuning.json</c> is the earlier
/// saves' — and captured again is the document it was, a member's potions aside.
/// Its hash is that tuning's on version 2's keys, and the game says the numbers have
/// changed exactly when a number version 2 hashes differs from that file's: so a
/// later retune of <c>tuning.json</c>, a data edit, fails nothing here. In the
/// content collection, since the restores run under a tuning made current.
/// </summary>
[Collection(TestContent.Collection)]
public class Phase6aSaveTests
{
    /// <summary>The campaign between runs, as the Phase 6a build wrote it.</summary>
    private const string Saved = "phase-6a-between-runs.json";

    /// <summary><c>Data/tuning.json</c> at <c>69d7bee</c>, byte for byte: the tuning the save was played under.</summary>
    private const string PlayedTuning = "phase-6a-tuning.json";

    /// <summary>The version the save was written at, and the version its hash is of.</summary>
    private const int SavedVersion = 2;

    private static string TextOf(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "saves", name));

    /// <summary>
    /// The tuning the save was played under, as the current reader reads the frozen
    /// file: over the shipped tuning, as the game reads a copy beside it. The keys it
    /// lacks — added since — are asserted to be ones no version-2 hash covers, so
    /// every number the save's hash is of is the file's own.
    /// </summary>
    private static TuningRead ReadPlayedTuning()
    {
        string text = TextOf(PlayedTuning);
        using (var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
        {
            var named = document.RootElement.EnumerateObject().Select(key => key.Name).ToHashSet(StringComparer.Ordinal);
            Assert.All(TuningSchema.Keys.Where(key => !named.Contains(key.Name)), key => Assert.True(!key.Played || key.HashedFrom > SavedVersion,
                $"{key.Name} is played at version {SavedVersion}, and the save's tuning has no number for it"));
        }
        return TuningJson.Read(text, ShippedContent.Tuning);
    }

    /// <summary>Whether a number version 2 hashes is not the same in the two tunings: what the game says has changed.</summary>
    private static bool Version2NumbersMoved(Tuning played, Tuning now)
        => TuningValues.Moved(played, now).Any(name => TuningSchema.Find(name.Split('.')[0]) is { Played: true } key && key.HashedFrom <= SavedVersion);

    /// <summary>
    /// The first place the two documents differ, as a path from the root, with what
    /// each holds there; null when they are the same document, member order
    /// included. <paramref name="setAside"/> names a member, by the path of the object
    /// it sits in and its name, that is not compared.
    /// </summary>
    private static string? FirstDifference(JsonNode? expected, JsonNode? actual, string path, Func<string, string, bool> setAside)
    {
        switch (expected, actual)
        {
            case (null, null):
                return null;
            case (JsonObject was, JsonObject now):
            {
                var wasNames = was.Select(member => member.Key).Where(name => !setAside(path, name)).ToList();
                var nowNames = now.Select(member => member.Key).Where(name => !setAside(path, name)).ToList();
                foreach (string name in wasNames)
                {
                    if (!now.ContainsKey(name))
                        return $"{path}.{name}: in the saved document, not in the capture";
                    if (FirstDifference(was[name], now[name], $"{path}.{name}", setAside) is { } found)
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
                    if (FirstDifference(was[i], now[i], $"{path}[{i}]", setAside) is { } found)
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
    public void APhase6aSaveRestoresWhole()
    {
        // The save's text is version 2, its hash of version 2's keys, and it reads at
        // version 3 through Phase 6b.1's step, which writes nothing: the document the
        // step returns is the document it was handed, and the reader stamps the
        // version. Its hash version is left as written.
        string text = TextOf(Saved);
        var saved = JsonNode.Parse(text)!.AsObject();
        Assert.Equal((SavedVersion, SavedVersion), (saved["version"]!.GetValue<int>(), saved["tuningHashVersion"]!.GetValue<int>()));
        var lifted = SaveCodec.LiftToVersion3(saved.DeepClone().AsObject());
        Assert.True(JsonNode.DeepEquals(saved, lifted), "the version-2 step writes nothing");
        var file = SaveCodec.Read(text);
        Assert.Equal((SaveCodec.CurrentVersion, SavedVersion), (file.Version, file.TuningHashVersion));
        Assert.All(file.Campaign.Party, member => Assert.Null(member.Potions));

        // Under the tuning it was played under, it restores with nothing changed —
        // every id and name it holds resolving — and holds what play left: the stable,
        // the queue, the deposit made, the clears.
        var played = ReadPlayedTuning().Tuning;
        using (TestContent.Use(tuning: played))
        {
            var loaded = SaveGame.Restore(file, GameContent.Current);
            Assert.False(loaded.TuningChanged);
            var campaign = loaded.Campaign;
            Assert.Null(campaign.Visit);
            Assert.Equal(["spear_hall", "tutorial"], campaign.ClearedInOrder);
            Assert.Equal(["tutorial"], campaign.RetiredInOrder);
            Assert.Equal("riposte_blade", Assert.Single(campaign.Stable).Id);
            var deposit = Assert.Single(campaign.AtTheEnchanter);
            Assert.Equal(("wand_of_the_cone", Service.Refine, 0, 2), (deposit.Weapon.Id, deposit.Service, deposit.RunsWorked, deposit.Runs));
            Assert.Equal(1, campaign.DepositsMade);
            Assert.All(campaign.Party, member => Assert.Empty(member.Potions));

            // Captured again it is the migrated document, member by member: the
            // version the reader stamped, and every member at its value and in its
            // order — but for the tuning hash and its version, the current version's
            // keys where the save's are version 2's, the time, which a capture handed
            // none writes null, and each member's potions, appended by Phase 6b.1 and
            // written null for none.
            var again = SaveGame.Capture(campaign, loaded.View);
            Assert.Equal((TuningJson.Hash(played), SaveCodec.CurrentVersion), (again.TuningHash, again.TuningHashVersion));
            var captured = JsonNode.Parse(SaveCodec.Write(again))!.AsObject();
            var migrated = lifted.DeepClone().AsObject();
            migrated["version"] = SaveCodec.CurrentVersion;
            static bool SetAside(string path, string name) => (path == "$" && name is "tuningHash" or "tuningHashVersion" or "savedAt")
                || (Regex.IsMatch(path, @"^\$\.campaign\.party\[[0-9]+\]$") && name == "potions");
            if (FirstDifference(migrated, captured, "$", SetAside) is { } difference)
                Assert.Fail($"captured again, the document is not the migrated save: {difference}");
            var rows = captured["campaign"]!["party"]!.AsArray();
            Assert.Equal(GameConstants.PartySize, rows.Count);
            Assert.All(rows, row =>
            {
                Assert.True(row!.AsObject().ContainsKey("potions"));
                Assert.Null(row["potions"]);
            });
            Assert.All(migrated["campaign"]!["party"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("potions")));
        }
    }

    [Fact]
    public void ItsHashIsItsTuningsHash()
    {
        // The hash the save carries is the hash of the tuning it was played under, as
        // the current reader reads that file, on version 2's keys — the version it
        // records its hash is of.
        var read = ReadPlayedTuning();
        Assert.Empty(read.Warnings);
        Assert.Equal(TuningJson.Hash(read.Tuning, version: SavedVersion), SaveCodec.Read(TextOf(Saved)).TuningHash);
    }

    [Fact]
    public void TheNumbersHaveChangedExactlyWhenTheyHave()
    {
        // The save is loaded under changed numbers exactly when a number version 2
        // hashes is not the one it was played under — under the shipped tuning, as the
        // game resumes it, whether any such number of the master copy differs from the
        // frozen file's; under the frozen file, never; under it with one of version 2's
        // numbers moved, always — what a hit wears among them, which version 2 began to
        // hash — and with only a number version 2 does not hash moved — the save slots,
        // the launcher's, and any a later version began to play — never.
        var played = ReadPlayedTuning().Tuning;
        var file = SaveCodec.Read(TextOf(Saved));
        bool Changed(Tuning tuning)
        {
            using (TestContent.Use(tuning: tuning))
                return SaveGame.Restore(file, GameContent.Current).TuningChanged;
        }

        Assert.Equal(Version2NumbersMoved(played, ShippedContent.Tuning), Changed(ShippedContent.Tuning));
        Assert.False(Changed(played));
        Assert.True(Changed(played with { MovementBudget = played.MovementBudget + 1 }));
        Assert.True(Changed(played with { WearPerHit = played.WearPerHit + 1 }));
        var unhashed = TuningSchema.Keys.OfType<IntKey>().Where(key => !key.Played || key.HashedFrom > SavedVersion).ToList();
        Assert.Contains(unhashed, key => key.Name == nameof(Tuning.SaveSlots));
        foreach (var key in unhashed)
        {
            int was = key.Get(played);
            var moved = key.Set(played, was < key.Max ? was + 1 : was - 1);
            Assert.False(Changed(moved), $"{key.Name} moved, which version 2 does not hash");
        }
    }
}

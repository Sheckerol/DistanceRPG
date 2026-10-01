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
/// migrated to 2 (Phase 6a.1: every weapon row unworn, and its hash of version 1's
/// keys) and on to 3 (Phase 6b.1, a step that writes nothing), restores whole under
/// the current content — every id and name it holds
/// resolves — and, opened as a resume opens it, writes back the same document as
/// the migration writes it, the tuning hash and its version aside; its tuning hash
/// is the hash of the tuning it was played under, on version 1's keys, read by the
/// current reader; the game says the numbers changed exactly when the numbers
/// it ships are not those; and a launch resumes each — the save folder itself
/// being the old autosave, which the launch moves into slot 1, where the listing
/// finds it the most recent save, with no time — the mid-run one in the dungeon,
/// where its visit plays on. In the content collection, since a restore runs
/// under the current content and the tuning cases swap it.
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

    /// <summary>The document's member naming the version whose keys that hash is of: 1 in a migrated save, the current version in a capture (PHASE 6a DECISION 9).</summary>
    private const string TuningHashVersionMember = "tuningHashVersion";

    /// <summary>The document's member holding when it was saved: appended since the saves were written, so a capture writes it (null, with no time handed in) and the saves have none.</summary>
    private const string SavedAtMember = "savedAt";

    /// <summary>
    /// The members appended since the saves were written, with their defaults (the
    /// header rule of <c>SaveFile.cs</c>), each by the object it sits in: the
    /// migration writes none of them, and a capture writes each empty one as its
    /// default — the stable (Phase 6a.2), and the enchanter's queue and a run's
    /// service reports (Phase 6a.3a) null, the count of deposits made 0, and every
    /// member's potions (Phase 6b.1) null — so the saves have none and the capture
    /// of one restored from them has each at its default. Set aside there, and only
    /// at its default: a capture that wrote one with anything in it differs. A place
    /// ending <c>[*]</c> is every row of that list: <c>$.campaign.party[*]</c> is each
    /// member's row.
    /// </summary>
    private static readonly (string At, string Member, JsonNode? Default)[] Appended =
    [
        ("$.campaign", "stable", null),
        ("$.campaign", "enchanter", null),
        ("$.campaign", "depositsMade", JsonValue.Create(0)),
        ("$.campaign.lastResult", "services", null),
        ("$.campaign.party[*]", "potions", null),
    ];

    /// <summary>Whether <paramref name="value"/> is <paramref name="expected"/>'s default as a capture writes it, compared as JSON: null, a number, a word.</summary>
    private static bool IsDefault(JsonNode? value, JsonNode? expected) => JsonNode.DeepEquals(value, expected);

    /// <summary>Whether the object at <paramref name="path"/> is the place <paramref name="at"/> names: its end, and for a place ending <c>[*]</c>, any row of that list.</summary>
    private static bool IsAt(string path, string at)
    {
        if (!at.EndsWith("[*]", StringComparison.Ordinal))
            return path.EndsWith(at, StringComparison.Ordinal);
        int open = path.LastIndexOf('[');
        return open >= 0 && path.EndsWith(']') && path[(open + 1)..^1].All(char.IsAsciiDigit) && path[(open + 1)..^1].Length > 0
            && path[..open].EndsWith(at[..^"[*]".Length], StringComparison.Ordinal);
    }

    private static readonly string[] Saves = [MidRun, BetweenRuns];

    private static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "saves", name);

    private static string TextOf(string name) => File.ReadAllText(PathOf(name));

    /// <summary>
    /// The keys added since <c>febe036</c>, which <see cref="PlayedTuning"/> is
    /// frozen before and so lacks — each one no rule of play reads, or one a rule
    /// began to read with the save's version 2, and so none a version-1 save's hash
    /// covers: what a hit wears, and what a deposit costs, the user's ruling of
    /// 2026-09-25, declared ahead of Phase 6a's enchanter and played from version
    /// 2 as it reads each (<see cref="TuningKey.HashedFrom"/>), and how many
    /// deposits the enchanter works on (Phase 6a.3a) and the improvement roll's
    /// odds and split (6a.4), played from version 2 too; and how many save slots
    /// there are, the ruling of the same day on the save model, the launcher's.
    /// </summary>
    private static readonly string[] KeysSinceTheSaves =
    [
        nameof(Tuning.WearPerHit),
        nameof(Tuning.WearCapacityPerBar), nameof(Tuning.FirstSlotCapacity), nameof(Tuning.SlotCapacityStep),
        nameof(Tuning.EnchantmentSlots), nameof(Tuning.FirstEnchantmentWear), nameof(Tuning.EnchantmentWearStep),
        nameof(Tuning.EnchanterWorksOn), nameof(Tuning.GraftBaseChance), nameof(Tuning.GraftSharePercent), nameof(Tuning.DeepenSignatureWeight),
        nameof(Tuning.SaveSlots),
    ];

    /// <summary>
    /// The tuning the saves were played under, as the current reader reads
    /// <see cref="PlayedTuning"/>: over the shipped tuning, as the game reads a
    /// copy beside it, since the file is frozen and predates the keys no rule of
    /// play reads (<see cref="TuningKey.Played"/>) and those version 2 began to
    /// hash (<see cref="TuningKey.HashedFrom"/>). Those are the only keys it lacks
    /// — asserted on every read — so every number a version-1 save's hash covers is
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
            Assert.All(lacking, key => Assert.True(!key.Played || key.HashedFrom > 1,
                $"{key.Name} is played at version 1, and the saves' tuning has no number for it"));
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
    /// writes the current tuning's hash there, and with it its
    /// <see cref="TuningHashVersionMember"/>, the current version, where a migrated
    /// save has 1. So is its <see cref="SavedAtMember"/>, which a capture writes and
    /// the saves, older than it, do not; and so is a member appended since
    /// (<see cref="Appended"/>) that the saves lack where they have its object and
    /// the capture writes at its default.
    /// </summary>
    private static string? FirstDifference(JsonNode? expected, JsonNode? actual, string path, bool root = false)
    {
        switch (expected, actual)
        {
            case (null, null):
                return null;
            case (JsonObject was, JsonObject now):
            {
                bool Compared(string name) => !(root && name is TuningHashMember or TuningHashVersionMember or SavedAtMember)
                    && !Appended.Any(appended => IsAt(path, appended.At) && appended.Member == name
                        && !was.ContainsKey(name) && IsDefault(now[name], appended.Default));
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

    /// <summary>
    /// What <paramref name="now"/> adds to <paramref name="was"/>, as paths from the
    /// root, once every member of <paramref name="was"/> is asserted to be in
    /// <paramref name="now"/> at its own value and in its own order: a failure
    /// names the first member that moved, went, or changed.
    /// </summary>
    private static List<string> Added(JsonNode? was, JsonNode? now, string path)
    {
        var added = new List<string>();
        void Walk(JsonNode? a, JsonNode? b, string at)
        {
            switch (a, b)
            {
                case (JsonObject left, JsonObject right):
                    var kept = right.Select(member => member.Key).Where(left.ContainsKey).ToList();
                    Assert.True(left.Select(member => member.Key).SequenceEqual(kept, StringComparer.Ordinal), $"{at}: the migration moved or dropped a member");
                    foreach (var (name, value) in left)
                        Walk(value, right[name], $"{at}.{name}");
                    added.AddRange(right.Select(member => member.Key).Where(name => !left.ContainsKey(name)).Select(name => $"{at}.{name}"));
                    break;
                case (JsonArray left, JsonArray right):
                    Assert.True(left.Count == right.Count, $"{at}: {left.Count} items saved, {right.Count} migrated");
                    for (int i = 0; i < left.Count; i++)
                        Walk(left[i], right[i], $"{at}[{i}]");
                    break;
                default:
                    Assert.True(a?.ToJsonString() == b?.ToJsonString(), $"{at}: {a?.ToJsonString() ?? "null"} saved, {b?.ToJsonString() ?? "null"} migrated");
                    break;
            }
        }
        Walk(was, now, path);
        return added;
    }

    /// <summary>Every weapon row's path in <paramref name="node"/>, found by its shape — an object carrying <c>forgedEnchantments</c> — never by where it sits: the test's own oracle for the step, which walks the places by name.</summary>
    private static IEnumerable<(string Path, JsonObject Row)> WeaponRows(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.ContainsKey("forgedEnchantments"))
                    yield return (path, obj);
                foreach (var (name, value) in obj)
                    foreach (var row in WeaponRows(value, $"{path}.{name}"))
                        yield return row;
                break;
            case JsonArray array:
                for (int i = 0; i < array.Count; i++)
                    foreach (var row in WeaponRows(array[i], $"{path}[{i}]"))
                        yield return row;
                break;
        }
    }

    [Fact]
    public void ASaveFromBeforeTheReworkRestoresWhole()
    {
        foreach (string name in Saves)
        {
            // The save's text is version 1, and it reads at version 3, lifted by the
            // two steps this build ships (Phase 6a.1's, and Phase 6b.1's, which writes
            // nothing): every member of the saved tree kept at its value and in its
            // order, and exactly two things added — an unworn wear on every weapon
            // row, wherever it sits, and the root's hash version 1, since its hash
            // was taken on version 1's keys.
            string text = TextOf(name);
            var saved = JsonNode.Parse(text)!.AsObject();
            Assert.Equal(1, saved["version"]!.GetValue<int>());
            var lifted = SaveCodec.LiftToVersion3(SaveCodec.LiftToVersion2(saved.DeepClone().AsObject()));
            var rows = WeaponRows(saved, "$").Select(row => row.Path).ToList();
            Assert.NotEmpty(rows);
            Assert.Equal(rows, WeaponRows(lifted, "$").Select(row => row.Path));
            Assert.Equal(
                rows.SelectMany(row => new[] { $"{row}.wear", $"{row}.wearCashed" }).Append($"$.{TuningHashVersionMember}").Order(StringComparer.Ordinal),
                Added(saved, lifted, "$").Order(StringComparer.Ordinal));
            Assert.Equal(1, lifted[TuningHashVersionMember]!.GetValue<int>());
            Assert.All(WeaponRows(lifted, "$"), row => Assert.Equal((0, 0), (row.Row["wear"]!.GetValue<int>(), row.Row["wearCashed"]!.GetValue<int>())));

            // Read, it restores under the content loaded now: every weapon,
            // enchantment, dungeon, status and member it names resolves, or the
            // restore refuses it, naming where. Every weapon is unworn, on a fresh
            // capacity.
            var file = SaveCodec.Read(text);
            Assert.Equal((SaveCodec.CurrentVersion, 1), (file.Version, file.TuningHashVersion));
            var loaded = SaveGame.Restore(file, GameContent.Current);
            var campaign = loaded.Campaign;
            Assert.Equal(name == MidRun, campaign.Visit is not null);
            Assert.All(campaign.Party.SelectMany(member => member.Inventory).OfType<Weapon>(),
                weapon => Assert.Equal((0, 0, GameContent.Current.Tuning.StartingPool), (weapon.Wear, weapon.WearCashed, weapon.WearCapacity)));

            // A run in progress is opened as the game's resume opens it — on the
            // visit's own die — and captured again it is the migrated document,
            // every weapon row field by field: nothing lost, nothing re-derived
            // differently. It is the tree the step wrote that is compared, not the
            // reader's rendering of it, so a member the reader drops or defaults —
            // one renamed since, say — fails here as a member of the save the
            // capture lacks. The step leaves the version to the reader, which
            // stamps the current one; the tuning hash is set aside, the current
            // tuning's, on the current version's keys, where the migrated save's is
            // of version 1's.
            campaign.Visit?.OpenTurns(campaign.Party);
            string again = SaveCodec.Write(SaveGame.Capture(campaign, loaded.View));
            string hash = TuningJson.Hash(GameContent.Current.Tuning);
            var recaptured = SaveCodec.Read(again);
            Assert.Equal((hash, SaveCodec.CurrentVersion), (recaptured.TuningHash, recaptured.TuningHashVersion));
            var migrated = lifted.DeepClone().AsObject();
            migrated["version"] = SaveCodec.CurrentVersion;
            if (FirstDifference(migrated, JsonNode.Parse(again), $"{name} $", root: true) is { } difference)
                Assert.Fail($"captured again, the document is not the migrated save: {difference}");

            // The members appended since are in the capture, at their defaults: the
            // stable, written null (Phase 6a.2); the enchanter's queue null and its
            // count 0, and the last run's service reports null (Phase 6a.3a); and
            // every member's potions null (Phase 6b.1) — which the migrated save
            // reads as the same, nothing having been in any stable, shop or bag of
            // potions before there was one. Where the save has no last result, its
            // reports have nowhere to be.
            var captured = JsonNode.Parse(again)!.AsObject();
            IReadOnlyList<JsonObject?> At(JsonObject tree, string at) => at switch
            {
                "$.campaign" => [tree["campaign"] as JsonObject],
                "$.campaign.lastResult" => [tree["campaign"]?["lastResult"] as JsonObject],
                "$.campaign.party[*]" => (tree["campaign"]?["party"] as JsonArray ?? []).Select(row => row as JsonObject).ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(at), at, "a place the test does not walk"),
            };
            foreach (var (at, member, defaulted) in Appended)
            {
                var migratedRows = At(lifted, at);
                var capturedRows = At(captured, at);
                Assert.Equal(migratedRows.Count, capturedRows.Count);
                Assert.NotEmpty(migratedRows);
                for (int i = 0; i < migratedRows.Count; i++)
                {
                    if (migratedRows[i] is not { } migratedAt)
                    {
                        Assert.Null(capturedRows[i]);
                        continue;
                    }
                    var capturedAt = capturedRows[i]!;
                    Assert.True(capturedAt.ContainsKey(member), $"{name}: the capture has no {at}.{member}");
                    Assert.True(IsDefault(capturedAt[member], defaulted), $"{name}: the capture wrote {at}.{member} as {capturedAt[member]?.ToJsonString() ?? "null"}");
                    Assert.False(migratedAt.ContainsKey(member), $"{name}: the migration wrote {at}.{member}");
                }
            }
            Assert.Empty(campaign.Stable);
            Assert.Null(file.Campaign.Stable);
            Assert.Equal((0, 0), (campaign.AtTheEnchanter.Count, campaign.DepositsMade));
            Assert.Null(file.Campaign.Enchanter);

            // And as text, whitespace and number formatting included: the migrated
            // save as the codec writes it — its time null, as it never had one — with
            // the capture's hash and hash version in place of its own.
            Assert.Null(file.SavedAt);
            Assert.True(SaveCodec.Write(file with { TuningHash = hash, TuningHashVersion = SaveCodec.CurrentVersion }) == again,
                $"{name}: captured again, the document's text is not the migrated save's, though every value and member order is");
        }
    }

    [Fact]
    public void ItsHashIsItsTuningsHash()
    {
        // The hash each save carries is the hash of the tuning it was played under,
        // as the current reader reads that file, on version 1's keys — the version
        // the migration says it is of: the hash covers the same numbers it did
        // before the rework, so a save written then is recognised as played under
        // exactly those numbers.
        var read = ReadPlayedTuning();
        Assert.Empty(read.Warnings);
        string hash = TuningJson.Hash(read.Tuning, version: 1);
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
        // moved, always — but for a number no rule played at version 1, one version
        // 2 began to hash, which is none it was played under (PHASE 6a DECISION 9).
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
            using (TestContent.Use(tuning: played with { WearPerHit = played.WearPerHit + 1 }))
                Assert.False(SaveGame.Restore(file, GameContent.Current).TuningChanged);
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
                // slots saved it — which the launch moves, before anything is listed,
                // into slot 1, byte for byte (the user's ruling of 2026-09-25 on the
                // save slots, R5; OldAutosave). The listing finds it there, first, with
                // no time recorded and nothing else saved: the most recent save.
                // Loaded, it resumes and says so, by the slot's name — and says the
                // numbers have changed only while they have.
                var slots = new SaveSlots(folder.FullName, GameContent.Current.Tuning.SaveSlots);
                var root = slots.Store(SaveSlots.OldAutosave);
                Assert.Equal(Path.GetFullPath(folder.FullName), root.Folder);
                File.Copy(PathOf(name), root.SavePath);
                Assert.Equal([CampaignBoot.AutosaveMoved(1)], OldAutosave.MoveIntoASlot(slots));
                var store = slots.Store(1);
                Assert.Empty(Directory.GetFiles(folder.FullName));
                Assert.Equal([SaveStore.FileName], Directory.GetFiles(store.Folder).Select(path => Path.GetFileName(path)!));
                Assert.Equal(File.ReadAllBytes(PathOf(name)), File.ReadAllBytes(store.SavePath));   // moved, the bytes identical
                var saved = SaveCodec.Read(TextOf(name)).Campaign;
                var listing = SaveListing.Read(slots, new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
                Assert.DoesNotContain(listing.Slots, slot => slot.Number == SaveSlots.OldAutosave);
                var first = listing.Slots[0];
                Assert.Equal((1, SlotState.Readable), (first.Number, first.State));
                Assert.Equal(new SlotHeader(null, saved.RunCounter, saved.Visit?.DungeonId, saved.Visit?.FloorIndex + 1, saved.Visit?.Turn), first.Header);
                Assert.Same(first, listing.Newest);
                Assert.All(listing.Slots.Skip(1), slot => Assert.Equal(SlotState.Empty, slot.State));
                Assert.Empty(listing.Notices);

                var boot = CampaignBoot.Load(slots.Slot(1), GameContent.Current);
                Assert.True(boot.Loaded);
                const string resumed = "CAMPAIGN RESUMED FROM SLOT 1";
                string[] notices = NumbersMoved(GameContent.Current.Tuning)
                    ? [resumed, CampaignBoot.TuningChanged]
                    : [resumed];
                Assert.Equal(notices, boot.Notices);
                Assert.True(store.WasResuming);
                store.ClearResuming();
                Assert.Equal([SaveStore.FileName], Directory.GetFiles(store.Folder).Select(path => Path.GetFileName(path)!));   // the load moved nothing

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
                // now, into the slot it came from — and the next launch moves nothing
                // and resumes that, with nothing more to say.
                Assert.Null(CampaignBoot.Save(slots.Slot(1), campaign));
                Assert.Empty(OldAutosave.MoveIntoASlot(slots));
                Assert.Equal([resumed], CampaignBoot.Load(slots.Slot(1), GameContent.Current).Notices);
                store.ClearResuming();
                Assert.Empty(Directory.GetFiles(folder.FullName));
            }
            finally
            {
                folder.Delete(recursive: true);
            }
        }
    }
}

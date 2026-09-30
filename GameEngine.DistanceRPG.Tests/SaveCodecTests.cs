using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.2's format, as text (PHASE5-002, -027, -028, -029): <c>System.Text.Json</c>
/// with an explicit version field, a newer document refused, an older one lifted
/// step by step, unknown members ignored and missing ones not, every float back
/// bit for bit, and a DTO layer in <c>Logic/Save/</c> so no gameplay type carries
/// a serialisation attribute. Documents built from DTOs by hand and read as text:
/// no content is resolved here (that is the restore's, <c>CampaignSaveTests</c>),
/// so nothing is swapped and nothing touches a file.
/// </summary>
public class SaveCodecTests
{
    /// <summary>A member row as the document spells one: two slots, one carried, statuses and a spent pool.</summary>
    private static MemberDto Member(string id, float x = 496f, float y = 1072f) => new(
        id, ColorIndex: 0, new StatsDto(1, 4, 2, 3),
        WeaponXp: new Dictionary<string, int> { ["Dagger"] = 140, ["Staff"] = 3 },
        HpXp: 12, ManaXp: 30,
        Innate: new Dictionary<string, int>(), Attunement: null,
        new ActorDto(x, y, Hp: 27, Mana: null, Alive: true, HeldShots: 0,
            [new StatusDto("Poison", null, 2), new StatusDto("Searing", "Flaming", 1)]),
        Inventory:
        [
            new WeaponDto("weakspot_stiletto", new Dictionary<string, int> { ["CritWindow"] = 1, ["CritMultiplier"] = 1, ["CritSunder"] = 1 },
                new Dictionary<string, int> { ["CritWindow"] = 1, ["CritMultiplier"] = 2, ["CritSunder"] = 1 },
                [new EnchantmentDto("vampiric", 3, 7)], ForgedEnchantments: 1, Wear: 0, WearCashed: 0),
            null,
        ],
        DistLeft: 112f, EffectiveMax: 160f, SavedMovement: 0f, BudgetBeforeMire: 160f);

    /// <summary>
    /// A document at the current version. Its weapon rows are unworn and its hash is
    /// of version 1's keys — what the step that lifts a version-1 document writes on
    /// both (<see cref="SaveCodec.LiftToVersion2"/>) — so a synthetic older version
    /// made of it, lifted through the real step, reads back as the same text.
    /// </summary>
    private static SaveFile Sample(params MemberDto[] party) => new(
        SaveCodec.CurrentVersion, "5c0e9a41d27b33f8", TuningHashVersion: 1,
        new CampaignDto(party.Length == 0 ? [Member("A")] : party, ["mire", "regeneration"], RunCounter: 2,
            [new LockDto("spear_hall", 8)], ["spear_hall", "tutorial"], ["tutorial"], LastResult: null),
        new ViewDto("A"));

    /// <summary>The document as a tree, for a test to cut or graft a member.</summary>
    private static JsonObject Tree(string text) => JsonNode.Parse(text)!.AsObject();

    private static JsonObject Campaign(JsonObject tree) => tree["campaign"]!.AsObject();

    private static JsonObject FirstMember(JsonObject tree) => Campaign(tree)["party"]![0]!.AsObject();

    private static SaveFormatException Refused(string text, IReadOnlyList<SaveMigration>? migrations = null)
        => Assert.Throws<SaveFormatException>(() => SaveCodec.Read(text, migrations));

    [Fact]
    public void TheDocumentCarriesItsVersion()
    {
        // PHASE5-027: System.Text.Json "with an explicit version field". This build
        // writes version 2 (Phase 6a: a weapon's wear), first in the document, and
        // reads it back.
        Assert.Equal(2, SaveCodec.CurrentVersion);
        string text = SaveCodec.Write(Sample());
        using (var document = JsonDocument.Parse(text))
        {
            var first = document.RootElement.EnumerateObject().First();
            Assert.Equal(("version", 2), (first.Name, first.Value.GetInt32()));
        }
        Assert.Equal(SaveCodec.CurrentVersion, SaveCodec.Read(text).Version);

        // Written for a person to open: indented, "\n" line ends on every OS, one at the end.
        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.Contains("\n  \"campaign\": {\n", text, StringComparison.Ordinal);

        // A document without a version, or with anything but a whole number
        // there, is not a save: the version is the first thing read, before
        // anything that depends on it.
        foreach (string version in new[] { "1.5", "\"1\"", "null", "true", "[1]", "99999999999" })
        {
            var tree = Tree(text);
            tree["version"] = JsonNode.Parse(version);
            Assert.Equal("version", Refused(tree.ToJsonString()).Where);
        }
        var none = Tree(text);
        none.Remove("version");
        var missing = Refused(none.ToJsonString());
        Assert.Equal("version", missing.Where);
        Assert.Contains("missing", missing.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ANewerDocumentIsRefused_AnOlderOneIsMigratedStepByStep()
    {
        // PHASE5-002, A11: the format must grow to carry 6a/6b's state without
        // breaking. A document from a newer build is refused whatever it holds —
        // an older build cannot know what it meant, and would write the next save
        // without it — and an older one is lifted one version at a time.
        string text = SaveCodec.Write(Sample());
        var newer = Tree(text);
        newer["version"] = SaveCodec.CurrentVersion + 1;
        var refused = Refused(newer.ToJsonString());
        Assert.Equal("version", refused.Where);
        Assert.Contains("newer", refused.Problem, StringComparison.Ordinal);

        // A synthetic version 0 that called the run counter "runs": nothing in
        // this build lifts it, so it is refused rather than read with a default...
        var old = Tree(text);
        old["version"] = 0;
        var campaign = Campaign(old);
        campaign["runs"] = campaign["runCounter"]!.DeepClone();
        campaign.Remove("runCounter");
        string versionZero = old.ToJsonString();
        var shipped = Assert.Single(SaveCodec.Migrations);   // version 1 is the first: this build ships the one step, 1 to 2 (Phase 6a)
        Assert.Equal(1, shipped.From);
        var unlifted = Refused(versionZero);
        Assert.Equal("version", unlifted.Where);
        Assert.Contains("no migration", unlifted.Problem, StringComparison.Ordinal);

        // ...and a step that renames the member lifts it to 1, and the real step
        // on to 2: the document reads at the current version. The real step writes
        // an unworn weapon row and version 1's hash, which the sample holds, so the
        // text is the sample's again.
        static SaveMigration Rename(int from, string was, string now, List<int> ran) => new(from, document =>
        {
            ran.Add(document["version"]!.GetValue<int>());   // what the step was handed says the version it reads
            var campaign = document["campaign"]!.AsObject();
            campaign[now] = campaign[was]!.DeepClone();
            campaign.Remove(was);
            return document;
        });
        var ran = new List<int>();
        var lifted = SaveCodec.Read(versionZero, [.. SaveCodec.Migrations, Rename(0, "runs", "runCounter", ran)]);
        Assert.Equal((SaveCodec.CurrentVersion, 2), (lifted.Version, lifted.Campaign.RunCounter));
        Assert.Equal([0], ran);
        Assert.Equal(text, SaveCodec.Write(lifted));   // the same document it started as, before version 0 was made of it

        // Step by step, in version order whatever order the table lists them in:
        // a version -1 that also called the seen set "met" runs -1 → 0 → 1 → 2,
        // each step handed the document at its own version.
        var older = Tree(versionZero);
        older["version"] = -1;
        var olderCampaign = Campaign(older);
        olderCampaign["met"] = olderCampaign["seen"]!.DeepClone();
        olderCampaign.Remove("seen");
        ran.Clear();
        var twice = SaveCodec.Read(older.ToJsonString(), [.. SaveCodec.Migrations, Rename(0, "runs", "runCounter", ran), Rename(-1, "met", "seen", ran)]);
        Assert.Equal([-1, 0], ran);
        Assert.Equal(text, SaveCodec.Write(twice));

        // A table with a gap is a refusal of the document; a table that lifts one
        // version two ways is a table that cannot mean anything.
        Assert.Equal("version", Refused(older.ToJsonString(), [.. SaveCodec.Migrations, Rename(0, "runs", "runCounter", ran)]).Where);
        Assert.Throws<ArgumentException>(() => SaveCodec.Read(versionZero,
            [Rename(0, "runs", "runCounter", ran), Rename(0, "runs", "runCounter", ran)]));
    }

    /// <summary>A weapon row as the document spells one, unworn: what version 1 means by a weapon's wear.</summary>
    private static WeaponDto Row(string id) => new(id, new Dictionary<string, int>(), new Dictionary<string, int>(), [], ForgedEnchantments: 0, Wear: 0, WearCashed: 0);

    /// <summary>
    /// A document with a weapon row in each of the five places a <see cref="WeaponDto"/>
    /// sits: A's bag, the last run's spoils kept and lost, a visited floor's enemy
    /// row, the drop a corpse lies on and a weapon put down on a floor. Plain values
    /// only: the codec resolves nothing, so none of it need be a campaign play made.
    /// </summary>
    private static SaveFile EveryWeaponPlace()
    {
        var body = new ActorDto(48f, 48f, Hp: 0, Mana: null, Alive: false, HeldShots: 0, []);
        var enemy = new EnemyDto(0, Boss: false, body, Row("hatchet"), DefeatedAtTurn: 2, TurnsSinceSeen: 0, DefeatCount: 1, RevivalsRolled: 1,
            RevivalDamage: 0, RevivalMaxHp: 0, DropTaken: false, GroundItem: new DropDto(Row("hunting_bow"), WasUniqueRoll: false));
        var floor = new FloorDto(0, [enemy], ["01"], [new LooseItemDto(Row("tower_guard"), Row: 3, Col: 4)]);
        var visit = new VisitDto("spear_hall", MapSeed: 4, BossFloor: 5, PromisedStacks: 1, FloorIndex: 0, DeepestFloorIndex: 0, Turn: 3,
            BossDefeated: false, BossDefeatedAtTurn: -1, new XpSnapshotDto([]), [], Carry: null, [floor]);
        var last = new RunResultDto("knife_warrens", "Death", BossDefeated: false, DeepestFloor: 1, BossFloorRevealed: null, ServiceTick: false,
            MercyTick: false, RunCounter: 2, LockedUntilRun: null, Retired: false, [Row("longbow")], [Row("great_axe")], []);
        var sample = Sample();
        return sample with { Campaign = sample.Campaign with { LastResult = last, Visit = visit } };
    }

    /// <summary>Every weapon row in <paramref name="node"/>, found by its shape — an object carrying <c>forgedEnchantments</c> — never by where it sits: the test's own oracle for the step, which walks the places by name.</summary>
    private static IEnumerable<JsonObject> WeaponRows(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.ContainsKey("forgedEnchantments"))
                    yield return obj;
                foreach (var (_, value) in obj)
                    foreach (var row in WeaponRows(value))
                        yield return row;
                break;
            case JsonArray array:
                foreach (var item in array)
                    foreach (var row in WeaponRows(item))
                        yield return row;
                break;
        }
    }

    /// <summary>
    /// <paramref name="text"/>, a version-2 document, as version 1 wrote the same
    /// campaign: version 1, no tuning hash version, and no wear on any weapon row.
    /// </summary>
    private static JsonObject AsVersion1(string text)
    {
        var tree = Tree(text);
        tree["version"] = 1;
        Assert.True(tree.Remove("tuningHashVersion"));
        foreach (var row in WeaponRows(tree).ToList())
        {
            Assert.True(row.Remove("wear"));
            Assert.True(row.Remove("wearCashed"));
        }
        return tree;
    }

    [Fact]
    public void TheVersion1StepAddsWearToEveryWeaponRow_AndTheHashVersion()
    {
        // The v2 hand-off (settled.md), Phase 6a.1: a weapon wears from version 2,
        // and a save records the version its tuning hash is of (PHASE 6a DECISION
        // 9). The one step this build ships lifts a version-1 document by writing
        // what version 1 meant by both — no weapon had worn, and its hash was of
        // version 1's keys — and nothing else: every weapon row wherever it sits,
        // in all five places, is 0 worn and 0 cashed, and the root's hash version 1.
        string text = SaveCodec.Write(EveryWeaponPlace());
        Assert.Equal(6, WeaponRows(Tree(text)).Count());   // a bag, two spoils, an enemy's, a drop, one put down
        var v1 = AsVersion1(text);
        Assert.DoesNotContain(WeaponRows(v1), row => row.ContainsKey("wear") || row.ContainsKey("wearCashed"));

        // The step alone: the rows it found are the rows the shape finds, each
        // unworn, and the root's hash is version 1's. It reshapes, and leaves the
        // version to the codec.
        var stepped = SaveCodec.LiftToVersion2(v1.DeepClone().AsObject());
        Assert.Equal(1, stepped["version"]!.GetValue<int>());
        Assert.Equal(1, stepped["tuningHashVersion"]!.GetValue<int>());
        Assert.Equal(6, WeaponRows(stepped).Count());
        Assert.All(WeaponRows(stepped), row => Assert.Equal((0, 0), (row["wear"]!.GetValue<int>(), row["wearCashed"]!.GetValue<int>())));

        // Read through the codec, the document is the version-2 one the campaign
        // writes today: every member as it was, the two new ones as the step wrote them.
        var read = SaveCodec.Read(v1.ToJsonString());
        Assert.Equal((SaveCodec.CurrentVersion, 1), (read.Version, read.TuningHashVersion));
        Assert.Equal(text, SaveCodec.Write(read));

        // A hand-edited version-1 row that already carries a stray wear — the
        // version-1 reader ignored it — is overwritten with version 1's meaning, 0,
        // and a stray root hash version with 1, whatever kind of thing it was: the
        // document's hash was taken on version 1's keys, whatever a hand wrote
        // beside it. Never refused, never kept.
        var stray = v1.DeepClone().AsObject();
        stray["campaign"]!["visit"]!["floors"]![0]!["looseItems"]![0]!["weapon"]!["wear"] = 17;
        stray["campaign"]!["party"]![0]!["inventory"]![0]!["wearCashed"] = 40;
        stray["tuningHashVersion"] = 2;
        Assert.Equal(text, SaveCodec.Write(SaveCodec.Read(stray.ToJsonString())));
        stray["tuningHashVersion"] = "one";
        Assert.Equal(text, SaveCodec.Write(SaveCodec.Read(stray.ToJsonString())));

        // And a stray campaign stable, a member version 1 never had (the 6a.2
        // review): to the version-1 reader it was nothing, so the step removes it,
        // whatever it holds — never read as weapons the campaign never owned, never
        // refused at a row's missing wear.
        stray["campaign"]!["stable"] = new JsonArray(v1["campaign"]!["party"]![0]!["inventory"]![0]!.DeepClone());
        Assert.False(SaveCodec.LiftToVersion2(stray.DeepClone().AsObject())["campaign"]!.AsObject().ContainsKey("stable"));
        Assert.Equal(text, SaveCodec.Write(SaveCodec.Read(stray.ToJsonString())));
        stray["campaign"]!["stable"] = "every weapon";
        Assert.Equal(text, SaveCodec.Write(SaveCodec.Read(stray.ToJsonString())));

        // At version 2 the members are the document's own, read as written.
        var worn = Tree(text);
        worn["campaign"]!["party"]![0]!["inventory"]![0]!["wear"] = 12;
        worn["tuningHashVersion"] = 2;
        var kept = SaveCodec.Read(worn.ToJsonString());
        Assert.Equal((12, 2), (kept.Campaign.Party[0].Inventory[0]!.Wear, kept.TuningHashVersion));
    }

    [Fact]
    public void TheVersion1StepPassesOverWhatItCannotRead()
    {
        // The step runs before the reader and outside its refusals (SaveCodec.Read),
        // so it must never throw on a hand-edited document: a member missing, null
        // or holding the wrong kind of thing is passed over, and the reader then
        // refuses the document where it is wrong, by the same path it names at
        // version 2 — never an exception that says nothing of where (the notice of
        // a save set aside shows the path, CampaignBoot.WhyUnreadable).
        string text = SaveCodec.Write(EveryWeaponPlace());
        (Action<JsonObject> Spoil, string Where)[] wrong =
        [
            (tree => tree["campaign"] = 4, "campaign"),
            (tree => Campaign(tree)["party"] = 4, "campaign.party"),
            (tree => Campaign(tree)["party"]![0] = "A", "campaign.party[0]"),
            (tree => FirstMember(tree)["inventory"] = new JsonObject(), "campaign.party[0].inventory"),
            (tree => FirstMember(tree)["inventory"] = 5, "campaign.party[0].inventory"),
            (tree => FirstMember(tree)["inventory"]![0] = "arming_sword", "campaign.party[0].inventory[0]"),
            (tree => Campaign(tree)["lastResult"] = 5, "campaign.lastResult"),
            (tree => Campaign(tree)["lastResult"]!["spoilsKept"] = "longbow", "campaign.lastResult.spoilsKept"),
            (tree => Campaign(tree)["lastResult"]!["spoilsLost"]![0] = 7, "campaign.lastResult.spoilsLost[0]"),
            (tree => Campaign(tree)["visit"] = new JsonArray(), "campaign.visit"),
            (tree => Campaign(tree)["visit"]!["floors"] = new JsonObject(), "campaign.visit.floors"),
            (tree => Campaign(tree)["visit"]!["floors"]![0] = 1, "campaign.visit.floors[0]"),
            (tree => Campaign(tree)["visit"]!["floors"]![0]!["enemies"] = new JsonObject(), "campaign.visit.floors[0].enemies"),
            (tree => Campaign(tree)["visit"]!["floors"]![0]!["enemies"]![0] = "x", "campaign.visit.floors[0].enemies[0]"),
            (tree => Campaign(tree)["visit"]!["floors"]![0]!["enemies"]![0]!["weapon"] = "hatchet", "campaign.visit.floors[0].enemies[0].weapon"),
            (tree => Campaign(tree)["visit"]!["floors"]![0]!["enemies"]![0]!["groundItem"] = 3, "campaign.visit.floors[0].enemies[0].groundItem"),
            (tree => Campaign(tree)["visit"]!["floors"]![0]!["enemies"]![0]!["groundItem"]!["weapon"] = 9, "campaign.visit.floors[0].enemies[0].groundItem.weapon"),
            (tree => Campaign(tree)["visit"]!["floors"]![0]!["looseItems"] = "sword", "campaign.visit.floors[0].looseItems"),
            (tree => Campaign(tree)["visit"]!["floors"]![0]!["looseItems"]![0] = 2, "campaign.visit.floors[0].looseItems[0]"),
            (tree => Campaign(tree)["visit"]!["floors"]![0]!["looseItems"]![0]!["weapon"] = true, "campaign.visit.floors[0].looseItems[0].weapon"),
        ];
        foreach (var (spoil, where) in wrong)
        {
            var two = Tree(text);
            spoil(two);
            var one = AsVersion1(text);
            spoil(one);
            Assert.Equal(where, Refused(two.ToJsonString()).Where);
            Assert.Equal(where, Refused(one.ToJsonString()).Where);   // a SaveFormatException, exactly: never the step's own exception
        }

        // And a null where a campaign belongs: the step has nothing to walk, and
        // the reader says so where the campaign is missing, at either version.
        var nothing = AsVersion1(text);
        nothing["campaign"] = null;
        var twoNothing = Tree(text);
        twoNothing["campaign"] = null;
        Assert.Equal(Refused(twoNothing.ToJsonString()).Where, Refused(nothing.ToJsonString()).Where);
    }

    [Fact]
    public void UnknownMembersAreIgnored_MissingOnesAreNot()
    {
        // A member a later build of the same version adds never breaks this
        // reader, wherever it sits; a member this reader needs and does not find
        // is an error, never a default; and a null where none belongs is one too.
        // (The campaign's graft is named for no version: "stable" is Phase 6a.2's.)
        string text = SaveCodec.Write(Sample());
        var grown = Tree(text);
        grown["futureMember"] = JsonNode.Parse("""{ "deep": [1, 2, { "x": null }] }""");
        Campaign(grown)["notAMemberOfAnyVersion"] = JsonNode.Parse("[]");
        FirstMember(grown)["wear"] = 12;
        FirstMember(grown)["inventory"]![0]!["wearCapacity"] = 30;
        Assert.Equal(text, SaveCodec.Write(SaveCodec.Read(grown.ToJsonString())));

        // Missing: at the root, in the campaign, in a member, in a weapon row. The
        // refusal names the object the member is missing from, and the member.
        (Func<JsonObject, JsonObject> Holder, string Member, string Where)[] cuts =
        [
            (tree => tree, "tuningHash", "$"),
            (tree => tree, "tuningHashVersion", "$"),   // version 2's, required from it
            (tree => tree, "campaign", "$"),
            (Campaign, "runCounter", "campaign"),
            (Campaign, "lastResult", "campaign"),   // nullable, and still required: null is written, never left out
            (FirstMember, "body", "campaign.party[0]"),
            (FirstMember, "attunement", "campaign.party[0]"),
            (tree => FirstMember(tree)["inventory"]![0]!.AsObject(), "forged", "campaign.party[0].inventory[0]"),
            (tree => FirstMember(tree)["inventory"]![0]!.AsObject(), "wear", "campaign.party[0].inventory[0]"),   // version 2's, required from it
            (tree => FirstMember(tree)["inventory"]![0]!.AsObject(), "wearCashed", "campaign.party[0].inventory[0]"),
        ];
        foreach (var (holder, member, where) in cuts)
        {
            var cut = Tree(text);
            Assert.True(holder(cut).Remove(member), member);
            var refused = Refused(cut.ToJsonString());
            Assert.Equal(where, refused.Where);
            Assert.Contains(member, refused.Problem, StringComparison.Ordinal);
        }

        // A null where the document has none.
        var blank = Tree(text);
        FirstMember(blank)["id"] = null;
        Assert.Equal("campaign.party[0].id", Refused(blank.ToJsonString()).Where);

        // A member with a default may be left out: the view is presentation, and a
        // document without one reads as saying nothing.
        var viewless = Tree(text);
        viewless.Remove("view");
        Assert.Null(SaveCodec.Read(viewless.ToJsonString()).View);
    }

    [Fact]
    public void ANonsenseDocumentIsRefusedSayingWhere()
    {
        // Every refusal says where — the path the reader was at, or the line a
        // syntax error is on — never a bare "could not load".
        string text = SaveCodec.Write(Sample());
        Assert.Equal("line 1", Refused("").Where);
        Assert.Equal("line 1", Refused("this is not a save").Where);
        Assert.Equal("$", Refused("[1, 2, 3]").Where);
        Assert.Equal("$", Refused("null").Where);
        Assert.Equal("$", Refused("\"campaign\"").Where);

        // Cut off part way: the line it stopped on.
        string[] lines = text.Split('\n');
        string truncated = string.Join('\n', lines.Take(12));
        Assert.Equal("line 12", Refused(truncated).Where);

        // A word the reader does not know, where a hand edit mistyped one: it
        // quotes the rest of the file, and the refusal keeps the word alone, on
        // one line — the notice a set-aside save gets is shown whole (the
        // rework's audit, for the content files' same reader).
        string mistyped = text.Replace("\"runCounter\": 2,", "\"runCounter\": tru,", StringComparison.Ordinal);
        Assert.NotEqual(text, mistyped);
        var literal = Refused(mistyped);
        Assert.Equal("'tru...' is an invalid JSON literal. Expected the literal 'true'.", literal.Problem);
        Assert.StartsWith("line ", literal.Where, StringComparison.Ordinal);

        // The right member holding the wrong kind of thing.
        (Action<JsonObject> Spoil, string Where)[] wrong =
        [
            (tree => Campaign(tree)["party"] = 4, "campaign.party"),
            (tree => FirstMember(tree)["body"]!["x"] = "far", "campaign.party[0].body.x"),
            (tree => FirstMember(tree)["hpXp"] = 1.5, "campaign.party[0].hpXp"),
            (tree => FirstMember(tree)["weaponXp"]!["Dagger"] = "lots", "campaign.party[0].weaponXp.Dagger"),
            (tree => Campaign(tree)["seen"] = JsonNode.Parse("""{ "mire": true }"""), "campaign.seen"),
        ];
        foreach (var (spoil, where) in wrong)
        {
            var tree = Tree(text);
            spoil(tree);
            Assert.Equal(where, Refused(tree.ToJsonString()).Where);
        }

        // A member named twice is two answers to one question: refused, whichever
        // it would have taken.
        string twice = text.Replace("\"hpXp\": 12,", "\"hpXp\": 12, \"hpXp\": 13,", StringComparison.Ordinal);
        Assert.NotEqual(text, twice);
        Assert.Contains("hpXp", Refused(twice).Problem, StringComparison.Ordinal);

        // A comment or a trailing comma a hand edit left behind is not nonsense.
        string edited = text.Replace("\"runCounter\": 2,", "\"runCounter\": 2, /* two runs counted */", StringComparison.Ordinal)
            .Replace("\"tutorial\"\n    ]", "\"tutorial\",\n    ]", StringComparison.Ordinal);
        Assert.NotEqual(text, edited);
        Assert.Equal(text, SaveCodec.Write(SaveCodec.Read(edited)));
    }

    [Fact]
    public void FloatsRoundTripBitForBit()
    {
        // A position or a budget comes back as the same bits, not merely close:
        // a member one ulp off a tile edge stands on the other tile. The writer
        // writes each float's shortest round-trip text, so values with no short
        // decimal form survive as surely as 160 does.
        float[] awkward =
        [
            0.1f, 1f / 3f, 2f / 3f, (float)Math.PI, 123.456f, 496.00003f, 0.30000001f, 16777216f, 1e-40f, float.Epsilon,
            float.MaxValue, -float.MaxValue, -0f, 0f, BitConverter.Int32BitsToSingle(0x3F8CCCCD), 1072.5f, 159.99998f,
        ];
        void RoundTrips(float value)
        {
            var file = Sample(Member("A", x: value, y: -value) with
            {
                DistLeft = value, EffectiveMax = value * 0.7f, SavedMovement = value / 3f, BudgetBeforeMire = value + 0.25f,
            });
            string text = SaveCodec.Write(file);
            var back = SaveCodec.Read(text).Campaign.Party[0];
            var sent = file.Campaign.Party[0];
            Assert.Equal(
                new[] { sent.Body.X, sent.Body.Y, sent.DistLeft, sent.EffectiveMax, sent.SavedMovement, sent.BudgetBeforeMire }.Select(BitConverter.SingleToInt32Bits),
                new[] { back.Body.X, back.Body.Y, back.DistLeft, back.EffectiveMax, back.SavedMovement, back.BudgetBeforeMire }.Select(BitConverter.SingleToInt32Bits));
            Assert.Equal(text, SaveCodec.Write(SaveCodec.Read(text)));   // one float, one text
        }

        foreach (float value in awkward)
            RoundTrips(value);

        // And a sweep over bit patterns rather than chosen values: every finite
        // float, drawn from a fixed stream so a failure repeats.
        var rng = new Random(20260924);
        for (int i = 0; i < 2000; i++)
        {
            float value = BitConverter.Int32BitsToSingle(rng.Next(int.MinValue, int.MaxValue));
            if (float.IsFinite(value))
                RoundTrips(value);
        }
    }

    [Fact]
    public void TheDtosLiveInLogicSave_AndNoGameplayTypeCarriesAnAttribute()
    {
        // PHASE5-028, -029: "a DTO layer in Logic/Save/ — the gameplay types must
        // not carry serialization attributes, or Logic/ stops being clean". The
        // DTOs are the records the document is made of, found from SaveFile down;
        // each declares the Logic namespace, as every Logic/ file does, so the
        // purity guards cover them.
        var dtos = new HashSet<Type>();
        void Collect(Type type)
        {
            if (!type.Name.EndsWith("Dto", StringComparison.Ordinal) && type != typeof(SaveFile)) return;
            if (!dtos.Add(type)) return;
            foreach (var parameter in type.GetConstructors().Single().GetParameters())
                foreach (var inner in Constituents(parameter.ParameterType))
                    Collect(inner);
        }
        Collect(typeof(SaveFile));
        Type[] expected =
        [
            typeof(SaveFile), typeof(ViewDto), typeof(CampaignDto), typeof(LockDto), typeof(MemberDto), typeof(StatsDto), typeof(ActorDto),
            typeof(StatusDto), typeof(WeaponDto), typeof(EnchantmentDto), typeof(RunResultDto), typeof(ClawbackDto),
            // Phase 5.6: the visit in progress.
            typeof(VisitDto), typeof(XpSnapshotDto), typeof(MemberXpDto), typeof(SpoilDto), typeof(CarryDto), typeof(CountDto),
            typeof(ReactionUseDto), typeof(ActorRefDto), typeof(FloorDto), typeof(EnemyDto), typeof(DropDto), typeof(LooseItemDto),
        ];
        Assert.Equal(expected.Select(t => t.Name).Order(StringComparer.Ordinal), dtos.Select(t => t.Name).Order(StringComparer.Ordinal));
        Assert.All(dtos, dto =>
        {
            Assert.Equal("GameEngine.DistanceRPG.Logic", dto.Namespace);
            Assert.True(dto.IsSealed && dto.GetMethod("<Clone>$") is not null, $"{dto.Name} is a sealed record");
        });

        // No DTO holds a gameplay type: plain values, lists and tables of them, and other DTOs.
        Type[] plain = [typeof(int), typeof(float), typeof(bool), typeof(string), typeof(int?), typeof(long)];   // long: a visit's map seed (5.6)
        var offenders = dtos
            .SelectMany(dto => dto.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (Where: $"{dto.Name}.{p.Name}", p.PropertyType)))
            .Where(p => Constituents(p.PropertyType).Any(t => !plain.Contains(t) && !dtos.Contains(t)
                && !(t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) || t.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)))))
            .Select(p => p.Where)
            .ToArray();
        Assert.Equal([], offenders);

        // And the only serialisation attributes in the game sit on the save's
        // context: none on a DTO, none on a gameplay type, none on a member.
        static bool IsJson(CustomAttributeData attribute) => attribute.AttributeType.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true;
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var carriers = new List<string>();
        foreach (var type in TestIl.Types(typeof(SaveFile).Assembly))
        {
            if (type.GetCustomAttributesData().Any(IsJson))
                carriers.Add(type.FullName!);
            var members = type.GetMembers(All);
            carriers.AddRange(members.Where(m => m.GetCustomAttributesData().Any(IsJson)).Select(m => $"{type.FullName}.{m.Name}"));
            carriers.AddRange(members.OfType<MethodBase>().SelectMany(m => m.GetParameters())
                .Where(p => p.GetCustomAttributesData().Any(IsJson)).Select(p => $"{type.FullName}({p.Name})"));
        }
        Assert.Equal([typeof(SaveJsonContext).FullName!], carriers.Distinct().ToArray());
    }

    /// <summary>A type and every type it is built from — a list's element, a table's key and value, a nullable's underlying type.</summary>
    private static IEnumerable<Type> Constituents(Type type)
    {
        yield return type;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                foreach (var inner in Constituents(argument))
                    yield return inner;
    }
}

using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The shipped data files' texts, and edits to them made the way a playtester
/// makes them — in the text, beside the executable — for the content-file suites.
/// Every edit asserts the text it replaces is there exactly once, so an edit that
/// matched nothing can never pass as a file that loads.
/// </summary>
internal static class DataTexts
{
    /// <summary>The generator's files: exactly what ships beside the executable (<see cref="ShippedDataTests"/> pins the two together).</summary>
    public static ContentTexts Shipped => ContentFiles.WriteDefaults();

    /// <summary>The compiled catalogue's entries, in file order.</summary>
    public static IReadOnlyList<EnchantmentDef> Catalogue => ShippedContent.Enchantments.Enchantments;

    /// <summary><paramref name="text"/> with <paramref name="from"/>, which it holds exactly once, replaced by <paramref name="to"/>.</summary>
    public static string Edit(string text, string from, string to)
    {
        int at = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"'{from}' is not in the text");
        Assert.True(text.IndexOf(from, at + 1, StringComparison.Ordinal) < 0, $"'{from}' is in the text more than once");
        return string.Concat(text.AsSpan(0, at), to, text.AsSpan(at + from.Length));
    }

    /// <summary>
    /// <paramref name="text"/>, a generated content file, with <paramref name="from"/>
    /// replaced by <paramref name="to"/> inside the row whose id is
    /// <paramref name="id"/> alone — from its id to the row's own closing brace —
    /// where <paramref name="from"/> is there exactly once.
    /// </summary>
    public static string InRow(string text, string id, string from, string to)
    {
        int start = text.IndexOf($"\"id\": \"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"no row '{id}' in the text");
        int end = text.IndexOf("\n    }", start, StringComparison.Ordinal);   // a row closes four spaces in; a nested table closes deeper
        Assert.True(end >= 0, $"row '{id}' does not close");
        return string.Concat(text.AsSpan(0, start), Edit(text[start..end], from, to), text.AsSpan(end));
    }

    /// <summary><paramref name="text"/>, a generated content file, with the whole row whose id is <paramref name="id"/> taken out, and the comma that separated it with it.</summary>
    public static string WithoutRow(string text, string id)
    {
        int at = text.IndexOf($"\"id\": \"{id}\"", StringComparison.Ordinal);
        Assert.True(at >= 0, $"no row '{id}' in the text");
        int start = text.LastIndexOf("\n    {\n", at, StringComparison.Ordinal) + 1;   // the row's opening line
        int close = text.IndexOf("\n    }", at, StringComparison.Ordinal) + "\n    }".Length;
        if (text[close] == ',')
            return text.Remove(start, close + 2 - start);   // "},\n": a row with another after it
        int comma = text.LastIndexOf("},", start, StringComparison.Ordinal);   // the last row: the one before it loses its comma
        return string.Concat(text.AsSpan(0, comma + 1), text.AsSpan(comma + 2, start - comma - 2), text.AsSpan(close + 1));
    }

    /// <summary>An <c>enchantments.json</c> of <paramref name="entries"/>, as the generator writes one.</summary>
    public static string Enchantments(IEnumerable<EnchantmentDef> entries)
        => EnchantmentsJson.Write(new EnchantmentsData(entries.ToList()), ShippedContent.Tuning);

    /// <summary>The shipped <c>enchantments.json</c> with the entry <paramref name="id"/> changed.</summary>
    public static string Enchantments(string id, Func<EnchantmentDef, EnchantmentDef> change)
    {
        Assert.Contains(Catalogue, e => e.Id == id);
        return Enchantments(Catalogue.Select(e => e.Id == id ? change(e) : e));
    }

    /// <summary>A fresh empty folder under the system's temp folder, deleted with everything in it once <paramref name="use"/> is done.</summary>
    public static void InTempFolder(Action<string> use)
    {
        var folder = Directory.CreateTempSubdirectory("distancerpg-");
        try
        {
            use(folder.FullName);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}

/// <summary>
/// The content files' load (§5.4; 1h): the relations and the catalogue read from
/// files beside the executable, in §5.4's order, a file that is not there played
/// as its compiled record and said so, a file that is there read whole, a broken
/// one stopping the game with the first stage to break stamped with its file, and
/// a number <c>tuning.json</c> owns edited in the wrong file said on screen. Every
/// case hands the loader text; <c>ContentFiles.Load</c> builds content it never
/// makes current, so nothing here swaps the current content, and the one case
/// that reads a folder reads a temporary one of its own.
/// </summary>
public class ContentFilesTests
{
    private static readonly ContentTexts Shipped = DataTexts.Shipped;

    private static (string? File, bool Compiled, string Entry, string Rule) Stamp(ContentException error)
        => (error.File, error.Compiled, error.EntryId, error.Rule);

    // A syntax error each: a missing comma between two groups, and between two fields.
    private static readonly string RestrictedSyntax = DataTexts.Edit(Shipped.Restricted!, "[\"Push\", \"Drag\", \"Rout\"],", "[\"Push\", \"Drag\", \"Rout\"]");
    private static readonly string EnchantmentsSyntax = DataTexts.Edit(Shipped.Enchantments!, "\"id\": \"vampiric\",", "\"id\": \"vampiric\"");

    // A semantic error each: a group naming an id twice, and a trigger of 0.
    private static readonly string RestrictedSelfExclusion = DataTexts.Edit(Shipped.Restricted!, "[\"Push\", \"Drag\", \"Rout\"]", "[\"Push\", \"Drag\", \"Rout\", \"Push\"]");
    private static readonly string EnchantmentsZeroTrigger = DataTexts.Enchantments("vampiric", e => e with { Trigger = 0 });

    // A typo in a relation (semantic: the id resolves to nothing), and one in an effect (the file's shape).
    private static readonly string RestrictedTypo = DataTexts.Edit(Shipped.Restricted!, "\"Riposte\": [\"Block\"]", "\"Riposte\": [\"Blok\"]");
    private static readonly string EnchantmentsUnknownEffect = DataTexts.Edit(Shipped.Enchantments!, "\"effect\": \"Vampiric\"", "\"effect\": \"Vampric\"");

    // A syntax error in the weapons (a missing comma between two fields), and a member the roster's shape does not have.
    private static readonly string WeaponsSyntax = DataTexts.Edit(Shipped.Weapons!, "\"id\": \"halberd\",", "\"id\": \"halberd\"");
    private static readonly string PartyUnknownMember = DataTexts.Edit(Shipped.Party!, "\"id\": \"B\",", "\"id\": \"B\",\n      \"title\": \"Sir\",");

    // Two dungeons that retire on clear: the Spear Hall pinned to floor 5 as well, so it breaks nothing else first.
    private static readonly string TwoRetiring = DataTexts.InRow(
        DataTexts.InRow(Shipped.Dungeons!, "spear_hall", "\"bossFloorMin\": null,\n      \"bossFloorMax\": null,", "\"bossFloorMin\": 5,\n      \"bossFloorMax\": 5,"),
        "spear_hall", "\"retiresOnClear\": false", "\"retiresOnClear\": true");

    private static ContentException Fails(ContentTexts texts)
    {
        var load = ContentFiles.Load(texts);
        Assert.Null(load.Content);
        return load.Error ?? throw new Xunit.Sdk.XunitException("the load has neither content nor an error");
    }

    [Fact]
    public void TheFailureReportedIsTheFirstStageToBreak()
    {
        // PHASE5-099, re-pinning -121: tuning -> restricted -> enchantments -> weapons
        // -> dungeons -> party. Every present file is parsed first, its shape failure
        // held back, and then the stages run in order, each reporting its own file's
        // shape before anything reads it: restricted.json's shape, enchantments.json's
        // shape, restricted.json's semantics, enchantments.json's semantics, then the
        // weapons, the dungeons, the roster. So a later file's syntax never pre-empts
        // an earlier file's semantics...

        // Two syntax errors: restricted.json's shape is the first stage.
        Assert.Equal((RestrictedJson.FileName, false, RestrictedJson.FileName, ContentValidator.RuleFileShape),
            Stamp(Fails(Shipped with { Restricted = RestrictedSyntax, Enchantments = EnchantmentsSyntax })));

        // A semantic error in each: restricted.json's own checks run before the
        // catalogue is built against them.
        Assert.Equal((RestrictedJson.FileName, false, "Push", ContentValidator.RuleSelfExclusion),
            Stamp(Fails(Shipped with { Restricted = RestrictedSelfExclusion, Enchantments = EnchantmentsZeroTrigger })));

        // A shape error in restricted.json beside a semantic one in enchantments.json: restricted.json.
        Assert.Equal((RestrictedJson.FileName, false, RestrictedJson.FileName, ContentValidator.RuleFileShape),
            Stamp(Fails(Shipped with { Restricted = RestrictedSyntax, Enchantments = EnchantmentsZeroTrigger })));

        // ...and the one order a reader might not expect is the rule, not an
        // accident: enchantments.json's shape is reported before restricted.json's
        // semantics, because restricted.json's ids are checked against the ids the
        // enchantments declare, which a file that did not parse has none of.
        Assert.Equal((EnchantmentsJson.FileName, false, "vampiric", ContentValidator.RuleUnknownEffect),
            Stamp(Fails(Shipped with { Restricted = RestrictedTypo, Enchantments = EnchantmentsUnknownEffect })));
        Assert.Equal((EnchantmentsJson.FileName, false, EnchantmentsJson.FileName, ContentValidator.RuleFileShape),
            Stamp(Fails(Shipped with { Restricted = RestrictedSelfExclusion, Enchantments = EnchantmentsSyntax })));

        // The weapons, the dungeons and the roster follow the same rule: a later
        // file's shape never pre-empts an earlier file's semantics. A syntax error
        // in weapons.json beside restricted.json's own semantic error, and an
        // unknown member in party.json beside a typo in a relation, both report
        // restricted.json, which is checked before anything reads it (S:207-209);
        // and a dungeons.json that is not JSON waits behind a weapon the
        // relations refuse.
        Assert.Equal((RestrictedJson.FileName, false, "Push", ContentValidator.RuleSelfExclusion),
            Stamp(Fails(Shipped with { Restricted = RestrictedSelfExclusion, Weapons = WeaponsSyntax })));
        Assert.Equal((RestrictedJson.FileName, false, "Blok", ContentValidator.RuleUnknownId),
            Stamp(Fails(Shipped with { Restricted = RestrictedTypo, Party = PartyUnknownMember })));
        string dungeonsSyntax = DataTexts.Edit(Shipped.Dungeons!, "\"id\": \"tutorial\",", "\"id\": \"tutorial\"");
        Assert.Equal((ContentFiles.WeaponsFileName, false, "arming_sword", ContentValidator.RuleForgedNotAllowed),
            Stamp(Fails(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, "[\"Light\", \"Resonant\"],", "[\"Light\", \"Resonant\"],\n    [\"Push\", \"Block\"],"), Dungeons = dungeonsSyntax })));

        // Each alone is what it is, so the pairs above are decided by the order.
        Assert.Equal((RestrictedJson.FileName, false, "Blok", ContentValidator.RuleUnknownId), Stamp(Fails(Shipped with { Restricted = RestrictedTypo })));
        Assert.Equal((EnchantmentsJson.FileName, false, "vampiric", ContentValidator.RuleZeroTrigger), Stamp(Fails(Shipped with { Enchantments = EnchantmentsZeroTrigger })));
        Assert.Equal((EnchantmentsJson.FileName, false, EnchantmentsJson.FileName, ContentValidator.RuleFileShape), Stamp(Fails(Shipped with { Enchantments = EnchantmentsSyntax })));
        Assert.Equal((ContentFiles.WeaponsFileName, false, ContentFiles.WeaponsFileName, ContentValidator.RuleFileShape), Stamp(Fails(Shipped with { Weapons = WeaponsSyntax })));
        Assert.Equal((ContentFiles.PartyFileName, false, "B", ContentValidator.RuleFileShape), Stamp(Fails(Shipped with { Party = PartyUnknownMember })));
        Assert.Equal((ContentFiles.DungeonsFileName, false, ContentFiles.DungeonsFileName, ContentValidator.RuleFileShape), Stamp(Fails(Shipped with { Dungeons = dungeonsSyntax })));

        // Whatever else the load had to say before the failure still comes back
        // beside it: the tuning's notice, for the screen and the log.
        var both = ContentFiles.Load(Shipped with { Tuning = """{ "MovementUnitsPerMana": 0 }""", Restricted = RestrictedTypo });
        Assert.Equal(["tuning.json: MovementUnitsPerMana 0 is below 1 - using 1"], both.Notices);
        Assert.Equal(["MovementUnitsPerMana 1"], both.TuningOverrides);
        Assert.Equal(RestrictedJson.FileName, both.Error!.File);
    }

    [Fact]
    public void AnAbsentFileIsTheCompiledOne_AndSaysSo_APresentOneIsReadWhole()
    {
        // 1h: no enchantments.json beside the game — the compiled catalogue plays,
        // one notice says so, and the edge's log line names the path it looked in.
        DataTexts.InTempFolder(folder =>
        {
            DataFiles.Write(folder, Shipped with { Enchantments = null });
            var report = new List<string>();
            var texts = DataFiles.Read(folder, report.Add);
            Assert.Null(texts.Enchantments);
            Assert.Contains($"[Data] {Path.Combine(folder, EnchantmentsJson.FileName)} is missing - the built-in one plays", report);

            var load = ContentFiles.Load(texts);
            Assert.Equal(["enchantments.json is missing - the built-in enchantments are playing"], load.Notices);
            Assert.Equal(DataTexts.Catalogue.Select(e => e.Id), load.Content!.Enchantments.All.Select(e => e.Id));
        });

        // The relations likewise, and one that is there but cannot be read says why
        // instead of calling itself missing.
        Assert.Equal(["restricted.json is missing - the built-in relations are playing"], ContentFiles.Load(Shipped with { Restricted = null }).Notices);
        var locked = ContentFiles.Load(Shipped with
        {
            Restricted = null,
            Unreadable = new Dictionary<string, string> { [RestrictedJson.FileName] = "another program has it open" },
        });
        Assert.Equal(["restricted.json could not be read (another program has it open) - the built-in relations are playing"], locked.Notices);
        Assert.NotNull(locked.Content);

        // A present file is read whole, never merged entry by entry with the
        // compiled record: an entry it edits is the file's, one it adds is there...
        var leech = new EnchantmentDef("leech", "Leech", EffectKind.Vampiric, TargetSide.Ally, Lock: 25, Trigger: 3, Potency: 2,
            ApplyPercent: 100, Applies: null, DamageType: null, Unique: false);
        var edited = ContentFiles.Load(Shipped with
        {
            Enchantments = DataTexts.Enchantments(DataTexts.Catalogue.Select(e => e.Id == "vampiric" ? e with { Lock = 35 } : e).Append(leech)),
        });
        Assert.Empty(edited.Notices);
        Assert.Equal(35, edited.Content!.Enchantments["vampiric"].Lock);
        Assert.Equal(leech, edited.Content.Enchantments["leech"]);

        // ...and one it leaves out is gone, not filled in from the compiled
        // catalogue: the drop pool names echoing, so the load stops on it.
        var without = Fails(Shipped with { Enchantments = DataTexts.Enchantments(DataTexts.Catalogue.Where(e => e.Id != "echoing")) });
        Assert.Equal((EnchantmentsJson.FileName, false, "echoing", ContentValidator.RuleDropPoolResolves), Stamp(without));
    }

    [Fact]
    public void ATuningTypoRunsTheGame_AContentTypoStopsIt()
    {
        // PHASE5-086, -097: the same figure out of range is a bad balance number in
        // tuning.json — clamped, said, and the game runs — and a broken invariant in
        // a content file, which "every downstream caller assumes holds": the game
        // does not start, and the screen says which file, which entry, which rule.
        var tuned = ContentFiles.Load(Shipped with { Tuning = """{ "StartingPool": 0 }""" });
        Assert.Null(tuned.Error);
        Assert.Equal(1, tuned.Content!.Tuning.StartingPool);
        Assert.Equal(["tuning.json: StartingPool 0 is below 1 - using 1"], tuned.Notices);

        var locked = Fails(Shipped with { Enchantments = DataTexts.Enchantments("regeneration", e => e with { Lock = 0 }) });
        Assert.Equal((EnchantmentsJson.FileName, false, "regeneration", ContentValidator.RuleEnchantmentLockIsPositive), Stamp(locked));
        Assert.Equal("lock 0", locked.Detail);
    }

    [Fact]
    public void AFailureNamesItsFile()
    {
        // 1h: every stage stamps its failure with its file (ContentException.File),
        // marked Compiled when the stage ran the built-in record because no file was
        // found — and then it was a file beside it that broke it.
        string noBurning = DataTexts.Enchantments(DataTexts.Catalogue.Where(e => e.Id != "burning"));
        string elementsTangled = DataTexts.Edit(Shipped.Restricted!, "[\"flaming\", \"cold\"]", "[\"flaming\", \"cold\", \"shocking\"]");
        string pushBlocks = DataTexts.Edit(Shipped.Restricted!, "[\"Push\", \"Drag\", \"Rout\"],", "[\"Push\", \"Drag\", \"Rout\"],\n    [\"Push\", \"Block\"],");
        string unknownField = DataTexts.Edit(Shipped.Enchantments!, "\"id\": \"vampiric\",", "\"id\": \"vampiric\",\n      \"condition\": \"OnHit\",");

        (ContentTexts Texts, (string File, bool Compiled, string Entry, string Rule) Expected)[] cases =
        [
            (Shipped with { Restricted = RestrictedSyntax }, (RestrictedJson.FileName, false, RestrictedJson.FileName, ContentValidator.RuleFileShape)),
            (Shipped with { Enchantments = unknownField }, (EnchantmentsJson.FileName, false, "vampiric", ContentValidator.RuleFileShape)),
            (Shipped with { Restricted = RestrictedTypo }, (RestrictedJson.FileName, false, "Blok", ContentValidator.RuleUnknownId)),
            // No restricted.json, and an enchantments.json with no burning: the built-in relations name it.
            (Shipped with { Restricted = null, Enchantments = noBurning }, (RestrictedJson.FileName, true, "burning", ContentValidator.RuleUnknownId)),
            (Shipped with { Enchantments = EnchantmentsZeroTrigger }, (EnchantmentsJson.FileName, false, "vampiric", ContentValidator.RuleZeroTrigger)),
            // No enchantments.json, and a restricted.json opposing shocking to flaming as well: the built-in elements break against it.
            (Shipped with { Enchantments = null, Restricted = elementsTangled }, (EnchantmentsJson.FileName, true, "flaming", ContentValidator.RuleOpposition)),
            // No weapons.json, and a restricted.json that makes a built-in weapon illegal: the built-in weapons break against it.
            (Shipped with { Restricted = pushBlocks, Weapons = null }, (ContentFiles.WeaponsFileName, true, "arming_sword", ContentValidator.RuleForgedNotAllowed)),
            // The same edit beside the shipped weapons.json: the file's weapon, stamped with the file.
            (Shipped with { Restricted = pushBlocks }, (ContentFiles.WeaponsFileName, false, "arming_sword", ContentValidator.RuleForgedNotAllowed)),
            // The dungeons' and the roster's files, broken each on its own: two dungeons that retire, a member's weapon that is not there.
            (Shipped with { Dungeons = TwoRetiring }, (ContentFiles.DungeonsFileName, false, "spear_hall", ContentValidator.RuleOneRetiringDungeon)),
            (Shipped with { Party = DataTexts.Edit(Shipped.Party!, "\"startingWeaponId\": \"weakspot_stiletto\"", "\"startingWeaponId\": \"weakspot_stilleto\"") },
                (ContentFiles.PartyFileName, false, "A", ContentValidator.RulePartyWeaponExists)),
            // No party.json, and a weapons.json that renames the weapon the built-in roster hands A: the built-in roster breaks against it.
            (Shipped with { Party = null, Weapons = DataTexts.Edit(Shipped.Weapons!, "\"id\": \"weakspot_stiletto\",", "\"id\": \"weakspot_dirk\",") },
                (ContentFiles.PartyFileName, true, "A", ContentValidator.RulePartyWeaponExists)),
        ];
        foreach (var (texts, expected) in cases)
            Assert.Equal(expected, Stamp(Fails(texts)));

        // The weapon an edit to the relations makes illegal names both (S:220-224):
        // the weapon is the entry, and the detail ends with the relation that
        // refused it, as restricted.json writes it.
        Assert.EndsWith("- restricted.json excludes [Push, Block]: Push is already forged here", Fails(Shipped with { Restricted = pushBlocks }).Detail);

        // The opposition failure reads restricted.json at the enchantments' stage,
        // and says so in its detail.
        Assert.Contains(RestrictedJson.FileName, Fails(Shipped with { Enchantments = null, Restricted = elementsTangled }).Detail);

        // One cross-file case: a restricted.json whose neverRolled forgets burning
        // fails at the enchantments' stage — the unique flag and neverRolled are one
        // question, asked where the catalogue is built — stamped enchantments.json,
        // its detail naming restricted.json.
        var forgot = Fails(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, ", \"burning\"]", "]") });
        Assert.Equal((EnchantmentsJson.FileName, false, "burning", ContentValidator.RuleUniqueNeverRolled), Stamp(forgot));
        Assert.Equal("unique, but absent from neverRolled in restricted.json", forgot.Detail);

        // The dungeons' and the roster's stages stamp theirs the same way when they
        // are handed to the staged load directly, as records a file would read to.
        ContentSource<T> Read<T>(string file, T data) where T : class => new(file, data, null, Compiled: false);
        var tuning = ShippedContent.Tuning;
        var restricted = ContentSource<RestrictedData>.Built(RestrictedJson.FileName, ShippedContent.Restricted);
        var enchantments = ContentSource<EnchantmentsData>.Built(EnchantmentsJson.FileName, ShippedContent.Enchantments);
        var weapons = ContentSource<WeaponsData>.Built(ContentFiles.WeaponsFileName, ShippedContent.Weapons);
        var dungeons = ContentSource<DungeonsData>.Built(ContentFiles.DungeonsFileName, ShippedContent.Dungeons);
        var party = ContentSource<PartyData>.Built(ContentFiles.PartyFileName, ShippedContent.Party);

        var noDungeons = Assert.Throws<ContentException>(() => GameContent.Load(tuning, restricted, enchantments, weapons,
            Read(ContentFiles.DungeonsFileName, new DungeonsData([])), party));
        Assert.Equal((ContentFiles.DungeonsFileName, false, "dungeons", ContentValidator.RuleOneRetiringDungeon), Stamp(noDungeons));

        var threeMembers = Assert.Throws<ContentException>(() => GameContent.Load(tuning, restricted, enchantments, weapons, dungeons,
            Read(ContentFiles.PartyFileName, new PartyData(ShippedContent.Party.Members.Take(3).ToList()))));
        Assert.Equal((ContentFiles.PartyFileName, false, "party", ContentValidator.RulePartySize), Stamp(threeMembers));

        // A parse failure held back is reported at its own stage, stamped, and
        // nothing past that stage is read.
        var held = new ContentException("dungeons.json", ContentValidator.RuleFileShape, "dungeons.json: the top level: the file is empty");
        var reported = Assert.Throws<ContentException>(() => GameContent.Load(tuning, restricted, enchantments, weapons,
            new ContentSource<DungeonsData>(ContentFiles.DungeonsFileName, null, held, Compiled: false),
            Read(ContentFiles.PartyFileName, new PartyData([]))));
        Assert.Equal((ContentFiles.DungeonsFileName, false, "dungeons.json", ContentValidator.RuleFileShape), Stamp(reported));
        Assert.Same(held, reported.InnerException);

        // The public load, which every test and the compiled content call, wraps its
        // records as built in code: the same failure, in the same order, stamped
        // with the stage's file and marked compiled.
        var publicLoad = Assert.Throws<ContentException>(() => GameContent.Load(tuning, ShippedContent.Restricted, ShippedContent.Enchantments,
            ShippedContent.Weapons, new PartyData([])));
        Assert.Equal((ContentFiles.PartyFileName, true, "party", ContentValidator.RulePartySize), Stamp(publicLoad));
        Assert.StartsWith("party.json (compiled): Invalid content entry 'party': ", publicLoad.Message, StringComparison.Ordinal);

        // A validator asked on its own belongs to no stage and names no file.
        var alone = Assert.Throws<ContentException>(() => ContentValidator.ValidateEnchantments(
            new EnchantmentsData(DataTexts.Catalogue.Select(e => e.Id == "vampiric" ? e with { Trigger = 0 } : e).ToList())));
        Assert.Equal((null, false, "vampiric", ContentValidator.RuleZeroTrigger), Stamp(alone));
    }

    [Fact]
    public void RelationsFromAFileNeedNoRecompile()
    {
        // PHASE5-091: "adding a modifier is: implement the behaviour, price it,
        // declare its relations — and none of that last part is a recompile." The
        // crit riders are a group by the shipped file, so a dagger cannot hold
        // CritSunder beside CritWeaken; take the group out of restricted.json and
        // the next start plays without it — settled.md's "revisit by ungrouping".
        var shipped = ContentFiles.Load(Shipped).Content!;
        var crit = ModifierSet.Of((CritWeaken, 1));
        Assert.False(shipped.Modifiers.Allowed(CritSunder, WeaponKind.Melee, crit));

        var ungrouped = ContentFiles.Load(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, "    [\"CritWeaken\", \"CritSunder\"],\n", "") });
        Assert.Empty(ungrouped.Notices);
        Assert.True(ungrouped.Content!.Modifiers.Allowed(CritSunder, WeaponKind.Melee, crit));
        Assert.True(ungrouped.Content.Modifiers.Allowed(CritWeaken, WeaponKind.Melee, ModifierSet.Of((CritSunder, 1))));

        // And a relation added is the same edit the other way: Pin and Softening,
        // which no weapon carries together, grouped in one line.
        var pin = ModifierSet.Of((Pin, 1));
        Assert.True(shipped.Modifiers.Allowed(Softening, WeaponKind.Ranged, pin));
        var grouped = ContentFiles.Load(Shipped with
        {
            Restricted = DataTexts.Edit(Shipped.Restricted!, "[\"Light\", \"Resonant\"],", "[\"Light\", \"Resonant\"],\n    [\"Pin\", \"Softening\"],"),
        });
        Assert.Empty(grouped.Notices);
        Assert.False(grouped.Content!.Modifiers.Allowed(Softening, WeaponKind.Ranged, pin));
        Assert.False(grouped.Content.Modifiers.Allowed(Pin, WeaponKind.Ranged, ModifierSet.Of((Softening, 1))));
    }

    [Fact]
    public void AnApplyPercentForNoEnchantmentWarnsOnceTheCatalogueLoads()
    {
        // 1g: ApplyPercent is keyed by enchantment id, which only the loaded
        // catalogue can answer for — the file's, now. A dial for an id no entry has
        // warns and is dropped, so the tuning the game hashes holds only numbers
        // something reads...
        const string dial = """{ "ApplyPercent": { "frost_rune": 30 } }""";
        var stray = ContentFiles.Load(Shipped with { Tuning = dial });
        Assert.Equal(["tuning.json: ApplyPercent.frost_rune names no enchantment - ignored"], stray.Notices);
        Assert.False(stray.Content!.Tuning.ApplyPercent.ContainsKey("frost_rune"));

        // ...and the same dial beside an enchantments.json that adds the entry is
        // read, and plays.
        var rune = new EnchantmentDef("frost_rune", "Frost Rune", EffectKind.Shattering, TargetSide.Enemy, Lock: 25, Trigger: 10, Potency: 1,
            ApplyPercent: 30, Applies: null, DamageType: null, Unique: false);
        var added = ContentFiles.Load(Shipped with { Tuning = dial, Enchantments = DataTexts.Enchantments(DataTexts.Catalogue.Append(rune)) });
        Assert.Empty(added.Notices);
        Assert.Equal(30, added.Content!.Tuning.ApplyPercent["frost_rune"]);
        Assert.Equal(30, added.Content.Enchantments["frost_rune"].ApplyPercent);
        Assert.Contains("ApplyPercent.frost_rune 30", added.TuningOverrides);

        // An enchantments.json that does not parse is no catalogue to ask: its own
        // failure is what the screen says, and no dial is called a stray.
        var broken = ContentFiles.Load(Shipped with { Tuning = dial, Enchantments = EnchantmentsSyntax });
        Assert.Equal(EnchantmentsJson.FileName, broken.Error!.File);
        Assert.Empty(broken.Notices);
    }

    [Fact]
    public void AnEnchantmentsEditToADialledValueSaysTheTuningWins()
    {
        // 1h: one owner for a dialled number — the dial. Flaming's applyPercent is
        // tuning.json's ApplyPercent.flaming, so an edit to the row cannot take
        // effect: the catalogue still plays 20, and the screen says so, naming both
        // files (1i: OVERRIDDEN BY TUNING.JSON APPLYPERCENT.FLAMING 20).
        var edited = ContentFiles.Load(Shipped with { Enchantments = DataTexts.Enchantments("flaming", e => e with { ApplyPercent = 30 }) });
        Assert.Equal(20, edited.Content!.Enchantments["flaming"].ApplyPercent);
        Assert.Equal(["enchantments.json flaming.applyPercent 30 is overridden by tuning.json ApplyPercent.flaming 20"], edited.Notices);
        Assert.Contains("OVERRIDDEN BY TUNING.JSON APPLYPERCENT.FLAMING 20", string.Join(" ", NoticeText.Rows(edited.Notices).Select(row => row.Trim())));

        // The dial turned in tuning.json with the row untouched: the dial plays,
        // and there is nothing to say. Nor when the row is turned to agree with it.
        var turned = ContentFiles.Load(Shipped with { Tuning = """{ "ApplyPercent": { "flaming": 30 } }""" });
        Assert.Equal(30, turned.Content!.Enchantments["flaming"].ApplyPercent);
        Assert.Empty(turned.Notices);
        var agreed = ContentFiles.Load(Shipped with
        {
            Tuning = """{ "ApplyPercent": { "flaming": 30 } }""",
            Enchantments = DataTexts.Enchantments("flaming", e => e with { ApplyPercent = 30 }),
        });
        Assert.Empty(agreed.Notices);

        // Arcane's potency is ArcanePotency's the same way.
        var arcane = ContentFiles.Load(Shipped with { Enchantments = DataTexts.Enchantments("arcane", e => e with { Potency = 9 }) });
        Assert.Equal(4, arcane.Content!.Enchantments["arcane"].Potency);
        Assert.Equal(["enchantments.json arcane.potency 9 is overridden by tuning.json ArcanePotency 4"], arcane.Notices);

        // A row no dial names plays its own number, and an edit to it is simply the edit.
        var own = ContentFiles.Load(Shipped with { Enchantments = DataTexts.Enchantments("regeneration", e => e with { ApplyPercent = 150 }) });
        Assert.Equal(150, own.Content!.Enchantments["regeneration"].ApplyPercent);
        Assert.Empty(own.Notices);

        // And the shipped pair says nothing at all.
        Assert.Empty(ContentFiles.Load(Shipped).Notices);
    }

    [Fact]
    public void AllFiveFilesTogether()
    {
        // PHASE5-085: five files hold content — restricted.json, enchantments.json,
        // weapons.json, dungeons.json and party.json — read beside tuning.json in
        // §5.4's order. The shipped set, read from a folder as the game reads the
        // one beside the executable, loads with nothing to say.
        Assert.Equal([TuningJson.FileName, RestrictedJson.FileName, EnchantmentsJson.FileName, WeaponsJson.FileName, DungeonsJson.FileName, PartyJson.FileName],
            ContentFiles.FileNames);
        var texts = DataFiles.Read(ShippedDataTests.ShippedFolder);
        Assert.All(new[] { texts.Tuning, texts.Restricted, texts.Enchantments, texts.Weapons, texts.Dungeons, texts.Party }, text => Assert.NotNull(text));
        var load = ContentFiles.Load(texts);
        Assert.Null(load.Error);
        Assert.Empty(load.Notices);
        Assert.Empty(load.TuningOverrides);

        // Each catalogue the game plays re-writes to the committed file: what the
        // files say is what the game holds, row for row and in their order.
        static string Lf(string? text) => text!.Replace("\r\n", "\n", StringComparison.Ordinal);
        var content = load.Content!;
        Assert.Equal(Lf(texts.Restricted), RestrictedJson.Write(RestrictedJson.Read(texts.Restricted!)));
        Assert.Equal(Lf(texts.Enchantments), EnchantmentsJson.Write(new EnchantmentsData(content.Enchantments.All), content.Tuning));
        Assert.Equal(Lf(texts.Weapons), WeaponsJson.Write(ShippedContent.Weapons with { Weapons = content.Weapons.All }));
        Assert.Equal(Lf(texts.Dungeons), DungeonsJson.Write(new DungeonsData(content.Dungeons.All)));
        Assert.Equal(Lf(texts.Party), PartyJson.Write(new PartyData(content.Party.All)));

        // Content is an edit to a file beside the game: a statline, a dungeon's
        // name and a member's loadout are each changed in their own file, and the
        // content that loads is the edited one — 1i's playtest: the Weakspot
        // Stiletto's damage 25.
        var edited = ContentFiles.Load(Shipped with
        {
            Weapons = DataTexts.InRow(Shipped.Weapons!, "weakspot_stiletto", "\"damage\": 15,", "\"damage\": 25,"),
            Dungeons = DataTexts.Edit(Shipped.Dungeons!, "\"name\": \"The Spear Hall\"", "\"name\": \"The Pike Hall\""),
            Party = DataTexts.InRow(Shipped.Party!, "D", "\"bagWeaponIds\": [\"staff_of_mire\"]", "\"bagWeaponIds\": [\"staff_of_blight\"]"),
        });
        Assert.Empty(edited.Notices);
        Assert.Equal(25, edited.Content!.Weapons["weakspot_stiletto"].Damage);
        Assert.Equal(15, edited.Content.Weapons["assassins_fang"].Damage);
        Assert.Equal("The Pike Hall", edited.Content.Dungeons["spear_hall"].Name);
        Assert.Equal(["staff_of_blight"], edited.Content.Party["D"].BagWeaponIds);

        // PHASE5-090: "which weapons and dungeons use it?" — "it" a modifier or an
        // enchantment, beside "what does it do?" (code) and "what may it coexist
        // with?" (restricted.json) — is the content files' to answer. So moving
        // one between rows is an edit to the files alone, and the content that
        // loads follows it. A modifier between weapons: Pin from the Pinning Lance
        // to the Halberd, and Push the other way, so each is still a Spear variant
        // adding one rider. An enchantment between weapons: the Staff of Mire's
        // innate to the Staff of Warding, and Ward the other way. A theme between
        // dungeons: the Long Gallery's Longshot to the Knife Warrens, and
        // CritWindow the other way, each boss carrying its new theme. And a
        // dungeon attuned: the Spear Hall to Cold, the cold enchantment's element.
        static string InRows(string text, params (string Id, string From, string To)[] edits)
            => edits.Aggregate(text, (done, edit) => DataTexts.InRow(done, edit.Id, edit.From, edit.To));
        var moved = ContentFiles.Load(Shipped with
        {
            Weapons = InRows(Shipped.Weapons!,
                ("halberd", "\"Push\": 1", "\"Pin\": 1"),
                ("pinning_lance", "\"Pin\": 1", "\"Push\": 1"),
                ("staff_of_warding", "{ \"id\": \"ward\" }", "{ \"id\": \"mire\" }"),
                ("staff_of_mire", "{ \"id\": \"mire\" }", "{ \"id\": \"ward\" }")),
            Dungeons = InRows(Shipped.Dungeons!,
                ("long_gallery", "\"theme\": \"Longshot\"", "\"theme\": \"CritWindow\""),
                ("long_gallery", "\"innate\": { \"Longshot\": 3 }", "\"innate\": { \"CritWindow\": 3 }"),
                ("knife_warrens", "\"theme\": \"CritWindow\"", "\"theme\": \"Longshot\""),
                ("knife_warrens", "\"innate\": { \"CritWindow\": 3 }", "\"innate\": { \"Longshot\": 3 }"),
                ("spear_hall", "\"attunement\": null", "\"attunement\": \"Cold\"")),
        });
        Assert.Null(moved.Error);
        Assert.Empty(moved.Notices);
        var after = moved.Content!;

        // Which weapons forge Pin and which carry mire or ward, which dungeon is
        // themed on Longshot or CritWindow, and what the Spear Hall is attuned to:
        // asked of the shipped content, then of the moved.
        static IEnumerable<string> Forging(GameContent c, ModifierType t) => c.Weapons.All.Where(w => w.Forged.GetValueOrDefault(t) > 0).Select(w => w.Id);
        static IEnumerable<string> Carrying(GameContent c, string id) => c.Weapons.All.Where(w => w.Enchantments.Any(r => r.Id == id)).Select(w => w.Id);
        static IEnumerable<string> ThemedOn(GameContent c, ModifierType t) => c.Dungeons.All.Where(d => d.Theme == t).Select(d => d.Id);
        Assert.Equal(["pinning_lance", "pinning_bow"], Forging(content, Pin));
        Assert.Equal(["halberd", "pinning_bow"], Forging(after, Pin));
        Assert.Equal((0, 1), (after.Weapons["halberd"].Forged.GetValueOrDefault(Push), after.Weapons["pinning_lance"].Forged.GetValueOrDefault(Push)));
        Assert.Equal(["staff_of_mire"], Carrying(content, "mire"));
        Assert.Equal(["staff_of_warding"], Carrying(after, "mire"));
        Assert.Equal(["staff_of_mire"], Carrying(after, "ward"));
        Assert.Equal(["long_gallery"], ThemedOn(content, Longshot));
        Assert.Equal(["knife_warrens"], ThemedOn(after, Longshot));
        Assert.Equal(["long_gallery"], ThemedOn(after, CritWindow));
        Assert.Equal(3, after.Dungeons["knife_warrens"].Boss.Innate[Longshot]);
        Assert.Null(content.Dungeons["spear_hall"].Attunement);
        Assert.Equal(DamageType.Cold, after.Dungeons["spear_hall"].Attunement);

        // A file that is missing plays its built-in record, and says so, for the
        // three new files as for the others.
        var missing = ContentFiles.Load(Shipped with { Weapons = null, Dungeons = null, Party = null });
        Assert.Equal(
        [
            "weapons.json is missing - the built-in weapons are playing",
            "dungeons.json is missing - the built-in dungeons are playing",
            "party.json is missing - the built-in party members are playing",
        ], missing.Notices);
        Assert.Equal(WeaponsJson.Write(ShippedContent.Weapons), WeaponsJson.Write(ShippedContent.Weapons with { Weapons = missing.Content!.Weapons.All }));
    }
}

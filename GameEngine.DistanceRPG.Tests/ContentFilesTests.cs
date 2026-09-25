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
    /// <summary>The master copies, as the build carries them (<see cref="ShippedData.Texts"/>): what ships beside the executable, and what plays when nothing does.</summary>
    public static ContentTexts Shipped => ShippedData.Texts;

    /// <summary><paramref name="text"/> with <paramref name="from"/>, which it holds exactly once, replaced by <paramref name="to"/>.</summary>
    public static string Edit(string text, string from, string to)
    {
        int at = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"'{from}' is not in the text");
        Assert.True(text.IndexOf(from, at + 1, StringComparison.Ordinal) < 0, $"'{from}' is in the text more than once");
        return string.Concat(text.AsSpan(0, at), to, text.AsSpan(at + from.Length));
    }

    /// <summary>
    /// <paramref name="text"/>, a shipped content file, with <paramref name="from"/>
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

    /// <summary><paramref name="text"/>, a shipped content file, with the whole row whose id is <paramref name="id"/> taken out, and the comma that separated it with it.</summary>
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

    /// <summary>
    /// <paramref name="text"/>, a <c>tuning.json</c>, with the one line of the
    /// top-level key <paramref name="key"/> taken out, whatever number it holds —
    /// so the edit names no number of the master's.
    /// </summary>
    public static string WithoutKey(string text, string key) => WithoutLine(text, 0, $"  \"{key}\": ");

    /// <summary><paramref name="text"/>, a <c>tuning.json</c>, with the line of <paramref name="member"/> inside the table <paramref name="table"/> taken out, whatever number it holds.</summary>
    public static string WithoutMember(string text, string table, string member)
    {
        int at = text.IndexOf($"\n  \"{table}\": {{\n", StringComparison.Ordinal);
        Assert.True(at >= 0, $"no table '{table}' in the text");
        return WithoutLine(text, at, $"    \"{member}\": ");
    }

    /// <summary><paramref name="text"/>, a <c>tuning.json</c>, with the top-level key <paramref name="key"/> set to <paramref name="raw"/>, written as given, in place of whatever number it holds.</summary>
    public static string WithKey(string text, string key, string raw)
    {
        var line = System.Text.RegularExpressions.Regex.Matches(text, $"\n  \"{key}\": [^,\n]*");
        Assert.True(line.Count == 1, $"key '{key}' is in the text {line.Count} times, not once");
        return string.Concat(text.AsSpan(0, line[0].Index), $"\n  \"{key}\": {raw}", text.AsSpan(line[0].Index + line[0].Length));
    }

    /// <summary><paramref name="text"/> with the first line after <paramref name="from"/> that starts <paramref name="start"/> taken out, its line end with it.</summary>
    private static string WithoutLine(string text, int from, string start)
    {
        int at = text.IndexOf("\n" + start, from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"no line '{start.Trim()}' in the text");
        int end = text.IndexOf('\n', at + 1);
        return text.Remove(at + 1, end - at);
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
/// The content files' load (§5.4; 1h): the relations, the weapons and the
/// dungeons read from files beside the executable, in the load order, beside
/// the game's own catalogue and starting party, which are code; a file that is
/// not there played as its built-in copy and said so, a file that is there read
/// whole, and a broken one — beside the executable or built in — stopping the
/// game with the first stage to break stamped with its file. Every case hands
/// the loader text; the load builds content it never makes current, so nothing
/// here swaps the current content, and the one case that reads a folder reads a
/// temporary one of its own.
/// </summary>
public class ContentFilesTests
{
    private static readonly ContentTexts Shipped = DataTexts.Shipped;

    private static (string? File, bool BuiltIn, string Entry, string Rule) Stamp(ContentException error)
        => (error.File, error.BuiltIn, error.EntryId, error.Rule);

    // A syntax error: a missing comma between two groups.
    private static readonly string RestrictedSyntax = DataTexts.Edit(Shipped.Restricted!, "[\"Push\", \"Drag\", \"Rout\"],", "[\"Push\", \"Drag\", \"Rout\"]");

    // A semantic error: a group naming an id twice.
    private static readonly string RestrictedSelfExclusion = DataTexts.Edit(Shipped.Restricted!, "[\"Push\", \"Drag\", \"Rout\"]", "[\"Push\", \"Drag\", \"Rout\", \"Push\"]");

    // A typo in a relation (semantic: the id resolves to nothing).
    private static readonly string RestrictedTypo = DataTexts.Edit(Shipped.Restricted!, "\"Riposte\": [\"Block\"]", "\"Riposte\": [\"Blok\"]");

    // A syntax error in the weapons (a missing comma between two fields).
    private static readonly string WeaponsSyntax = DataTexts.Edit(Shipped.Weapons!, "\"id\": \"halberd\",", "\"id\": \"halberd\"");

    // Two dungeons that retire on clear: the Spear Hall pinned to floor 5 as well, so it breaks nothing else first.
    private static readonly string TwoRetiring = DataTexts.InRow(
        DataTexts.InRow(Shipped.Dungeons!, "spear_hall", "\"bossFloorMin\": null,\n      \"bossFloorMax\": null,", "\"bossFloorMin\": 5,\n      \"bossFloorMax\": 5,"),
        "spear_hall", "\"retiresOnClear\": false", "\"retiresOnClear\": true");

    private static ContentException Fails(ContentTexts texts)
    {
        var load = ShippedData.Load(texts);
        Assert.Null(load.Content);
        return load.Error ?? throw new Xunit.Sdk.XunitException("the load has neither content nor an error");
    }

    [Fact]
    public void TheFailureReportedIsTheFirstStageToBreak()
    {
        // PHASE5-099, re-pinning -121: the game's catalogue -> tuning -> restricted
        // -> weapons -> dungeons -> the starting party. Every present file is parsed
        // first, its shape failure held back, and then the stages run in order, each
        // reporting its own file's shape before anything reads it: restricted.json's
        // shape, restricted.json's semantics (and the code's catalogue against them),
        // then the weapons, the dungeons, the code's roster. So a later file's syntax
        // never pre-empts an earlier file's semantics.

        // Two syntax errors: restricted.json's shape is the first stage.
        Assert.Equal((RestrictedJson.FileName, false, RestrictedJson.FileName, ContentValidator.RuleFileShape),
            Stamp(Fails(Shipped with { Restricted = RestrictedSyntax, Weapons = WeaponsSyntax })));

        // The weapons and the dungeons follow the same rule: a later file's shape
        // never pre-empts an earlier file's semantics. A syntax error in
        // weapons.json beside restricted.json's own semantic error reports
        // restricted.json, which is checked before anything reads it (S:207-209);
        // and a dungeons.json that is not JSON waits behind a weapon the
        // relations refuse.
        Assert.Equal((RestrictedJson.FileName, false, "Push", ContentValidator.RuleSelfExclusion),
            Stamp(Fails(Shipped with { Restricted = RestrictedSelfExclusion, Weapons = WeaponsSyntax })));
        string dungeonsSyntax = DataTexts.Edit(Shipped.Dungeons!, "\"id\": \"tutorial\",", "\"id\": \"tutorial\"");
        Assert.Equal((ContentFiles.WeaponsFileName, false, "arming_sword", ContentValidator.RuleForgedNotAllowed),
            Stamp(Fails(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, "[\"Light\", \"Resonant\"],", "[\"Light\", \"Resonant\"],\n    [\"Push\", \"Block\"],"), Dungeons = dungeonsSyntax })));

        // Each alone is what it is, so the pairs above are decided by the order.
        Assert.Equal((RestrictedJson.FileName, false, "Blok", ContentValidator.RuleUnknownId), Stamp(Fails(Shipped with { Restricted = RestrictedTypo })));
        Assert.Equal((ContentFiles.WeaponsFileName, false, ContentFiles.WeaponsFileName, ContentValidator.RuleFileShape), Stamp(Fails(Shipped with { Weapons = WeaponsSyntax })));
        Assert.Equal((ContentFiles.DungeonsFileName, false, ContentFiles.DungeonsFileName, ContentValidator.RuleFileShape), Stamp(Fails(Shipped with { Dungeons = dungeonsSyntax })));

        // Whatever else the load had to say before the failure still comes back
        // beside it: the tuning's notice, for the screen and the log.
        var both = ShippedData.Load(Shipped with { Tuning = """{ "MovementUnitsPerMana": 0 }""", Restricted = RestrictedTypo });
        Assert.Equal(["tuning.json: MovementUnitsPerMana 0 is below 1 - using 1"], both.Notices);
        Assert.Equal(["MovementUnitsPerMana 1"], both.TuningOverrides);
        Assert.Equal(RestrictedJson.FileName, both.Error!.File);
    }

    [Fact]
    public void AnAbsentFileIsTheBuiltInOne_AndSaysSo_APresentOneIsReadWhole()
    {
        // 1h: no weapons.json beside the game — the built-in weapons play, one
        // notice says so, and the edge's log line names the path it looked in.
        DataTexts.InTempFolder(folder =>
        {
            DataFiles.Write(folder, Shipped with { Weapons = null });
            var report = new List<string>();
            var texts = DataFiles.Read(folder, report.Add);
            Assert.Null(texts.Weapons);
            Assert.Contains($"[Data] {Path.Combine(folder, WeaponsJson.FileName)} is missing - the built-in one plays", report);

            var load = ShippedData.Load(texts);
            Assert.Equal(["weapons.json is missing - the built-in weapons are playing"], load.Notices);
            Assert.Equal(ShippedContent.Weapons.Weapons.Select(w => w.Id), load.Content!.Weapons.All.Select(w => w.Id));
        });

        // The relations likewise, and one that is there but cannot be read says why
        // instead of calling itself missing.
        Assert.Equal(["restricted.json is missing - the built-in relations are playing"], ShippedData.Load(Shipped with { Restricted = null }).Notices);
        var locked = ShippedData.Load(Shipped with
        {
            Restricted = null,
            Unreadable = new Dictionary<string, string> { [RestrictedJson.FileName] = "another program has it open" },
        });
        Assert.Equal(["restricted.json could not be read (another program has it open) - the built-in relations are playing"], locked.Notices);
        Assert.NotNull(locked.Content);

        // A present file is read whole, never merged entry by entry with the
        // built-in copy: a row it edits is the file's...
        var edited = ShippedData.Load(Shipped with { Weapons = DataTexts.InRow(Shipped.Weapons!, "hatchet", "\"damage\": 18,", "\"damage\": 20,") });
        Assert.Empty(edited.Notices);
        Assert.Equal(20, edited.Content!.Weapons["hatchet"].Damage);
        Assert.Equal(18, edited.Content.Weapons["great_axe"].Damage);

        // ...and one it leaves out is gone, not filled in from the built-in
        // weapons: the code names arming_sword as what a dummy holds, so the load
        // stops on it.
        var without = Fails(Shipped with { Weapons = DataTexts.WithoutRow(Shipped.Weapons!, "arming_sword") });
        Assert.Equal((ContentFiles.WeaponsFileName, false, "arming_sword", ContentValidator.RuleCodeNamedWeaponsExist), Stamp(without));
    }

    [Fact]
    public void AMissingFileIsTheBuiltInCopy_ABrokenBuiltInCopyStopsTheGame()
    {
        // The content rework, 1h: no weapons.json beside the game — the built-in
        // weapons play, the notice says so, and it is not among the files read
        // from beside the executable.
        var missing = ShippedData.Load(Shipped with { Weapons = null });
        Assert.Null(missing.Error);
        Assert.Equal(["weapons.json is missing - the built-in weapons are playing"], missing.Notices);
        Assert.Equal([RestrictedJson.FileName, DungeonsJson.FileName], missing.ReadBeside);
        Assert.Equal(ShippedContent.Weapons.Weapons.Select(w => w.Id), missing.Content!.Weapons.All.Select(w => w.Id));

        // A broken built-in weapons.json — a master copy edited and never tested —
        // with none beside the game: it plays, so the game stops on it, marked
        // built-in, with the notice that the file beside it was missing.
        var brokenBuiltIn = Shipped with { Weapons = WeaponsSyntax };
        var broken = ContentFiles.Load(Shipped with { Weapons = null }, brokenBuiltIn);
        Assert.Null(broken.Content);
        Assert.Equal((ContentFiles.WeaponsFileName, true, ContentFiles.WeaponsFileName, ContentValidator.RuleFileShape), Stamp(broken.Error!));
        Assert.Equal(["weapons.json is missing - the built-in weapons are playing"], broken.Notices);

        // A broken one beside the game and a sound built-in one: the one beside is
        // what stops the game — a present file is read whole, never swapped for the
        // built-in copy in silence (§5.4).
        var beside = ShippedData.Load(Shipped with { Weapons = WeaponsSyntax });
        Assert.Equal((ContentFiles.WeaponsFileName, false, ContentFiles.WeaponsFileName, ContentValidator.RuleFileShape), Stamp(beside.Error!));
        Assert.Empty(beside.Notices);

        // A sound one beside the game covers a broken built-in one, which is never
        // read: the game plays, with no error and nothing to say.
        var covered = ContentFiles.Load(Shipped, brokenBuiltIn);
        Assert.Null(covered.Error);
        Assert.Empty(covered.Notices);
        Assert.Empty(covered.TuningOverrides);
        Assert.Equal(ShippedContent.Weapons.Weapons.Select(w => w.Id), covered.Content!.Weapons.All.Select(w => w.Id));

        // And a built-in copy the build does not carry — no text, and why — with
        // none beside the game: the game stops on it, marked built-in, under the
        // rule that the game carries every file it plays.
        var lacking = Shipped with
        {
            Weapons = null,
            Unreadable = new Dictionary<string, string> { [WeaponsJson.FileName] = "the game was built without it" },
        };
        var notCarried = ContentFiles.Load(Shipped with { Weapons = null }, lacking);
        Assert.Null(notCarried.Content);
        Assert.Equal((ContentFiles.WeaponsFileName, true, ContentFiles.WeaponsFileName, ContentValidator.RuleBuiltInCopy), Stamp(notCarried.Error!));
        Assert.Equal("weapons.json (built-in): the game was built without it", notCarried.Error!.Detail);
    }

    [Fact]
    public void ATuningTypoRunsTheGame_AContentTypoStopsIt()
    {
        // PHASE5-086, -097: the same figure out of range is a bad balance number in
        // tuning.json — clamped, said, and the game runs — and a broken invariant in
        // a content file, which "every downstream caller assumes holds": the game
        // does not start, and the screen says which file, which entry, which rule.
        var tuned = ShippedData.Load(Shipped with { Tuning = """{ "StartingPool": 0 }""" });
        Assert.Null(tuned.Error);
        Assert.Equal(1, tuned.Content!.Tuning.StartingPool);
        Assert.Equal(["tuning.json: StartingPool 0 is below 1 - using 1"], tuned.Notices);

        var unstacked = Fails(Shipped with { Weapons = DataTexts.InRow(Shipped.Weapons!, "flensing_knife", "\"Light\": 1", "\"Light\": 0") });
        Assert.Equal((ContentFiles.WeaponsFileName, false, "flensing_knife", ContentValidator.RuleForgedStackCount), Stamp(unstacked));
        Assert.Equal("Light x0", unstacked.Detail);
    }

    [Fact]
    public void AFailureNamesItsFile()
    {
        // 1h: every stage stamps its failure with its file (ContentException.File),
        // marked BuiltIn when the stage ran the built-in copy because no file was
        // found — and then it was a file beside it that broke it, or the copy
        // itself. The game's catalogue and starting party are code, so a failure
        // against either is stamped with the file a playtester would fix:
        // restricted.json for the catalogue, weapons.json for the party.
        string elementsTangled = DataTexts.Edit(Shipped.Restricted!, "[\"flaming\", \"cold\"]", "[\"flaming\", \"cold\", \"shocking\"]");
        string noSuchSoul = DataTexts.Edit(Shipped.Restricted!, ", \"burning\"]", ", \"burning\", \"frostbite\"]");
        string pushBlocks = DataTexts.Edit(Shipped.Restricted!, "[\"Push\", \"Drag\", \"Rout\"],", "[\"Push\", \"Drag\", \"Rout\"],\n    [\"Push\", \"Block\"],");

        (ContentTexts Texts, (string File, bool BuiltIn, string Entry, string Rule) Expected)[] cases =
        [
            (Shipped with { Restricted = RestrictedSyntax }, (RestrictedJson.FileName, false, RestrictedJson.FileName, ContentValidator.RuleFileShape)),
            (Shipped with { Restricted = RestrictedTypo }, (RestrictedJson.FileName, false, "Blok", ContentValidator.RuleUnknownId)),
            // A restricted.json naming an enchantment the game's catalogue lacks: the code's catalogue against the file.
            (Shipped with { Restricted = noSuchSoul }, (RestrictedJson.FileName, false, "frostbite", ContentValidator.RuleUnknownId)),
            // A restricted.json opposing shocking to flaming as well: the code's elements break against it.
            (Shipped with { Restricted = elementsTangled }, (RestrictedJson.FileName, false, "flaming", ContentValidator.RuleOpposition)),
            // No weapons.json, and a restricted.json that makes a built-in weapon illegal: the built-in weapons break against it.
            (Shipped with { Restricted = pushBlocks, Weapons = null }, (ContentFiles.WeaponsFileName, true, "arming_sword", ContentValidator.RuleForgedNotAllowed)),
            // The same edit beside the shipped weapons.json: the file's weapon, stamped with the file.
            (Shipped with { Restricted = pushBlocks }, (ContentFiles.WeaponsFileName, false, "arming_sword", ContentValidator.RuleForgedNotAllowed)),
            // The dungeons' file, broken on its own: two dungeons that retire.
            (Shipped with { Dungeons = TwoRetiring }, (ContentFiles.DungeonsFileName, false, "spear_hall", ContentValidator.RuleOneRetiringDungeon)),
            // A weapons.json that renames the weapon the code's roster hands A: the roster breaks against it.
            (Shipped with { Weapons = DataTexts.Edit(Shipped.Weapons!, "\"id\": \"weakspot_stiletto\",", "\"id\": \"weakspot_dirk\",") },
                (ContentFiles.WeaponsFileName, false, "A", ContentValidator.RulePartyWeaponExists)),
        ];
        foreach (var (texts, expected) in cases)
            Assert.Equal(expected, Stamp(Fails(texts)));

        // The weapon an edit to the relations makes illegal names both (S:220-224):
        // the weapon is the entry, and the detail ends with the relation that
        // refused it, as restricted.json writes it.
        Assert.EndsWith("- restricted.json excludes [Push, Block]: Push is already forged here", Fails(Shipped with { Restricted = pushBlocks }).Detail);

        // The opposition failure reads restricted.json's excludes, and says so in
        // its detail.
        Assert.Contains(RestrictedJson.FileName, Fails(Shipped with { Restricted = elementsTangled }).Detail);

        // One cross-file case: a restricted.json whose neverRolled forgets burning
        // fails at restricted.json's stage — the unique flag is the code's and
        // neverRolled the file's, one question asked where the catalogue is built
        // against the relations — stamped restricted.json, the file to fix, its
        // detail naming it.
        var forgot = Fails(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, ", \"burning\"]", "]") });
        Assert.Equal((RestrictedJson.FileName, false, "burning", ContentValidator.RuleUniqueNeverRolled), Stamp(forgot));
        Assert.Equal("unique, but absent from neverRolled in restricted.json", forgot.Detail);

        // The relations', the dungeons' and the party's stages stamp theirs the same
        // way when they are handed to the staged load directly: records a file beside
        // the game would read to, or the built-in copies'.
        ContentSource<T> Read<T>(string file, T data) where T : class => new(file, data, null, BuiltIn: false);
        ContentSource<T> BuiltIn<T>(string file, T data) where T : class => new(file, data, null, BuiltIn: true);
        var tuning = ShippedContent.Tuning;
        var enchantments = ShippedContent.Enchantments;
        var restricted = BuiltIn(RestrictedJson.FileName, ShippedContent.Restricted);
        var weapons = BuiltIn(ContentFiles.WeaponsFileName, ShippedContent.Weapons);
        var dungeons = BuiltIn(ContentFiles.DungeonsFileName, ShippedContent.Dungeons);
        var party = ShippedContent.Party;

        // The built-in relations, broken by a code edit under them: a catalogue
        // that loses burning — handed to the staged load the way a test simulates
        // any code edit, since the game's load hands it the code's own — leaves
        // restricted.json's requires and neverRolled naming a soul that is gone.
        // The load stops at restricted.json's stage, stamped with that file and
        // marked built-in.
        var noBurning = new EnchantmentsData(ShippedContent.Enchantments.Enchantments.Where(e => e.Id != "burning").ToList());
        var burningGone = Assert.Throws<ContentException>(() => GameContent.Load(tuning, noBurning, restricted, weapons, dungeons, party));
        Assert.Equal((RestrictedJson.FileName, true, "burning", ContentValidator.RuleUnknownId), Stamp(burningGone));

        var noDungeons = Assert.Throws<ContentException>(() => GameContent.Load(tuning, enchantments, restricted, weapons,
            Read(ContentFiles.DungeonsFileName, new DungeonsData([])), party));
        Assert.Equal((ContentFiles.DungeonsFileName, false, "dungeons", ContentValidator.RuleOneRetiringDungeon), Stamp(noDungeons));

        // A three-member party is stamped weapons.json, with that file's built-in
        // flag: read from a file beside the game, or the built-in copy.
        var threeMembers = new PartyData(ShippedContent.Party.Members.Take(3).ToList());
        var threeBesideAFile = Assert.Throws<ContentException>(() => GameContent.Load(tuning, enchantments, restricted,
            Read(ContentFiles.WeaponsFileName, ShippedContent.Weapons), dungeons, threeMembers));
        Assert.Equal((ContentFiles.WeaponsFileName, false, "party", ContentValidator.RulePartySize), Stamp(threeBesideAFile));
        var threeBesideTheBuiltIn = Assert.Throws<ContentException>(() => GameContent.Load(tuning, enchantments, restricted, weapons, dungeons, threeMembers));
        Assert.Equal((ContentFiles.WeaponsFileName, true, "party", ContentValidator.RulePartySize), Stamp(threeBesideTheBuiltIn));

        // A parse failure held back is reported at its own stage, stamped, and
        // nothing past that stage is read: not even an empty party.
        var held = new ContentException("dungeons.json", ContentValidator.RuleFileShape, "dungeons.json: the top level: the file is empty");
        var reported = Assert.Throws<ContentException>(() => GameContent.Load(tuning, enchantments, restricted, weapons,
            new ContentSource<DungeonsData>(ContentFiles.DungeonsFileName, null, held, BuiltIn: false),
            new PartyData([])));
        Assert.Equal((ContentFiles.DungeonsFileName, false, "dungeons.json", ContentValidator.RuleFileShape), Stamp(reported));
        Assert.Same(held, reported.InnerException);

        // The records load, the suite's door to content built from parts, hands its
        // records to the staged load as given — no built-in copy: the same failure,
        // in the same order, stamped with the stage's file and not marked built-in.
        var recordsLoad = Assert.Throws<ContentException>(() => GameContent.Load(tuning, ShippedContent.Restricted, ShippedContent.Enchantments,
            ShippedContent.Weapons, new PartyData([]), ShippedContent.Dungeons));
        Assert.Equal((ContentFiles.WeaponsFileName, false, "party", ContentValidator.RulePartySize), Stamp(recordsLoad));
        Assert.StartsWith("weapons.json: Invalid content entry 'party': ", recordsLoad.Message, StringComparison.Ordinal);

        // A validator asked on its own belongs to no stage and names no file; so
        // does the catalogue checked on its own, first, by the staged load: it is
        // the game's code, which no data file edits.
        var brokenRow = new EnchantmentsData(ShippedContent.Enchantments.Enchantments.Select(e => e.Id == "vampiric" ? e with { Trigger = 0 } : e).ToList());
        var alone = Assert.Throws<ContentException>(() => ContentValidator.ValidateEnchantments(brokenRow));
        Assert.Equal((null, false, "vampiric", ContentValidator.RuleZeroTrigger), Stamp(alone));
        var staged = Assert.Throws<ContentException>(() => GameContent.Load(tuning, brokenRow, restricted, weapons, dungeons, party));
        Assert.Equal((null, false, "vampiric", ContentValidator.RuleZeroTrigger), Stamp(staged));
    }

    [Fact]
    public void RelationsFromAFileNeedNoRecompile()
    {
        // PHASE5-091: "adding a modifier is: implement the behaviour, price it,
        // declare its relations — and none of that last part is a recompile." The
        // crit riders are a group by the shipped file, so a dagger cannot hold
        // CritSunder beside CritWeaken; take the group out of restricted.json and
        // the next start plays without it — settled.md's "revisit by ungrouping".
        var shipped = ShippedData.Load(Shipped).Content!;
        var crit = ModifierSet.Of((CritWeaken, 1));
        Assert.False(shipped.Modifiers.Allowed(CritSunder, WeaponKind.Melee, crit));

        var ungrouped = ShippedData.Load(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, "    [\"CritWeaken\", \"CritSunder\"],\n", "") });
        Assert.Empty(ungrouped.Notices);
        Assert.True(ungrouped.Content!.Modifiers.Allowed(CritSunder, WeaponKind.Melee, crit));
        Assert.True(ungrouped.Content.Modifiers.Allowed(CritWeaken, WeaponKind.Melee, ModifierSet.Of((CritSunder, 1))));

        // And a relation added is the same edit the other way: Pin and Softening,
        // which no weapon carries together, grouped in one line.
        var pin = ModifierSet.Of((Pin, 1));
        Assert.True(shipped.Modifiers.Allowed(Softening, WeaponKind.Ranged, pin));
        var grouped = ShippedData.Load(Shipped with
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
        // 1g: ApplyPercent is keyed by enchantment id, which only the catalogue can
        // answer for — the game's, which is code. A dial for an id no entry has
        // warns and is dropped, so the tuning the game hashes holds only numbers
        // something reads...
        const string dial = """{ "ApplyPercent": { "frost_rune": 30 } }""";
        var stray = ShippedData.Load(Shipped with { Tuning = dial });
        Assert.Equal(["tuning.json: ApplyPercent.frost_rune names no enchantment - ignored"], stray.Notices);
        Assert.False(stray.Content!.Tuning.ApplyPercent.ContainsKey("frost_rune"));

        // ...while a dial on an id the catalogue has, and no shipped dial names, is
        // read, and plays: regeneration's percentage turned to 50, an override
        // the log lists, and nothing to say.
        var turned = ShippedData.Load(Shipped with { Tuning = """{ "ApplyPercent": { "regeneration": 50 } }""" });
        Assert.Empty(turned.Notices);
        Assert.Equal(50, turned.Content!.Tuning.ApplyPercent["regeneration"]);
        Assert.Equal(50, turned.Content.Enchantments["regeneration"].ApplyPercent);
        Assert.Contains("ApplyPercent.regeneration 50", turned.TuningOverrides);
    }

    [Fact]
    public void ADialTurnedInTuningPlays_AndSaysNothing()
    {
        // 1h: one owner for a dialled number — the dial. Flaming's percentage is
        // tuning.json's ApplyPercent.flaming and Arcane's potency its
        // ArcanePotency: the catalogue lays each dial over its row, so turning one
        // in tuning.json is the whole edit — it plays, and there is nothing to say.
        var turned = ShippedData.Load(Shipped with { Tuning = """{ "ApplyPercent": { "flaming": 30 } }""" });
        Assert.Equal(30, turned.Content!.Enchantments["flaming"].ApplyPercent);
        Assert.Empty(turned.Notices);

        var arcane = ShippedData.Load(Shipped with { Tuning = """{ "ArcanePotency": 9 }""" });
        Assert.Equal(9, arcane.Content!.Enchantments["arcane"].Potency);
        Assert.Empty(arcane.Notices);

        // And the shipped files say nothing at all.
        Assert.Empty(ShippedData.Load(Shipped).Notices);
    }

    [Fact]
    public void AllFourFilesTogether()
    {
        // PHASE5-085, as the user's ruling of 2026-09-25 leaves it: three files hold
        // content — restricted.json, weapons.json and dungeons.json — read beside
        // tuning.json in the load order; the enchantment catalogue and the starting
        // party are code. The shipped set, the master copies as the build carries
        // them, loads beside the game with nothing to say.
        Assert.Equal([TuningJson.FileName, RestrictedJson.FileName, WeaponsJson.FileName, DungeonsJson.FileName],
            ContentFiles.FileNames);
        var texts = ShippedData.Texts;
        Assert.All(new[] { texts.Tuning, texts.Restricted, texts.Weapons, texts.Dungeons }, text => Assert.NotNull(text));
        var load = ShippedData.Load(texts);
        Assert.Null(load.Error);
        Assert.Empty(load.Notices);
        Assert.Empty(load.TuningOverrides);

        // What the files say is what the game holds, row for row and in their
        // order: every weapon and every dungeon the game plays is its file's row,
        // field for field.
        var content = load.Content!;
        Assert.Equal(ShippedContent.Weapons.Weapons.Select(w => w.Id), content.Weapons.All.Select(w => w.Id));
        foreach (var (file, played) in ShippedContent.Weapons.Weapons.Zip(content.Weapons.All))
            WeaponsJsonTests.Same(file, played);
        Assert.Equal(ShippedContent.Dungeons.Dungeons.Select(d => d.Id), content.Dungeons.All.Select(d => d.Id));
        foreach (var (file, played) in ShippedContent.Dungeons.Dungeons.Zip(content.Dungeons.All))
            DungeonsJsonTests.Same(file, played);

        // Content is an edit to a file beside the game: a statline and a dungeon's
        // name are each changed in their own file, and the content that loads is
        // the edited one — 1i's playtest: the Weakspot Stiletto's damage 25.
        var edited = ShippedData.Load(Shipped with
        {
            Weapons = DataTexts.InRow(Shipped.Weapons!, "weakspot_stiletto", "\"damage\": 15,", "\"damage\": 25,"),
            Dungeons = DataTexts.Edit(Shipped.Dungeons!, "\"name\": \"The Spear Hall\"", "\"name\": \"The Pike Hall\""),
        });
        Assert.Empty(edited.Notices);
        Assert.Equal(25, edited.Content!.Weapons["weakspot_stiletto"].Damage);
        Assert.Equal(15, edited.Content.Weapons["assassins_fang"].Damage);
        Assert.Equal("The Pike Hall", edited.Content.Dungeons["spear_hall"].Name);

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
        var moved = ShippedData.Load(Shipped with
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

        // A file that is missing plays its built-in copy, and says so, for the
        // weapons and the dungeons as for the relations: exactly the built-in
        // weapons, row for row.
        var missing = ShippedData.Load(Shipped with { Weapons = null, Dungeons = null });
        Assert.Equal(
        [
            "weapons.json is missing - the built-in weapons are playing",
            "dungeons.json is missing - the built-in dungeons are playing",
        ], missing.Notices);
        Assert.Equal(ShippedContent.Weapons.Weapons.Select(w => w.Id), missing.Content!.Weapons.All.Select(w => w.Id));
        foreach (var (file, played) in ShippedContent.Weapons.Weapons.Zip(missing.Content.Weapons.All))
            WeaponsJsonTests.Same(file, played);
    }
}

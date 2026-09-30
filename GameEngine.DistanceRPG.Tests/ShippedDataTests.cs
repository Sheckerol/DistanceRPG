using System.Text.Json;
using System.Text.RegularExpressions;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The shipped data files are the master copy (the user's ruling of 2026-09-25;
/// the content rework, 1f): hand-kept in <c>GameEngine.DistanceRPG/Data</c>,
/// built into the game, and the fallback for their copies beside the executable.
/// So nothing here holds a value of theirs — a literal table of the numbers, or
/// the content's ids in order, would be a second copy of the master in C#, and a
/// designer's edit to the file would fail it. What is pinned is structure: the
/// four are built in and nothing else is; each is LF; each reads with nothing to
/// say, and the four load together so; the built-in tuning names every key and
/// member; a provisional row says so; and each header says it is the master copy
/// and names nothing the rework removed. Read from the build's own texts
/// (<see cref="ShippedData"/>), never from a folder.
/// <para>
/// The rest of the suite holds none either (the content rework's R5): a test that
/// leans on a number of the masters reads it where the game does
/// (<see cref="ShippedContent"/>), or stands on it as its own input
/// (<see cref="Premise"/>), so an edit to any number of theirs fails nothing —
/// bar the few values a design doc fixes (REWORK DECISION 3), pinned as literals in
/// the tests below, so an edit that moves one fails by name and its doc moves with it:
/// <list type="bullet">
/// <item><c>MovementBudget</c> 160, and <c>MovementUnitsPerMana</c> 10 derived from
/// it — phase-1-weapons.md L773-775, <c>(160 − 40) / (8 + 3)</c>:
/// <see cref="MovementBudgetTests.TheBudgetIsTheTuningsKey"/>.</item>
/// <item><c>FriendlyFireEnabled</c> false — phase-1-weapons.md L1062-1063, wands
/// ship with the friendly-fire switch off until the placement scorer exists
/// (settled.md L134-136):
/// <see cref="WandTests.FriendlyFire_OffByDefault_HalfWhenOn"/>.</item>
/// <item>The eight class rows, the thirty-two weapons' statlines and spreads, the
/// staves' own mana and the wands' shapes — phase-1-weapons.md §1.2–§1.4 (the
/// costs at L131-138, the staves at L642-647, the wands at L897-902):
/// <see cref="WeaponContentTests.Statlines_MatchTheTables"/>,
/// <see cref="WeaponContentTests.WandShapes_MatchTheTable"/> and
/// <see cref="WeaponClassTests.AClassRowIsWhatTheClassCarries"/>.</item>
/// <item>The unique table — phase-1-weapons.md L1075-1083 (§1.5):
/// <c>UniqueContentTests</c>' <c>Table</c>.</item>
/// <item>The tutorial's row: its boss pinned to floor 2, and its golem's 80 HP,
/// movement budget of 40 and Block x3 — phase-5-save.md L376 and L377 (§5.8):
/// <see cref="DungeonContentTests.TheTutorialIsTheShippedDungeon"/>.</item>
/// <item>A weapon's wear capacity: <c>StartingPool</c> 30, the capacity it opens
/// on, and <c>WearCapacityPerBar</c> 1, a point for every full bar cashed — the
/// user's ruling of 2026-09-25 on what a deposit costs, R4 (settled.md L1653-1676),
/// and its worked capacities: <see cref="WearTests.ACapacityReplaysFullBars_TheRulingsNumbers"/>.</item>
/// </list>
/// A relation, an id or a row is no number: a test that names one fails, when an
/// edit moves it, at the assertion that states it.
/// </para>
/// </summary>
public class ShippedDataTests
{
    /// <summary>The four files, in the load order, with their built-in texts.</summary>
    private static IEnumerable<(string Name, string? Text)> BuiltIn()
    {
        var texts = ShippedData.Texts;
        yield return (TuningJson.FileName, texts.Tuning);
        yield return (RestrictedJson.FileName, texts.Restricted);
        yield return (WeaponsJson.FileName, texts.Weapons);
        yield return (DungeonsJson.FileName, texts.Dungeons);
    }

    [Fact]
    public void EveryShippedFileIsBuiltIn_AndNoOther()
    {
        // The four data files — the tuning and the three content files — are built
        // into the game under their names, each read out of the build whole; the
        // enchantment catalogue and the starting party are code, and ship no file.
        var names = typeof(ShippedData).Assembly.GetManifestResourceNames().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(ContentFiles.FileNames.Select(name => ShippedData.ResourcePrefix + name).Order(StringComparer.Ordinal), names);
        Assert.DoesNotContain(names, name => name.EndsWith("enchantments.json", StringComparison.Ordinal) || name.EndsWith("party.json", StringComparison.Ordinal));

        var texts = ShippedData.Texts;
        Assert.Null(texts.Unreadable);
        Assert.Null(texts.Retired);
        Assert.All(BuiltIn(), file => Assert.False(string.IsNullOrWhiteSpace(file.Text), $"{file.Name} is not built in"));

        // Read once: the same texts every time they are asked for.
        Assert.Same(texts, ShippedData.Texts);
    }

    [Fact]
    public void TheBuiltInCopiesAreLf()
    {
        // .gitattributes keeps the masters LF in every checkout, so the texts the
        // suite edits by searching for "\n" are the texts the game carries,
        // whatever core.autocrlf says. A checkout that turned them fails here, by name.
        foreach (var (name, text) in BuiltIn())
        {
            Assert.False(text!.Contains('\r', StringComparison.Ordinal), $"{name} is built in with CRLF line ends: the checkout ignored .gitattributes");
            Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheShippedFilesLoadWithNothingToSay()
    {
        // Each built-in copy reads with no warning: the tuning strictly, every key
        // there and in range, and each content file to its record.
        var texts = ShippedData.Texts;
        Assert.Empty(TuningJson.ReadBuiltIn(texts.Tuning!).Warnings);
        RestrictedJson.Read(texts.Restricted!);
        WeaponsJson.Read(texts.Weapons!);
        DungeonsJson.Read(texts.Dungeons!);

        // And the four load together, as the game loads them beside the executable
        // and as it loads its built-in copies alone: no notice, no number moved or
        // fitted, no failure — a game started beside them plays exactly what a game
        // with nothing beside it plays.
        foreach (var load in new[] { ShippedData.Load(texts), ContentFiles.Load(texts, texts) })
        {
            Assert.Null(load.Error);
            Assert.Empty(load.Notices);
            Assert.Empty(load.TuningOverrides);
            Assert.Equal([RestrictedJson.FileName, WeaponsJson.FileName, DungeonsJson.FileName], load.ReadBeside);
            Assert.Equal(TuningJson.Hash(ShippedData.Content.Tuning), TuningJson.Hash(load.Content!.Tuning));
            Assert.Same(load.Tuning, load.Content.Tuning);
        }

        // Nothing beside the executable: the built-in copies play, each said.
        var alone = ShippedData.Load(new ContentTexts());
        Assert.Null(alone.Error);
        Assert.Empty(alone.ReadBeside);
        Assert.Equal(ContentFiles.FileNames.Count, alone.Notices.Count);
        Assert.Equal(TuningJson.Hash(ShippedData.Content.Tuning), TuningJson.Hash(alone.Content!.Tuning));
    }

    [Fact]
    public void TheBuiltInTuningNamesEveryKeyAndMember()
    {
        // The built-in tuning.json is every number the game has: nothing in code
        // stands under a key it leaves out. So it names every key once, every
        // member of every table keyed by an enum, and every ApplyPercent id the
        // catalogue leaves to it — and no key still to come.
        using var document = JsonDocument.Parse(ShippedData.Texts.Tuning!, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = document.RootElement;
        var named = root.EnumerateObject().Select(key => key.Name).ToList();
        Assert.Equal(named.Count, named.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(TuningSchema.Keys.Select(key => key.Name).Order(StringComparer.Ordinal), named.Order(StringComparer.Ordinal));
        foreach (var table in TuningSchema.Keys.OfType<TableKey>())
            if (table.Members is { } members)
                Assert.Equal(members.Order(StringComparer.Ordinal), root.GetProperty(table.Name).EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        var dials = root.GetProperty(nameof(Tuning.ApplyPercent)).EnumerateObject().Select(member => member.Name).ToHashSet(StringComparer.Ordinal);
        Assert.All(EnchantmentDefinitions.Data.Enchantments.Where(e => e.ApplyPercent == EnchantmentDefinitions.SetByTuning),
            e => Assert.Contains(e.Id, dials));
        Assert.DoesNotContain(named, TuningSchema.Deferred.Values.SelectMany(deferred => deferred.Keys).Contains);

        // So it reads strictly, and the game plays it: its numbers are the ones the
        // built-in content holds.
        TuningValues.Same(ShippedData.Content.Tuning, TuningJson.ReadBuiltIn(ShippedData.Texts.Tuning!).Tuning);
    }

    /// <summary>
    /// The rows the docs did not fix: the three uniques they leave unnamed, and the
    /// four dungeons beside the tutorial with their bosses, names and statlines
    /// alike — first guesses, expected to move after play. Which rows the docs
    /// left unfixed is a fact about the docs, not a value of the files, so it is
    /// held here; the enchantment catalogue's own guesses (the shielding entry's
    /// id, Burning's lock) are code, and their comments there say so.
    /// </summary>
    private static readonly string[] ProvisionalIds =
    [
        "flensing_knife_unique", "wand_of_the_nova_unique", "staff_of_renewal_unique",   // weapons.json: the Nameless three
        "spear_hall", "spear_warden", "long_gallery", "gallery_marksman",               // dungeons.json: the four and their bosses
        "knife_warrens", "knife_mother", "quiver_pits", "quiver_keeper",
    ];

    /// <summary>What follows the id of a provisional row, and nothing else, in its file.</summary>
    private const string ProvisionalNote = "/* provisional */";

    [Fact]
    public void AProvisionalRowSaysSo()
    {
        // A designer tuning a copy can tell a guessed row from one the docs fixed,
        // as tuning.json's "Provisional:" comments mark a number: every provisional
        // row is followed by /* provisional */ after its id, and no other row is.
        var marked = new List<string>();
        var rows = new List<string>();
        var texts = ShippedData.Texts;
        foreach (string? text in new[] { texts.Restricted, texts.Weapons, texts.Dungeons })
        {
            foreach (Match row in Regex.Matches(text!, "\"id\": \"([^\"]+)\",?( /\\* provisional \\*/)?\n"))
            {
                rows.Add(row.Groups[1].Value);
                if (row.Groups[2].Success)
                    marked.Add(row.Groups[1].Value);
            }
            // The note appears after an id and nowhere else in the file.
            Assert.Equal(Regex.Matches(text!, Regex.Escape(ProvisionalNote)).Count,
                Regex.Matches(text!, "\"id\": \"[^\"]+\",? " + Regex.Escape(ProvisionalNote)).Count);
        }

        Assert.Equal(ProvisionalIds.Order(StringComparer.Ordinal), marked.Order(StringComparer.Ordinal));

        // Every provisional id is a row some file holds, so the list cannot go stale
        // behind a renamed row; and the docs' own rows are not marked.
        Assert.Subset(rows.ToHashSet(StringComparer.Ordinal), ProvisionalIds.ToHashSet(StringComparer.Ordinal));
        foreach (string fixedByTheDocs in new[] { "tutorial", "stone_golem", "widowmaker", "arming_sword", "regeneration" })
            Assert.DoesNotContain(fixedByTheDocs, marked);
    }

    [Fact]
    public void EveryHeaderSaysItIsTheMasterCopy()
    {
        // Each file opens on a header naming itself, saying it is the master copy —
        // GameEngine.DistanceRPG/Data/<name> — that the game carries it built in,
        // and that --write-data writes the built-in copies out: so a playtester
        // reading the copy beside the executable knows where a change worth keeping
        // goes. And no shipped file names what the rework removed: the two files
        // the game no longer reads, the compiled copy, or a generator — nor, since
        // the weapon classes became rows of weapons.json (Phase 5.R4), the flat
        // format's thirteen fields a weapon named, or the baseline re-based by an
        // edit to all four of a class's variants.
        foreach (var (name, builtIn) in BuiltIn())
        {
            string text = builtIn!;
            Assert.StartsWith($"/*\n * {name} - ", text, StringComparison.Ordinal);
            string header = text[..(text.IndexOf("*/", StringComparison.Ordinal) + 2)];
            string words = Regex.Replace(header, @"\s*\n \*\s*", " ");
            Assert.Contains($"GameEngine.DistanceRPG/Data/{name} is the master copy", words, StringComparison.Ordinal);
            Assert.Contains("built with it inside", words, StringComparison.Ordinal);
            Assert.Contains("DistanceRPG --write-data <folder> writes the built-in copies out", words, StringComparison.Ordinal);

            // Read as words, so a phrase a comment wraps over two lines is found too.
            string flat = Regex.Replace(text, @"\s*\n \*\s*|\s+", " ");
            foreach (string removed in new[] { "enchantments.json", "party.json", "ContentDefaults", "generated", "thirteen fields", "an edit to all four" })
                Assert.False(flat.Contains(removed, StringComparison.OrdinalIgnoreCase), $"{name} names {removed}");
        }
    }
}

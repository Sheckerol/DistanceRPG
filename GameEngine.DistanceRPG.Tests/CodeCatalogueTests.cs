using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The enchantment catalogue and the starting party are code (the user's ruling
/// of 2026-09-25: "behaviour, modifiers, and enchantments living in the code due
/// to them being reference data"; the content rework's 1b, 1c), and the load
/// still checks them: the catalogue on its own and against the relations, the
/// party against the weapons, each failure naming the entry, the rule and the
/// file a playtester would fix — <c>restricted.json</c> for the catalogue,
/// <c>weapons.json</c> for the party. A row broken in code fails here, before a
/// playtester could meet it. Carries what the removed <c>enchantments.json</c>
/// and <c>party.json</c> suites held that still applies (PHASE5-137, -140, -141,
/// -175). Every case hands the loader text or records, and makes nothing current.
/// </summary>
public class CodeCatalogueTests
{
    private static readonly ContentTexts Shipped = DataTexts.Shipped;

    private static (string? File, bool Compiled, string Entry, string Rule) Stamp(ContentException error)
        => (error.File, error.Compiled, error.EntryId, error.Rule);

    private static ContentException Fails(ContentTexts texts)
    {
        var load = ContentFiles.Load(texts);
        Assert.Null(load.Content);
        return load.Error ?? throw new Xunit.Sdk.XunitException("the load has neither content nor an error");
    }

    [Fact]
    public void TheCatalogueIsCode_AndPassesItsOwnChecks()
    {
        // The catalogue and its souls: twenty-two rows, passing the checks the
        // load asks of them first and alone — ids unique, a lock of at least one,
        // no trigger of zero, a trigger or a status and never both, the drop pool
        // resolving.
        var rows = EnchantmentDefinitions.Data.Enchantments;
        Assert.Equal(22, rows.Count);
        ContentValidator.ValidateEnchantments(EnchantmentDefinitions.Data);   // does not throw

        // The unique flag is on the nine souls and only on them — exactly the ids
        // the shipped restricted.json never rolls: the flag is the code's, the
        // relation the file's, and the load holds the two together.
        Assert.Equal(9, rows.Count(e => e.Unique));
        Assert.Equal(ShippedContent.Restricted.NeverRolled.Order(StringComparer.Ordinal),
            rows.Where(e => e.Unique).Select(e => e.Id).Order(StringComparer.Ordinal));

        // PHASE5-137's fields, as the rows hold them: an applier quotes no flat
        // trigger and names the status it lands; a catalogue entry quotes one; an
        // element carries its damage type by name, and nothing else carries one;
        // a soul is unique.
        EnchantmentDef Row(string id) => rows.Single(e => e.Id == id);
        Assert.Null(Row("regeneration").Trigger);
        Assert.Equal(StatusEffectType.Regeneration, Row("regeneration").Applies);
        Assert.Equal(2, Row("vampiric").Trigger);
        Assert.Equal(EffectKind.ElementalDamage, Row("flaming").Effect);
        Assert.Equal(DamageType.Flaming, Row("flaming").DamageType);
        Assert.Null(Row("vampiric").DamageType);
        Assert.True(Row("serrated").Unique);
        Assert.All(rows.Where(e => e.DamageType is not null),
            e => Assert.True(e.Effect is EffectKind.ElementalDamage or EffectKind.LingeringElement, e.Id));

        // PHASE5-138, -148 by construction: every row's effect is an EffectKind —
        // typed, so a typo is a build error rather than a silent no-op.
        Assert.All(rows, e => Assert.True(Enum.IsDefined(e.Effect), e.Id));

        // And the game plays these rows, in this order, from the shipped files.
        var content = ContentFiles.Load(Shipped).Content!;
        Assert.Equal(rows.Select(e => e.Id), content.Enchantments.All.Select(e => e.Id));
    }

    [Fact]
    public void TheCodePartyIsCheckedAgainstWeaponsJson()
    {
        // The roster, A to D, passes its own rules against the shipped weapons:
        // four members, each spread a permutation of 1-4, every weapon there and
        // within the carry, a caster in hand.
        var shipped = TestContent.Load();
        ContentValidator.ValidateParty(StartingParty.Data, shipped.Weapons, shipped.Tuning);   // does not throw
        Assert.Equal(["A", "B", "C", "D"], shipped.Party.All.Select(member => member.Id));

        // PHASE5-175 through the one file that can break it: a weapons.json that
        // renames weakspot_stiletto — a Dagger variant nothing but A names — stops
        // the game at the party's stage, stamped weapons.json, naming the member,
        // the rule and the id.
        var renamed = Fails(Shipped with { Weapons = DataTexts.Edit(Shipped.Weapons!, "\"id\": \"weakspot_stiletto\",", "\"id\": \"weakspot_dirk\",") });
        Assert.Equal((WeaponsJson.FileName, false, "A", ContentValidator.RulePartyWeaponExists), Stamp(renamed));
        Assert.Equal("weakspot_stiletto", renamed.Detail);

        // So does C's great_axe, the other hand weapon only the roster names.
        var greatAxe = Fails(Shipped with { Weapons = DataTexts.Edit(Shipped.Weapons!, "\"id\": \"great_axe\",", "\"id\": \"greater_axe\",") });
        Assert.Equal((WeaponsJson.FileName, false, "C", ContentValidator.RulePartyWeaponExists), Stamp(greatAxe));
        Assert.Equal("great_axe", greatAxe.Detail);

        // A bag is checked the same way. No weapons.json edit can reach it — every
        // bag weapon of the shipped roster is one the code names, refused earlier
        // at the weapons' stage — so it is pinned through records built in code:
        // an A whose bag names staff_of_nothing.
        var party = new PartyData(StartingParty.Data.Members.Select(m => m.Id == "A" ? m with { BagWeaponIds = ["staff_of_nothing"] } : m).ToList());
        var bag = Assert.Throws<ContentException>(() => TestContent.Load(party: party));
        Assert.Equal((WeaponsJson.FileName, true, "A", ContentValidator.RulePartyWeaponExists), Stamp(bag));
        Assert.Equal("staff_of_nothing", bag.Detail);

        // And a weapons.json without staff_of_mire, D's bag staff, is refused at the
        // weapons' stage first, for what the code needs of it — the caster drop
        // order names it — before the party is asked.
        var noMire = Fails(Shipped with { Weapons = DataTexts.WithoutRow(Shipped.Weapons!, "staff_of_mire") });
        Assert.Equal((WeaponsJson.FileName, false, "staff_of_mire", ContentValidator.RuleCasterVariantPool), Stamp(noMire));
    }

    [Fact]
    public void ARelationNamingAnEnchantmentTheGameLacksStopsTheGame()
    {
        // PHASE5-089 against the code's catalogue: restricted.json names an
        // enchantment only by an id the game's catalogue has. "frost" is none, and
        // near none: the detail says where the ids come from.
        var frost = Fails(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, "[\"flaming\", \"cold\"]", "[\"flaming\", \"frost\"]") });
        Assert.Equal((RestrictedJson.FileName, false, "frost", ContentValidator.RuleUnknownId), Stamp(frost));
        Assert.Equal("not a modifier, nor an enchantment in the enchantment catalogue", frost.Detail);

        // A typo of one is pointed at the id it is nearest.
        var typo = Fails(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, "[\"flaming\", \"cold\"]", "[\"flaming\", \"colld\"]") });
        Assert.Equal((RestrictedJson.FileName, false, "colld", ContentValidator.RuleUnknownId), Stamp(typo));
        Assert.Equal("not a modifier, nor an enchantment in the enchantment catalogue - did you mean cold?", typo.Detail);
    }

    [Fact]
    public void TheCatalogueAgainstTheRelationsIsRestrictedJsonsToFix()
    {
        // PHASE5-141: the unique flag is the code's and neverRolled restricted.json's,
        // and the load cross-checks the two. A neverRolled that forgets burning —
        // a soul — stops the game stamped restricted.json, the file to fix, and
        // its detail names it.
        var forgot = Fails(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, ", \"burning\"]", "]") });
        Assert.Equal((RestrictedJson.FileName, false, "burning", ContentValidator.RuleUniqueNeverRolled), Stamp(forgot));
        Assert.Equal("unique, but absent from neverRolled in restricted.json", forgot.Detail);

        // And the other way: neverRolled naming an entry that is no soul.
        var notUnique = Fails(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, ", \"burning\"]", ", \"burning\", \"vampiric\"]") });
        Assert.Equal((RestrictedJson.FileName, false, "vampiric", ContentValidator.RuleUniqueNeverRolled), Stamp(notUnique));

        // PHASE5-140: the code's four elements, opposed in two pairs by
        // restricted.json's excludes. Flaming opposed by cold and shocking both
        // stops the game stamped restricted.json, naming its excludes.
        var tangled = Fails(Shipped with { Restricted = DataTexts.Edit(Shipped.Restricted!, "[\"flaming\", \"cold\"]", "[\"flaming\", \"cold\", \"shocking\"]") });
        Assert.Equal((RestrictedJson.FileName, false, "flaming", ContentValidator.RuleOpposition), Stamp(tangled));
        Assert.Contains("restricted.json excludes", tangled.Detail);
    }

    [Fact]
    public void AWeaponNamingAnEnchantmentTheGameLacksStopsTheGame()
    {
        // weapons.json names an enchantment by the catalogue's id: a staff whose
        // innate is no entry of the game's catalogue stops the game at the
        // weapons' stage, naming the weapon, the rule and the id.
        var regen = Fails(Shipped with { Weapons = DataTexts.InRow(Shipped.Weapons!, "staff_of_renewal", "{ \"id\": \"regeneration\" }", "{ \"id\": \"regen\" }") });
        Assert.Equal((WeaponsJson.FileName, false, "staff_of_renewal", ContentValidator.RuleUnknownId), Stamp(regen));
        Assert.Equal("regen", regen.Detail);
    }

    [Fact]
    public void NoFileHoldsTheCatalogueOrTheParty()
    {
        // The data files are four — tuning.json and the three content files — and
        // neither the catalogue nor the party is among them, nor has a text to hand
        // the load: the two files the game once read are only named, as retired.
        Assert.Equal([TuningJson.FileName, RestrictedJson.FileName, WeaponsJson.FileName, DungeonsJson.FileName], ContentFiles.FileNames);
        Assert.Equal(["Tuning", "Restricted", "Weapons", "Dungeons", "Unreadable", "Retired"],
            typeof(ContentTexts).GetConstructors().Single().GetParameters().Select(parameter => parameter.Name));
        Assert.DoesNotContain(typeof(ContentTexts).GetProperties(), property => property.Name is "Enchantments" or "Party");
        Assert.Equal(["enchantments.json", "party.json"], ContentFiles.Retired.Keys.Order(StringComparer.Ordinal));
        Assert.All(ContentFiles.Retired.Keys, name => Assert.DoesNotContain(name, ContentFiles.FileNames));

        // The generator writes no file for either.
        var written = ContentFiles.WriteDefaults();
        Assert.All(new[] { written.Tuning, written.Restricted, written.Weapons, written.Dungeons }, text => Assert.NotNull(text));
        Assert.Null(written.Retired);
    }
}

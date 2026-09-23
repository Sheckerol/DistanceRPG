using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.5's uniques as content: the eleven derive from a variant with one
/// modifier raised to x3 (or trade depth for souls, or raise nothing and put
/// the artifact in their enchantments), the validator refuses what the
/// derivation rule forbids, the nine souls are never rolled and pinned at tier
/// 1, and each named unique reads as the doc says it does. In the content
/// collection because tests here swap the current content.
/// </summary>
[Collection(TestContent.Collection)]
public class UniqueContentTests
{
    private const float Tile = GameConstants.Tile;

    private static WeaponCatalogue Catalogue => GameContent.Current.Weapons;
    private static ModifierRules Rules => GameContent.Current.Modifiers;
    private static EnchantmentCatalogue Enchantments => GameContent.Current.Enchantments;

    /// <summary>The unique table (W:1075-1083, W:1345-1350): id, name, the variant it derives from, the one modifier raised — none for the Light shape and the casters — and what it carries.</summary>
    private static readonly (string Id, string Name, string DerivedFrom, ModifierType? Raised, string[] Enchantments)[] Table =
    [
        ("the_bulwark", "The Bulwark", "tower_guard", Block, ["sturdy"]),
        ("feathered_death", "Feathered Death", "bandolier", Charges, ["weightless"]),
        ("shieldbreaker", "Shieldbreaker", "reaver", Splitting, ["momentum"]),
        ("widowmaker", "Widowmaker", "assassins_fang", CritWindow, ["siphon"]),
        ("hoplites_wall", "Hoplite's Wall", "phalanx_spear", Brace, ["immovable"]),
        ("stormcrow", "Stormcrow", "longbow", Longshot, ["piercing"]),
        ("flensing_knife_unique", "Nameless Knife", "flensing_knife", null, ["serrated", "vampiric", "overheal"]),
        ("rotwood", "Rotwood", "staff_of_blight", null, ["poison"]),
        ("the_long_candle", "The Long Candle", "wand_of_the_beam", null, ["shocking", "flaming"]),
        ("wand_of_the_nova_unique", "Nameless Nova", "wand_of_the_nova", null, ["flaming", "burning"]),
        ("staff_of_renewal_unique", "Nameless Renewal", "staff_of_renewal", null, ["regeneration", "overheal"]),
    ];

    private static readonly string[] Souls = ["siphon", "weightless", "sturdy", "momentum", "overheal", "serrated", "immovable", "piercing", "burning"];

    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Member(Weapon? weapon, string id = "A")
        => TestPools.Holding(id, weapon);

    private static PartyMemberState Char(string id, int r, int c, string weaponId)
    {
        var (x, y) = At(r, c);
        return TestPools.Holding(id, TestWeapons.Get(weaponId), x: x, y: y);
    }

    private static EnemyState Enemy(int r, int c, string weaponId = "arming_sword", int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = TestWeapons.Get(weaponId), Hp = hp };
    }

    private static Weapon Fists => TestWeapons.Make("Fists", 40, 1, 0);

    /// <summary>The settled payload through a fresh compiled chain with no applier: the numbers alone, nothing written.</summary>
    private static DamagePayload Settle(ActorState attacker, ActorState defender, int roll, int distanceUnits = 0)
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        return table.Raise(GameEvent.DamageTaken,
            DamagePayload.Initial(attacker.EquippedWeapon!, roll, distanceUnits), attacker, defender);
    }

    // ── The table as content ─────────────────────────────────────────────────

    [Fact]
    public void MaxForged_HoldsOverTheWholeUniqueTable()
    {
        // The whole table, walked: eleven uniques, every forged stack under MaxForged — x3 is the ceiling a
        // unique's raise reaches and nothing is forged past it — the raised modifier at exactly x3, and the
        // Light shape's signature at x2. Nothing acquired at instantiation.
        var uniques = Catalogue.Uniques;
        Assert.Equal(11, uniques.Count);
        Assert.Equal(Table.Select(t => t.Id), uniques.Select(u => u.Id));
        Assert.Equal(Catalogue.All.Where(w => w.Unique), uniques);
        Assert.All(Enum.GetValues<WeaponClass>(), c => Assert.DoesNotContain(Catalogue.ByClass(c), w => w.Unique));   // a class's variants are what a roll chooses among

        foreach (var (id, name, derivedFrom, raised, enchantments) in Table)
        {
            var def = Catalogue[id];
            Assert.True(def.Unique);
            Assert.Equal(name, def.Name);
            Assert.Equal(derivedFrom, def.DerivedFrom);
            Assert.Null(def.Role);
            Assert.Equal(enchantments, def.Enchantments.Select(e => e.Id));

            var weapon = TestWeapons.Get(id);
            Assert.True(weapon.Unique);
            Assert.Equal(weapon.Forged, weapon.Modifiers);
            foreach (var (t, n) in weapon.Forged.Entries)
                Assert.True(n <= Rules.MaxForged(t), $"{id}: {t} x{n} is forged past {Rules.MaxForged(t)}");
            if (raised is { } t3)
                Assert.Equal(3, weapon.Forged.Stacks(t3));
        }
        Assert.Equal(2, TestWeapons.Get("flensing_knife_unique").Forged.Stacks(CritWindow));

        // The ceiling of the raise is x3 + 5 acquired: x8, with a full five-stack acquired budget still unspent above it.
        foreach (var t in Enum.GetValues<ModifierType>())
            Assert.Equal(8, Rules.Cap(t, 3));

        // The ceilings the combat doc quotes: a Weakspot Stiletto at CritSunder x6, a Stiletto-derived unique
        // raising it at x8, and the Bulwark at Block x8.
        Assert.Equal(6, Rules.Cap(CritSunder, TestWeapons.Get("weakspot_stiletto").Forged.Stacks(CritSunder)));
        using (TestContent.Use(weapons: new WeaponsData([.. ContentDefaults.Weapons.Weapons,
            Martial("test_sundering_stiletto", "weakspot_stiletto", [(CritWindow, 1), (CritMultiplier, 1), (CritSunder, 3)], "siphon")])))
        {
            var unique = TestWeapons.Get("test_sundering_stiletto");
            unique.Acquire(CritSunder, 10);
            Assert.Equal((8, 8), (Rules.Cap(CritSunder, unique.Forged.Stacks(CritSunder)), unique.Stacks(CritSunder)));
        }
        Assert.Equal(8, Rules.Cap(Block, TestWeapons.Get("the_bulwark").Forged.Stacks(Block)));
    }

    [Fact]
    public void EveryUnique_DerivesFromAVariant_OneModifierAtThree_OrLightShape()
    {
        // Every unique matches its variant's spread with exactly one modifier it already carried raised to
        // x3 and nothing else changed; the Light-forged one raises the signature to x2 and is paid for the
        // stack it gave up in a third soul; a caster raises nothing. Statline, class and shape are the
        // variant's in every case: "everything else is unchanged".
        foreach (var (id, _, derivedFrom, raised, _) in Table)
        {
            var def = Catalogue[id];
            var variant = Catalogue[derivedFrom];
            Assert.False(variant.Unique);
            Assert.Equal((variant.Class, variant.Range, variant.Damage, variant.Cost, variant.ManaCost, variant.Shape),
                (def.Class, def.Range, def.Damage, def.Cost, def.ManaCost, def.Shape));

            var changed = Enum.GetValues<ModifierType>()
                .Where(t => def.Forged.GetValueOrDefault(t) != variant.Forged.GetValueOrDefault(t))
                .ToList();
            if (Weapon.KindOf(def.Class) == WeaponKind.Caster)
            {
                Assert.Empty(changed);
                Assert.NotEqual(variant.Enchantments, def.Enchantments);
            }
            else if (raised is { } t)
            {
                Assert.Equal(new[] { t }, changed);
                Assert.True(variant.Forged[t] >= 1);
                Assert.Equal(3, def.Forged[t]);
                Assert.Single(def.Enchantments);
            }
            else
            {
                Assert.Equal(new[] { CritWindow }, changed);   // the Dagger signature, from the Flensing Knife's x1
                Assert.Equal(2, def.Forged[CritWindow]);
                Assert.Equal(1, def.Forged[Light]);
                Assert.Equal(3, def.Enchantments.Count);       // x2 buys three
                Assert.Contains(def.Enchantments, r => Enchantments[r.Id].Unique);
                Assert.Contains(def.Enchantments, r => r.Tier == 3);
            }
        }

        // Refused: a freely-authored spread, a modifier the variant lacks, a second x3, a raise short of
        // x3, and the variant itself under a unique flag.
        var ex = LoadWith(Martial("test_free", "phalanx_spear", (Brace, 3), (Pin, 2)));
        Assert.Equal(("test_free", ContentValidator.RuleUniqueDerivation), (ex.EntryId, ex.Rule));
        ex = LoadWith(Martial("test_added", "tower_guard", (Block, 2), (Push, 1), (Riposte, 3)));
        Assert.Equal(("test_added", ContentValidator.RuleUniqueDerivation), (ex.EntryId, ex.Rule));
        Assert.Contains("adds Riposte", ex.Message);
        ex = LoadWith(Martial("test_two", "tower_guard", (Block, 3), (Push, 3)));
        Assert.Equal(("test_two", ContentValidator.RuleUniqueDerivation), (ex.EntryId, ex.Rule));
        ex = LoadWith(Martial("test_shallow", "tower_guard", (Block, 2), (Push, 2)));
        Assert.Equal(("test_shallow", ContentValidator.RuleUniqueDerivation), (ex.EntryId, ex.Rule));
        Assert.Contains("Push x2, not x3", ex.Message);
        ex = LoadWith(Martial("test_same", "tower_guard", (Block, 2), (Push, 1)));
        Assert.Equal(("test_same", ContentValidator.RuleUniqueDerivation), (ex.EntryId, ex.Rule));

        // And the modifier raised is one its variant's role may raise (the four shapes): a Purity variant
        // deepens the signature alone, so a Tower Guard unique never raises its Push; a Control or Support
        // variant raises the signature or its own rider, so a Halberd unique never raises the spear's second
        // baseline modifier, Longshot, nor a Weakspot Stiletto unique the dagger's CritMultiplier. The
        // Shieldbreaker, raising the Reaver's own Splitting, loads with the defaults, as does the Halberd
        // braced three times (below).
        ex = LoadWith(Martial("test_purity_rider", "tower_guard", [(Block, 2), (Push, 3)], "sturdy"));
        Assert.Equal(("test_purity_rider", ContentValidator.RuleUniqueRaisedByRole), (ex.EntryId, ex.Rule));
        Assert.Contains("deepens the Sword signature Block alone", ex.Message);
        ex = LoadWith(Martial("test_long_halberd", "halberd", [(Brace, 1), (Longshot, 3), (Push, 1)], "immovable"));
        Assert.Equal(("test_long_halberd", ContentValidator.RuleUniqueRaisedByRole), (ex.EntryId, ex.Rule));
        Assert.Contains("raises the signature Brace or its own Push", ex.Message);
        ex = LoadWith(Martial("test_heavy_stiletto", "weakspot_stiletto", [(CritWindow, 1), (CritMultiplier, 3), (CritSunder, 1)], "siphon"));
        Assert.Equal(("test_heavy_stiletto", ContentValidator.RuleUniqueRaisedByRole), (ex.EntryId, ex.Rule));
        Assert.Contains("raises the signature CritWindow or its own CritSunder", ex.Message);

        // The Light shape: the floor is x2; x3 buys two souls, x2 three; one of them unique or at tier 3;
        // and it is the signature that is raised, never the currency, never the second axis.
        ex = LoadWith(Martial("test_floor", "flensing_knife", [(CritWindow, 1), (CritMultiplier, 1), (Light, 1)], "serrated", "siphon", "sturdy"));
        Assert.Equal(("test_floor", ContentValidator.RuleLightUniqueShape), (ex.EntryId, ex.Rule));
        ex = LoadWith(Martial("test_count", "flensing_knife", [(CritWindow, 3), (CritMultiplier, 1), (Light, 1)], "serrated", "siphon", "sturdy"));
        Assert.Equal(("test_count", ContentValidator.RuleLightUniqueShape), (ex.EntryId, ex.Rule));
        Assert.Contains("3 enchantments at CritWindow x3, not 2", ex.Message);
        ex = LoadWith(Martial("test_plain", "flensing_knife", [(CritWindow, 2), (CritMultiplier, 1), (Light, 1)], "flaming", "shocking", "poison"));
        Assert.Equal(("test_plain", ContentValidator.RuleLightUniqueShape), (ex.EntryId, ex.Rule));
        ex = LoadWith(Martial("test_axis", "flensing_knife", [(CritWindow, 1), (CritMultiplier, 3), (Light, 1)], "serrated", "siphon"));
        Assert.Equal(("test_axis", ContentValidator.RuleLightUniqueShape), (ex.EntryId, ex.Rule));
        Assert.Contains("not the Dagger signature CritWindow", ex.Message);

        // A unique names a variant — not nothing, not another unique — and a variant names nothing.
        ex = LoadWith(Martial("test_orphan", null, (Block, 3), (Push, 1)));
        Assert.Equal(("test_orphan", ContentValidator.RuleUniqueDerivedFrom), (ex.EntryId, ex.Rule));
        ex = LoadWith(Martial("test_grandchild", "the_bulwark", (Block, 3), (Push, 1)));
        Assert.Equal(("test_grandchild", ContentValidator.RuleUniqueDerivedFrom), (ex.EntryId, ex.Rule));
        ex = LoadEditing("tower_guard", w => w with { DerivedFrom = "arming_sword" });
        Assert.Equal(("tower_guard", ContentValidator.RuleUniqueDerivedFrom), (ex.EntryId, ex.Rule));

        // The statline is the variant's, and a unique carries no role.
        ex = LoadEditing("the_bulwark", w => w with { Damage = 12 });
        Assert.Equal(("the_bulwark", ContentValidator.RuleUniqueStatline), (ex.EntryId, ex.Rule));
        ex = LoadEditing("widowmaker", w => w with { Role = VariantRole.Purity });
        Assert.Equal(("widowmaker", ContentValidator.RuleRoleOnMartialOnly), (ex.EntryId, ex.Rule));

        // A legitimate derivation loads: the Halberd braced three times, the doc's own example.
        using var _ = TestContent.Use(weapons: new WeaponsData([.. ContentDefaults.Weapons.Weapons,
            Martial("test_halberd_wall", "halberd", [(Brace, 3), (Longshot, 1), (Push, 1)], "immovable")]));
        Assert.Equal(3, TestWeapons.Get("test_halberd_wall").Stacks(Brace));
        Assert.Equal(32 + 11 + 1, Catalogue.All.Count);
    }

    [Fact]
    public void CasterUniques_KeepResonantX1_AndTheStatline()
    {
        // A caster unique is not a bigger number on the weapon: Resonant x1 and the statline are the
        // variant's, and every point of its advantage sits in the enchantment list.
        foreach (var (id, _, derivedFrom, _, _) in Table.Where(t => Weapon.KindOf(Catalogue[t.Id].Class) == WeaponKind.Caster))
        {
            var unique = TestWeapons.Get(id);
            var variant = TestWeapons.Get(derivedFrom, Catalogue[derivedFrom].Class == WeaponClass.Wand ? DamageType.Flaming : null);
            Assert.Equal("Resonant x1", unique.Forged.ToString());
            Assert.Equal((variant.Range, variant.Damage, variant.Cost, variant.ManaCost, variant.ResolvedManaCost, variant.AreaShape),
                (unique.Range, unique.Damage, unique.Cost, unique.ManaCost, unique.ResolvedManaCost, unique.AreaShape));
            Assert.True(unique.IsCaster);
        }
        Assert.Equal(("poison", 3), (TestWeapons.Get("rotwood").Innate!.Id, TestWeapons.Get("rotwood").Innate!.Tier));
        Assert.Equal(new[] { "shocking", "flaming" }, TestWeapons.Get("the_long_candle").Enchantments.Select(e => e.Id));
        Assert.Equal(DamageType.Shocking, TestWeapons.Get("the_long_candle").Innate!.Def.DamageType);
        Assert.Equal(DamageType.Flaming, TestWeapons.Get("wand_of_the_nova_unique").Innate!.Def.DamageType);
        Assert.Equal(new[] { "regeneration", "overheal" }, TestWeapons.Get("staff_of_renewal_unique").Enchantments.Select(e => e.Id));

        // A wand unique is the one weapon whose damage type is not a roll: it cannot be made as another.
        Assert.Throws<ArgumentException>(() => TestWeapons.Get("the_long_candle", DamageType.Flaming));
        Assert.Throws<ArgumentException>(() => TestWeapons.Get("wand_of_the_nova_unique", DamageType.Cold));

        // Refused: a caster unique that changes its cost, or whose list is the variant's own, and anything
        // arriving with more than three enchantments, which is the most any weapon drops carrying.
        var ex = LoadEditing("rotwood", w => w with { Cost = 50 });
        Assert.Equal(("rotwood", ContentValidator.RuleUniqueStatline), (ex.EntryId, ex.Rule));
        ex = LoadEditing("rotwood", w => w with { Enchantments = [new EnchantmentRef("poison")] });
        Assert.Equal(("rotwood", ContentValidator.RuleCasterUniqueEnchantments), (ex.EntryId, ex.Rule));
        ex = LoadWith(Staff("test_four_souls", "staff_of_renewal", "regeneration", "overheal", "ward", "mire"));
        Assert.Equal(("test_four_souls", ContentValidator.RuleArrivingEnchantments), (ex.EntryId, ex.Rule));
        Assert.All(Catalogue.All, w => Assert.True(w.Enchantments.Count <= 3, $"{w.Id} arrives with {w.Enchantments.Count}"));
    }

    [Fact]
    public void UniqueSouls_AreNeverRolled_AndTierPinned()
    {
        // The unique flag gates two things and nothing else: the tier is pinned at 1, and no roll grants
        // the entry — the latter in restricted.json, cross-checked on load. Vampiric is the catalogue
        // entry the dagger arrives with at tier 3: not unique, rollable, its tier its own. The catalogue
        // in file order: the eight innates, Vampiric, then the nine souls in the §3.3 table's order.
        Assert.Equal(
            new[]
            {
                "regeneration", "ward", "poison", "mire", "flaming", "cold", "shocking", "acidic",
                "vampiric",
                "siphon", "weightless", "sturdy", "momentum", "overheal", "serrated", "immovable", "piercing", "burning",
            },
            Enchantments.All.Select(e => e.Id));
        foreach (var id in Souls)
        {
            var soul = Enchantments[id];
            Assert.True(soul.Unique, $"{id} is not unique");
            Assert.Contains(id, Enchantments.NeverRolled);
            Assert.Contains(id, ContentDefaults.Restricted.NeverRolled);
            Assert.DoesNotContain(Enchantments.Rollable, e => e.Id == id);
            Assert.Equal(1, new Enchantment(soul, 3).Tier);
            Assert.Equal(1, (new Enchantment(soul, 1) with { Tier = 3 }).Tier);   // no path lifts the pin, a copy included
        }
        Assert.Equal(Souls.Length, Enchantments.NeverRolled.Count);
        Assert.Equal(8 + 1, Enchantments.Rollable.Count);
        Assert.False(Enchantments["vampiric"].Unique);
        Assert.Contains(Enchantments.Rollable, e => e.Id == "vampiric");
        Assert.Equal(3, new Enchantment(Enchantments["vampiric"], 3).Tier);

        // Instantiation pins the souls and honours the catalogue entry's tier.
        var knife = TestWeapons.Get("flensing_knife_unique");
        Assert.Equal(new[] { ("serrated", 1), ("vampiric", 3), ("overheal", 1) }, knife.Enchantments.Select(e => (e.Id, e.Tier)));
        Assert.Equal(3, knife.Enchantments[1].LevelsFor(1));   // tier scales a catalogue entry: 3 HP a drink
        Assert.Equal(3, TestWeapons.Get("rotwood").Innate!.Tier);
        using (TestContent.Use(weapons: new WeaponsData([.. ContentDefaults.Weapons.Weapons,
            Martial("test_deep_soul", "tower_guard", [(Block, 3), (Push, 1)], ("sturdy", 3))])))
            Assert.Equal(1, Assert.Single(TestWeapons.Get("test_deep_soul").Enchantments).Tier);

        // The cross-check, both ways: a unique entry missing from neverRolled, a never-rolled id that is
        // not unique, and a soul whose flag was dropped, each abort naming the entry.
        var ex = Assert.Throws<ContentException>(() => GameContent.Load(ContentDefaults.Tuning,
            ContentDefaults.Restricted with { NeverRolled = Souls.Where(s => s != "burning").ToArray() },
            ContentDefaults.Enchantments, ContentDefaults.Weapons));
        Assert.Equal(("burning", ContentValidator.RuleUniqueNeverRolled), (ex.EntryId, ex.Rule));
        ex = Assert.Throws<ContentException>(() => GameContent.Load(ContentDefaults.Tuning,
            ContentDefaults.Restricted with { NeverRolled = [.. Souls, "flaming"] },
            ContentDefaults.Enchantments, ContentDefaults.Weapons));
        Assert.Equal(("flaming", ContentValidator.RuleUniqueNeverRolled), (ex.EntryId, ex.Rule));
        ex = Assert.Throws<ContentException>(() => GameContent.Load(ContentDefaults.Tuning, ContentDefaults.Restricted,
            new EnchantmentsData(ContentDefaults.Enchantments.Enchantments.Select(e => e.Id == "siphon" ? e with { Unique = false } : e).ToList()),
            ContentDefaults.Weapons));
        Assert.Equal(("siphon", ContentValidator.RuleUniqueNeverRolled), (ex.EntryId, ex.Rule));

        // And a soul exists only on the uniques that carry it: content handing one to a variant is refused.
        ex = LoadEditing("tower_guard", w => w with { Enchantments = [new EnchantmentRef("sturdy")] });
        Assert.Equal(("tower_guard", ContentValidator.RuleUniqueSoulOnUnique), (ex.EntryId, ex.Rule));
        Assert.All(Catalogue.All.Where(w => !w.Unique), w => Assert.DoesNotContain(w.Enchantments, r => Enchantments[r.Id].Unique));
    }

    [Fact]
    public void Souls_CarryTheCatalogueNumbers_MagnitudesQuoteNoFlatTrigger()
    {
        // The §3.3 table as loaded: lock and trigger per soul, and Vampiric's catalogue row. A soul that is
        // a rule quotes a flat trigger — whole or nothing for the ones that fire once, scaled for Overheal's
        // grant — and one that is a magnitude — Serrated's
        // Bleeding, Burning's Searing — quotes none and applies a status instead, so its price rides the
        // ladder over the levels it lands, and names the source it takes its percentage of.
        var rules = new (string Id, EffectKind Kind, int Lock, int Trigger)[]
        {
            ("vampiric", EffectKind.Vampiric, 20, 2), ("siphon", EffectKind.Siphon, 20, 5), ("weightless", EffectKind.Weightless, 25, 5),
            ("sturdy", EffectKind.Sturdy, 30, 40), ("momentum", EffectKind.Momentum, 30, 10), ("overheal", EffectKind.Overheal, 25, 8),
            ("immovable", EffectKind.Immovable, 25, 10), ("piercing", EffectKind.Piercing, 20, 6),
        };
        foreach (var (id, kind, @lock, trigger) in rules)
        {
            var def = Enchantments[id];
            Assert.Equal((kind, @lock, (int?)trigger, (StatusEffectType?)null), (def.Effect, def.Lock, def.Trigger, def.Applies));
        }
        Assert.Equal(15, Enchantments["siphon"].Potency);   // a kill restores 15: net +10
        Assert.Equal(1, Enchantments["vampiric"].Potency);  // 1 HP per tier, per instance

        var serrated = Enchantments["serrated"];
        Assert.Equal((EffectKind.Serrated, 20, (int?)null, (StatusEffectType?)StatusEffectType.Bleeding, 20),
            (serrated.Effect, serrated.Lock, serrated.Trigger, serrated.Applies, serrated.ApplyPercent));   // BleedPercent: Tuning.ApplyPercent["serrated"]
        var burning = Enchantments["burning"];
        Assert.Equal((EffectKind.LingeringElement, 20, (int?)null, (StatusEffectType?)StatusEffectType.Searing, (DamageType?)DamageType.Flaming),
            (burning.Effect, burning.Lock, burning.Trigger, burning.Applies, burning.DamageType));

        // Refused: a magnitude quoting a flat trigger instead of its status, or beside it, and a lingering
        // element naming no element.
        var ex = LoadEnchantmentEditing("serrated", e => e with { Trigger = 6, Applies = null });
        Assert.Equal(("serrated", ContentValidator.RuleApplierNamesStatus), (ex.EntryId, ex.Rule));
        ex = LoadEnchantmentEditing("serrated", e => e with { Trigger = 6 });
        Assert.Equal(("serrated", ContentValidator.RuleTriggerXorApplies), (ex.EntryId, ex.Rule));
        ex = LoadEnchantmentEditing("burning", e => e with { DamageType = null });
        Assert.Equal(("burning", ContentValidator.RuleLingerNamesElement), (ex.EntryId, ex.Rule));
    }

    [Fact]
    public void MartialUniques_CarryOneUniqueSoul_CasterUniques_TakeOneOfThreeShapes()
    {
        // A martial unique that raises one modifier to x3 carries exactly one enchantment, and one that
        // exists nowhere else — "one modifier at x3, plus its unique enchantment": not none, not a catalogue
        // entry however deep, not two souls. A second soul is what only the Light shape buys (above).
        var ex = LoadEditing("the_bulwark", w => w with { Enchantments = [] });
        Assert.Equal(("the_bulwark", ContentValidator.RuleMartialUniqueOneSoul), (ex.EntryId, ex.Rule));
        ex = LoadWith(Martial("test_flaming_wall", "tower_guard", [(Block, 3), (Push, 1)], "flaming"));
        Assert.Equal(("test_flaming_wall", ContentValidator.RuleMartialUniqueOneSoul), (ex.EntryId, ex.Rule));
        Assert.Contains("'flaming' is a catalogue entry", ex.Message);
        ex = LoadWith(Martial("test_deep_leech", "tower_guard", [(Block, 3), (Push, 1)], ("vampiric", 3)));
        Assert.Equal(("test_deep_leech", ContentValidator.RuleMartialUniqueOneSoul), (ex.EntryId, ex.Rule));
        ex = LoadWith(Martial("test_two_souls", "tower_guard", [(Block, 3), (Push, 1)], "sturdy", "siphon"));
        Assert.Equal(("test_two_souls", ContentValidator.RuleMartialUniqueOneSoul), (ex.EntryId, ex.Rule));
        Assert.Contains("2 enchantments", ex.Message);

        // A caster unique raises nothing, so its artifact is one of three shapes on its innate: a unique
        // enchantment beside it (the Nova's Burning, the Renewal's Overheal), the innate alone at tier 3
        // (Rotwood's Poison), or two non-opposing catalogue entries at tier 1 (the Long Candle).
        Assert.Equal(new[] { ("poison", 3) }, Catalogue["rotwood"].Enchantments.Select(r => (r.Id, r.Tier)));
        Assert.Equal(new[] { ("shocking", 1), ("flaming", 1) }, Catalogue["the_long_candle"].Enchantments.Select(r => (r.Id, r.Tier)));
        Assert.Equal(new[] { false, true }, Catalogue["wand_of_the_nova_unique"].Enchantments.Select(r => Enchantments[r.Id].Unique));
        Assert.Equal(new[] { false, true }, Catalogue["staff_of_renewal_unique"].Enchantments.Select(r => Enchantments[r.Id].Unique));

        // Refused: a wand fixing one plain element (an ordinary drop's), an innate deepened short of tier 3,
        // a soul beside a deepened innate (two shapes at once), a third entry, and a staff starting from an
        // innate its variant does not cast.
        ex = LoadWith(Wand("test_plain_wand", "wand_of_the_blast", "flaming"));
        Assert.Equal(("test_plain_wand", ContentValidator.RuleCasterUniqueShape), (ex.EntryId, ex.Rule));
        ex = LoadEditing("rotwood", w => w with { Enchantments = [new EnchantmentRef("poison", 2)] });
        Assert.Equal(("rotwood", ContentValidator.RuleCasterUniqueShape), (ex.EntryId, ex.Rule));
        Assert.Contains("'poison' at tier 2", ex.Message);
        ex = LoadEditing("staff_of_renewal_unique", w => w with { Enchantments = [new EnchantmentRef("regeneration", 3), new EnchantmentRef("overheal")] });
        Assert.Equal(("staff_of_renewal_unique", ContentValidator.RuleCasterUniqueShape), (ex.EntryId, ex.Rule));
        ex = LoadWith(Staff("test_three", "staff_of_renewal", "regeneration", "ward", "poison"));
        Assert.Equal(("test_three", ContentValidator.RuleCasterUniqueShape), (ex.EntryId, ex.Rule));
        ex = LoadEditing("rotwood", w => w with { Enchantments = [new EnchantmentRef("ward", 3)] });   // a Staff of Blight casts Poison
        Assert.Equal(("rotwood", ContentValidator.RuleCasterUniqueShape), (ex.EntryId, ex.Rule));
        Assert.Contains("'poison'", ex.Message);

        // Legitimate: a wand fixing its element at tier 3, and a staff dropping with a second catalogue entry.
        using var _ = TestContent.Use(weapons: new WeaponsData([.. ContentDefaults.Weapons.Weapons,
            Wand("test_deep_ember", "wand_of_the_blast", "flaming") with { Enchantments = [new EnchantmentRef("flaming", 3)] },
            Staff("test_mending_ward", "staff_of_renewal", "regeneration", "ward")]));
        Assert.Equal(("flaming", 3), (TestWeapons.Get("test_deep_ember").Innate!.Id, TestWeapons.Get("test_deep_ember").Innate!.Tier));
        Assert.Equal(new[] { "regeneration", "ward" }, TestWeapons.Get("test_mending_ward").Enchantments.Select(e => e.Id));
    }

    [Fact]
    public void Overheal_OnlyBesideAHealingSource()
    {
        // Overheal alone is inert, so it appears only beside something that heals the wielder: the dagger's
        // Vampiric, the staff's Regeneration. Content placing it anywhere else is refused, naming the entry.
        Assert.Contains("vampiric", Catalogue["flensing_knife_unique"].Enchantments.Select(e => e.Id));
        Assert.Contains("regeneration", Catalogue["staff_of_renewal_unique"].Enchantments.Select(e => e.Id));

        var ex = LoadWith(Martial("test_alone", "tower_guard", [(Block, 3), (Push, 1)], "overheal"));
        Assert.Equal(("test_alone", ContentValidator.RuleOverhealBesideHealing), (ex.EntryId, ex.Rule));
        ex = LoadWith(Martial("test_no_heal", "flensing_knife", [(CritWindow, 3), (CritMultiplier, 1), (Light, 1)], "serrated", "overheal"));
        Assert.Equal(("test_no_heal", ContentValidator.RuleOverhealBesideHealing), (ex.EntryId, ex.Rule));
        ex = LoadWith(Staff("test_warding_overheal", "staff_of_warding", "ward", "overheal"));   // Ward absorbs; it does not heal
        Assert.Equal(("test_warding_overheal", ContentValidator.RuleOverhealBesideHealing), (ex.EntryId, ex.Rule));

        // And the other dependency: a lingering element sits after its element, on the same weapon.
        ex = LoadWith(Martial("test_cold_burn", "tower_guard", [(Block, 3), (Push, 1)], "burning"));
        Assert.Equal(("test_cold_burn", ContentValidator.RuleEnchantmentDependency), (ex.EntryId, ex.Rule));
        Assert.Contains("'burning' needs 'flaming' ahead of it", ex.Message);
        ex = LoadWith(Wand("test_backwards", "wand_of_the_nova", "burning", "flaming"));
        Assert.Equal(("test_backwards", ContentValidator.RuleEnchantmentDependency), (ex.EntryId, ex.Rule));
        Assert.Equal(new[] { "flaming" }, Enchantments.Prerequisites("burning"));
        Assert.Empty(Enchantments.Prerequisites("flaming"));

        // A dependency needs the combo platform: only a weapon forged with a currency modifier — Light earns
        // the second trigger's mana, Resonant spends less on both — may carry an entry that depends on another.
        ex = LoadWith(Martial("test_burning_blade", "tower_guard", [(Block, 3), (Push, 1)], "flaming", "burning"));
        Assert.Equal(("test_burning_blade", ContentValidator.RuleDependencyOnCurrency), (ex.EntryId, ex.Rule));
        ex = LoadWith(Martial("test_drinking_axe", "great_axe", [(Cleave, 3), (Opportunist, 1)], ("vampiric", 3), ("overheal", 1)));
        Assert.Equal(("test_drinking_axe", ContentValidator.RuleDependencyOnCurrency), (ex.EntryId, ex.Rule));
        Assert.All(Catalogue.All.Where(w => w.Enchantments.Any(r => r.Id is "overheal" or "burning")),
            w => Assert.True(w.Forged.ContainsKey(Light) || w.Forged.ContainsKey(Resonant), $"{w.Id} carries a dependency off the currency group"));

        // Both legitimate homes load beside the defaults, with the element ahead of the burn.
        using var _ = TestContent.Use(weapons: new WeaponsData([.. ContentDefaults.Weapons.Weapons,
            Martial("test_leech", "flensing_knife", [(CritWindow, 3), (CritMultiplier, 1), (Light, 1)], ("vampiric", 3), ("overheal", 1)),
            Wand("test_cinder", "wand_of_the_blast", "flaming", "burning")]));
        Assert.Equal(new[] { "vampiric", "overheal" }, TestWeapons.Get("test_leech").Enchantments.Select(e => e.Id));
        Assert.Equal(new[] { "flaming", "burning" }, TestWeapons.Get("test_cinder").Enchantments.Select(e => e.Id));
    }

    [Fact]
    public void Allowed_HoldsOverUniques()
    {
        // The same walk the thirty-two get, over the eleven: every forged modifier is allowed beside the rest
        // of its spread on its kind of weapon, so no unique quietly gives someone two reactions or a
        // displacement pair.
        foreach (var def in Catalogue.Uniques)
        {
            var weapon = TestWeapons.Get(def.Id);
            foreach (var (t, n) in weapon.Forged.Entries)
                Assert.True(Rules.Allowed(t, weapon.Kind, weapon.Forged), $"{def.Id}: {t} x{n} is not allowed beside {weapon.Forged}");
        }

        // A unique that would break a relation is refused for the relation before the derivation rule sees it.
        var ex = LoadWith(Martial("test_two_zones", "phalanx_spear", (Brace, 3), (Longshot, 1), (Opportunist, 1)));
        Assert.Equal(("test_two_zones", ContentValidator.RuleForgedNotAllowed), (ex.EntryId, ex.Rule));
    }

    // ── The named uniques, as the doc reads them ─────────────────────────────

    [Fact]
    public void Widowmaker_CritsOn17_CapsAt8()
    {
        // CritWindow x3: finds the gap on 17+, and a ceiling of 8 with five stacks of headroom above it — 12+ fully worked.
        var widowmaker = TestWeapons.Get("widowmaker");
        Assert.Equal("CritWindow x3, CritMultiplier x1", widowmaker.Forged.ToString());
        Assert.Equal(17, CombatRules.CritThreshold(widowmaker));
        Assert.Equal(8, Rules.Cap(CritWindow, widowmaker.Forged.Stacks(CritWindow)));

        var a = Member(widowmaker);
        var dummy = Member(null, "B");
        Assert.Equal((RollOutcome.Crit, 45), (Settle(a, dummy, 17).Outcome, Settle(a, dummy, 17).Dealt));   // 15 x3
        Assert.Equal((RollOutcome.Normal, 15), (Settle(a, dummy, 16).Outcome, Settle(a, dummy, 16).Dealt));

        widowmaker.Acquire(CritWindow, 10);
        Assert.Equal(8, widowmaker.Stacks(CritWindow));
        Assert.Equal(3, widowmaker.Forged.Stacks(CritWindow));
        Assert.Equal(12, CombatRules.CritThreshold(widowmaker));
        Assert.True(Settle(a, dummy, 12).IsCrit);
    }

    [Fact]
    public void Stormcrow_CritsOn19_LongshotX3()
    {
        // Longshot x3 and CritWindow x1 — the Longbow's window, not the stale ladder row: +3 a tile beyond
        // the free three, so 26 at ten tiles, 52 on a 19, and the 5 it looks like at knife range.
        var stormcrow = TestWeapons.Get("stormcrow");
        Assert.Equal("CritWindow x1, Longshot x3", stormcrow.Forged.ToString());
        Assert.Equal(19, CombatRules.CritThreshold(stormcrow));
        var a = Member(stormcrow);
        Assert.Equal(3, a.Value(Longshot));
        var target = Member(null, "B");

        Assert.Equal(26, Settle(a, target, 10, distanceUnits: 320).Dealt);   // 5 + 7 x 3
        Assert.Equal(52, Settle(a, target, 19, distanceUnits: 320).Dealt);
        Assert.Equal(RollOutcome.Normal, Settle(a, target, 18, distanceUnits: 320).Outcome);
        Assert.Equal(5, Settle(a, target, 10, distanceUnits: 96).Dealt);
    }

    [Fact]
    public void Bulwark_Absorbs9()
    {
        // Block x3 absorbs 9; worked to its ceiling of x8 it absorbs 24, the Bulwark's ceiling the combat doc quotes.
        var bulwark = TestWeapons.Get("the_bulwark");
        Assert.Equal("Block x3, Push x1", bulwark.Forged.ToString());
        var shield = Member(bulwark, "B");
        Assert.Equal(9, shield.Value(Block));
        var stiletto = Member(TestWeapons.Get("weakspot_stiletto"));
        var hit = Settle(stiletto, shield, 10);
        Assert.Equal((15, 9, 6, true), (hit.WeaponShare, hit.Absorbed, hit.Dealt, hit.Blocked));

        bulwark.Acquire(Block, 5);
        Assert.Equal((8, 24), (bulwark.Stacks(Block), shield.Value(Block)));
        var maul = Member(TestWeapons.Make("Maul", 40, 30, 30));
        Assert.Equal((24, 6), (Settle(maul, shield, 10).Absorbed, Settle(maul, shield, 10).Dealt));
        Assert.Equal(1, Settle(stiletto, shield, 10).Dealt);   // never below 1 of the weapon's share
    }

    [Fact]
    public void Shieldbreaker_Ignores9()
    {
        // Splitting x3 ignores 9 of the target's Block across the swing: the Bulwark's 9 is nothing to it.
        var shieldbreaker = TestWeapons.Get("shieldbreaker");
        Assert.Equal("Cleave x1, Splitting x3, Opportunist x1", shieldbreaker.Forged.ToString());
        var axe = Member(shieldbreaker);
        Assert.Equal(9, axe.Value(Splitting));
        var shield = Member(TestWeapons.Get("the_bulwark"), "B");
        Assert.Equal(0, CombatBehaviours.BlockAgainst(axe, shield));
        Assert.Equal((18, 0, false), (Settle(axe, shield, 10).Dealt, Settle(axe, shield, 10).Absorbed, Settle(axe, shield, 10).Blocked));

        shield.EquippedWeapon!.Acquire(Block, 1);   // x4: 12, of which 9 are ignored
        Assert.Equal(3, CombatBehaviours.BlockAgainst(axe, shield));
        Assert.Equal(15, Settle(axe, shield, 10).Dealt);
    }

    [Fact]
    public void HoplitesWall_ThreeRetaliations()
    {
        // Brace x3: three retaliations a turn at full reach — a fourth entry into the line's reach goes
        // unanswered until the next turn — each paying Longshot's point at the fourth tile.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "tower_guard");
        var s = Char("S", 5, 11, "hoplites_wall");   // reach covers tiles 7..10 of the row
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a, s }, new[] { enemy }, () => 10);
        Assert.Equal(3, s.Value(Brace));
        int braces = 0;
        turns.BraceTriggered += _ => braces++;

        for (int entry = 1; entry <= 4; entry++)
        {
            (enemy.X, enemy.Y) = At(5, 6);
            turns.NotifyActorMoved(enemy, MoveKind.Forced);   // back out of reach: the pair is released
            a.DistLeft = GameConstants.MaxDistance;
            Assert.True(turns.TryAttack(a, enemy));           // shoved to tile 7, into the line
            Assert.Equal(At(5, 7), (enemy.X, enemy.Y));
            Assert.Equal(Math.Min(entry, 3), braces);
        }
        Assert.Equal(200 - 4 * 7 - 3 * 5, enemy.Hp);   // four sword hits into Block 3, three braces of 7 + 1 into Block 3
    }

    [Fact]
    public void FeatheredDeath_FourThrows()
    {
        // Charges x3: four throws a turn — the Offset's one plus three — and no fifth with movement to spare.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "feathered_death");
        Assert.Equal("CritMultiplier x1, Charges x3", a.EquippedWeapon!.Forged.ToString());
        var enemy = Enemy(5, 8, hp: 1000);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        Assert.Equal(4, turns.AttacksPerTurn(a));

        for (int i = 0; i < 4; i++)
            Assert.True(turns.TryAttack(a, enemy));
        Assert.False(turns.CanAttack(a, enemy));
        Assert.Equal(GameConstants.MaxDistance - 4 * 15, a.DistLeft);
        Assert.Equal(1000 - 4 * 6, enemy.Hp);   // 9 into Block 3, four times
        Assert.Equal(8, Rules.Cap(Charges, 3));   // nine throws for 135 at the ceiling
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>A martial test unique: the statline of <paramref name="derivedFrom"/>'s class, the given spread, and catalogue souls at tier 1.</summary>
    private static WeaponDef Martial(string id, string? derivedFrom, params (ModifierType Type, int Stacks)[] forged)
        => Martial(id, derivedFrom, forged, Array.Empty<string>());

    private static WeaponDef Martial(string id, string? derivedFrom, (ModifierType Type, int Stacks)[] forged, params string[] enchantmentIds)
        => Martial(id, derivedFrom, forged, enchantmentIds.Select(e => (e, 1)).ToArray());

    private static WeaponDef Martial(string id, string? derivedFrom, (ModifierType Type, int Stacks)[] forged, params (string Id, int Tier)[] enchantments)
    {
        var variant = ContentDefaults.Weapons.Weapons.First(w => w.Id == (derivedFrom == "the_bulwark" ? "tower_guard" : derivedFrom ?? "tower_guard"));
        return new WeaponDef(id, id, variant.Class, Role: null, variant.Range, variant.Damage, variant.Cost, variant.ManaCost,
            forged.ToDictionary(f => f.Type, f => f.Stacks),
            enchantments.Select(e => new EnchantmentRef(e.Id, e.Tier)).ToList(),
            Shape: null, Unique: true, DerivedFrom: derivedFrom);
    }

    private static WeaponDef Staff(string id, string derivedFrom, params string[] enchantmentIds)
    {
        var variant = ContentDefaults.Weapons.Weapons.First(w => w.Id == derivedFrom);
        return variant with { Id = id, Name = id, Enchantments = enchantmentIds.Select(e => new EnchantmentRef(e)).ToList(), Unique = true, DerivedFrom = derivedFrom };
    }

    private static WeaponDef Wand(string id, string derivedFrom, params string[] enchantmentIds)
        => Staff(id, derivedFrom, enchantmentIds);

    private static ContentException LoadWith(WeaponDef extra)
        => Assert.Throws<ContentException>(() => GameContent.Load(
            ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments,
            new WeaponsData([.. ContentDefaults.Weapons.Weapons, extra])));

    private static ContentException LoadEnchantmentEditing(string id, Func<EnchantmentDef, EnchantmentDef> edit)
        => Assert.Throws<ContentException>(() => GameContent.Load(
            ContentDefaults.Tuning, ContentDefaults.Restricted,
            new EnchantmentsData(ContentDefaults.Enchantments.Enchantments.Select(e => e.Id == id ? edit(e) : e).ToList()),
            ContentDefaults.Weapons));

    private static ContentException LoadEditing(string id, Func<WeaponDef, WeaponDef> edit)
        => Assert.Throws<ContentException>(() => GameContent.Load(
            ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments,
            new WeaponsData(ContentDefaults.Weapons.Weapons.Select(w => w.Id == id ? edit(w) : w).ToList())));
}

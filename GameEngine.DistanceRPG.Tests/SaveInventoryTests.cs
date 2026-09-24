using System.Reflection;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The save's inventory of live state (1b), as code: every instance field of
/// every type the save walks is listed here as saved (and where in the
/// document), derived (and from what), or not saved (and why). A field added
/// later — a counter, a cache, a flag — fails this until someone decides which
/// it is, so nothing new slips out of the save by being forgotten. Phase 5.6
/// extends it to the visit's types and 5.8 to the scenes'.
/// </summary>
public class SaveInventoryTests
{
    private enum Kept
    {
        /// <summary>Written to the document, and handed back by a load.</summary>
        Saved,

        /// <summary>Replayed on read from what is saved, or read off the content by a saved id.</summary>
        Derived,

        /// <summary>Not in the document, for the reason given.</summary>
        NotSaved,
    }

    private sealed record Row(string Member, Kept Kept, string Why);

    private static readonly Dictionary<Type, Row[]> Inventory = new()
    {
        [typeof(CampaignState)] =
        [
            new("_party", Kept.Saved, "campaign.party[], in roster order"),
            new("_seen", Kept.Saved, "campaign.seen[] = SeenInOrder — never the set's own enumeration"),
            new("RunCounter", Kept.Saved, "campaign.runCounter"),
            new("_lockedUntilRun", Kept.Saved, "campaign.locks[] = LockedInOrder, an early offer's rewrite as it stands"),
            new("_cleared", Kept.Saved, "campaign.cleared[] = ClearedInOrder"),
            new("_retired", Kept.Saved, "campaign.retired[] = RetiredInOrder"),
            new("LastResult", Kept.Saved, "campaign.lastResult: the summary over the list between runs survives a relaunch"),
            new("Visit", Kept.NotSaved, "the visit in progress is Phase 5.6's to save; until then SaveGame.Capture refuses a campaign holding one, and between runs it is null"),
            new("_onWiped", Kept.NotSaved, "wiring: the subscription Enter makes on a visit's Wiped, remade with the visit"),
            new("RunEnded", Kept.NotSaved, "wiring: whoever plays the campaign subscribes afresh"),
        ],
        [typeof(ActorState)] =
        [
            new("X", Kept.Saved, "body.x, the shortest text that reads back to the same float"),
            new("Y", Kept.Saved, "body.y, likewise"),
            new("_hp", Kept.Saved, "body.hp as stored (RawHp): null is unwritten, which reads as the maximum"),
            new("_mana", Kept.Saved, "body.mana as stored (RawMana): null is unwritten, which reads as the spendable ceiling"),
            new("Alive", Kept.Saved, "body.alive"),
            new("HeldShots", Kept.Saved, "body.heldShots"),
            new("StatusEffects", Kept.Saved, "body.statuses[] in landing order, OverhealPool among them — §5.1's Overheal remainder"),
            new("Innate", Kept.Saved, "a member's innate: modifiers by name (empty on every member today); an enemy's is re-derived (5.6)"),
            new("Attunement", Kept.Saved, "a member's attunement by name (none today); an enemy's is its dungeon's (5.6)"),
        ],
        [typeof(PartyMemberState)] =
        [
            new("Id", Kept.Saved, "id: the roster's stable id"),
            new("ColorIndex", Kept.Saved, "colorIndex"),
            new("Stats", Kept.Saved, "stats: the save's spread, never re-read from party.json"),
            new("WeaponXp", Kept.Saved, "weaponXp by class name, zeros left out — never by position"),
            new("HpXp", Kept.Saved, "hpXp"),
            new("ManaXp", Kept.Saved, "manaXp"),
            new("Inventory", Kept.Saved, "inventory[], its length the count the member was built with"),
            new("DistLeft", Kept.Saved, "distLeft: a save mid-turn resumes mid-turn"),
            new("EffectiveMax", Kept.Saved, "effectiveMax"),
            new("SavedMovement", Kept.Saved, "savedMovement"),
            new("BudgetBeforeMire", Kept.Saved, "budgetBeforeMire: the whole this turn's cap was cut from"),
        ],
        [typeof(Weapon)] =
        [
            new("Id", Kept.Saved, "id: the catalogue row"),
            new("Forged", Kept.Saved, "forged, by modifier name: the identity spread a ceiling is read off"),
            new("Modifiers", Kept.Saved, "modifiers, by modifier name: the live total"),
            new("_enchantments", Kept.Saved, "enchantments[] in attachment order, (id, tier, xp) — never sorted"),
            new("ForgedEnchantmentCount", Kept.Saved, "forgedEnchantments"),
            new("Enchantments", Kept.Derived, "the read-only view of _enchantments"),
            new("Name", Kept.Derived, "the row's, by id: a content edit to a statline reaches every saved copy"),
            new("Class", Kept.Derived, "the row's, by id"),
            new("Role", Kept.Derived, "the row's, by id"),
            new("Range", Kept.Derived, "the row's, by id"),
            new("Damage", Kept.Derived, "the row's, by id"),
            new("Cost", Kept.Derived, "the row's, by id"),
            new("ManaCost", Kept.Derived, "the row's, by id"),
            new("AreaShape", Kept.Derived, "the row's, by id"),
            new("Unique", Kept.Derived, "the row's, by id"),
            new("ResolvedCost", Kept.Derived, "Resolve(): the cost less Light's discount on the live spread"),
            new("ResolvedManaCost", Kept.Derived, "Resolve(): the mana cost less Resonant's"),
        ],
        [typeof(Enchantment)] =
        [
            new("Def", Kept.Saved, "id: the content's own entry by id — the tuned def, so a unique's pin holds"),
            new("_tier", Kept.Saved, "tier"),
            new("Xp", Kept.Saved, "xp: the remainder into the tier"),
        ],
        [typeof(WeaponXpBook)] =
        [
            new("_xp", Kept.Saved, "weaponXp: each class by name, zeros left out"),
        ],
    };

    /// <summary>A field's member name: an auto-property's backing field is its property, a field-like event its event.</summary>
    private static string MemberOf(FieldInfo field)
    {
        string name = field.Name;
        return name.StartsWith('<') && name.EndsWith(">k__BackingField", StringComparison.Ordinal) ? name[1..name.IndexOf('>')] : name;
    }

    [Fact]
    public void EveryFieldIsSavedDerivedOrNamedAsNot()
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var (type, rows) in Inventory)
        {
            var fields = type.GetFields(Declared).Select(MemberOf).Order(StringComparer.Ordinal).ToArray();
            var listed = rows.Select(row => row.Member).Order(StringComparer.Ordinal).ToArray();
            Assert.True(listed.Distinct().Count() == listed.Length, $"{type.Name} lists a member twice");
            Assert.True(fields.SequenceEqual(listed),
                $"{type.Name}: every instance field is saved, derived or named as not. Unlisted: [{string.Join(", ", fields.Except(listed))}]; listed but gone: [{string.Join(", ", listed.Except(fields))}]");
            Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Why), $"{type.Name}.{row.Member} says neither where nor why"));
        }

        // The scan reads real fields, the raw pools among them: the ones a save
        // must keep below the public state, which reads them as their ceilings.
        Assert.Contains("_hp", typeof(ActorState).GetFields(Declared).Select(MemberOf));
        Assert.Contains("BudgetBeforeMire", typeof(PartyMemberState).GetFields(Declared).Select(MemberOf));
    }
}

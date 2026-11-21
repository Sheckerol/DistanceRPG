using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The user's ruling of 2026-11-21, after play ("I am running out of slots without
/// even getting out", "30 per individual or three rows of 10", "we shouldn't be
/// relying on pressing keys to swap ... for now having right click equip is fine,
/// but a gui with mouse over text"): the dungeon's bag is a grid of cells, ten to
/// a row, each naming what it holds in two short lines; the cell under the mouse
/// is read whole in a tooltip; and a right click on a cell picks its slot. Where
/// the cells are drawn is the drawing's; where each lies, what it says, what a
/// tooltip says and where it opens, and what a right click reaches are pure, and
/// walked here.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class BagGridTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private const string Font = @"^[A-Z0-9 .,:_!?()/<>\[\]+=#*-]+$";

    [Fact]
    public void TheGridIsTenAcross_CentredInTheWindow()
    {
        // In the shipped window a cell and its gap are 96 px: ten of them, less the
        // last gap, centred. A narrower window's cells shrink to keep ten across
        // inside its margins.
        Assert.Equal(10, DungeonHud.BagColumns);
        Assert.Equal(DungeonHud.BagCellPitchMost, DungeonHud.BagCellPitch(1280f));
        float width = DungeonHud.InventoryWidth(1280f);
        Assert.Equal(10 * 96f - DungeonHud.BagCellGap, width);
        Assert.Equal((1280f - width) / 2f, DungeonHud.InventoryLeft(1280f));
        Assert.Equal(width - 36f, DungeonHud.InventoryDetailWidth(1280f));

        for (int w = 320; w <= 2560; w += 20)
        {
            float left = DungeonHud.InventoryLeft(w), across = DungeonHud.InventoryWidth(w);
            Assert.True(left >= 0f && left + across <= w, $"w={w}: the grid runs off the window");
            Assert.Equal(w / 2f, left + across / 2f, 3);
            Assert.InRange(DungeonHud.BagCellChars(DungeonHud.BagCellPitch(w)), 1, 11);
            Assert.True(DungeonHud.InventoryDetailWidth(w) > 0f);
        }
        Assert.True(DungeonHud.BagCellPitch(640f) < DungeonHud.BagCellPitchMost);
        Assert.Equal(11, DungeonHud.BagCellChars(DungeonHud.BagCellPitch(1280f)));
    }

    [Fact]
    public void ACellIsItsSlots_AndTheGapsAreNobodys()
    {
        // Bag slot 1 is the first cell of the first row, slot 11 the first of the
        // second; a point in a cell is that slot's, a point in a gap or past the
        // bag's last slot is none's, and the hand is no cell at all.
        const float left = 162f, top = 300f;
        float pitch = DungeonHud.BagCellPitch(1280f);
        Assert.Equal(new Vector2(left, top), DungeonHud.BagCell(1, left, top, pitch));
        Assert.Equal(new Vector2(left + 9 * pitch, top), DungeonHud.BagCell(10, left, top, pitch));
        Assert.Equal(new Vector2(left, top + DungeonHud.BagRowPitch), DungeonHud.BagCell(11, left, top, pitch));
        Assert.Equal(new Vector2(left + 9 * pitch, top + 2 * DungeonHud.BagRowPitch), DungeonHud.BagCell(30, left, top, pitch));

        const int slots = GameConstants.MaxInventorySlots;
        for (int slot = 1; slot < slots; slot++)
        {
            var cell = DungeonHud.BagCell(slot, left, top, pitch);
            Assert.Equal(slot, DungeonHud.BagSlotAt(cell, left, top, pitch, slots));
            Assert.Equal(slot, DungeonHud.BagSlotAt(cell + new Vector2(pitch - DungeonHud.BagCellGap - 1f, DungeonHud.BagCellHeight - 1f), left, top, pitch, slots));
            Assert.Equal(-1, DungeonHud.BagSlotAt(cell + new Vector2(pitch - 1f, 5f), left, top, pitch, slots));                      // the gap right of it
            Assert.Equal(-1, DungeonHud.BagSlotAt(cell + new Vector2(5f, DungeonHud.BagCellHeight + 1f), left, top, pitch, slots));   // the gap under it
        }
        Assert.Equal(-1, DungeonHud.BagSlotAt(new Vector2(left - 1f, top + 5f), left, top, pitch, slots));
        Assert.Equal(-1, DungeonHud.BagSlotAt(new Vector2(left + 5f, top - 1f), left, top, pitch, slots));
        Assert.Equal(-1, DungeonHud.BagSlotAt(new Vector2(left + 10 * pitch + 5f, top + 5f), left, top, pitch, slots));
        Assert.Equal(-1, DungeonHud.BagSlotAt(new Vector2(left + 5f, top + 3 * DungeonHud.BagRowPitch + 5f), left, top, pitch, slots));

        // The fixture's bag of five: a short row, and the cells past it are no slot's.
        Assert.Equal(5, DungeonHud.BagSlotAt(DungeonHud.BagCell(5, left, top, pitch) + new Vector2(2f, 2f), left, top, pitch, slots: 6));
        Assert.Equal(-1, DungeonHud.BagSlotAt(DungeonHud.BagCell(6, left, top, pitch) + new Vector2(2f, 2f), left, top, pitch, slots: 6));
        Assert.Equal(-1, DungeonHud.BagSlotAt(DungeonHud.BagCell(1, left, top, pitch) + new Vector2(2f, 2f), left, top, pitch, slots: 1));   // a hand and no bag
    }

    [Fact]
    public void ACellNamesItsItemOnTwoShortLines()
    {
        // Broken between words, a word longer than a line cut to it, and what two
        // lines do not hold left out: the tooltip has the whole name.
        Assert.Equal(("WAND OF THE", "BLAST"), DungeonHud.CellLabel("WAND OF THE BLAST", 11));
        Assert.Equal(("GREAT AXE", ""), DungeonHud.CellLabel("GREAT AXE", 11));
        Assert.Equal(("HEALTH", "POTION Q2"), DungeonHud.CellLabel("HEALTH POTION Q2", 11));
        Assert.Equal(("TARGE AND", "CLAYMORE"), DungeonHud.CellLabel("TARGE AND CLAYMORE", 11));
        Assert.Equal(("STAFF OF", "RENEWAL"), DungeonHud.CellLabel("STAFF OF RENEWAL", 8));
        Assert.Equal(("WEAK", "STIL"), DungeonHud.CellLabel("WEAKSPOT STILETTO", 4));
        Assert.Equal(("HEAL", "POTI"), DungeonHud.CellLabel("HEALTH POTION Q2147483647", 4));
        Assert.Equal(("A B", "C D"), DungeonHud.CellLabel("A B C D E F", 3));
        Assert.Equal(("", ""), DungeonHud.CellLabel("", 11));
        Assert.Equal(("", ""), DungeonHud.CellLabel("HATCHET", 0));

        // Every weapon the content ships and the widest potion a save may hold, at
        // every width a cell comes in: each line fits its cell, in the HUD's font,
        // and a named item's cell is never blank.
        var names = GameContent.Current.Weapons.All.Select(def => Weapon.Shown(def.Name))
            .Append(TestPotions.Health(ContentBounds.MostCount).ShownName)
            .Append(TestPotions.Mana(1).ShownName);
        foreach (string name in names)
            for (int chars = 1; chars <= 11; chars++)
            {
                var (first, second) = DungeonHud.CellLabel(name, chars);
                Assert.InRange(first.Length, 1, chars);
                Assert.InRange(second.Length, 0, chars);
                Assert.Matches(Font, first);
                Assert.StartsWith(first.Split(' ')[0], name, StringComparison.Ordinal);
            }

        // And the label over the grid counts what the bag holds, weapons and potions alike.
        var member = TestPools.Holding("A", TestWeapons.Get("hatchet"));
        Assert.Equal("BAG - 0 OF 5", DungeonHud.BagLabel(member));
        member.Inventory[2] = TestWeapons.Get("longbow");
        member.Inventory[4] = TestPotions.Health(2);
        Assert.Equal("BAG - 2 OF 5", DungeonHud.BagLabel(member));
        Assert.Matches(Font, DungeonHud.BagLabel(member));
        Assert.Matches(Font, DungeonHud.EmptyHand);
    }

    [Fact]
    public void ATooltipReadsTheItemWhole_AndSaysWhatARightClickDoes()
    {
        var member = TestPools.Holding("A", TestWeapons.Get("hatchet"));
        var bow = TestWeapons.Get("longbow");
        var potion = TestPotions.Health(2);
        member.Inventory[1] = bow;
        member.Inventory[2] = potion;
        float width = DungeonHud.TooltipTextWidth(1280f);
        Assert.Equal(DungeonHud.TooltipTextWidthMost, width);
        Assert.True(DungeonHud.TooltipTextWidth(320f) < DungeonHud.TooltipTextWidthMost);

        // A weapon's: the statline the hand's line would give it in this member's
        // hands, in a bag slot's colour. A potion's: what it restores now.
        Assert.Equal(DungeonHud.SlotDetailLines(bow, member, 0, DungeonHud.WeaponLineColor(1), width), DungeonHud.HoverLines(bow, 1, member, 0, width));
        Assert.NotEmpty(DungeonHud.HoverLines(bow, 1, member, 0, width));
        Assert.Equal(DungeonHud.PotionDetailLines(potion, member), DungeonHud.HoverLines(potion, 2, member, 0, width));

        // Its last line: what the click does, and with X armed, yellow, what it does then.
        Assert.Equal(new DungeonHud.Run("RIGHT CLICK: EQUIP", DungeonHud.Grey), DungeonHud.HoverAction(bow, leaving: false));
        Assert.Equal(new DungeonHud.Run("RIGHT CLICK: DRINK", DungeonHud.Grey), DungeonHud.HoverAction(potion, leaving: false));
        Assert.Equal(new DungeonHud.Run("RIGHT CLICK: LEAVE IT HERE", DungeonHud.Yellow), DungeonHud.HoverAction(bow, leaving: true));
        Assert.Equal(new DungeonHud.Run("RIGHT CLICK: POUR IT OUT", DungeonHud.Yellow), DungeonHud.HoverAction(potion, leaving: true));
        foreach (bool leaving in new[] { false, true })
            foreach (IItem item in new IItem[] { bow, potion })
                Assert.Matches(Font, DungeonHud.HoverAction(item, leaving).Text);

        // Its backing covers the widest of its lines and all of them, padded.
        var lines = DungeonHud.HoverLines(bow, 1, member, 0, width);
        var size = DungeonHud.TooltipSize(bow.ShownName, lines, "RIGHT CLICK: EQUIP");
        Assert.True(size.X >= bow.ShownName.Length * 6f * 2f);
        Assert.True(size.X >= "RIGHT CLICK: EQUIP".Length * 6f * 1.2f);
        Assert.All(lines, line => Assert.True(size.X >= (line.Sum(run => run.Text.Length) + 2 * (line.Count - 1)) * 6f * DungeonHud.InventoryDetailScale));
        var taller = DungeonHud.TooltipSize(bow.ShownName, [.. lines, .. lines], "RIGHT CLICK: EQUIP");
        Assert.Equal(lines.Count * 12f, taller.Y - size.Y);
        Assert.True(size.Y > 14f + lines.Count * 12f + 7f * 1.2f);
    }

    [Fact]
    public void ATooltipOpensBesideTheMouse_AndStaysInTheWindow()
    {
        var size = new Vector2(300f, 120f);
        float off = DungeonHud.TooltipOffset, margin = DungeonHud.TooltipMargin;

        // Below and right of the mouse while there is room; left of it at the
        // window's right, above it at the bottom.
        Assert.Equal(new Vector2(400f + off, 300f + off), DungeonHud.PlaceTooltip(new Vector2(400f, 300f), size, 1280f, 720f));
        Assert.Equal(new Vector2(1200f - off - 300f, 300f + off), DungeonHud.PlaceTooltip(new Vector2(1200f, 300f), size, 1280f, 720f));
        Assert.Equal(new Vector2(400f + off, 700f - off - 120f), DungeonHud.PlaceTooltip(new Vector2(400f, 700f), size, 1280f, 720f));

        // Wherever the mouse is, the whole of it is inside the margins — and never
        // over the mouse itself, while the window has the room either side.
        for (float x = 0f; x <= 1280f; x += 40f)
            for (float y = 0f; y <= 720f; y += 40f)
            {
                var at = DungeonHud.PlaceTooltip(new Vector2(x, y), size, 1280f, 720f);
                Assert.True(at.X >= margin && at.X + size.X <= 1280f - margin, $"({x}, {y}): off the sides");
                Assert.True(at.Y >= margin && at.Y + size.Y <= 720f - margin, $"({x}, {y}): off the top or bottom");
                bool over = x >= at.X && x < at.X + size.X && y >= at.Y && y < at.Y + size.Y;
                Assert.False(over, $"({x}, {y}): the tooltip is under the mouse");
            }

        // A window too small for it keeps its left and top.
        Assert.Equal(new Vector2(margin, margin), DungeonHud.PlaceTooltip(new Vector2(50f, 50f), size, 200f, 100f));
    }

    [Fact]
    public void ARightClickTakesItsOwnZones_AndALeftClickNeverReachesThem()
    {
        // A cell of the bag is the right click's: the left is kept for the drag to
        // come, so a left click on a cell does nothing, and a right click on a
        // key's words does nothing.
        using var zones = new ClickZones(() => new InputEventManager());
        var clicked = new List<string>();
        zones.Add(0, 0, 50, 50, () => clicked.Add("left"));
        zones.AddRight(100, 0, 50, 50, () => clicked.Add("cell 1"));
        zones.AddRight(100, 0, 20, 20, () => clicked.Add("over cell 1"));

        Assert.False(zones.Click(new Vector2(120, 30)));
        Assert.False(zones.RightClick(new Vector2(10, 10)));
        Assert.Empty(clicked);
        Assert.True(zones.RightClick(new Vector2(120, 30)));
        Assert.True(zones.RightClick(new Vector2(110, 10)));   // the last laid down over the point
        Assert.True(zones.Click(new Vector2(10, 10)));
        Assert.Equal(["cell 1", "over cell 1", "left"], clicked);
        Assert.Equal((1, 2), (zones.Zones.Count, zones.RightZones.Count));

        // The scene's right click reaches the bag through those zones, and the HUD
        // lays one a cell, handing it the scene's one door (PickBagSlot).
        var hud = typeof(DungeonHud).GetMethod("DrawInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.Contains(nameof(ClickZones.AddRight), TestIl.CallsIn(hud).Select(method => method.Name));
        Assert.Equal(["DungeonHud.DrawInventory"], TestIl.CallersOf(typeof(ClickZones), nameof(ClickZones.AddRight)));
        Assert.StartsWith("DungeonScene.<WireInput>", Assert.Single(TestIl.CallersOf(typeof(ClickZones), nameof(ClickZones.RightClick))), StringComparison.Ordinal);
    }
}

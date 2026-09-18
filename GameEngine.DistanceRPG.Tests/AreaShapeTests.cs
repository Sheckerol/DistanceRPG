using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The four wand geometries (§1.4) in logic units: what each shape contains
/// from where the caster stands and where it is aimed, that every shape stops
/// at walls, and that the caster is never inside its own cast.
/// </summary>
public class AreaShapeTests
{
    private const float Tile = GameConstants.Tile;

    private static readonly int[,] Open = new int[20, 20];

    private static PartyMemberState Caster(float x, float y)
    {
        var c = new PartyMemberState { Id = "A", ColorIndex = 0, X = x, Y = y };
        c.Inventory[0] = TestWeapons.Get("wand_of_the_blast", DamageType.Flaming);
        return c;
    }

    private static EnemyState Dummy(float x, float y) => new() { X = x, Y = y };

    private static AreaShape Shape(string wandId) => TestWeapons.Get(wandId, DamageType.Flaming).AreaShape!;

    private static IReadOnlyList<ActorState> Targets(AreaShape shape, ActorState caster, (float X, float Y) aim, int[,] grid, params ActorState[] actors)
        => AreaShapes.Targets(shape, caster, aim, actors, grid);

    [Fact]
    public void Blast_ContainsWithinRadius48_PointClampedTo160()
    {
        var blast = Shape("wand_of_the_blast");
        Assert.Equal(new AreaShape(AreaShapeKind.Blast, 48, 0, 0, 0, 160), blast);
        var a = Caster(100f, 100f);

        // Aimed a hundred units off: the circle sits on the point, and a body
        // is inside when its centre is within 48 of it — the edge included.
        var aim = (X: 200f, Y: 100f);
        var near = Dummy(240f, 100f);      // 40 off the point
        var edge = Dummy(200f, 148f);      // exactly 48
        var past = Dummy(249f, 100f);      // 49
        var between = Dummy(150f, 100f);   // 50, on the caster's side of the point
        Assert.Equal(new ActorState[] { edge, near }, Targets(blast, a, aim, Open, near, edge, past, between));   // nearest the caster first
        Assert.True(AreaShapes.Place(blast, a, aim)!.Value.Contains(200f, 148f));
        Assert.False(AreaShapes.Place(blast, a, aim)!.Value.Contains(249f, 100f));

        // Aimed further than the wand reaches, the point is pulled back along
        // the line to 160 from the caster: the circle lands at 260, not 500.
        var far = (X: 500f, Y: 100f);
        var placed = AreaShapes.Place(blast, a, far)!.Value;
        Assert.Equal((260f, 100f), (placed.OriginX, placed.OriginY));
        var atClamp = Dummy(300f, 100f);   // 40 from the clamped point
        var atAim = Dummy(500f, 100f);     // on the aim itself: out of reach
        Assert.Equal(new ActorState[] { atClamp }, Targets(blast, a, far, Open, atAim, atClamp));

        // Aimed on the caster itself, the circle sits on the caster; the
        // caster is never inside its own cast, a neighbour is.
        var beside = Dummy(130f, 100f);
        Assert.Equal(new ActorState[] { beside }, Targets(blast, a, (a.X, a.Y), Open, a, beside));
    }

    [Fact]
    public void Cone_90Degrees128()
    {
        var cone = Shape("wand_of_the_cone");
        Assert.Equal(new AreaShape(AreaShapeKind.Cone, 0, 128, 0, 90, 0), cone);
        var a = Caster(160f, 160f);
        var aim = (X: 260f, Y: 160f);   // pointing along +x

        var straight = Dummy(260f, 160f);   // 100 out along the axis
        var inside = Dummy(240f, 200f);     // 80 along, 40 across: 27 degrees off, 89 out
        var nearEdge = Dummy(250f, 240f);   // 90 along, 80 across: 42 degrees, 120 out
        var tooFar = Dummy(300f, 160f);     // 140 out along the axis
        var tooWide = Dummy(200f, 220f);    // 40 along, 60 across: 56 degrees
        var behind = Dummy(100f, 160f);
        var square = Dummy(160f, 200f);     // 90 degrees off: outside a 90 degree cone
        Assert.Equal(new ActorState[] { inside, straight, nearEdge },
            Targets(cone, a, aim, Open, tooWide, square, straight, tooFar, nearEdge, behind, inside));

        // Exactly on the edge counts: 45 degrees off the axis, well within the length.
        Assert.True(AreaShapes.Place(cone, a, aim)!.Value.Contains(200f, 200f));
        Assert.True(AreaShapes.Place(cone, a, aim)!.Value.Contains(160f, 160f));   // the apex
        Assert.False(AreaShapes.Place(cone, a, aim)!.Value.Contains(289f, 160f));  // 129 out

        // A cone needs a direction: aimed on the caster it has no placement and catches nobody.
        Assert.Null(AreaShapes.Place(cone, a, (a.X, a.Y)));
        Assert.Empty(Targets(cone, a, (a.X, a.Y), Open, straight, inside));
    }

    [Fact]
    public void Beam_Width32Length224()
    {
        var beam = Shape("wand_of_the_beam");
        Assert.Equal(new AreaShape(AreaShapeKind.Beam, 0, 224, 32, 0, 0), beam);
        var a = Caster(64f, 64f);
        var aim = (X: 64f, Y: 200f);   // pointing down the map; how far along the aim is does not matter

        var end = Dummy(64f, 288f);         // 224 along: the end of the line
        var offAxis = Dummy(70f, 150f);     // 6 across
        var edge = Dummy(80f, 100f);        // 16 across: half the width, inside
        var pastEnd = Dummy(64f, 289f);
        var tooWide = Dummy(81f, 100f);     // 17 across
        var nextColumn = Dummy(96f, 100f);  // a tile over: a beam one tile wide catches its own row alone
        var behind = Dummy(64f, 40f);
        Assert.Equal(new ActorState[] { edge, offAxis, end },
            Targets(beam, a, aim, Open, pastEnd, nextColumn, end, tooWide, offAxis, behind, edge));

        // The same line however far the aim sits along it.
        Assert.Equal(new ActorState[] { edge, offAxis, end }, Targets(beam, a, (64f, 65f), Open, end, offAxis, edge));
        Assert.Null(AreaShapes.Place(beam, a, (a.X, a.Y)));
    }

    [Fact]
    public void Nova_Radius96()
    {
        var nova = Shape("wand_of_the_nova");
        Assert.Equal(new AreaShape(AreaShapeKind.Nova, 96, 0, 0, 0, 0), nova);
        var a = Caster(160f, 160f);

        var edge = Dummy(256f, 160f);     // exactly 96
        var close = Dummy(160f, 70f);     // 90
        var past = Dummy(257f, 160f);
        var diagonal = Dummy(100f, 60f);  // 60 and 100: 117 off
        Assert.Equal(new ActorState[] { close, edge }, Targets(nova, a, (0f, 0f), Open, past, edge, diagonal, close));

        // A Nova ignores the aim: the same circle wherever the cast points, the caster itself included.
        Assert.Equal(new ActorState[] { close, edge }, Targets(nova, a, (999f, 999f), Open, past, edge, diagonal, close));
        Assert.Equal(new ActorState[] { close, edge }, Targets(nova, a, (a.X, a.Y), Open, past, edge, diagonal, close));
        var placed = AreaShapes.Place(nova, a, (999f, 999f))!.Value;
        Assert.Equal((a.X, a.Y), (placed.OriginX, placed.OriginY));
    }

    [Fact]
    public void BehindWall_NotHit()
    {
        // Every shape stops at walls: a body inside the shape with a wall
        // between it and the caster is not hit, whichever shape it is.
        var grid = new int[20, 20];
        grid[5, 6] = 1;   // the wall tile spans x 192..224 on row 5
        var a = Caster(5 * Tile + 16f, 5 * Tile + 16f);   // tile (5,5), centre 176,176
        var walled = Dummy(7 * Tile + 16f, 5 * Tile + 16f);   // tile (5,7): 64 off, behind the wall
        var seen = Dummy(3 * Tile + 16f, 5 * Tile + 16f);     // tile (5,3): 64 off, open floor
        var around = Dummy(6 * Tile + 16f, 7 * Tile + 16f);   // tile (7,6): 72 off, and the sight line passes under the wall

        Assert.Equal(new ActorState[] { seen, around }, Targets(Shape("wand_of_the_nova"), a, (a.X, a.Y), grid, walled, seen, around));
        Assert.Equal(new ActorState[] { around }, Targets(Shape("wand_of_the_cone"), a, (walled.X, walled.Y + 40f), grid, walled, around));
        Assert.Empty(Targets(Shape("wand_of_the_beam"), a, (walled.X, walled.Y), grid, walled));
        Assert.Empty(Targets(Shape("wand_of_the_blast"), a, (walled.X, walled.Y), grid, walled));   // the point is past the wall; the body inside it is unseen

        // The same bodies on an open grid are all caught.
        Assert.Equal(3, Targets(Shape("wand_of_the_nova"), a, (a.X, a.Y), Open, walled, seen, around).Count);
    }

    [Fact]
    public void CasterExcluded()
    {
        var nova = Shape("wand_of_the_nova");
        var a = Caster(160f, 160f);
        var ally = new PartyMemberState { Id = "B", ColorIndex = 0, X = 190f, Y = 160f };   // the geometry takes whoever it is given: sides are the caller's
        var far = Dummy(160f, 250f);      // 90
        var mid = Dummy(224f, 160f);      // 64
        var dead = Dummy(192f, 160f);     // 32, but dead: a shape hits actors, not corpses
        dead.Alive = false;
        dead.Hp = 0;

        var caught = Targets(nova, a, (a.X, a.Y), Open, far, a, dead, mid, ally);
        Assert.Equal(new ActorState[] { ally, mid, far }, caught);
        Assert.DoesNotContain(a, caught);
        Assert.DoesNotContain(dead, caught);

        // Ties in the given order: a stable sort, so the order the hits land in is data.
        var left = Dummy(96f, 160f);
        var right = Dummy(224f, 160f);
        Assert.Equal(new ActorState[] { right, left }, Targets(nova, a, (a.X, a.Y), Open, right, left));
        Assert.Equal(new ActorState[] { left, right }, Targets(nova, a, (a.X, a.Y), Open, left, right));
    }
}

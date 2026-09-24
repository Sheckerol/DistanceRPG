using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The one wrapper every notice goes through before it is drawn (1h): upper-cased
/// for the HUD's capitals, wrapped at the width the screens' lines wrap at, and
/// capped at three rows plus one saying how many more are in the log — so a
/// file with fifty mistakes never pushes the dungeon list off the screen.
/// </summary>
public class NoticeTextTests
{
    [Fact]
    public void ALongNoticeWrapsAtTheLineWidth_AndTheRowsCap()
    {
        // A malformed file's warning runs to about 150 characters: it wraps into
        // rows under 64, every word kept and in order, the rows after the first
        // indented under it.
        const string malformed = "tuning.json: line 3, column 3 is not valid JSON (the reader expected a comma or a closing brace after the value) - the built-in numbers are playing";
        Assert.InRange(malformed.Length, 140, 160);
        var rows = NoticeText.Rows([malformed]);
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.InRange(row.Length, 1, NoticeText.LineChars));
        Assert.All(rows.Skip(1), row => Assert.StartsWith("  ", row));
        Assert.Equal(malformed.ToUpperInvariant(), string.Join(" ", rows.Select(row => row.Trim())));

        // Five one-row notices: three rows, then how many more are in the log.
        var five = Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1").ToList();
        Assert.Equal(["TUNING.JSON: KEY1 0 IS BELOW 1 - USING 1", "TUNING.JSON: KEY2 0 IS BELOW 1 - USING 1", "TUNING.JSON: KEY3 0 IS BELOW 1 - USING 1", "+2 MORE - SEE THE LOG"],
            NoticeText.Rows(five));

        // A notice cut short counts among those not shown whole.
        var rows4 = NoticeText.Rows(["short one", malformed]);
        Assert.Equal(NoticeText.MaxRows + 1, rows4.Count);
        Assert.Equal("+1 MORE - SEE THE LOG", rows4[^1]);

        // Exactly three rows is no cut; none is nothing.
        Assert.Equal(3, NoticeText.Rows(five.Take(3).ToList()).Count);
        Assert.Empty(NoticeText.Rows([]));

        // A word longer than a row is cut where the row ends, never lost.
        string word = new('X', 150);
        var cut = NoticeText.Rows([word]);
        Assert.All(cut, row => Assert.InRange(row.Length, 1, NoticeText.LineChars));
        Assert.Equal(word, string.Concat(cut.Select(row => row.Trim())));
    }

    [Fact]
    public void ABadTuningValueIsSaidOnScreenAsTheFileSaysIt()
    {
        // The playtest's line (1i): a MovementUnitsPerMana of 0 runs the game and
        // says, under the between-runs title or over the first floor,
        // TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1.
        var load = ContentFiles.Load(new ContentTexts(Tuning: """{ "MovementUnitsPerMana": 0 }"""));
        Assert.NotNull(load.Content);
        Assert.Equal(["TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1"], NoticeText.Rows(load.Notices));

        // A deleted file plays the compiled numbers, and says so.
        Assert.Equal(["TUNING.JSON IS MISSING - THE BUILT-IN NUMBERS ARE PLAYING"], NoticeText.Rows(ContentFiles.Load(new ContentTexts()).Notices));
    }
}

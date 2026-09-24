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
    public void TheSavesWordIsShownWhole_AndWhatFollowsIsCapped()
    {
        // S5's review: the screen between runs leads with the save's word on the
        // campaign, which names a set-aside file's full path and how to try it
        // again, and it is shown whole; the cap of three rows is for what follows
        // it, the data files' notices.
        string setAside = "campaign.json could not be read ($: 'n' is an invalid start of a value) - set aside as "
            + @"C:\Users\someone\AppData\Local\DistanceRPG\saves\campaign.unreadable-1.json - RENAME IT TO CAMPAIGN.JSON TO TRY AGAIN";
        var five = Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1").ToList();
        List<string> notices = ["CAMPAIGN RESUMED FROM ITS BACKUP, ONE SAVE OLDER", setAside, .. five];

        var whole = NoticeText.Wrap(notices[0]).Concat(NoticeText.Wrap(setAside)).ToList();
        Assert.True(whole.Count > NoticeText.MaxRows);   // more than the cap alone would show
        Assert.Equal([.. whole, .. NoticeText.Rows(five)], NoticeText.Rows(notices, whole: 2));
        Assert.Equal("+2 MORE - SEE THE LOG", NoticeText.Rows(notices, whole: 2)[^1]);

        // Shown whole, nothing of it is lost: the path is cut where a row ends, never
        // dropped, and drawn with slashes, since the HUD's font has no backslash.
        Assert.Equal(setAside.ToUpperInvariant().Replace('\\', '/').Replace(" ", ""), string.Concat(NoticeText.Wrap(setAside)).Replace(" ", ""));
        Assert.Contains(NoticeText.Wrap(setAside), row => row.Contains("C:/USERS/SOMEONE/APPDATA/LOCAL/DISTANCERPG/SAVES/", StringComparison.Ordinal));
        Assert.DoesNotContain(NoticeText.Wrap(setAside), row => row.Contains('\\'));

        // None shown whole is the cap as ever; more than there are is all of them,
        // uncapped; fewer than none is refused.
        Assert.Equal(NoticeText.Rows(notices), NoticeText.Rows(notices, whole: 0));
        Assert.Equal(notices.SelectMany(NoticeText.Wrap), NoticeText.Rows(notices, whole: notices.Count + 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoticeText.Rows(notices, whole: -1));

        // Even the save's word has a ceiling, for a save folder with a path of
        // unusual length: past it the rows are cut as ever, and counted.
        string longest = string.Join(" ", Enumerable.Repeat(setAside, 6));
        var ceiling = NoticeText.Rows([longest, "tuning.json: Key 0 is below 1 - using 1"], whole: 1);
        Assert.Equal(NoticeText.MaxWholeRows + 1, ceiling.Count);
        Assert.Equal(NoticeText.Wrap(longest).Take(NoticeText.MaxWholeRows), ceiling.Take(NoticeText.MaxWholeRows));
        Assert.Equal(NoticeText.More(2), ceiling[^1]);
    }

    [Fact]
    public void ABadTuningValueIsSaidOnScreenAsTheFileSaysIt()
    {
        // The playtest's line (1i): a MovementUnitsPerMana of 0 runs the game and
        // says, under the between-runs title or over the first floor,
        // TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1. The other data
        // files sit beside it, as they ship, so every notice here is the tuning's.
        var others = ContentFiles.WriteDefaults() with { Tuning = null };
        var load = ContentFiles.Load(others with { Tuning = """{ "MovementUnitsPerMana": 0 }""" });
        Assert.NotNull(load.Content);
        Assert.Equal(["TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1"], NoticeText.Rows(load.Notices));

        // A deleted file plays the compiled numbers, and says so.
        Assert.Equal(["TUNING.JSON IS MISSING - THE BUILT-IN NUMBERS ARE PLAYING"], NoticeText.Rows(ContentFiles.Load(others).Notices));

        // So does one another program is holding open, and it says why rather
        // than calling the file missing. That takes two rows, within the cap.
        var unreadable = others with { Unreadable = new Dictionary<string, string> { [TuningJson.FileName] = "another program has it open" } };
        Assert.Equal(["TUNING.JSON COULD NOT BE READ (ANOTHER PROGRAM HAS IT OPEN) -", "  THE BUILT-IN NUMBERS ARE PLAYING"],
            NoticeText.Rows(ContentFiles.Load(unreadable).Notices));
    }
}

using System.Text;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The one wrapper every notice goes through before it is drawn (1h): what the
/// data files raised at startup — a <c>tuning.json</c> value clamped, a file
/// missing — under the between-runs title, over the first floor of a dungeon an
/// override boots straight into or a saved run resumes on, or on the screen a
/// broken content file stops the game on, whose own lines it wraps too. Each
/// notice is upper-cased (the HUD's font is capitals) and wrapped at the width
/// the screens' lines wrap at (<see cref="LineChars"/>, the summary's), its
/// continuation rows indented under it; and the whole is capped at
/// <see cref="MaxRows"/> rows plus one saying how many notices did not fit, so a
/// file with fifty mistakes never pushes the dungeon list off the screen. The
/// log has every one of them.
/// <para>
/// <strong>The save's word is shown whole</strong> (S5's review: 1f meets 1h).
/// The screen between runs leads with what became of the campaign — that it
/// resumed, the numbers changed since it was saved (PHASE5-084), a save set
/// aside, by its full path, and how to try it again — and a notice cut off
/// there would hide the one thing the player has to act on. A floor a saved run
/// resumes on leads with the same word, over its banner (S8's review). Those
/// notices are few by construction (a launch sets at most two files aside), so
/// the screen hands their count in (<see cref="Rows"/>'s <c>whole</c>) and they
/// are shown in full, up to <see cref="MaxWholeRows"/>; the cap of
/// <see cref="MaxRows"/> is for what follows them, the data files' notices,
/// which a broken file can make as many of as it likes.
/// </para>
/// </summary>
internal static class NoticeText
{
    /// <summary>The most characters a row runs to: the between-runs summary's width, a little over half the shipped window at its scale.</summary>
    public const int LineChars = 64;

    /// <summary>The most rows of notices shown after any shown whole; past them, one more row says how many are in the log.</summary>
    public const int MaxRows = 3;

    /// <summary>
    /// The most rows the notices shown whole take (<see cref="Rows"/>): the save's
    /// word on the campaign. Its longest case — a new campaign after the save and
    /// its backup were both set aside, each notice naming its file's full path —
    /// takes about ten rows in the player's own save folder, so this is a ceiling
    /// only a save folder with an unusually long path reaches; past it the rows
    /// are cut like any others, and with <see cref="MaxRows"/> after them and the
    /// row that counts the rest, the dungeon list still clears a 720-pixel window,
    /// and so does a resumed floor's banner, which moves down under them
    /// (<see cref="DungeonHud.BannerLine"/>). settled.md's entry on the save's
    /// word records the ceiling and why.
    /// </summary>
    public const int MaxWholeRows = 14;

    /// <summary>How a notice's continuation rows start, so a wrapped notice reads as one.</summary>
    private const string Indent = "  ";

    /// <summary>
    /// The rows <paramref name="notices"/> are drawn as, in order: each wrapped
    /// under <see cref="LineChars"/>. The first <paramref name="whole"/> of them
    /// are shown in full, up to <see cref="MaxWholeRows"/> rows; the rest take at
    /// most <see cref="MaxRows"/> rows after them. Then <c>+N MORE - SEE THE LOG</c>
    /// for the notices not shown whole. None for none.
    /// </summary>
    /// <param name="notices">The notices, in the order they are shown.</param>
    /// <param name="whole">How many of <paramref name="notices"/>, from the first, are shown whole: the save's word on the campaign, which the screen between runs and a resumed floor lead with. None by default: every notice is capped.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="whole"/> is negative.</exception>
    public static IReadOnlyList<string> Rows(IReadOnlyList<string> notices, int whole = 0)
    {
        ArgumentNullException.ThrowIfNull(notices);
        ArgumentOutOfRangeException.ThrowIfNegative(whole);
        var rows = new List<string>();
        int wholeRows = 0;   // the rows the notices shown whole took: the cap counts from there
        for (int i = 0; i < notices.Count; i++)
        {
            bool shownWhole = i < whole;
            int ceiling = shownWhole ? MaxWholeRows : wholeRows + MaxRows;
            foreach (string row in Wrap(notices[i] ?? ""))
            {
                if (rows.Count == ceiling)
                {
                    rows.Add(More(notices.Count - i));
                    return rows;
                }
                rows.Add(row);
            }
            if (shownWhole)
                wholeRows = rows.Count;
        }
        return rows;
    }

    /// <summary>The last row when notices did not fit: how many, and where they all are.</summary>
    internal static string More(int count) => $"+{count} MORE - SEE THE LOG";

    /// <summary>
    /// One notice — or one line of the screen that stops the game — as rows,
    /// upper-cased, each under <see cref="LineChars"/>: words kept whole where
    /// they fit, a word longer than a row cut, every row after the first
    /// indented. Uncapped: <see cref="Rows"/> is what caps a list of notices.
    /// The HUD's font is capitals and has no backslash — it would draw a gap — so
    /// a backslash is drawn as a slash: a Windows path, such as a set-aside
    /// save's, still reads as one, and Windows reads it the same. Any whitespace
    /// separates words — a line break or a tab included — so no row ever holds
    /// one: a notice is one line, wrapped here and nowhere else.
    /// </summary>
    internal static IReadOnlyList<string> Wrap(string notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var rows = new List<string>();
        var row = new StringBuilder();
        foreach (string word in notice.ToUpperInvariant().Replace('\\', '/').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string rest = word;
            while (rest.Length > 0)
            {
                if (row.Length == 0 && rows.Count > 0)
                    row.Append(Indent);
                bool fresh = row.Length == (rows.Count > 0 ? Indent.Length : 0);
                int room = LineChars - row.Length - (fresh ? 0 : 1);
                if (rest.Length <= room)
                {
                    if (!fresh) row.Append(' ');
                    row.Append(rest);
                    rest = "";
                }
                else if (!fresh)
                {
                    rows.Add(row.ToString());   // the word starts the next row
                    row.Clear();
                }
                else
                {
                    row.Append(rest[..room]);   // longer than a whole row: cut where the row ends
                    rest = rest[room..];
                    rows.Add(row.ToString());
                    row.Clear();
                }
            }
        }
        if (row.Length > (rows.Count > 0 ? Indent.Length : 0))
            rows.Add(row.ToString());
        return rows;
    }
}

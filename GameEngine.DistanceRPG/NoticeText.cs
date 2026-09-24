using System.Text;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The one wrapper every notice goes through before it is drawn (1h): what the
/// data files raised at startup — a <c>tuning.json</c> value clamped, a file
/// missing — under the between-runs title, over the first floor of a dungeon
/// an override boots straight into, or on the screen a broken content file
/// stops the game on, whose own lines it wraps too. Each notice is upper-cased (the HUD's font
/// is capitals) and wrapped at the width the screens' lines wrap at
/// (<see cref="LineChars"/>, the summary's), its continuation rows indented
/// under it; and the whole is capped at <see cref="MaxRows"/> rows plus one
/// saying how many notices did not fit, so a file with fifty mistakes never
/// pushes the dungeon list off the screen. The log has every one of them.
/// </summary>
internal static class NoticeText
{
    /// <summary>The most characters a row runs to: the between-runs summary's width, a little over half the shipped window at its scale.</summary>
    public const int LineChars = 64;

    /// <summary>The most rows of notices shown; past them, one more row says how many are in the log.</summary>
    public const int MaxRows = 3;

    /// <summary>How a notice's continuation rows start, so a wrapped notice reads as one.</summary>
    private const string Indent = "  ";

    /// <summary>
    /// The rows <paramref name="notices"/> are drawn as, in order: each wrapped
    /// under <see cref="LineChars"/>, at most <see cref="MaxRows"/> of them, then
    /// <c>+N MORE - SEE THE LOG</c> for the notices not shown whole. None for none.
    /// </summary>
    public static IReadOnlyList<string> Rows(IReadOnlyList<string> notices)
    {
        ArgumentNullException.ThrowIfNull(notices);
        var rows = new List<string>();
        for (int i = 0; i < notices.Count; i++)
        {
            foreach (string row in Wrap(notices[i] ?? ""))
            {
                if (rows.Count == MaxRows)
                {
                    rows.Add(More(notices.Count - i));
                    return rows;
                }
                rows.Add(row);
            }
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
    /// </summary>
    internal static IReadOnlyList<string> Wrap(string notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var rows = new List<string>();
        var row = new StringBuilder();
        foreach (string word in notice.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
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

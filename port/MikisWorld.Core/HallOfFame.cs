namespace MikisWorld.Core;

/// <summary>High-score table (original class <c>k</c>, minus the RecordStore plumbing): <c>tables</c> lists of
/// <c>entries</c> (name, score), kept sorted; in descending mode a higher score is better.</summary>
public sealed class HallOfFame
{
    public readonly int Tables, Entries;
    private readonly bool _descending;
    private readonly string[][] _names;
    private readonly int[][] _scores;

    /// <summary>Game 1's class tests <c>name.equals(" ")</c> (so an empty name stays empty); game 2's tests <c>equals("")</c> (empty becomes a space).</summary>
    private readonly bool _emptyBecomesSpace;

    public HallOfFame(string defaultName, int defaultScore, int tables, int entries, bool descending, bool emptyBecomesSpace = false)
    {
        _emptyBecomesSpace = emptyBecomesSpace;
        Tables = tables; Entries = entries; _descending = descending;
        _names = new string[tables][];
        _scores = new int[tables][];
        for (int t = 0; t < tables; t++)
        {
            _names[t] = new string[entries];
            _scores[t] = new int[entries];
            for (int i = 0; i < entries; i++) { _names[t][i] = defaultName; _scores[t][i] = defaultScore; }
        }
    }

    /// <summary>Back to the factory table (every entry the default name and score).</summary>
    public void Clear(int table, string defaultName, int defaultScore)
    {
        for (int i = 0; i < Entries; i++) { _names[table][i] = defaultName; _scores[table][i] = defaultScore; }
    }

    public string Name(int table, int index) => _names[table][index];
    public int Score(int table, int index) => _scores[table][index];

    /// <summary>Original k.do(int,int): does <paramref name="score"/> beat the worst entry?</summary>
    public bool IsHighScore(int table, int score)
    {
        int last = _scores[table][Entries - 1];
        return _descending ? score > last : score < last;
    }

    /// <summary>Original k.a(int,String,int): insert below equal scores, dropping the last entry.</summary>
    /// <returns>the row the entry landed in, or -1 when it did not fit</returns>
    public int Insert(int table, string name, int score)
    {
        if (name == null || name == (_emptyBecomesSpace ? "" : " ")) name = " ";
        int pos = Entries - 1;
        if (_descending) { while (pos >= 0 && score > _scores[table][pos]) --pos; }
        else { while (pos >= 0 && score < _scores[table][pos]) --pos; }
        ++pos;
        for (int i = Entries - 2; i >= pos; --i)
        {
            _names[table][i + 1] = _names[table][i];
            _scores[table][i + 1] = _scores[table][i];
        }
        if (pos >= Entries) return -1;
        _names[table][pos] = name;
        _scores[table][pos] = score;
        return pos;
    }

    public (string Name, int Score)[] Export(int table)
    {
        var r = new (string, int)[Entries];
        for (int i = 0; i < Entries; i++) r[i] = (_names[table][i], _scores[table][i]);
        return r;
    }

    public void Import(int table, IReadOnlyList<(string Name, int Score)> rows)
    {
        for (int i = 0; i < Entries && i < rows.Count; i++) { _names[table][i] = rows[i].Name; _scores[table][i] = rows[i].Score; }
    }
}

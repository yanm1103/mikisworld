namespace MikisWorld.Core;

/// <summary>Everything the original kept in RecordStores: settings (AppData), bonus unlock (GameData), HallOfFame.</summary>
public sealed class SaveData
{
    public bool Sound { get; set; } = true;
    public bool BonusUnlocked { get; set; }
    public List<ScoreEntry> HighScores { get; set; } = new();
}

public sealed class ScoreEntry
{
    public string Name { get; set; } = "aaa";
    public int Score { get; set; }
}

public interface IStorage
{
    SaveData Load();
    void Save(SaveData data);
}

public sealed class MemoryStorage : IStorage
{
    public SaveData Data = new();
    public SaveData Load() => Data;
    public void Save(SaveData data) { Data = data; }
}

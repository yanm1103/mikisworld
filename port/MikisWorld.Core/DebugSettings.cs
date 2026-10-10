namespace MikisWorld.Core;

/// <summary>Switches of the debug menu (F3). Session only, never saved.</summary>
public sealed class DebugSettings
{
    /// <summary>No damage at all: hits are ignored (no life lost, no sound / rumble / spark).</summary>
    public bool Invincible { get; set; }

    /// <summary>Hits still happen (life lost, sound, rumble, spark) but the life comes back at once, so the game never ends.</summary>
    public bool InfiniteLives { get; set; }

    /// <summary>Wireframe of the boxes the logic tests.</summary>
    public bool ShowHitboxes { get; set; }
}

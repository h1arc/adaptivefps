using Dalamud.Configuration;
using System;

namespace AdaptiveFPS;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Enable/disable behavior without unloading plugin.
    public bool Enabled { get; set; } = true;

    // Desired caps (defaults to 60 in combat, 30 out)
    public uint CombatCap { get; set; } = 2u; // 2 = 60 fps
    public uint OutOfCombatCap { get; set; } = 3u; // 3 = 30 fps

    // Remember last user-set cap to restore on dispose if desired
    public uint? LastUserCap { get; set; }
}

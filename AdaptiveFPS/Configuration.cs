using Dalamud.Configuration;
using System;

namespace AdaptiveFPS;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Enable/disable behavior without unloading plugin.
    public bool Enabled { get; set; } = true;

    // Desired caps (defaults to main display in combat, 30fps out of combat)
    public uint CombatCap { get; set; } = 1u; // 1 = main display refresh rate
    public uint OutOfCombatCap { get; set; } = 3u; // 3 = 30 fps

    // Remember last user-set cap to restore on dispose if desired
    public uint? LastUserCap { get; set; }
}

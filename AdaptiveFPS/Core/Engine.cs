using AdaptiveFPS.Core.Game;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;

namespace AdaptiveFPS.Core;

internal readonly struct Snapshot(bool loggedIn, bool inCombat, uint currentCap, uint refreshHz)
{
    public readonly bool LoggedIn = loggedIn;
    public readonly bool InCombat = inCombat;
    public readonly uint CurrentCap = currentCap;
    public readonly uint RefreshHz = refreshHz;

    public bool IsApplicable(Configuration cfg) => cfg.Enabled && LoggedIn;
    public uint DesiredCap(Configuration cfg) => InCombat ? cfg.CombatCap : cfg.OutOfCombatCap;
}

internal static class Engine
{
    // Tracks last UI state to skip redundant DTR updates (includes cache stamp + config)
    private static long _lastUiStamp = -1;

    public static void ApplyAndRefresh(Plugin plugin, ref bool isApplied, IDtrBarEntry? entry)
    {
        var stamp = MicroCache.Get(out var snap);
        // Always apply in case config changed (stamp only reflects game state)
        ApplyForCurrentState(plugin, in snap, ref isApplied);
        var uiStamp = ComputeUiStamp(plugin.Config, stamp);
        if (uiStamp != _lastUiStamp)
        {
            RefreshStatus(plugin, entry, in snap);
            _lastUiStamp = uiStamp;
        }
    }

    public static void DisableAndRefresh(Plugin plugin, ref bool isApplied, IDtrBarEntry? entry)
    {
        if (isApplied && plugin.Config.LastUserCap.HasValue)
        {
            FpsHelpers.SetCap(plugin.Config.LastUserCap.Value);
            isApplied = false;
        }
        var stamp = MicroCache.Get(out var snap);
        var uiStamp = ComputeUiStamp(plugin.Config, stamp);
        if (uiStamp != _lastUiStamp)
        {
            RefreshStatus(plugin, entry, in snap);
            _lastUiStamp = uiStamp;
        }
    }

    public static void OnEntryClick(Plugin plugin, DtrInteractionEvent e, ref bool isApplied, IDtrBarEntry? entry)
    {
        if (e.ClickType == MouseClickType.Left)
            RotateCombatCap(plugin);
        else
            RotateOutOfCombatCap(plugin);

        ApplyAndRefresh(plugin, ref isApplied, entry);
    }

    public static void RotateOutOfCombatCap(Plugin plugin)
    {
        plugin.Config.OutOfCombatCap = plugin.Config.OutOfCombatCap switch
        {
            1u => 2u,
            2u => 3u,
            3u => 1u,
            _ => 1u,
        };
        plugin.Save();
    }

    public static void RotateCombatCap(Plugin plugin)
    {
        plugin.Config.CombatCap = plugin.Config.CombatCap switch
        {
            1u => 2u,
            2u => 3u,
            3u => 1u,
            _ => 1u,
        };
        plugin.Save();
    }

    private static void RefreshStatus(Plugin plugin, IDtrBarEntry? entry, in Snapshot snap)
    {
        if (entry == null) return;
        entry.Text = UI.UI.BuildStatus(plugin.Config, in snap);
        entry.Tooltip = new SeStringBuilder()
            .AddText(UI.UI.BuildTooltip(snap.RefreshHz)).Build();
    }

    private static void ApplyForCurrentState(Plugin plugin, in Snapshot snap, ref bool isApplied)
    {
        if (!snap.IsApplicable(plugin.Config))
            return;

        var desired = snap.DesiredCap(plugin.Config);
        var current = snap.CurrentCap;

        if (!isApplied)
        {
            plugin.Config.LastUserCap = current;
            isApplied = true;
            plugin.Save();
        }

        if (current != desired)
        {
            Plugin.Log.Information($"{UI.Strings.PluginName}: {(snap.InCombat ? "combat" : "ooc")} -> cap {desired}");
            FpsHelpers.SetCap(desired);
        }
    }

    private static long ComputeUiStamp(Configuration cfg, long frameStamp)
    {
        unchecked
        {
            long s = frameStamp;
            s ^= ((long)cfg.CombatCap << 17);
            s ^= ((long)cfg.OutOfCombatCap << 9);
            if (cfg.Enabled) s ^= 0x1L;
            return s;
        }
    }
}

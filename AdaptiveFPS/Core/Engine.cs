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
    public static void ApplyAndRefresh(Plugin plugin, ref bool isApplied, IDtrBarEntry? entry)
    {
        var snap = MicroCache.Current;
        ApplyForCurrentState(plugin, in snap, ref isApplied);
        RefreshStatus(plugin, entry, in snap);
    }

    public static void DisableAndRefresh(Plugin plugin, ref bool isApplied, IDtrBarEntry? entry)
    {
        if (isApplied && plugin.Config.LastUserCap.HasValue)
        {
            FpsHelpers.SetCap(plugin.Config.LastUserCap.Value);
            isApplied = false;
        }
        var snap = MicroCache.Current;
        RefreshStatus(plugin, entry, in snap);
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

        // Save user's original setting on first application
        if (!isApplied)
        {
            plugin.Config.LastUserCap = snap.CurrentCap;
            isApplied = true;
            plugin.Save();
        }

        // Only set if it's different from current to avoid spam
        if (snap.CurrentCap != desired)
        {
            FpsHelpers.SetCap(desired);
        }
    }
}

using Dalamud.Game.Text.SeStringHandling;
using static Dalamud.Game.Text.SeStringHandling.BitmapFontIcon;
using AdaptiveFPS.Core.Game;

namespace AdaptiveFPS.UI;

internal static class Strings
{
    public const string PluginName = "AdaptiveFPS";

    // Commands
    public const string CommandName = "/afps";
    public const string CommandHelp = "AdaptiveFPS: /afps ic|ooc 1|2|3, /afps toggle";
    public const string StatusFormat = "AdaptiveFPS: IC={0}, OOC={1}, Enabled={2}";
    public const string UsageIc = "Usage: /afps ic 1|2|3";
    public const string UsageOoc = "Usage: /afps ooc 1|2|3";
    public const string UnknownCommand = "AdaptiveFPS: Unknown command. Use /afps for help.";
    public const string EnabledFormat = "AdaptiveFPS: {0}"; // enabled/disabled
    public const string CombatSetFormat = "AdaptiveFPS: Combat cap set to {0}";
    public const string OocSetFormat = "AdaptiveFPS: OOC cap set to {0}";

    // Tooltip
    public const string TooltipBase = "Left-click cycles in combat and right-click cycles out of combat framerate.";
    public const string TooltipWithMainSuffix = "\n\nCurrent Main: {0} Hz";

    // UI literals
    public const string OffText = "Off";
    public const string MidSeparator = " | ";
    public const string Space = " ";
}

internal static class UI
{
    public static SeString BuildStatus(Configuration cfg, in AdaptiveFPS.Core.Snapshot snap)
    {
        var sb = new SeStringBuilder();
        sb.AddIcon(CameraMode);
        sb.AddText(Strings.Space);

        if (!cfg.Enabled)
        {
            sb.AddText(Strings.OffText);
            return sb.Build();
        }

        if (snap.InCombat)
        {
            sb.AddUiGlow(5).AddText(FpsHelpers.FormatCap(cfg.CombatCap, snap.RefreshHz)).AddUiGlowOff();
        }
        else
        {
            sb.AddText(FpsHelpers.FormatCap(cfg.CombatCap, snap.RefreshHz));
        }

        sb.AddText(Strings.MidSeparator);

        if (!snap.InCombat)
        {
            sb.AddUiGlow(5).AddText(FpsHelpers.FormatCap(cfg.OutOfCombatCap, snap.RefreshHz)).AddUiGlowOff();
        }
        else
        {
            sb.AddText(FpsHelpers.FormatCap(cfg.OutOfCombatCap, snap.RefreshHz));
        }

        return sb.Build();
    }

    public static string BuildTooltip(uint mainHz)
    {
        if (mainHz > 0)
            return Strings.TooltipBase + string.Format(Strings.TooltipWithMainSuffix, mainHz);
        return Strings.TooltipBase;
    }
}

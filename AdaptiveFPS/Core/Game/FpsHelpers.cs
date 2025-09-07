using Dalamud.Game.Config;

namespace AdaptiveFPS.Core.Game;

internal static class FpsHelpers
{
    public static uint GetRefreshRateHz() => Plugin.GameConfig.TryGet(SystemConfigOption.Refreshrate, out uint hz) ? hz : 0u;

    public static uint GetCap() => Plugin.GameConfig.TryGet(SystemConfigOption.Fps, out uint uv) ? uv : 0u;

    public static void SetCap(uint value) => Plugin.GameConfig.Set(SystemConfigOption.Fps, value);

    public static string FormatCap(uint cap)
    {
        if (cap == 1u)
        {
            var hz = GetRefreshRateHz();
            return hz > 0 ? $"{hz} (main)" : "main";
        }
        if (cap == 2u) return "60";
        if (cap == 3u) return "30";
        return cap.ToString();
    }

    public static string FormatCap(uint cap, uint refreshHz)
    {
        if (cap == 1u) return refreshHz > 0 ? $"{refreshHz} (main)" : "main";
        if (cap == 2u) return "60";
        if (cap == 3u) return "30";
        return cap.ToString();
    }
}

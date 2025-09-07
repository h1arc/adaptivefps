using System;
using AdaptiveFPS.Core.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace AdaptiveFPS.Core;

// Cache game state once per framework tick to avoid hammering Dalamud services
internal static class MicroCache
{
    private static volatile bool _seeded;
    private static volatile bool _loggedIn;
    private static volatile bool _inCombat;
    private static volatile uint _currentCap;
    private static volatile uint _refreshHz;
    private static Action? _onUpdate;

    public static Snapshot Current => new(_loggedIn, _inCombat, _currentCap, _refreshHz);

    public static void Initialize(Action? onUpdate = null)
    {
        if (_seeded) return;
        _onUpdate = onUpdate;
        // Capture initial state
        CaptureNow();
        Plugin.Framework.Update += OnFrameworkUpdate;
        _seeded = true;
    }

    public static void Dispose()
    {
        if (!_seeded) return;
        Plugin.Framework.Update -= OnFrameworkUpdate;
        _onUpdate = null;
        _seeded = false;
    }

    private static void OnFrameworkUpdate(IFramework _)
    {
        CaptureNow();
        // Trigger plugin logic every frame
        _onUpdate?.Invoke();
    }

    private static void CaptureNow()
    {
        var loggedIn = Plugin.ClientState.IsLoggedIn;
        var inCombat = loggedIn && Plugin.Condition[ConditionFlag.InCombat];
        var currentCap = FpsHelpers.GetCap();
        var refreshHz = FpsHelpers.GetRefreshRateHz();

        // Only log combat state changes (most important for user feedback)
        if (_seeded && inCombat != _inCombat)
        {
            Plugin.Log.Information($"AdaptiveFPS: Combat {(inCombat ? "started" : "ended")}");
        }

        _loggedIn = loggedIn;
        _inCombat = inCombat;
        _currentCap = currentCap;
        _refreshHz = refreshHz;
    }
}

using System.Threading;
using AdaptiveFPS.Core.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace AdaptiveFPS.Core;

// Minimal on-demand cache to avoid hammering Dalamud services. Thread-safe, low overhead.
internal static class MicroCache
{
    private static volatile bool _seeded;
    private static volatile bool _loggedIn;
    private static volatile bool _inCombat;
    private static volatile uint _currentCap;
    private static volatile uint _refreshHz;
    private static long _stamp; // Monotonic change stamp; increments only when any value changes

    public static Snapshot Current
    {
        get
        {
            return new Snapshot(_loggedIn, _inCombat, _currentCap, _refreshHz);
        }
    }

    // Expose current stamp to allow callers to skip work if unchanged
    public static long FrameStamp => Interlocked.Read(ref _stamp);

    // Helper to fetch both snapshot and stamp in one call
    public static long Get(out Snapshot snapshot)
    {
        // Read-consistent snapshot: read stamp, copy, re-check once.
        var s1 = Interlocked.Read(ref _stamp);
        snapshot = new Snapshot(_loggedIn, _inCombat, _currentCap, _refreshHz);
        var s2 = Interlocked.Read(ref _stamp);
        if (s1 != s2)
        {
            // One retry to reduce torn reads without spinning
            snapshot = new Snapshot(_loggedIn, _inCombat, _currentCap, _refreshHz);
            s1 = s2;
        }
        return s1;
    }

    public static void Initialize()
    {
        if (_seeded) return;
        // Seed snapshot immediately
        CaptureNow();
        Plugin.Framework.Update += OnFrameworkUpdate;
        _seeded = true;
    }

    public static void Dispose()
    {
        if (!_seeded) return;
        Plugin.Framework.Update -= OnFrameworkUpdate;
        _seeded = false;
    }

    private static void OnFrameworkUpdate(IFramework _)
    {
        CaptureNow();
    }

    private static void CaptureNow()
    {
        var loggedIn = Plugin.ClientState.IsLoggedIn;
        var inCombat = loggedIn && Plugin.Condition[ConditionFlag.InCombat];
        var currentCap = FpsHelpers.GetCap();
        var refreshHz = FpsHelpers.GetRefreshRateHz();

        bool changed = !_seeded
            || loggedIn != _loggedIn
            || inCombat != _inCombat
            || currentCap != _currentCap
            || refreshHz != _refreshHz;

        if (changed)
        {
            _loggedIn = loggedIn;
            _inCombat = inCombat;
            _currentCap = currentCap;
            _refreshHz = refreshHz;
            Interlocked.Increment(ref _stamp);
        }
    }
}

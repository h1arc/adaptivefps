using System;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.System.Configuration;
using System.Diagnostics;

namespace AdaptiveFPS;

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IStatusBar StatusBar { get; private set; } = null!;

    private Configuration Config { get; }
    private bool _isApplied;
    private IStatusBarEntry? _entry;
    private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
    private long _lastFrameTicks;
    private double _emaFps = 60.0; // start at a sane default

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        PluginInterface.UiBuilder.OpenConfigUi += Toggle; // reserved for future settings

        Condition.ConditionChange += OnConditionChange;
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;
        Framework.Update += OnFrameworkUpdate;

        // Create status bar entry
        _entry = StatusBar.CreateEntry();
        _entry.Text = BuildStatusText();
        _entry.Tooltip = "AdaptiveFPS: Left-click to rotate OOC cap (Main/60/30). Right-click toggles plugin.";
        _entry.OnClick = () =>
        {
            RotateOutOfCombatCap();
            ApplyForCurrentState();
            RefreshStatus();
        };
        _entry.OnRightClick = () =>
        {
            Config.Enabled = !Config.Enabled;
            Save();
            ApplyForCurrentState();
            RefreshStatus();
        };
        StatusBar.AddEntry(_entry);

        // If already in game, apply now on framework thread
        Framework.RunOnFrameworkThread(() => ApplyForCurrentState());
    }

    public void Dispose()
    {
        Condition.ConditionChange -= OnConditionChange;
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        Framework.Update -= OnFrameworkUpdate;

        if (_entry != null)
        {
            StatusBar.RemoveEntry(_entry);
            _entry = null;
        }

        // restore original cap if we changed it
        if (Config.LastUserCap.HasValue)
        {
            SetCap(Config.LastUserCap.Value);
            Config.LastUserCap = null;
            Save();
        }
    }

    private void OnLogin() => Framework.RunOnFrameworkThread(ApplyForCurrentState);
    private void OnLogout(int type, int code) { /* nothing */ }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.InCombat)
        {
            Framework.RunOnFrameworkThread(ApplyForCurrentState);
        }
    }

    private void Toggle() { /* reserved for future on/off UI */ }

    private void ApplyForCurrentState()
    {
        if (!Config.Enabled || !ClientState.IsLoggedIn)
            return;

        var inCombat = Condition[ConditionFlag.InCombat];
        var desired = inCombat ? Config.CombatCap : Config.OutOfCombatCap;

        // Capture original user cap once, before first change
        if (!_isApplied)
        {
            var current = GetCap();
            Config.LastUserCap = current;
            _isApplied = true;
            Save();
        }

        if (GetCap() != desired)
        {
            Log.Information($"AdaptiveFPS: {(inCombat ? "combat" : "ooc")} -> cap {desired}");
            SetCap(desired);
        }
    }

    private void RotateOutOfCombatCap()
    {
        // Cycle through: 1 (Main Display) -> 2 (60) -> 3 (30) -> 1 ...
        uint next = Config.OutOfCombatCap switch
        {
            1u => 2u,
            2u => 3u,
            3u => 1u,
            _ => 1u,
        };
        Config.OutOfCombatCap = next;
        Save();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // FPS estimator using EMA for smooth status bar display
        var now = _fpsTimer.ElapsedTicks;
        if (_lastFrameTicks != 0)
        {
            var dtTicks = now - _lastFrameTicks;
            if (dtTicks > 0)
            {
                var dt = (double)dtTicks / Stopwatch.Frequency;
                var fps = 1.0 / dt;
                const double alpha = 0.15; // smoothing factor
                _emaFps = (_emaFps * (1 - alpha)) + (fps * alpha);
            }
        }
        _lastFrameTicks = now;

        // Update the status bar text at ~10Hz to reduce churn
        _statusUpdateAccumulator += framework.UpdateDelta.TotalSeconds;
        if (_statusUpdateAccumulator >= 0.1)
        {
            _statusUpdateAccumulator = 0;
            RefreshStatus();
        }
    }

    private double _statusUpdateAccumulator = 0;

    private void RefreshStatus()
    {
        if (_entry == null) return;
        _entry.Text = BuildStatusText();
        _entry.Tooltip = BuildTooltip();
    }

    private string BuildStatusText()
    {
        var capLabel = Config.OutOfCombatCap switch
        {
            1u => "Main",
            2u => "60",
            3u => "30",
            _ => Config.OutOfCombatCap.ToString(),
        };

        var mode = Config.Enabled ? "Adaptive" : "Manual";
        return $"FPS {Math.Round(_emaFps, 1)} | OOC {capLabel} | {mode}";
    }

    private string BuildTooltip()
    {
        var inCombat = Condition[ConditionFlag.InCombat];
        var currentCap = GetCap();
        var currentLabel = currentCap switch
        {
            0u => "None",
            1u => "Main Display",
            2u => "60 fps",
            3u => "30 fps",
            _ => currentCap.ToString(),
        };
        var desired = inCombat ? Config.CombatCap : Config.OutOfCombatCap;
        return $"Current cap: {currentLabel}\nDesired: {desired} ({(inCombat ? "combat" : "ooc")})\nLeft-click: cycle OOC cap\nRight-click: toggle Adaptive mode";
    }

    private static uint GetCap()
    {
        var cfg = ConfigModule.Instance();
        if (cfg == null)
            return 0u;
        // SystemConfigOption.FramerateCap is 0x2D in current dawntrail; use enum if available
        return cfg->GetUint(SystemConfigOption.FramerateCap);
    }

    private static void SetCap(uint value)
    {
        var cfg = ConfigModule.Instance();
        if (cfg == null)
            return;
        cfg->Set(SystemConfigOption.FramerateCap, value);
        cfg->SaveSystemConfig();
    }

    private void Save() => PluginInterface.SavePluginConfig(Config);
}

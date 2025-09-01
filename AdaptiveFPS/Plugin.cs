using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using AdaptiveFPS.Core.Commands;
using AdaptiveFPS.Core.Game;
using AdaptiveFPS.UI;
using AdaptiveFPS.Core;

namespace AdaptiveFPS;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    internal Configuration Config { get; }
    internal bool _isApplied;
    private IDtrBarEntry? _entry;
    private readonly AfpsCommands _commands;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Condition.ConditionChange += OnConditionChange;
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;

        // Register no-op UI callbacks to satisfy validator (no windows in this plugin)
        PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;

        // Start micro-cache updates on framework tick
        MicroCache.Initialize();
        var initSnap = MicroCache.Current;

        _entry = DtrBar.Get(Strings.PluginName);
        _entry.Text = UI.UI.BuildStatus(Config, initSnap);
        _entry.Tooltip = new SeStringBuilder().AddText(UI.UI.BuildTooltip(initSnap.RefreshHz)).Build();
        _entry.OnClick = OnEntryClick;

        // Register chat command handler
        _commands = new AfpsCommands(this);

        // If already in game, apply now on framework thread
        Framework.RunOnFrameworkThread(ApplyAndRefresh);
    }

    public void Dispose()
    {
        Condition.ConditionChange -= OnConditionChange;
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;

        PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;

        _commands.Dispose();
        if (_entry != null)
        {
            DtrBar.Remove(Strings.PluginName);
            _entry = null;
        }

        if (Config.LastUserCap.HasValue)
        {
            FpsHelpers.SetCap(Config.LastUserCap.Value);
            Config.LastUserCap = null;
            Save();
        }

        // Stop micro-cache updates
        MicroCache.Dispose();
    }

    private void OnOpenConfigUi() { /* noop: commands only */ }
    private void OnOpenMainUi() { /* noop: no main UI */ }

    private void OnLogin() => Framework.RunOnFrameworkThread(ApplyAndRefresh);
    private void OnLogout(int type, int code) { /* nothing for now */ }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.InCombat)
        {
            Framework.RunOnFrameworkThread(ApplyAndRefresh);
        }
    }

    // Helper to apply current state and refresh DTR without lambda captures
    internal void ApplyAndRefresh() => Engine.ApplyAndRefresh(this, ref _isApplied, _entry);

    // Helper to restore original cap on disable and refresh DTR
    internal void DisableAndRefresh() => Engine.DisableAndRefresh(this, ref _isApplied, _entry);

    private void OnEntryClick(DtrInteractionEvent e) => Engine.OnEntryClick(this, e, ref _isApplied, _entry);

    internal void Save() => PluginInterface.SavePluginConfig(Config);
}

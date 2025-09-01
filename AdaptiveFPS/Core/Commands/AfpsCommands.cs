using System;
using AdaptiveFPS.UI;
using AdaptiveFPS.Core.Game;
using Dalamud.Game.Command;

namespace AdaptiveFPS.Core.Commands;

internal sealed class AfpsCommands : IDisposable
{
    private readonly Plugin _plugin;

    public AfpsCommands(Plugin plugin)
    {
        _plugin = plugin;
        Plugin.CommandManager.AddHandler(Strings.CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = Strings.CommandHelp
        });
    }

    public void Dispose()
    {
        Plugin.CommandManager.RemoveHandler(Strings.CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var tokens = (args ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            var hz = FpsHelpers.GetRefreshRateHz();
            Plugin.ChatGui.Print(string.Format(
                Strings.StatusFormat,
                FpsHelpers.FormatCap(_plugin.Config.CombatCap, hz),
                FpsHelpers.FormatCap(_plugin.Config.OutOfCombatCap, hz),
                _plugin.Config.Enabled ? "on" : "off"));
            Plugin.ChatGui.Print(Strings.CommandHelp);
            return;
        }

        switch (tokens[0].ToLowerInvariant())
        {
            case "ic":
            case "incombat":
                if (tokens.Length >= 2 && TryParseCapNumber(tokens[1], out var c))
                {
                    _plugin.Config.CombatCap = c;
                    _plugin.Save();
                    Plugin.Framework.RunOnFrameworkThread(_plugin.ApplyAndRefresh);
                    var hz = FpsHelpers.GetRefreshRateHz();
                    Plugin.ChatGui.Print(string.Format(Strings.CombatSetFormat, FpsHelpers.FormatCap(c, hz)));
                }
                else Plugin.ChatGui.Print(Strings.UsageIc);
                break;
            case "ooc":
            case "outofcombat":
                if (tokens.Length >= 2 && TryParseCapNumber(tokens[1], out var o))
                {
                    _plugin.Config.OutOfCombatCap = o;
                    _plugin.Save();
                    Plugin.Framework.RunOnFrameworkThread(_plugin.ApplyAndRefresh);
                    var hz = FpsHelpers.GetRefreshRateHz();
                    Plugin.ChatGui.Print(string.Format(Strings.OocSetFormat, FpsHelpers.FormatCap(o, hz)));
                }
                else Plugin.ChatGui.Print(Strings.UsageOoc);
                break;
            case "toggle":
                _plugin.Config.Enabled = !_plugin.Config.Enabled;
                _plugin.Save();
                if (!_plugin.Config.Enabled)
                    Plugin.Framework.RunOnFrameworkThread(_plugin.DisableAndRefresh);
                else
                    Plugin.Framework.RunOnFrameworkThread(_plugin.ApplyAndRefresh);
                Plugin.ChatGui.Print(string.Format(Strings.EnabledFormat, _plugin.Config.Enabled ? "enabled" : "disabled"));
                break;
            default:
                Plugin.ChatGui.Print(Strings.UnknownCommand);
                break;
        }
    }

    private static bool TryParseCapNumber(string token, out uint cap)
    {
        if (uint.TryParse(token, out var n) && (n == 1 || n == 2 || n == 3))
        {
            cap = n;
            return true;
        }
        cap = 0;
        return false;
    }
}

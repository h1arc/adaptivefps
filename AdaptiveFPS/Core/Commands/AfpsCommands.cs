using System;
using AdaptiveFPS.UI;
using AdaptiveFPS.Core.Game;
using AdaptiveFPS.Core;
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
            var snap = MicroCache.Current;
            Plugin.ChatGui.Print(string.Format(
                Strings.StatusFormat,
                FpsHelpers.FormatCap(_plugin.Config.CombatCap, snap.RefreshHz),
                FpsHelpers.FormatCap(_plugin.Config.OutOfCombatCap, snap.RefreshHz),
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
                    var snapCombat = MicroCache.Current;
                    Plugin.ChatGui.Print(string.Format(Strings.CombatSetFormat, FpsHelpers.FormatCap(c, snapCombat.RefreshHz)));
                }
                else Plugin.ChatGui.Print(Strings.UsageIc);
                break;
            case "ooc":
            case "outofcombat":
                if (tokens.Length >= 2 && TryParseCapNumber(tokens[1], out var o))
                {
                    _plugin.Config.OutOfCombatCap = o;
                    _plugin.Save();
                    var snapOoc = MicroCache.Current;
                    Plugin.ChatGui.Print(string.Format(Strings.OocSetFormat, FpsHelpers.FormatCap(o, snapOoc.RefreshHz)));
                }
                else Plugin.ChatGui.Print(Strings.UsageOoc);
                break;
            case "toggle":
                _plugin.Config.Enabled = !_plugin.Config.Enabled;
                _plugin.Save();
                if (!_plugin.Config.Enabled)
                    _plugin.DisableAndRefresh();
                Plugin.ChatGui.Print(string.Format(Strings.EnabledFormat, _plugin.Config.Enabled ? "enabled" : "disabled"));
                break;
            case "debug":
                var snapDebug = MicroCache.Current;
                var desired = snapDebug.InCombat ? _plugin.Config.CombatCap : _plugin.Config.OutOfCombatCap;
                Plugin.ChatGui.Print($"AdaptiveFPS: {(snapDebug.InCombat ? "Combat" : "Out of Combat")} | Current: {FpsHelpers.FormatCap(snapDebug.CurrentCap, snapDebug.RefreshHz)} | Target: {FpsHelpers.FormatCap(desired, snapDebug.RefreshHz)}");
                break;
            case "reset":
                _plugin.Config.CombatCap = 1u; // Main display
                _plugin.Config.OutOfCombatCap = 3u; // 30fps
                _plugin.Save();
                Plugin.ChatGui.Print("AdaptiveFPS: Reset to defaults - Combat: Main Display, OOC: 30fps");
                break;
            case "glow":
                if (tokens.Length >= 2)
                {
                    if (tokens[1].Equals("toggle", StringComparison.InvariantCultureIgnoreCase))
                    {
                        _plugin.Config.GlowEnabled = !_plugin.Config.GlowEnabled;
                        _plugin.Save();
                        Plugin.ChatGui.Print(string.Format(Strings.GlowToggleFormat, _plugin.Config.GlowEnabled ? "enabled" : "disabled"));
                    }
                    else if (TryParseGlowNumber(tokens[1], out var glowType))
                    {
                        _plugin.Config.GlowType = glowType;
                        _plugin.Save();
                        Plugin.ChatGui.Print(string.Format(Strings.GlowSetFormat, glowType));
                    }
                    else
                    {
                        Plugin.ChatGui.Print(Strings.UsageGlow);
                    }
                }
                else Plugin.ChatGui.Print(Strings.UsageGlow);
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

    private static bool TryParseGlowNumber(string token, out ushort glowType)
    {
        if (ushort.TryParse(token, out var n) && n >= 0 && n <= 5)
        {
            glowType = n;
            return true;
        }
        glowType = 0;
        return false;
    }
}

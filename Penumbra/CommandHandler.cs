using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI;

namespace Penumbra;

public class CommandHandler : IDisposable
{
    private const string CommandName = "/penumbra";

    private readonly ICommandManager   _commandManager;
    private readonly RedrawService     _redrawService;
    private readonly IChatGui          _chat;
    private readonly Configuration     _config;
    private readonly ConfigWindow      _configWindow;
    private readonly ActorManager      _actors;
    private readonly ModManager        _modManager;
    private readonly CollectionManager _collectionManager;
    private readonly Penumbra          _penumbra;
    private readonly CollectionEditor  _collectionEditor;

    public CommandHandler(IFramework framework, ICommandManager commandManager, IChatGui chat, RedrawService redrawService,
        Configuration config,
        ConfigWindow configWindow, ModManager modManager, CollectionManager collectionManager, ActorManager actors, Penumbra penumbra,
        CollectionEditor collectionEditor)
    {
        _commandManager    = commandManager;
        _redrawService     = redrawService;
        _config            = config;
        _configWindow      = configWindow;
        _modManager        = modManager;
        _collectionManager = collectionManager;
        _actors            = actors;
        _chat              = chat;
        _penumbra          = penumbra;
        _collectionEditor  = collectionEditor;
        framework.RunOnFrameworkThread(() =>
        {
            if (_commandManager.Commands.ContainsKey(CommandName))
                _commandManager.RemoveHandler(CommandName);
            _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "不带参数开关主窗口。 在聊天框输入'/penumbra help'查看命令行参数使用说明。",
                ShowInHelp  = true,
            });
            Penumbra.Log.Information($"Registered {CommandName} with Dalamud.");
        });
    }

    public void Dispose()
        => _commandManager.RemoveHandler(CommandName);

    private void OnCommand(string command, string arguments)
    {
        if (arguments.Length == 0)
            arguments = "window";

        var argumentList = arguments.Split(' ', 2);
        arguments = argumentList.Length == 2 ? argumentList[1] : string.Empty;

        var _ = argumentList[0].ToLowerInvariant() switch
        {
            "window"     => ToggleWindow(arguments),
            "enable"     => SetPenumbraState(arguments, true),
            "disable"    => SetPenumbraState(arguments, false),
            "toggle"     => SetPenumbraState(arguments, null),
            "reload"     => Reload(arguments),
            "redraw"     => Redraw(arguments),
            "lockui"     => SetUiLockState(arguments),
            "size"       => SetUiMinimumSize(arguments),
            "debug"      => SetDebug(arguments),
            "collection" => SetCollection(arguments),
            "mod"        => SetMod(arguments),
            "bulktag"    => SetTag(arguments),
            _            => PrintHelp(argumentList[0]),
        };
    }

    private bool PrintHelp(string arguments)
    {
        if (!string.Equals(arguments, "help", StringComparison.OrdinalIgnoreCase) && arguments == "?")
            _chat.Print(new SeStringBuilder().AddText("给出的参数 ").AddRed(arguments, true)
                .AddText(" 无效.。有效的参数为：").BuiltString);
        else
            _chat.Print("/penumbra'命令的有效参数为：");

        _chat.Print(new SeStringBuilder().AddCommand("window",
                "开关Penumbra的主设置窗口。可与[on|off]一起使用以强制维持特定状态。未提供参数时则切换状态。")
            .BuiltString);
        _chat.Print(new SeStringBuilder()
            .AddCommand("enable", "启用模组并强制重绘所有游戏对象（如果以前取消勾选了'启用模组'）。").BuiltString);
        _chat.Print(new SeStringBuilder()
            .AddCommand("disable", "禁用模组并强制重绘所有游戏对象（如果以前勾选了'启用模组'）。").BuiltString);
        _chat.Print(new SeStringBuilder().AddCommand("toggle", "切换启用模组的状态，并强制重绘所有游戏对象。")
            .BuiltString);
        _chat.Print(new SeStringBuilder().AddCommand("reload", "重新搜寻模组目录并加载所有模组。").BuiltString);
        _chat.Print(new SeStringBuilder()
            .AddCommand("redraw", "重绘所有游戏对象。可以指定一个名称重绘特定对象。").BuiltString);
        _chat.Print(new SeStringBuilder()
            .AddCommand("lockui", "切换Penumbra主窗口的锁定状态。可与[on|off]一起使用以强制维持特定状态。")
            .BuiltString);
        _chat.Print(new SeStringBuilder().AddCommand("尺寸", "将配置窗口的最小尺寸重设为默认值。").BuiltString);
        _chat.Print(new SeStringBuilder()
            .AddCommand("debug", "切换Penumbra的调试模式。可与[on|off]一起使用以强制维持特定状态。").BuiltString);
        _chat.Print(new SeStringBuilder()
            .AddCommand("collection", "更改激活合集的设置，不加参数获取此命令的详细说明。")
            .BuiltString);
        _chat.Print(new SeStringBuilder()
            .AddCommand("mod", "更改特定模组的设置，不加参数获取此命令的详细说明。").BuiltString);
        _chat.Print(new SeStringBuilder()
            .AddCommand("bulktag", "根据标签来修改多个模组的设置，不加参数获取此命令的详细说明。")
            .BuiltString);
        return true;
    }

    private bool ToggleWindow(string arguments)
    {
        var value = ParseTrueFalseToggle(arguments) ?? !_configWindow.IsOpen;
        if (value == _configWindow.IsOpen)
            return false;

        _configWindow.Toggle();
        return true;
    }

    private bool Reload(string _)
    {
        _modManager.DiscoverMods();
        Print($"重新加载了Penumbra模组. 你有 {_modManager.Count} 个模组。");
        return true;
    }

    private bool Redraw(string arguments)
    {
        if (arguments.Length > 0)
            _redrawService.RedrawObject(arguments, RedrawType.Redraw);
        else
            _redrawService.RedrawAll(RedrawType.Redraw);

        return true;
    }

    private bool SetDebug(string arguments)
    {
        var value = ParseTrueFalseToggle(arguments) ?? !_config.DebugMode;
        if (value == _config.DebugMode)
            return false;

        Print(value ? "调试模式已启用。" : "调试模式已禁用。" );

        _config.DebugMode = value;
        _config.Save();
        return true;
    }

    private bool SetPenumbraState(string _, bool? newValue)
    {
        var value = newValue ?? !_config.EnableMods;

        if (value == _config.EnableMods)
        {
            Print(value
                ? "你的模组已经启用。要禁用模组，请使用此命令：/penumbra disable"
                : "你的模组已经禁用。要启用模组，请使用此命令：/penumbra enable" );
            return false;
        }

        Print(value
            ? "你的模组已启用。"
            : "你的模组已禁用。" );
        return _penumbra.SetEnabled(value);
    }

    private bool SetUiLockState(string arguments)
    {
        var value = ParseTrueFalseToggle(arguments) ?? !_config.Ephemeral.FixMainWindow;
        if (value == _config.Ephemeral.FixMainWindow)
            return false;

        if (value)
        {
            Print("Penumbra UI locked in place.");
            _configWindow.Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        }
        else
        {
            Print("Penumbra UI unlocked.");
            _configWindow.Flags &= ~(ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);
        }

        _config.Ephemeral.FixMainWindow = value;
        _config.Ephemeral.Save();
        return true;
    }

    private bool SetUiMinimumSize(string _)
    {
        if (_config.MinimumSize.X == Configuration.Constants.MinimumSizeX && _config.MinimumSize.Y == Configuration.Constants.MinimumSizeY)
            return false;

        _config.MinimumSize.X = Configuration.Constants.MinimumSizeX;
        _config.MinimumSize.Y = Configuration.Constants.MinimumSizeY;
        _config.Save();
        return true;
    }

    private bool SetCollection(string arguments)
    {
        if (arguments.Length == 0)
        {
            _chat.Print(new SeStringBuilder().AddText("Use with /penumbra collection ").AddBlue("[Collection Type]")
                .AddText(" | ").AddYellow("[Collection Name]")
                .AddText(" | ").AddGreen("<Identifier>").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》 Valid Collection Types are ").AddBlue("Base").AddText(", ")
                .AddBlue("Ui").AddText(", ")
                .AddBlue("Selected").AddText(", ")
                .AddBlue("Individual").AddText(", and all those selectable in Character Groups.").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》 Valid Collection Names are ").AddYellow("None")
                .AddText(", all collections you have created by their full names, and ").AddYellow("Delete")
                .AddText(" to remove assignments (not valid for all types).")
                .BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》 If the type is ").AddBlue("Individual")
                .AddText(" you need to specify an individual with an identifier of the form:").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("<me>").AddText(" or ").AddGreen("<t>")
                .AddText(" or ").AddGreen("<mo>")
                .AddText(" or ").AddGreen("<f>")
                .AddText(" as placeholders for your character, your target, your mouseover or your focus, if they exist.").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("p").AddText(" | ")
                .AddWhite("[Player Name]@<World Name>")
                .AddText(", if no @ is provided, Any World is used.").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("r").AddText(" | ").AddWhite("[Retainer Name]")
                .BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("n").AddText(" | ").AddPurple("[NPC Type]")
                .AddText(" : ")
                .AddRed("[NPC Name]").AddText(", where NPC Type can be ").AddInitialPurple("Mount").AddInitialPurple("Companion")
                .AddInitialPurple("Accessory")
                .AddInitialPurple("Event NPC").AddText("or ")
                .AddInitialPurple("Battle NPC", false).AddText(".").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("o").AddText(" | ").AddPurple("[NPC Type]")
                .AddText(" : ")
                .AddRed("[NPC Name]").AddText(" | ").AddWhite("[Player Name]@<World Name>").AddText(".").BuiltString);
            return true;
        }

        var split    = arguments.Split('|', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var typeName = split[0];

        if (!CollectionTypeExtensions.TryParse(typeName, out var type))
        {
            _chat.Print(new SeStringBuilder().AddText("The argument ").AddRed(typeName, true)
                .AddText(" is not a valid collection type.").BuiltString);
            return false;
        }

        if (split.Length == 1)
        {
            _chat.Print("There was no collection name provided.");
            return false;
        }

        if (!GetModCollection(split[1], out var collection))
            return false;

        var identifiers = Array.Empty<ActorIdentifier>();
        if (type is CollectionType.Individual)
        {
            if (split.Length == 2)
            {
                _chat.Print(
                    "Setting an individual collection requires a collection name and an identifier, but no identifier was provided.");
                return false;
            }

            try
            {
                if (_redrawService.GetName(split[2].ToLowerInvariant(), out var obj))
                {
                    var identifier = _actors.FromObject(obj, false, true, true);
                    if (!identifier.IsValid)
                    {
                        _chat.Print(new SeStringBuilder().AddText("The placeholder ").AddGreen(split[2])
                            .AddText(" did not resolve to a game object with a valid identifier.").BuiltString);
                        return false;
                    }

                    identifiers = new[]
                    {
                        identifier,
                    };
                }
                else
                {
                    identifiers = _actors.FromUserString(split[2], false);
                }
            }
            catch (ActorManager.IdentifierParseError e)
            {
                _chat.Print(new SeStringBuilder().AddText("The argument ").AddRed(split[2], true)
                    .AddText($" could not be converted to an identifier. {e.Message}")
                    .BuiltString);
                return false;
            }
        }

        var anySuccess = false;
        foreach (var identifier in identifiers.Distinct().DefaultIfEmpty(ActorIdentifier.Invalid))
        {
            var oldCollection = _collectionManager.Active.ByType(type, identifier);
            if (collection == oldCollection)
            {
                _chat.Print(collection == null
                    ? $"The {type.ToName()} Collection{(identifier.IsValid ? $" for {identifier}" : string.Empty)} is already unassigned"
                    : $"{collection.Name} already is the {type.ToName()} Collection{(identifier.IsValid ? $" for {identifier}." : ".")}");
                continue;
            }

            var individualIndex = _collectionManager.Active.Individuals.Index(identifier);

            if (oldCollection == null)
            {
                if (type.IsSpecial())
                {
                    _collectionManager.Active.CreateSpecialCollection(type);
                }
                else if (identifier.IsValid)
                {
                    var identifierGroup = _collectionManager.Active.Individuals.GetGroup(identifier);
                    individualIndex = _collectionManager.Active.Individuals.Count;
                    _collectionManager.Active.CreateIndividualCollection(identifierGroup);
                }
            }
            else if (collection == null)
            {
                if (type.IsSpecial())
                {
                    _collectionManager.Active.RemoveSpecialCollection(type);
                }
                else if (individualIndex >= 0)
                {
                    _collectionManager.Active.RemoveIndividualCollection(individualIndex);
                }
                else
                {
                    _chat.Print(
                        $"Can not remove the {type.ToName()} Collection assignment {(identifier.IsValid ? $" for {identifier}." : ".")}");
                    continue;
                }

                Print(
                    $"Removed {oldCollection.Name} as {type.ToName()} Collection assignment {(identifier.IsValid ? $" for {identifier}." : ".")}");
                anySuccess = true;
                continue;
            }

            _collectionManager.Active.SetCollection(collection!, type, individualIndex);
            Print($"Assigned {collection!.Name} as {type.ToName()} Collection{(identifier.IsValid ? $" for {identifier}." : ".")}");
        }

        return anySuccess;
    }

    private bool SetMod(string arguments)
    {
        if (arguments.Length == 0)
        {
            var seString = new SeStringBuilder()
                .AddText("Use with /penumbra mod ").AddBlue("[enable|disable|inherit|toggle]").AddText("  ").AddYellow("[Collection Name]")
                .AddText(" | ")
                .AddPurple("[Mod Name or Mod Directory Name]");
            _chat.Print(seString.BuiltString);
            return true;
        }

        var split = arguments.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var nameSplit = split.Length != 2
            ? Array.Empty<string>()
            : split[1].Split('|', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (nameSplit.Length != 2)
        {
            _chat.Print("Not enough arguments provided.");
            return false;
        }

        var state = ConvertToSettingState(split[0]);
        if (state == -1)
        {
            _chat.Print(new SeStringBuilder().AddRed(split[0], true).AddText(" is not a valid type of setting.").BuiltString);
            return false;
        }

        if (!GetModCollection(nameSplit[0], out var collection) || collection == ModCollection.Empty)
            return false;

        if (!_modManager.TryGetMod(nameSplit[1], nameSplit[1], out var mod))
        {
            _chat.Print(new SeStringBuilder().AddText("The mod ").AddRed(nameSplit[1], true).AddText(" does not exist.")
                .BuiltString);
            return false;
        }

        if (HandleModState(state, collection!, mod))
            return true;

        _chat.Print(new SeStringBuilder().AddText("Mod ").AddPurple(mod.Name, true)
            .AddText("already had the desired state in collection ")
            .AddYellow(collection!.Name, true).AddText(".").BuiltString);
        return false;
    }

    private enum TagType
    {
        Local,
        Mod,
        Both,
    }

    private bool SetTag(string arguments)
    {
        if (arguments.Length == 0)
        {
            var seString = new SeStringBuilder()
                .AddText("Use with /penumbra bulktag ").AddBlue("[enable|disable|toggle|inherit]").AddText("  ").AddYellow("[Collection Name]")
                .AddText(" | ")
                .AddPurple("[Tag]");
            _chat.Print(seString.BuiltString);
            var tagString = new SeStringBuilder()
                .AddText("    》 ")
                .AddPurple("[Tag]")
                .AddText(" is only Local tags by default, but can be prefixed with '")
                .AddWhite("b:")
                .AddText("' for both types of tags or '")
                .AddWhite("m:")
                .AddText("' for only Mod tags.");
            _chat.Print(tagString.BuiltString);
            return true;
        }

        var split = arguments.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var nameSplit = split.Length != 2
            ? Array.Empty<string>()
            : split[1].Split('|', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (nameSplit.Length != 2)
        {
            _chat.Print("Not enough arguments provided.");
            return false;
        }

        var state = ConvertToSettingState(split[0]);

        if (state == -1)
        {
            _chat.Print(new SeStringBuilder().AddRed(split[0], true).AddText(" is not a valid type of setting.").BuiltString);
            return false;
        }

        if (!GetModCollection(nameSplit[0], out var collection) || collection == ModCollection.Empty)
            return false;

        var tagType = nameSplit[1].Length < 3 || nameSplit[1][1] != ':'
            ? TagType.Local
            : nameSplit[1][0] switch
            {
                'b' => TagType.Both,
                'm' => TagType.Mod,
                _   => TagType.Local,
            };
        var tag = tagType is TagType.Local ? nameSplit[1] : nameSplit[1][2..];

        var mods = tagType switch
        {
            TagType.Local => _modManager.Where(m => m.LocalTags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList(),
            TagType.Mod   => _modManager.Where(m => m.ModTags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList(),
            _             => _modManager.Where(m => m.LocalTags.Concat(m.ModTags).Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList(),
        };

        if (mods.Count == 0)
        {
            _chat.Print(new SeStringBuilder().AddText("The tag ").AddRed(tag, true).AddText(" does not match any mods.")
                .BuiltString);
            return false;
        }

        var changes = false;
        foreach (var mod in mods)
            changes |= HandleModState(state, collection!, mod);

        if (!changes)
            Print(() => new SeStringBuilder().AddText("No mod states were changed in collection ").AddYellow(collection!.Name, true)
                .AddText(".").BuiltString);

        return true;
    }

    private bool GetModCollection(string collectionName, out ModCollection? collection)
    {
        var lowerName = collectionName.ToLowerInvariant();
        if (lowerName == "delete")
        {
            collection = null;
            return true;
        }

        collection = string.Equals(lowerName, ModCollection.Empty.Name, StringComparison.OrdinalIgnoreCase)
            ? ModCollection.Empty
            : _collectionManager.Storage.ByName(lowerName, out var c)
                ? c
                : null;
        if (collection != null)
            return true;

        _chat.Print(new SeStringBuilder().AddText("The collection ").AddRed(collectionName, true).AddText(" does not exist.")
            .BuiltString);
        return false;
    }

    private static bool? ParseTrueFalseToggle(string value)
        => value.ToLowerInvariant() switch
        {
            "0"        => false,
            "false"    => false,
            "off"      => false,
            "disable"  => false,
            "disabled" => false,

            "1"       => true,
            "true"    => true,
            "on"      => true,
            "enable"  => true,
            "enabled" => true,

            _ => null,
        };

    private static int ConvertToSettingState(string text)
        => text.ToLowerInvariant() switch
        {
            "enable"    => 0,
            "enabled"   => 0,
            "disable"   => 1,
            "disabled"  => 1,
            "toggle"    => 2,
            "inherit"   => 3,
            "inherited" => 3,
            _           => -1,
        };

    private bool HandleModState(int settingState, ModCollection collection, Mod mod)
    {
        var settings = collection.Settings[mod.Index];
        switch (settingState)
        {
            case 0:
                if (!_collectionEditor.SetModState(collection, mod, true))
                    return false;

                Print(() => new SeStringBuilder().AddText("Enabled mod ").AddPurple(mod.Name, true).AddText(" in collection ")
                    .AddYellow(collection.Name, true)
                    .AddText(".").BuiltString);
                return true;

            case 1:
                if (!_collectionEditor.SetModState(collection, mod, false))
                    return false;

                Print(() => new SeStringBuilder().AddText("Disabled mod ").AddPurple(mod.Name, true).AddText(" in collection ")
                    .AddYellow(collection.Name, true)
                    .AddText(".").BuiltString);
                return true;

            case 2:
                var setting = !(settings?.Enabled ?? false);
                if (!_collectionEditor.SetModState(collection, mod, setting))
                    return false;

                Print(() => new SeStringBuilder().AddText(setting ? "Enabled mod " : "Disabled mod ").AddPurple(mod.Name, true)
                    .AddText(" in collection ")
                    .AddYellow(collection.Name, true)
                    .AddText(".").BuiltString);
                return true;

            case 3:
                if (!_collectionEditor.SetModInheritance(collection, mod, true))
                    return false;

                Print(() => new SeStringBuilder().AddText("Set mod ").AddPurple(mod.Name, true).AddText(" in collection ")
                    .AddYellow(collection.Name, true)
                    .AddText(" to inherit.").BuiltString);
                return true;
        }

        return false;
    }

    private void Print(string text)
    {
        if (_config.PrintSuccessfulCommandsToChat)
            _chat.Print(text);
    }

    private void Print(DefaultInterpolatedStringHandler text)
    {
        if (_config.PrintSuccessfulCommandsToChat)
            _chat.Print(text.ToStringAndClear());
    }

    private void Print(Func<SeString> text)
    {
        if (_config.PrintSuccessfulCommandsToChat)
            _chat.Print(text());
    }
}

using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Luna;
using Penumbra.Api.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.Knowledge;
using Penumbra.UI.MainWindow;

namespace Penumbra;

public class CommandHandler : IDisposable, IApiService
{
    private const string CommandName = "/penumbra";

    private readonly ICommandManager   _commandManager;
    private readonly RedrawService     _redrawService;
    private readonly IChatGui          _chat;
    private readonly Configuration     _config;
    private readonly MainWindow        _mainWindow;
    private readonly ActorManager      _actors;
    private readonly ModManager        _modManager;
    private readonly CollectionManager _collectionManager;
    private readonly CollectionEditor  _collectionEditor;
    private readonly KnowledgeWindow   _knowledgeWindow;

    public CommandHandler(IFramework framework, ICommandManager commandManager, IChatGui chat, RedrawService redrawService,
        Configuration config, MainWindow mainWindow, ModManager modManager, CollectionManager collectionManager, ActorManager actors,
        CollectionEditor collectionEditor, KnowledgeWindow knowledgeWindow)
    {
        _commandManager    = commandManager;
        _redrawService     = redrawService;
        _config            = config;
        _mainWindow        = mainWindow;
        _modManager        = modManager;
        _collectionManager = collectionManager;
        _actors            = actors;
        _chat              = chat;
        _collectionEditor  = collectionEditor;
        _knowledgeWindow   = knowledgeWindow;
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

        _ = argumentList[0].ToLowerInvariant() switch
        {
            "window"        => ToggleWindow(arguments),
            "enable"        => SetPenumbraState(arguments, true),
            "disable"       => SetPenumbraState(arguments, false),
            "toggle"        => SetPenumbraState(arguments, null),
            "reload"        => Reload(arguments),
            "redraw"        => Redraw(arguments),
            "size"          => SetUiMinimumSize(arguments),
            "debug"         => SetDebug(arguments),
            "collection"    => SetCollection(arguments),
            "mod"           => SetMod(arguments),
            "bulktag"       => SetTag(arguments),
            "clearsettings" => ClearSettings(arguments),
            "knowledge"     => HandleKnowledge(arguments),
            _               => PrintHelp(argumentList[0]),
        };
    }

    private bool PrintHelp(string arguments)
    {
        if (!string.Equals(arguments, "help", StringComparison.OrdinalIgnoreCase) && arguments != "?")
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
        _chat.Print(new SeStringBuilder().AddCommand("size", "将配置窗口的最小尺寸重设为默认值。").BuiltString);
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
        _chat.Print(new SeStringBuilder()
            .AddCommand("clearsettings",
                "清除当前或所有合集中手动通过Penumbra应用的所有临时设置。使用'all'参数清除所有。")
            .BuiltString);
        return true;
    }

    private bool ClearSettings(string arguments)
    {
        if (arguments.Trim().ToLowerInvariant() is "all")
            foreach (var collection in _collectionManager.Storage)
                _collectionEditor.ClearTemporarySettings(collection);
        else
            _collectionEditor.ClearTemporarySettings(_collectionManager.Active.Current);

        return true;
    }

    private bool ToggleWindow(string arguments)
    {
        var value = ParseTrueFalseToggle(arguments) ?? !_mainWindow.IsOpen;
        if (value == _mainWindow.IsOpen)
            return false;

        _mainWindow.Toggle();
        return true;
    }

    private bool Reload(string _)
    {
        _modManager.DiscoverMods();
        Print($"重新加载了Penumbra模组。你有 {_modManager.Count} 个模组。");
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
        var value = ParseTrueFalseToggle(arguments) ?? !_config.Advanced.DebugMode;
        if (value == _config.Advanced.DebugMode)
            return false;

        Print(value ? "调试模式已启用。" : "调试模式已禁用。" );

        _config.Advanced.DebugMode = value;
        return true;
    }

    private bool SetPenumbraState(string _, bool? newValue)
    {
        var value = newValue ?? !_config.Main.EnableMods;

        if (value == _config.Main.EnableMods)
        {
            Print(value
                ? "你的模组已经启用。要禁用模组，请使用此命令：/penumbra disable"
                : "你的模组已经禁用。要启用模组，请使用此命令：/penumbra enable" );
            return false;
        }

        Print(value
            ? "你的模组已启用。"
            : "你的模组已禁用。");
        _config.Main.EnableMods = value;
        return true;
    }

    private bool SetUiMinimumSize(string _)
    {
        if (_config.Advanced.MinimumSize.X == AdvancedConfig.MinimumSizeX && _config.Advanced.MinimumSize.Y == AdvancedConfig.MinimumSizeY)
            return false;

        _config.Advanced.MinimumSize = new Vector2(AdvancedConfig.MinimumSizeX, AdvancedConfig.MinimumSizeY);
        return true;
    }

    private bool SetCollection(string arguments)
    {
        if (arguments.Length is 0)
        {
            _chat.Print(new SeStringBuilder().AddText("用法：/penumbra collection ").AddBlue("[合集类型]")
                .AddText(" | ").AddYellow("[合集名称]")
                .AddText(" | ").AddGreen("<标识符>").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》 有效的合集类型为 ").AddBlue("Base").AddText("、")
                .AddBlue("Ui").AddText("、")
                .AddBlue("Selected").AddText("、")
                .AddBlue("Individual").AddText("，以及角色组中所有可选类型。").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》 有效的合集名称为 ").AddYellow("None")
                .AddText("、你已创建的所有合集的完整名称，以及 ").AddYellow("Delete")
                .AddText(" 用于移除分配（并非所有类型都有效）。")
                .BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》 如果类型为 ").AddBlue("Individual")
                .AddText("，需要指定个体及其标识符，格式如下：").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("<me>").AddText(" 或 ").AddGreen("<t>")
                .AddText(" 或 ").AddGreen("<mo>")
                .AddText(" 或 ").AddGreen("<f>")
                .AddText(" 分别作为你的角色、当前目标、鼠标悬停对象或焦点对象的占位符（若存在）。").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("p").AddText(" | ")
                .AddWhite("[玩家名称]@<服务器名称>")
                .AddText("，若未提供 @，则使用任意服务器。").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("r").AddText(" | ").AddWhite("[雇员名称]")
                .BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("n").AddText(" | ").AddPurple("[NPC 类型]")
                .AddText(" : ")
                .AddRed("[NPC 名称]").AddText("，其中 NPC 类型可以是 ").AddInitialPurple("Mount").AddInitialPurple("Companion")
                .AddInitialPurple("Accessory")
                .AddInitialPurple("Event NPC").AddText("或 ")
                .AddInitialPurple("Battle NPC", false).AddText("。").BuiltString);
            _chat.Print(new SeStringBuilder().AddText("    》》》 ").AddGreen("o").AddText(" | ").AddPurple("[NPC 类型]")
                .AddText(" : ")
                .AddRed("[NPC 名称]").AddText(" | ").AddWhite("[玩家名称]@<服务器名称>").AddText("。").BuiltString);
            return true;
        }

        var split    = arguments.Split('|', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var typeName = split[0];

        if (!CollectionTypeExtensions.TryParse(typeName, out var type))
        {
            _chat.Print(new SeStringBuilder().AddText("参数 ").AddRed(typeName, true)
                .AddText(" 不是有效的合集类型。").BuiltString);
            return false;
        }

        if (split.Length == 1)
        {
            _chat.Print("未提供合集名称。");
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
                    "设置个体合集需要合集名称和标识符，但未提供标识符。");
                return false;
            }

            try
            {
                if (_redrawService.GetName(split[2].ToLowerInvariant(), out var obj))
                {
                    var identifier = _actors.FromObject(obj, false, true, true);
                    if (!identifier.IsValid)
                    {
                        _chat.Print(new SeStringBuilder().AddText("占位符 ").AddGreen(split[2])
                            .AddText(" 未能解析为具有有效标识符的游戏对象。").BuiltString);
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
            catch (ActorIdentifierFactory.IdentifierParseError e)
            {
                _chat.Print(new SeStringBuilder().AddText("参数 ").AddRed(split[2], true)
                    .AddText($" 无法转换为标识符。{e.Message}")
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
                    ? $"{type.ToName()} 合集{(identifier.IsValid ? $"（{identifier}）" : string.Empty)} 已处于未分配状态"
                    : $"{collection.Identity.Name} 已经是 {type.ToName()} 合集{(identifier.IsValid ? $"（{identifier}）。" : "。")}");
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
                        $"无法移除 {type.ToName()} 合集分配{(identifier.IsValid ? $"（{identifier}）。" : "。")}");
                    continue;
                }

                Print(
                    $"已移除 {oldCollection.Identity.Name} 作为 {type.ToName()} 合集的分配{(identifier.IsValid ? $"（{identifier}）。" : "。")}");
                anySuccess = true;
                continue;
            }

            _collectionManager.Active.SetCollection(collection!, type, individualIndex);
            Print($"已将 {collection!.Identity.Name} 分配为 {type.ToName()} 合集{(identifier.IsValid ? $"（{identifier}）。" : "。")}");
        }

        return anySuccess;
    }

    private bool SetMod(string arguments)
    {
        if (arguments.Length == 0)
        {
            var seString = new SeStringBuilder()
                .AddText("用法：/penumbra mod ").AddBlue("[enable|disable|inherit|toggle|").AddGreen("setting").AddBlue("]").AddText("  ")
                .AddYellow("[合集名称]")
                .AddText(" | ")
                .AddPurple("[模组名称或模组目录名]")
                .AddGreen(" <| [选项组名称] | [选项1;选项2;...]>");
            _chat.Print(seString.BuiltString);
            return true;
        }

        var split = arguments.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var nameSplit = split.Length != 2
            ? []
            : split[1].Split('|', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (nameSplit.Length != 2)
        {
            _chat.Print("提供的参数不足。");
            return false;
        }

        var state = ConvertToSettingState(split[0]);
        if (state == -1)
        {
            _chat.Print(new SeStringBuilder().AddRed(split[0], true).AddText(" 不是有效的设置类型。").BuiltString);
            return false;
        }

        if (!GetModCollection(nameSplit[0], out var collection) || collection == ModCollection.Empty)
            return false;

        var groupName   = string.Empty;
        var optionNames = Array.Empty<string>();
        if (state is 4)
        {
            var split2 = nameSplit[1].Split('|', 3, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (split2.Length < 2)
            {
                _chat.Print(
                    "更改设置的参数不足。请提供组名称和设置名称列表（多选项组可以为空）。");
                return false;
            }

            nameSplit[1] = split2[0];
            groupName    = split2[1];
            if (split2.Length == 3)
                optionNames = split2[2].Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        if (!_modManager.TryGetMod(nameSplit[1], nameSplit[1], out var mod))
        {
            _chat.Print(new SeStringBuilder().AddText("模组 ").AddRed(nameSplit[1], true).AddText(" 不存在。")
                .BuiltString);
            return false;
        }

        if (state < 4)
        {
            if (HandleModState(state, collection!, mod))
                return true;

            _chat.Print(new SeStringBuilder().AddText("模组 ").AddPurple(mod.Name, true)
                .AddText(" 在合集 ")
                .AddYellow(collection!.Identity.Name, true).AddText(" 中已处于目标状态。").BuiltString);
            return false;
        }

        switch (ModSettingsApi.ConvertModSetting(mod, groupName, optionNames, out var groupIndex, out var setting))
        {
            case PenumbraApiEc.OptionGroupMissing:
                _chat.Print(new SeStringBuilder().AddText("模组 ").AddRed(nameSplit[1], true).AddText(" 没有组 ")
                    .AddGreen(groupName, true).AddText("。").BuiltString);
                break;
            case PenumbraApiEc.OptionMissing:
                _chat.Print(new SeStringBuilder().AddText("模组 ").AddRed(nameSplit[1], true)
                    .AddText(" 中并非所有指定选项都能在组 ").AddGreen(groupName, true).AddText(" 中找到。").BuiltString);
                break;
            case PenumbraApiEc.Success:
                _collectionEditor.SetModSetting(collection!, mod, groupIndex, setting);
                Print(() => new SeStringBuilder().AddText("已更改组 ").AddGreen(groupName, true).AddText(" 在模组 ")
                    .AddPurple(mod.Name, true).AddText(" 中的设置，合集为 ")
                    .AddYellow(collection!.Identity.Name, true).AddText("。").BuiltString);
                return true;
        }

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
                .AddText("用法：/penumbra bulktag ").AddBlue("[enable|disable|toggle|inherit]").AddText("  ").AddYellow("[合集名称]")
                .AddText(" | ")
                .AddPurple("[标签]");
            _chat.Print(seString.BuiltString);
            var tagString = new SeStringBuilder()
                .AddText("    》 ")
                .AddPurple("[标签]")
                .AddText(" 默认仅匹配本地标签，可加前缀 '")
                .AddWhite("b:")
                .AddText("' 以同时匹配两种标签，或 '")
                .AddWhite("m:")
                .AddText("' 以仅匹配模组标签。");
            _chat.Print(tagString.BuiltString);
            return true;
        }

        var split = arguments.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var nameSplit = split.Length != 2
            ? Array.Empty<string>()
            : split[1].Split('|', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (nameSplit.Length != 2)
        {
            _chat.Print("提供的参数不足。");
            return false;
        }

        var state = ConvertToSettingState(split[0]);

        if (state == -1)
        {
            _chat.Print(new SeStringBuilder().AddRed(split[0], true).AddText(" 不是有效的设置类型。").BuiltString);
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
            _chat.Print(new SeStringBuilder().AddText("标签 ").AddRed(tag, true).AddText(" 未匹配任何模组。")
                .BuiltString);
            return false;
        }

        var changes = false;
        foreach (var mod in mods)
            changes |= HandleModState(state, collection!, mod);

        if (!changes)
            Print(() => new SeStringBuilder().AddText("合集 ").AddYellow(collection!.Identity.Name, true)
                .AddText(" 中没有任何模组状态被更改。").BuiltString);

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

        collection = string.Equals(lowerName, ModCollection.Empty.Identity.Name, StringComparison.OrdinalIgnoreCase)
            ? ModCollection.Empty
            : _collectionManager.Storage.ByIdentifier(lowerName, out var c)
                ? c
                : null;
        if (collection != null)
            return true;

        _chat.Print(new SeStringBuilder().AddText("合集 ").AddRed(collectionName, true).AddText(" 不存在。")
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
            "setting"   => 4,
            "settings"  => 4,
            _           => -1,
        };

    private bool HandleModState(int settingState, ModCollection collection, Mod mod)
    {
        var settings = collection.GetOwnSettings(mod.Index);
        switch (settingState)
        {
            case 0:
                if (!_collectionEditor.SetModState(collection, mod, true))
                    return false;

                Print(() => new SeStringBuilder().AddText("已启用模组 ").AddPurple(mod.Name, true).AddText("，合集为 ")
                    .AddYellow(collection.Identity.Name, true)
                    .AddText(".").BuiltString);
                return true;

            case 1:
                if (!_collectionEditor.SetModState(collection, mod, false))
                    return false;

                Print(() => new SeStringBuilder().AddText("已禁用模组 ").AddPurple(mod.Name, true).AddText("，合集为 ")
                    .AddYellow(collection.Identity.Name, true)
                    .AddText(".").BuiltString);
                return true;

            case 2:
                var setting = !(settings?.Enabled ?? false);
                if (!_collectionEditor.SetModState(collection, mod, setting))
                    return false;

                Print(() => new SeStringBuilder().AddText(setting ? "已启用模组 " : "已禁用模组 ").AddPurple(mod.Name, true)
                    .AddText("，合集为 ")
                    .AddYellow(collection.Identity.Name, true)
                    .AddText(".").BuiltString);
                return true;

            case 3:
                if (!_collectionEditor.SetModInheritance(collection, mod, true))
                    return false;

                Print(() => new SeStringBuilder().AddText("已将模组 ").AddPurple(mod.Name, true).AddText(" 在合集 ")
                    .AddYellow(collection.Identity.Name, true)
                    .AddText(" 中设为继承。").BuiltString);
                return true;
        }

        return false;
    }

    private void Print(string text)
    {
        if (_config.Main.PrintSuccessfulCommandsToChat)
            _chat.Print(text);
    }

    private void Print(DefaultInterpolatedStringHandler text)
    {
        if (_config.Main.PrintSuccessfulCommandsToChat)
            _chat.Print(text.ToStringAndClear());
    }

    private void Print(Func<SeString> text)
    {
        if (_config.Main.PrintSuccessfulCommandsToChat)
            _chat.Print(text());
    }

    private bool HandleKnowledge(string _)
    {
        _knowledgeWindow.Toggle();
        return true;
    }
}

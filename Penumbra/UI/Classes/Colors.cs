using OtterGui.Custom;

namespace Penumbra.UI.Classes;

public enum ColorId
{
    EnabledMod,
    DisabledMod,
    UndefinedMod,
    InheritedMod,
    InheritedDisabledMod,
    NewMod,
    ConflictingMod,
    HandledConflictMod,
    FolderExpanded,
    FolderCollapsed,
    FolderLine,
    ItemId,
    IncreasedMetaValue,
    DecreasedMetaValue,
    SelectedCollection,
    RedundantAssignment,
    NoModsAssignment,
    NoAssignment,
    SelectorPriority,
    InGameHighlight,
    InGameHighlight2,
    ResTreeLocalPlayer,
    ResTreePlayer,
    ResTreeNetworked,
    ResTreeNonNetworked,
    PredefinedTagAdd,
    PredefinedTagRemove,
}

public static class Colors
{
    // These are written as 0xAABBGGRR.
    public const uint PressEnterWarningBg = 0xFF202080;
    public const uint RegexWarningBorder  = 0xFF0000B0;
    public const uint MetaInfoText        = 0xAAFFFFFF;
    public const uint RedTableBgTint      = 0x40000080;
    public const uint DiscordColor        = CustomGui.DiscordColor;
    public const uint FilterActive        = 0x807070FF;
    public const uint TutorialMarker      = 0xFF20FFFF;
    public const uint TutorialBorder      = 0xD00000FF;
    public const uint ReniColorButton     = CustomGui.ReniColorButton;
    public const uint ReniColorHovered    = CustomGui.ReniColorHovered;
    public const uint ReniColorActive     = CustomGui.ReniColorActive;

    public static (uint DefaultColor, string Name, string Description) Data(this ColorId color)
        => color switch
        {
            // @formatter:off
            ColorId.EnabledMod           => ( 0xFFFFFFFF, "启用的模组",                         "此模组在当前选中合集中已启用。" ),
            ColorId.DisabledMod          => ( 0xFF686880, "禁用的模组",                         "此模组在当前选中合集中已禁用。" ),
            ColorId.UndefinedMod         => ( 0xFF808080, "未设置的模组",                       "此模组未在当前选择的合集或其继承的任何合集中配置，所以间接地禁用了。" ),
            ColorId.InheritedMod         => ( 0xFFD0FFFF, "已在继承中启用的模组",               "此模组未在当前选中的合集中配置，但在选中合集继承的合集中已启用。" ),
            ColorId.InheritedDisabledMod => ( 0xFF688080, "已在继承中禁用的模组",               "此模组未在当前选中的合集中配置，但在选中合集继承的合集中已禁用。"),
            ColorId.NewMod               => ( 0xFF66DD66, "新模组",                             "此模组在此次Penumbra加载期间导入或创建，且尚未启用。" ),
            ColorId.ConflictingMod       => ( 0xFFAAAAFF, "未解决冲突的模组",                   "此模组已启用，但与另一个处于同一优先级的已启用模组发生冲突。" ),
            ColorId.HandledConflictMod   => ( 0xFFD0FFD0, "已解决冲突的模组",                   "此模组已启用，但与另一个处于不同优先级的已启用模组发生冲突。" ),
            ColorId.FolderExpanded       => ( 0xFFFFF0C0, "已展开的折叠组",                     "此折叠组已展开。" ),
            ColorId.FolderCollapsed      => ( 0xFFFFF0C0, "已最小化的折叠组",                   "此折叠组已最小化。" ),
            ColorId.FolderLine           => ( 0xFFFFF0C0, "展开的折叠组的结构线",               "表示哪些模组属于当前展开的折叠组的指示线。" ),
            ColorId.ItemId               => ( 0xFF808080, "物品ID",                             "更改项目右侧括号里显示的物品ID" ),
            ColorId.IncreasedMetaValue   => ( 0x80008000, "增加的元数据操作值",                 "表示元数据操作设置的浮点值相对原始数值增加，或元数据选项的启用状态（默认状态是禁用时）。"),
            ColorId.DecreasedMetaValue   => ( 0x80000080, "减少的元数据操作值",                 "表示元数据操作设置的浮点值相对原始数值减少，或元数据选项的禁用状态（默认状态是启用时）。"),
            ColorId.SelectedCollection   => ( 0x6069C056, "当前选中合集的分配对象",       		"当前选中并正在编辑的合集，其影响的分配对象的颜色。"),
            ColorId.RedundantAssignment  => ( 0x6050D0D0, "多余的合集分配",     				"当前无效的合集分配对象，因为它已经被其他包含它的对象涵盖了。"),
            ColorId.NoModsAssignment     => ( 0x50000080, "合集分配设置为'不使用模组'", 		"此合集分配被设置为完全不使用任何模组。"),
            ColorId.NoAssignment         => ( 0x00000000, "未分配合集的对象",    				"当前没有任何合集分配给该对象。"),
            ColorId.SelectorPriority     => ( 0xFF808080, "模组选择器优先级标识",               "在模组选择器里模组名称后显示优先级非0数字。"),
            ColorId.InGameHighlight      => ( 0xFFEBCF89, "游戏中高亮",                   		"为便于编辑而高亮显示的游戏中元素。"),
            ColorId.InGameHighlight2     => ( 0xFF446CC0, "游戏内高亮（次要）",       			"另一个为便于编辑而高亮显示的游戏中元素。"),
            ColorId.ResTreeLocalPlayer   => ( 0xFFFFE0A0, "画面角色：你",                       "在画面角色选项卡中，你和属于你的东西(坐骑，时尚配饰，宠物等等)。" ),
            ColorId.ResTreePlayer        => ( 0xFFC0FFC0, "画面角色：其他玩家",           		"在画面角色选项卡中，其他玩家和属于他们的东西" ),
            ColorId.ResTreeNetworked     => ( 0xFFFFFFFF, "画面角色：NPC（网络）",  			"在画面角色选项卡中，由游戏服务器处理的NPC。" ),
            ColorId.ResTreeNonNetworked  => ( 0xFFC0C0FF, "画面角色：NPC（本地）",      		"在画面角色选项卡中，由本地处理的NPC。" ),
            ColorId.PredefinedTagAdd     => ( 0xFF44AA44, "预定义标签：添加标签",               "当前MOD上不存在且可以添加的预定义标签。" ),
            ColorId.PredefinedTagRemove  => ( 0xFF2222AA, "预定义标签：删除标签",             	"当前MOD上已存在且可以删除的预定义标签。" ),
            _                            => throw new ArgumentOutOfRangeException( nameof( color ), color, null ),
            // @formatter:on
        };

    private static IReadOnlyDictionary<ColorId, uint> _colors = new Dictionary<ColorId, uint>();

    /// <summary> Obtain the configured value for a color. </summary>
    public static uint Value(this ColorId color)
        => _colors.TryGetValue(color, out var value) ? value : color.Data().DefaultColor;

    /// <summary> Set the configurable colors dictionary to a value. </summary>
    public static void SetColors(Configuration config)
        => _colors = config.Colors;
}

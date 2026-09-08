using ImSharp;
using Luna;
using static Penumbra.UI.Classes.ColorId;

namespace Penumbra.UI.Classes;

public readonly struct ColorIdData : IColorData<ColorId>
{
    private static readonly ColorData<ColorId>[] ColorData = CreateData();

    public static ColorData<ColorId> Data(in ColorId id)
    {
        if ((int)id < 0 || (int)id >= ColorData.Length)
            return ColorData<ColorId>.Invalid;

        return ColorData[(int)id];
    }

    public static StringU8 Parent { get; } = new("Penumbra"u8);

    private static readonly StringU8 OptionColorTooltip =
        new("用于模组选项的可选文本或标签的颜色。模组作者可以从这 8 种颜色中选择一种（或选择无以使用默认文本），但你可以自行决定实际显示的颜色。"u8);

    private static ColorData<ColorId>[] CreateData()
    {
        var modSelector  = "模组选择器"u8;
        var metadata     = "元数据"u8;
        var collections  = "合集"u8;
        var resourceTree = "屏幕角色"u8;
        var modSettings  = "模组设置"u8;

        var ret = new ColorData<ColorId>[ColorId.Values.Count];
        // Mod Selector
        ret[(int)EnabledMod] = new ColorData<ColorId>(ImGuiColor.Text, "启用的模组"u8,
            "此模组在当前选中合集中已启用。"u8, modSelector);
        ret[(int)DisabledMod] = new ColorData<ColorId>(0xFF686880, "禁用的模组"u8,
            "此模组在当前选中合集中已禁用。"u8, modSelector);
        ret[(int)UndefinedMod] = new ColorData<ColorId>(ImGuiColor.TextDisabled, "未设置的模组"u8,
            "此模组未在当前选择的合集或其继承的任何合集中配置，所以间接地禁用了。"u8,
            modSelector);
        ret[(int)InheritedMod] = new ColorData<ColorId>(0xFFD0FFFF, "已在继承中启用的模组"u8,
            "此模组未在当前选中的合集中配置，但在选中合集继承的合集中已启用。"u8, modSelector);
        ret[(int)InheritedDisabledMod] = new ColorData<ColorId>(0xFF688080, "已在继承中禁用的模组"u8,
            "此模组未在当前选中的合集中配置，但在选中合集继承的合集中已禁用。"u8, modSelector);
        ret[(int)NewMod] = new ColorData<ColorId>(DalamudColor.SuccessForeground, "新模组"u8,
            "此模组在此次Penumbra加载期间导入或创建，且尚未启用。"u8, modSelector);
        ret[(int)ConflictingMod] = new ColorData<ColorId>(DalamudColor.WarningBackground, "未解决冲突的模组"u8,
            "此模组已启用，但与另一个处于同一优先级的已启用模组发生冲突。"u8, modSelector);
        ret[(int)NewModTint] = new ColorData<ColorId>(DalamudColor.SuccessForeground, "新模组色调"u8,
            "一个在当前会话中刚刚导入或创建的模组，尚未启用。此颜色用作常规状态颜色的色调。"u8,
            modSelector);
        ret[(int)HandledConflictMod] = new ColorData<ColorId>(0xFFD0FFD0, "已解决冲突的模组"u8,
            "此模组已启用，但与另一个处于不同优先级的已启用模组发生冲突。"u8, modSelector);
        ret[(int)FolderExpanded] =
            new ColorData<ColorId>(FolderLine, "已展开的折叠组"u8, "此折叠组已展开。"u8, modSelector);
        ret[(int)FolderCollapsed] = new ColorData<ColorId>(FolderLine, "已最小化的折叠组"u8,
            "此折叠组已最小化。"u8, modSelector);
        ret[(int)FolderLine] = new ColorData<ColorId>(0xFFFFF0C0, "展开的折叠组的结构线"u8,
            "表示哪些模组属于当前展开的折叠组的奇数行指示线。"u8, modSelector);
        ret[(int)AlternatingFolderLine] = new ColorData<ColorId>(FolderLine, "展开的折叠组的结构线（交替）"u8,
            "表示哪些模组属于当前展开的折叠组的偶数行指示线。"u8, modSelector);
        ret[(int)SelectorPriority] = new ColorData<ColorId>(ImGuiColor.TextDisabled, "模组选择器优先级标识"u8,
            "在模组选择器里模组名称后显示优先级非0数字。"u8, modSelector);
        ret[(int)TemporaryModSettingsTint] = new ColorData<ColorId>(0x30FF0000, "具有临时设置的模组"u8,
            "一个具有临时设置的模组。此颜色用作常规状态颜色的色调。"u8, modSelector);
        ret[(int)NoTint] = new ColorData<ColorId>(Rgba32.Transparent, "无色调"u8,
            "所有模组的默认色调。"u8, modSelector);

        // Meta stuff
        ret[(int)ItemId] = new ColorData<ColorId>(ImGuiColor.TextDisabled, "物品ID"u8,
            "更改项目右侧括号里显示的物品ID"u8, metadata);
        ret[(int)IncreasedMetaValue] = new ColorData<ColorId>(DalamudColor.SuccessBackground, "增加的元数据操作值"u8,
            "表示元数据操作设置的浮点值相对原始数值增加，或元数据选项的启用状态（默认状态是禁用时）。"u8, metadata);
        ret[(int)DecreasedMetaValue] = new ColorData<ColorId>(DalamudColor.ErrorBackground, "减少的元数据操作值"u8,
            "表示元数据操作设置的浮点值相对原始数值减少，或元数据选项的禁用状态（默认状态是启用时）。"u8, metadata);
        ret[(int)PredefinedTagAdd] = new ColorData<ColorId>(DalamudColor.SuccessBackground, "预定义标签：添加标签"u8,
            "当前MOD上不存在且可以添加的预定义标签。"u8, metadata);
        ret[(int)PredefinedTagRemove] = new ColorData<ColorId>(DalamudColor.ErrorBackground, "预定义标签：删除标签"u8,
            "当前MOD上已存在且可以删除的预定义标签。"u8, metadata);
        ret[(int)ChangedItemPreferenceStar] = new ColorData<ColorId>(0x30FFFFFF, "首选更改项目星标"u8,
            "模组面板的更改项目标签页中，用于优先处理特定项目的星标按钮颜色。"u8, metadata);
        ret[(int)InGameHighlight] = new ColorData<ColorId>(0xFFEBCF89, "游戏中高亮"u8,
            "为便于编辑而高亮显示的游戏中元素。"u8, metadata);
        ret[(int)InGameHighlight2] = new ColorData<ColorId>(0xFF446CC0, "游戏内高亮（次要）"u8,
            "另一个为便于编辑而高亮显示的游戏中元素。"u8, metadata);
        ret[(int)ModSpecificPreset] = new ColorData<ColorId>(DalamudColor.HealerGreen, "模组专属设置预设"u8,
            "在预设下拉菜单中，此模组专属设置预设（相对于通用设置预设）的颜色。"u8, metadata);

        // Collections
        ret[(int)SelectedCollection] = new ColorData<ColorId>(0x6069C056, "当前选中合集的分配对象"u8,
            "当前选中并正在编辑的合集，其影响的分配对象的颜色。"u8, collections);
        ret[(int)RedundantAssignment] = new ColorData<ColorId>(DalamudColor.AttentionBackground, "多余的合集分配"u8,
            "当前无效的合集分配对象，因为它已经被其他包含它的对象涵盖了。"u8, collections);
        ret[(int)NoModsAssignment] = new ColorData<ColorId>(0x50000080, "合集分配设置为'不使用模组'"u8,
            "此合集分配被设置为完全不使用任何模组。"u8, collections);
        ret[(int)NoAssignment] = new ColorData<ColorId>(Rgba32.Transparent, "未分配合集的对象"u8,
            "当前没有任何合集分配给该对象。"u8, collections);

        // Resource Tree
        ret[(int)ResTreeLocalPlayer] = new ColorData<ColorId>(0xFFFFE0A0, "画面角色：你"u8,
            "在画面角色选项卡中，你和属于你的东西(坐骑，时尚配饰，宠物等等)。"u8, resourceTree);
        ret[(int)ResTreePlayer] = new ColorData<ColorId>(0xFFC0FFC0, "画面角色：其他玩家"u8,
            "在画面角色选项卡中，其他玩家和属于他们的东西"u8, resourceTree);
        ret[(int)ResTreeNetworked] = new ColorData<ColorId>(ImGuiColor.Text, "画面角色：NPC（网络）"u8,
            "在画面角色选项卡中，由游戏服务器处理的NPC。"u8, resourceTree);
        ret[(int)ResTreeNonNetworked] = new ColorData<ColorId>(0xFFC0C0FF, "画面角色：NPC（本地）"u8,
            "在画面角色选项卡中，由本地处理的NPC。"u8, resourceTree);

        // Mod Settings
        ret[(int)OptionColor1] = new ColorData<ColorId>(0xFFF8CD8E, "模组选项可选颜色 #1"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor2] = new ColorData<ColorId>(0xFFAAD898, "模组选项可选颜色 #2"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor3] = new ColorData<ColorId>(0xFF8AD1E6, "模组选项可选颜色 #3"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor4] = new ColorData<ColorId>(0xFF6B8CD9, "模组选项可选颜色 #4"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor5] = new ColorData<ColorId>(0xFFA38FD9, "模组选项可选颜色 #5"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor6] = new ColorData<ColorId>(0xFFDB9DB3, "模组选项可选颜色 #6"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor7] = new ColorData<ColorId>(0xFF6A5CC7, "模组选项可选颜色 #7"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionColor8] = new ColorData<ColorId>(0xFF6BB5A6, "模组选项可选颜色 #8"u8, OptionColorTooltip, modSettings);
        ret[(int)OptionTreeLine] = new ColorData<ColorId>(ImGuiColor.Separator, "选项组依赖树连线"u8,
            "模组设置面板中连接选项组与节点的连线颜色。"u8, modSettings);
        ret[(int)GroupLabelBackground] = new ColorData<ColorId>(Rgba32.Transparent, "选项组标签背景（非交互）"u8,
            "选项组标签不可折叠时的背景颜色。"u8, modSettings);
        ret[(int)GroupLabelBorder] = new ColorData<ColorId>(OptionTreeLine, "选项组标签边框（非交互）"u8,
            "选项组标签不可折叠时的边框颜色。"u8, modSettings);
        ret[(int)GroupLabelText] = new ColorData<ColorId>(ImGuiColor.Text, "选项组标签文本（非交互）"u8,
            "选项组标签不可折叠时的文本颜色。"u8, modSettings);
        ret[(int)GroupLabelBackgroundExpanded] = new ColorData<ColorId>(ImGuiColor.Header, "选项组标签背景（展开）"u8,
            "选项组可折叠且当前展开时，组标签的背景颜色。"u8, modSettings);
        ret[(int)GroupLabelBorderExpanded] = new ColorData<ColorId>(OptionTreeLine, "选项组标签边框（展开）"u8,
            "选项组可折叠且当前展开时，组标签的边框颜色。"u8, modSettings);
        ret[(int)GroupLabelTextExpanded] = new ColorData<ColorId>(ImGuiColor.Text, "选项组标签（展开）"u8,
            "选项组展开时，组标签文本的颜色。"u8, modSettings);
        ret[(int)GroupLabelBackgroundCollapsed] = new ColorData<ColorId>(ImGuiColor.Header, "选项组标签背景（折叠）"u8,
            "选项组当前折叠时，组标签的背景颜色。"u8, modSettings);
        ret[(int)GroupLabelBorderCollapsed] = new ColorData<ColorId>(OptionTreeLine, "选项组标签边框（折叠）"u8,
            "选项组当前折叠时，组标签的边框颜色。"u8, modSettings);
        ret[(int)GroupLabelTextCollapsed] = new ColorData<ColorId>(ImGuiColor.Text, "选项组标签（折叠）"u8,
            "选项组折叠时，组标签文本的颜色。"u8, modSettings);
        ret[(int)OptionBorder] = new ColorData<ColorId>(OptionTreeLine, "选项复选框/单选按钮/下拉框边框"u8,
            "选项复选框、单选按钮或单选项组下拉框周围边框的颜色。"u8, modSettings);
        ret[(int)HiddenOptionIndicator] = new ColorData<ColorId>(0x00FFFFFF, "隐藏选项指示"u8,
            "当组或选项存在未显示的更多选项或子组时，指示线的颜色。"u8, modSettings);

        foreach (var data in ret)
        {
            if (data.Default.Value is 0)
                throw new SystemException("A color ID has no data assigned.");
        }

        return ret;
    }

    /// <summary> The old hardcoded default values used for migration. </summary>
    internal static Rgba32 OldDefault(ColorId id)
        => id switch
        {
            EnabledMod                    => 0xFFFFFFFF,
            DisabledMod                   => 0xFF686880,
            UndefinedMod                  => 0xFF808080,
            InheritedMod                  => 0xFFD0FFFF,
            InheritedDisabledMod          => 0xFF688080,
            NewMod                        => 0xFF66DD66,
            ConflictingMod                => 0xFFAAAAFF,
            HandledConflictMod            => 0xFFD0FFD0,
            FolderExpanded                => 0xFFFFF0C0,
            FolderCollapsed               => 0xFFFFF0C0,
            FolderLine                    => 0xFFFFF0C0,
            ItemId                        => 0xFF808080,
            IncreasedMetaValue            => 0x80008000,
            DecreasedMetaValue            => 0x80000080,
            SelectedCollection            => 0x6069C056,
            RedundantAssignment           => 0x6050D0D0,
            NoModsAssignment              => 0x50000080,
            NoAssignment                  => 0x00000000,
            SelectorPriority              => 0xFF808080,
            InGameHighlight               => 0xFFEBCF89,
            InGameHighlight2              => 0xFF446CC0,
            ResTreeLocalPlayer            => 0xFFFFE0A0,
            ResTreePlayer                 => 0xFFC0FFC0,
            ResTreeNetworked              => 0xFFFFFFFF,
            ResTreeNonNetworked           => 0xFFC0C0FF,
            PredefinedTagAdd              => 0xFF44AA44,
            PredefinedTagRemove           => 0xFF2222AA,
            TemporaryModSettingsTint      => 0x30FF0000,
            NewModTint                    => 0x8000FF00,
            NoTint                        => 0x00000000,
            ChangedItemPreferenceStar     => 0x30FFFFFF,
            OptionColor1                  => 0xFFF8CD8E,
            OptionColor2                  => 0xFFAAD898,
            OptionColor3                  => 0xFF8AD1E6,
            OptionColor4                  => 0xFF6B8CD9,
            OptionColor5                  => 0xFFA38FD9,
            OptionColor6                  => 0xFFDB9DB3,
            OptionColor7                  => 0xFF6A5CC7,
            OptionColor8                  => 0xFF6BB5A6,
            OptionTreeLine                => 0x80FFF0C0,
            GroupLabelBackground          => 0x8A4A4A4A,
            GroupLabelBorder              => 0x80FFF0C0,
            GroupLabelText                => 0xFFFFFFFF,
            GroupLabelBackgroundExpanded  => 0x4F969696,
            GroupLabelBorderExpanded      => 0x80FFF0C0,
            GroupLabelTextExpanded        => 0xFFFFFFFF,
            GroupLabelBackgroundCollapsed => 0x4F969696,
            GroupLabelBorderCollapsed     => 0x80FFF0C0,
            GroupLabelTextCollapsed       => 0xFFFFFFFF,
            OptionBorder                  => 0x80FFF0C0,
            _                             => 0,
        };
}

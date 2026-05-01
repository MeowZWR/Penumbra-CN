using ImSharp;

namespace Penumbra.UI.Classes;

/// <summary> List of currently available tutorials. </summary>
public enum BasicTutorialSteps
{
    GeneralTooltips,
    ModDirectory,
    EnableMods,
    Deprecated1,
    GeneralSettings,
    Collections,
    EditingCollections,
    CurrentCollection,
    SimpleAssignments,
    IndividualAssignments,
    GroupAssignments,
    CollectionDetails,
    Incognito,
    Deprecated2,
    Mods,
    ModImport,
    AdvancedHelp,
    ModFilters,
    CollectionSelectors,
    Redrawing,
    EnablingMods,
    Priority,
    ModOptions,
    Fin,
    Deprecated3,
    Faq1,
    Faq2,
    Favorites,
    Tags,
}

/// <summary> Service for the in-game tutorial. </summary>
public class TutorialService(EphemeralConfig config) : Luna.IUiService
{
    private readonly Luna.Tutorial _tutorial = new Luna.Tutorial
        {
            BorderColor    = new Rgba32(Colors.TutorialBorder).ToVector(),
            HighlightColor = new Rgba32(Colors.TutorialMarker).ToVector(),
            PopupLabel     = new StringU8("设置教程"u8),
        }
        .Register("小贴士"u8, "此符号会为你提供旁边选项的更多信息。\n\n"u8
          + "当你不确定某项设置的作用或不知道该如何操作时，将鼠标悬停在该符号上。"u8)
        .Register("初始设置，步骤1：模组目录"u8,
            "首先要设置模组目录，也就是解压并存放模组文件的位置。\n\n"u8
          + "模组目录路径应尽量简短，例如「C:\\FFXIVMods」，并放在你读写最快的磁盘上，有助于提升性能。\n\n"u8
          + "该文件夹应为空文件夹，且不要有其他程序向其中写入数据。"u8)
        .Register("初始设置，步骤2：启用模组"u8, "若尚未启用模组，请记得勾选启用，否则模组不会生效。"u8)
        .Deprecated()
        .Register("常规设置"u8, "开始深入使用前，建议先浏览这些设置，往往很有帮助。\n\n"u8
          + "若暂时看不懂其中某些项，可以稍后再回来查看。"u8)
        .Register("初始设置，步骤3：合集"u8, "合集是你已安装模组的各项设置的集合。\n\n"u8
          + "接下来请前往此处。\n\n"u8
          + "在完成根目录（模组目录）设置后，请打开本页以继续教程。"u8)
        .Register("初始设置，步骤4：管理合集"u8,
            "左侧是合集选择器。在此可以新建合集（空白或复制现有合集），也可以删除不再需要的合集。\n"u8
          + "始终会存在一个名为「Default」的合集，且无法删除。"u8)
        .Register("初始设置，步骤5：选中的合集"u8,
            "选中的合集即选择器中高亮显示的那一项，也就是你当前正在查看和编辑的合集。\n之后在「模组」页对模组设置所做的更改，都会作用到该合集上。\n"u8
          + "初次设置时，一般已选中名为「Default」的合集；若只做简单配置，此处通常无需额外操作。\n\n"u8)
        .Register("初始设置，步骤6：简单分配"u8,
            "除作为设置的集合外，还可以将合集分配到不同用途，从而让不同角色应用不同的模组配置。\n"u8
          + "「简单分配」面板列出了对多数人足够用的分配方式，并附有说明。\n"u8
          + "初次使用时你会看到「Default」合集已分配给 Default 与 Interface。\n"u8
          + "也可以点击功能按钮，将某项用途设为「不使用任何模组」以替代指定合集。"u8)
        .Register("独立分配"u8,
            "在「独立分配」面板中，你可以为特定角色或怪物手动指定合集，而不仅限于自己或当前可选中的目标。"u8)
        .Register("组分配"u8,
            "在「组分配」面板中，可以按种族、年龄等条件为更细分的角色群体创建分配。"u8)
        .Register("合集详情"u8,
            "在「合集详情」面板中，可查看当前选中合集的占用概况，清理过时的模组设置，并配置继承关系。\n"u8
          + "继承可使一个合集沿用另一个合集的设置，只要本合集未单独配置该模组即可。"u8)
        .Register("匿名模式"u8,
            "此按钮可切换匿名模式：所有合集名称会缩短为两个字母加数字，\n"u8
          + "独立分配中显示的角色名会改为缩写与世界名，便于分享截图。\n"u8
          + "使用 Penumbra 时，强烈建议不要在公开截图中暴露完整角色名。"u8)
        .Deprecated()
        .Register("初始设置，步骤7：模组"u8, "最后一站是「模组」页，可在此导入并配置模组。\n\n"u8
          + "请在确认选中的合集与基础合集均符合你的预期后，再前往该页。"u8)
        .Register("初始设置，步骤8：导入模组"u8,
            "点击此按钮打开文件选择器，选择 TTMP 模组文件，可一次多选批量导入。\n\n"u8
          + "不建议导入包含大量 TexTools 模组的大型整合包，更推荐分别导入各模组，否则会错失许多 Penumbra 独有功能。\n\n"u8
          + "在高级编辑中可导入纹身等用的原始纹理类模组，该功能可用但仍在完善中。"u8)
        .Register("进阶帮助"u8, "点击此按钮可查看模组选择器中可用操作的详细说明。\n\n"u8
          + "请先导入并选中一个模组，再继续下一步。"u8)
        .Register("模组筛选器"u8, "可在此按名称、作者、修改项或多种属性筛选模组列表。"u8)
        .Register("合集选择器"u8, "这一行提供快速切换「选中的合集」的快捷方式。\n\n"u8
          + "第一个按钮设为你的基础合集（若已设置）。\n\n"u8
          + "第二个按钮设为当前所选模组设置所继承来源的合集（若有）。\n\n"u8
          + "第三个为常规合集下拉，可在全部合集间任选其一。"u8)
        .Register("重绘"u8,
            "修改模组配置后，更改不会立刻生效，需要强制游戏重新加载相关文件（若无法重载则请重启游戏）。\n\n"u8
          + "为此 Penumbra 提供这些按钮，以及「/penumbra redraw」命令，可一次性重绘所有角色；也可使用帮助图标中说明的修饰参数。\n\n"u8
          + "也可将上述斜杠命令写入宏中使用，例如「/penumbra redraw self」仅重绘自己。"u8)
        .Register("初始设置，步骤9：启用模组"u8,
            "在此启用模组。已禁用的模组不会对当前合集中的任何对象生效。\n\n"u8
          + "模组在每个合集中可设为启用、禁用或未配置；未配置时将按继承规则处理。"u8)
        .Register("初始设置，步骤10：优先级"u8,
            "同一合集中若两个已启用的模组修改了相同文件，即产生冲突。\n\n"u8
          + "可通过优先级解决：数字更大的模组，其文件在冲突项中优先生效。\n\n"u8
          + "只要优先级设置正确，冲突本身并不可怕；优先级也可以为负数。"u8)
        .Register("模组选项"u8, "许多模组自带可选项，可在此进行选择。\n\n"u8
          + "下拉类选项通常互斥；带勾选框的选项则可各自独立开关。"u8)
        .Register("初始设置 - 结束语"u8, "至此你已具备让 Penumbra 正常运行与使用的基本知识。\n\n"u8
          + "若仍有疑问或需要高级功能方面的帮助，请参阅设置页中链接的指南。"u8)
        .Deprecated()
        .Register("FAQ 1"u8,
            "不建议同时使用 TexTools 与 Penumbra。若 TexTools 损坏了游戏索引，Penumbra 可能无法正常工作。"u8)
        .Register("FAQ 2"u8, "Penumbra 可以更改模组使用的皮肤材质，相关选项在高级编辑中。"u8)
        .Register("收藏"u8,
            "可通过此按钮将模组标为收藏，并在模组选择器中按收藏筛选。收藏保存在本地，不在模组文件内，且与各合集相互独立。"u8)
        .Register("标签"u8,
            "模组现支持两类标签：\n\n- 本地标签：由你自行设置，仅保存在本机，不会写入模组目录。\n- 模组标签：保存在模组元数据中，一般由作者设定并随模组分发，仅在「编辑模组」页中修改。\n\n若某标签同时存在于模组标签中，则会覆盖同名的本地标签。\n\n在模组筛选器中可使用「t:文本」按标签筛选。"u8)
        .EnsureSize(BasicTutorialSteps.Values.Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OpenTutorial(BasicTutorialSteps step)
        => _tutorial.Open((int)step, config.TutorialStep, v =>
        {
            config.TutorialStep = v;
            config.Save();
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipTutorial(BasicTutorialSteps step)
        => _tutorial.Skip((int)step, config.TutorialStep, v =>
        {
            config.TutorialStep = v;
            config.Save();
        });

    /// <summary> Update the current tutorial step if tutorials have changed since last update. </summary>
    public void UpdateTutorialStep()
    {
        var tutorial = _tutorial.CurrentEnabledId(config.TutorialStep);
        if (tutorial != config.TutorialStep)
        {
            config.TutorialStep = tutorial;
            config.Save();
        }
    }
}

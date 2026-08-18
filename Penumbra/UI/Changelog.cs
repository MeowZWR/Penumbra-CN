using Luna;

namespace Penumbra.UI;

public class PenumbraChangelog : IUiService
{
    public const int LastChangelogVersion = 0;

    private readonly Configuration _config;
    public readonly  Changelog     Changelog;

    public PenumbraChangelog(Configuration config)
    {
        _config   = config;
        Changelog = new Changelog("Penumbra 更新日志", ConfigData, Save);

        Add5_7_0(Changelog);
        Add5_7_1(Changelog);
        Add5_8_0(Changelog);
        Add5_8_7(Changelog);
        Add5_9_0(Changelog);
        Add5_10_0(Changelog);
        Add5_11_0(Changelog);
        Add5_11_1(Changelog);
        Add6_0_0(Changelog);
        Add6_0_2(Changelog);
        Add6_0_5(Changelog);
        Add6_1_0(Changelog);
        Add6_1_1(Changelog);
        Add6_2_0(Changelog);
        Add6_3_0(Changelog);
        Add6_4_0(Changelog);
        Add6_5_0(Changelog);
        Add6_5_2(Changelog);
        Add6_6_0(Changelog);
        Add6_6_1(Changelog);
        Add7_0_0(Changelog);
        Add7_0_1(Changelog);
        Add7_0_4(Changelog);
        Add7_1_0(Changelog);
        Add7_1_2(Changelog);
        Add7_2_0(Changelog);
        Add7_3_0(Changelog);
        Add8_0_0(Changelog);
        Add8_1_1(Changelog);
        Add8_1_2(Changelog);
        Add8_2_0(Changelog);
        Add8_3_0(Changelog);
        Add1_0_0_0(Changelog);
        AddDummy(Changelog);
        AddDummy(Changelog);
        Add1_1_0_0(Changelog);
        Add1_1_1_0(Changelog);
        Add1_2_1_0(Changelog);
        Add1_3_0_0(Changelog);
        Add1_3_1_0(Changelog);
        Add1_3_2_0(Changelog);
        Add1_3_3_0(Changelog);
        Add1_3_4_0(Changelog);
        Add1_3_5_0(Changelog);
        Add1_3_6_0(Changelog);
        Add1_3_6_4(Changelog);
        Add1_4_0_0(Changelog);
        Add1_5_0_0(Changelog);
        Add1_5_1_0(Changelog);
        AddDummy(Changelog);
        Add1_6_0_0(Changelog);
        Add1_6_1_0(Changelog);
        Add1_7_0_0(Changelog);
    }

    #region Changelogs

    private static void Add1_7_0_0(Changelog log)
        => log.NextVersion("版本 1.7.0.0"u8)
            .RegisterImportant("更新到此版本时，所有已安装的模组将迁移到新的元数据版本。"u8)
            .RegisterEntry("本版本移除了选项组与默认选项的独立 JSON 文件，改为再次将全部信息存入 meta.json。"u8, 1)
            .RegisterEntry("旧文件应会被移至备份；迁移前还会创建一份包含所有模组 JSON 的永久归档，以防出错时需要恢复。"u8, 1)
            .RegisterHighlight("所有模组将获得稳定的 GUID 标识符，每个选项组与选项也会各有一个 GUID。"u8)
            .RegisterEntry("模组 GUID 目前尚未使用；待作者为其模组建立稳定标识后，日后可用于验证依赖等用途。"u8, 1)
            .RegisterEntry("选项 GUID 已在单个模组内用于多项新功能。"u8, 1)
            .RegisterHighlight("模组作者获得多项非常灵活的新选项："u8)
            .RegisterEntry("选项组现会尊重从 TexTools 导入的数值型「页面」（Page）字段（当然也可在 Penumbra 中设置）。"u8, 1)
            .RegisterEntry("若存在多个含有实际可见选项的页面，这些页面会在设置标签页中以子标签形式显示。"u8, 2)
            .RegisterEntry("默认情况下，页面命名为「页面 #」，但可在组编辑中为数值页面指定自定义名称。"u8, 2)
            .RegisterEntry("可在设置中选择将页面显示为标签栏，或以折叠标题分段显示。"u8, 2)
            .RegisterEntry("选项组现可挂靠到其他组或选项下，从而显示在对应组或选项的正下方。"u8, 1)
            .RegisterEntry("使用挂靠时有多种布局选项——可选择是否缩进该组，以及是否显示组标题。"u8, 2)
            .RegisterEntry("凡有选项或其他组挂靠其下的组标题现均可折叠，且可将组设为默认折叠。"u8, 1)
            .RegisterHighlight("整个选项组以及单个选项均可基于本模组内的设置指定任意条件。"u8, 1)
            .RegisterEntry("若条件未满足，该选项或组将不会生效（无论其设置如何），并根据设置选择完全不显示，或以禁用状态显示。"u8, 2)
            .RegisterEntry("此外，选项可被指定 8 种颜色之一（由用户定义，而非作者定义），并可设置为在其后添加分隔线。"u8, 1)
            .RegisterHighlight("为支持上述新功能，设置标签页 UI 已大幅改动。欢迎反馈如何进一步改进显示效果。"u8)
            .RegisterEntry("随之而来的变化是：在不考虑挂靠或页面的情况下，选项组现在始终按正确顺序显示，而不再先按显示类型筛选。"u8, 1)
            .RegisterHighlight("任务生成的敌对 NPC 等不再受「基于所有者使用合集」设置影响，除非另行启用「包含敌对所有者角色」。"u8)
            .RegisterHighlight("用于自动导入模组的文件监视器现可设置为窥探压缩包内部，识别其中的模组压缩包并安装打包的模组（感谢 Stoia 与 Ny！）。"u8)
            .RegisterEntry("「启用/禁用/继承子折叠组」右键菜单按钮现已加入防误触。"u8)
            .RegisterEntry("编辑选项描述的弹窗现在可以调整大小。"u8)
            .RegisterHighlight(
                "Penumbra 中可配置的颜色现在可以引用其他颜色（如 ImGui 或 Dalamud 颜色，或其他 Penumbra 颜色）。部分颜色在默认选项中即采用此方式。"u8)
            .RegisterEntry("颜色配置应会自动迁移：仍使用旧默认值的项目会迁移到新默认值，并写入另一个文件。"u8, 1)
            .RegisterEntry("屏幕内标签页现在会在顶级条目组之后显示分隔线，大致按装备栏位划分。"u8)
            .RegisterEntry("支持信息中新增了若干插件，以及所有调用特定 IPC 函数的插件。"u8)
            .RegisterEntry("应普遍需求，高级编辑的模型标签页现改用从 0 开始的索引，而不再从 1 开始。"u8)
            .RegisterEntry("使用文件系统监视器时，并发的模组安装通知数量限制为 3 条。"u8)
            .RegisterEntry("多项反序列化与序列化函数已改用 System.Text.Json，速度显著提升。"u8)
            .RegisterEntry("在多种情况下对语法损坏的 JSON 文件采用了新的恢复策略（感谢 Ny！）。"u8)
            .RegisterEntry("「Failed to Load Resource」日志警告将不再因 EasyEyes 故意使用的失败路径而触发。"u8)
            .RegisterEntry("针对导入时涉及文件夹外文件的部分漏洞增加了防护。"u8)
            .RegisterEntry("修复了更改选项时临时设置失效的问题。"u8)
            .RegisterEntry("修复了自动备份功能的若干问题。"u8)
            .RegisterEntry("修复了合集继承显示与更新的问题。"u8)
            .RegisterEntry("修复了文件冗余保存的若干问题。"u8)
            .RegisterEntry("修复了模型导出时材质后缀的问题。"u8)
            .RegisterEntry("修复了通过资源树解析饰品皮肤材质时的崩溃。"u8)
            .RegisterEntry("修复了模组选择器中文件夹树连线的问题。"u8)
            .RegisterEntry("修复了编辑颜色表时数值未钳制的问题。"u8)
            .RegisterEntry("修复了「移至快速文件夹」按钮选中状态的问题。"u8)
            .RegisterHighlight("新增用于管理无法正确加载的损坏 mod 的管理标签页。(1.6.1.9)"u8)
            .RegisterEntry("修复了模组压缩包内文件夹分隔符的问题。(1.6.1.9)"u8)
            .RegisterEntry("特定动画现会正确关联到角色。(1.6.1.7)"u8)
            .RegisterEntry("仅大小写变化时，文件夹与 mod 现也可重命名。(1.6.1.6)"u8)
            .RegisterEntry("模组不会在短时间内被意外重复导入。(1.6.1.6)"u8)
            .RegisterEntry("备份文件现已从 PMP 导出中排除。(1.6.1.6)"u8);

    private static void Add1_6_1_0(Changelog log)
        => log.NextVersion("版本 1.6.1.0"u8)
            .RegisterHighlight("Penumbra 已适配游戏版本 7.50 与 Dalamud API 15。"u8)
            .RegisterEntry("在找到更稳妥的实现方式之前，HDR 启动选项已暂时关闭。"u8, 1)
            .RegisterHighlight("模组文件系统新增功能："u8)
            .RegisterEntry("现可在模组文件系统中添加分隔线；右键分隔线可设置颜色、与文件夹或文件一同参与排序时的归类方式，以及具体排序规则。"u8,1)
            .RegisterEntry("微调了模组文件系统中文件夹的右键菜单：在「编辑文件夹」子菜单中，可为指定文件夹单独设置颜色与排序模式。"u8,1)
            .RegisterEntry("高级材质编辑中新增一项默认关闭的设置，可显示理论上支持的全部四个染色通道。多出的两个通道目前仅在本材质编辑器中有用，日后可能会在 Glamourer 中增加支持（感谢 Ny！）。"u8)
            .RegisterEntry("导入压缩包时，无效字符改为用下划线替换，而不再直接删除。"u8)
            .RegisterEntry("新版元数据编辑区域的标签页样式更易辨认。"u8)
            .RegisterEntry("修正了部分颠倒的折叠箭头图标。"u8)
            .RegisterEntry("修正了部分元数据编辑界面的标签文案。"u8)
            .RegisterEntry("将与「禁止文件」相关的表述统一更名为「保留文件」，以使其听起来不那么吓人。"u8);

    private static void Add1_6_0_0(Changelog log)
        => log.NextVersion("版本 1.6.0.0"u8)
            .RegisterImportant(
                "本次更新更换了整套 UI 后端 —— 这是过去数月一直在做的工作。\n希望不会给用户带来明显差异，但由于所有涉及 ImGui 的代码都已修改，可能会出现新的或旧有的问题。\n如果发现配置丢失，您可以在 %AppData%\\XIVLauncherCN\\backups\\Penumbra 中找到备份设置进行恢复。"u8)
            .RegisterEntry("Penumbra 现在会记住 mod 的完整选择状态以及哪些文件夹已展开、哪些未展开。"u8, 1)
            .RegisterEntry(
                "Penumbra 现在会记住大部分您输入的筛选状态——若希望筛选在每次打开时重置，也可在设置中关闭此功能。"u8,
                1)
            .RegisterEntry(
                "将 mod 拖入游戏进行安装时，不再需要拖到 mod 选择器或保持 Penumbra 窗口打开，只需拖入游戏窗口即可。"u8,
                1)
            .RegisterEntry("多项 UI 控件已更加精确、一致。"u8, 1)
            .RegisterEntry(
                "mod 导入弹窗已改为 Dalamud 通知形式；仅在点击通知查看详情时才会打开弹窗（感谢 Ny！）。"u8)
            .RegisterEntry(
                "设置中可让通知保持显示直至手动关闭，或始终像以往一样打开详细弹窗。"u8, 1)
            .RegisterEntry("通知会汇总多次导入活动，而不会弹出多个弹窗。"u8,       1)
            .RegisterHighlight("现已支持同时打开多个高级编辑窗口（感谢 Ny！）。"u8)
            .RegisterEntry("默认情况下，每个打开的标签页都会固定到为其打开时所对应的 mod。"u8, 1)
            .RegisterEntry(
                "您可以取消固定窗口，或选择默认不固定窗口的标签页。此时窗口会像以往一样跟随当前选中的 mod，但将无法再打开更多窗口。"u8,
                1)
            .RegisterHighlight("新增「管理」标签页。"u8)
            .RegisterEntry("管理标签页用于帮助用户清理未使用的 mod。"u8, 1)
            .RegisterEntry(
                "未使用 mod 面板可筛选未在任何合集中启用的 mod，并按最近配置变更时间排序（现已记录该时间，此前未记录）。"u8,
                1)
            .RegisterEntry(
                "新增 IPC，允许其他插件在查询未启用 mod 时添加备注或将它们标为活跃。"u8, 1)
            .RegisterEntry(
                "「禁止文件」标签页可用于检查并移除因稳定性问题而不再被 Penumbra 允许的文件重定向。若启用这些文件会触发通知，且可能需要更新 mod 以保留功能。"u8,
                1)
            .RegisterEntry("其他管理标签页可用于清理和优化 mod，但目前仍为开发中。"u8, 1)
            .RegisterEntry("重复模组面板会检查是否存在多个同名模组。"u8,                                  1)
            .RegisterEntry("清理功能已从高级设置移至通用清理面板。"u8,                 1)
            .RegisterEntry("模组编辑标签页中新增了可快速重排选项组顺序的模式。"u8)
            .RegisterEntry("纹理编辑标签页新增了多项模式与功能，并有所改进（感谢 Ny！）。"u8)
            .RegisterEntry("挂载点 BLD 和 BL2 已识别为双剑（Twinblades）。"u8)
            .RegisterEntry("使用物品交换创建 mod 时会尝试保留相关的 ATR 与 SHP 元数据编辑。"u8)
            .RegisterEntry("多设计（Multi-design）操作现在会尊重临时设置模式。"u8)
            .RegisterEntry("修复了材质与 avfx 文件子文件资源重定向的多项线程问题。"u8)
            .RegisterEntry(
                "更新 Penumbra 时会显示通知，提示用户若遇到问题可先重启游戏再反馈。"u8)
            .RegisterEntry("在高级编辑标签页的文件组合框中增加了部分右键菜单选项。"u8)
            .RegisterEntry("将本地模组数据从「每个模组一个文件」改为合并为单一文件，并普遍改善启动速度。"u8)
            .RegisterEntry("改进了对无效 IMC 编辑的处理。"u8)
            .RegisterEntry("修复了 Penumbra 崩溃处理器的若干问题，使其更加健壮。"u8)
            .RegisterEntry("修复了过场动画或 NPC 道具上的挂载点问题。"u8)
            .RegisterEntry("修复了交换跟宠时的相关问题。"u8)
            .RegisterHighlight("纹理压缩 IPC 增加对其他块压缩类型的支持 (1.5.1.12)。"u8)
            .RegisterHighlight(
                "新增 IPC，在 Penumbra 设置标签页中展示其他插件的 Penumbra 相关设置（感谢 Ny！）(1.5.1.9)。"u8)
            .RegisterEntry("修复材料高级编辑标签页中的多处问题（感谢 Ny！）(1.5.1.9)。"u8)
            .RegisterEntry("新增 IPC，可重绘指定合集的成员（感谢 Karou！）(1.5.1.8)。"u8)
            .RegisterEntry("修复其他插件通过 API 设置过场索引时的问题 (1.5.1.7)。"u8)
            .RegisterHighlight(
                "新增文件监视器，在文件保存到已配置目录时自动尝试安装 mod（感谢 Stoia！）(1.5.1.7)。"u8)
            .RegisterEntry(
                "默认名称的合集现不可重命名或删除，并在选择器中排在顶部 (1.5.1.3)。"u8)
            .RegisterEntry("屏幕内标签页中不再对外部路径进行脱敏 (1.5.1.2)。"u8)
            .RegisterEntry("增加对云同步目录的检测 (1.5.1.2)。"u8)
            .RegisterEntry("保存 PCP 文件时增加更多选项 (1.5.1.1)。"u8)
            .RegisterEntry("当重定向文件的扩展名不匹配时增加警告 (1.5.1.1)。"u8);

    private static void Add1_5_1_0(Changelog log)
        => log.NextVersion("版本 1.5.1.0"u8)
            .RegisterHighlight("在屏幕内标签页中新增将角色当前数据导出为 .pcp 模组包的选项。"u8)
            .RegisterEntry("其他插件可以接入此功能，打包并解析各自的数据。"u8, 1)
            .RegisterEntry("安装 .pcp 模组包时，可以为当初创建该包所对应的角色创建并分配合集。"u8, 1)
            .RegisterEntry("这基本上提供了一种更简便的手动同步其他玩家的方式，但不包含任何自动化。"u8, 1)
            .RegisterEntry("设置中可精细控制安装 PCP 时的行为，并提供按钮用于清理任何由 PCP 创建的数据。"u8, 1)
            .RegisterEntry("在屏幕内标签页中，于游戏完整性损坏时新增警告提示。"u8)
            .RegisterEntry("屏幕内标签页新增 .kdb 文件及相关功能（感谢 Ny！）。"u8)
            .RegisterEntry("创建临时合集现在需要传入身份标识。"u8)
            .RegisterEntry("新增通过添加特定属性，来更改使用 stockings 着色器的模型中皮肤材质后缀的选项（感谢 Ny！）。"u8)
            .RegisterEntry("多模组选择中新增预定义标签工具。"u8)
            .RegisterEntry("修复了角色登录时若未分配模组则无法自动选择合集的问题。"u8)
            .RegisterImportant("修复了新的变形器数据相关问题，使不含该数据的模组变形器也能隐式正常工作。仍建议进行更新 (1.5.0.5)。"u8)
            .RegisterEntry("修复了补丁后的各类问题 (1.5.0.1 - 1.5.0.4)。"u8);

    private static void Add1_5_0_0(Changelog log)
        => log.NextVersion("版本 1.5.0.0"u8)
            .RegisterImportant("更新以支持游戏版本 7.30 和 Dalamud API13，该版本使用了新的 GUI 后端。某些功能可能无法正常工作。如果您遇到任何问题，请告知我。"u8)
            .RegisterEntry("添加了使用两种顶点颜色方案导出模型的支持（感谢 zeroeightysix！）。"u8)
            .RegisterEntry("可能改善了导出模型时创建的基础颜色纹理的颜色准确性（感谢 zeroeightysix！）。"u8)
            .RegisterEntry("由于崩溃问题，禁用了使用 characterstockings 着色器的材质的透明度启用功能（感谢 zeroeightysix！）。"u8)
            .RegisterEntry("修复了模型输入/输出和无效切线的一些问题（感谢 PassiveModding！）"u8)
            .RegisterEntry("更改了在使用模组标准化器与组合群组时默认目录名称的行为。"u8)
            .RegisterEntry("向 HTTP API 添加了跳转到特定模组的功能。"u8)
            .RegisterEntry("修复了角色声音模组的问题（1.4.0.6）。"u8)
            .RegisterHighlight("添加了对第一个切换之外的饰品的 IMC 切换属性支持（1.4.0.5）。"u8)
            .RegisterEntry("修复了在槽位间交换物品时模型中的一些槽位特定属性和形态（1.4.0.5）。"u8)
            .RegisterEntry("为屏幕显示标签页和类似功能添加了人类皮肤材质处理（感谢 Ny！）（1.4.0.5）。"u8)
            .RegisterEntry("资源记录器中添加了加载资源的操作系统线程 ID（1.4.0.5）。"u8)
            .RegisterEntry("在设置标签页中添加了链接到我（Ottermandias）的 Ko-Fi 和 Patreon 的按钮。欢迎但不强制使用！:D "u8)
            .RegisterHighlight("模组设置组合框现在支持使用 Ctrl 键进行鼠标滚轮滚动并具有筛选功能（1.4.0.4）。"u8)
            .RegisterEntry("使用鼠标中键切换设计现在可以正确配合临时设置工作（1.4.0.4）。"u8)
            .RegisterEntry("更新了一些 BNPC 关联（1.4.0.3）。"u8)
            .RegisterEntry("修复了形态和属性的进一步问题（1.4.0.4）。"u8)
            .RegisterEntry("Penumbra 现在可以处理导入时被 TexTools 破坏的 MipMap 偏移纹理，并移除不必要的 MipMaps（1.4.0.3）。"u8)
            .RegisterEntry("为新的群组类型更新了模组合并器（1.4.0.3）。"u8)
            .RegisterEntry("添加了通过 IPC 查询 Penumbra 支持功能的接口（1.4.0.3）。"u8)
            .RegisterEntry("形态名称现在可以在 Penumbra 的模型编辑器中编辑（1.4.0.2）。"u8)
            .RegisterEntry("属性和形态现在可以完全切换（1.4.0.2）。"u8)
            .RegisterEntry("修复了属性和形态的几个问题（1.4.0.1）。"u8);

    private static void Add1_4_0_0(Changelog log)
        => log.NextVersion("版本 1.4.0.0"u8)
            .RegisterHighlight("添加了两种新的元数据变更类型：SHP和ATR（感谢Karou！）"u8)
            .RegisterEntry("这些允许模组创作者分别开启或关闭模型的自定义形态键和属性。"u8, 1)
            .RegisterEntry("自定义形态键需要遵循'shpx_*'格式，自定义属性需要遵循'atrx_*'格式。"u8, 1)
            .RegisterHighlight(
                "以下格式的形态键会在相关槽位包含相同形态键时自动开启："u8, 1)
            .RegisterEntry("'shpx_wa_*'，用于身体和腿部槽位之间的腰部接缝，"u8, 2)
            .RegisterEntry("'shpx_wr_*'，用于身体和手部槽位之间的手腕接缝，"u8, 2)
            .RegisterEntry("'shpx_an_*'，用于腿部和脚部槽位之间的脚踝接缝。"u8, 2)
            .RegisterEntry(
                "自定义形态键和属性可以在高级设置部分关闭，但不建议这样做。"u8,
                1)
            .RegisterHighlight("模组选择器宽度现在可以在特定限制范围内拖动（限制取决于总窗口宽度）。"u8)
            .RegisterEntry("当前的效果可能不是最终版本，如果您有任何意见请告诉我。"u8, 1)
            .RegisterEntry("改进了NPC标识符的命名，使用了Haselnussbomber的新命名功能（感谢Hasel！）。"u8)
            .RegisterEntry("添加了全局EQP条目，可以始终隐藏敖龙族角、维埃拉族耳朵或猫魅族耳朵。"u8)
            .RegisterEntry("如果相应种族没有进行模组修改，这会在头部留下空洞。"u8, 1)
            .RegisterEntry("在模组选择器面板中添加了临时设置筛选功能（感谢Caraxi）。"u8)
            .RegisterEntry("使模组标签页中的临时设置模式切换复选框更加醒目。"u8)
            .RegisterEntry("改进了高级编辑中的选项选择组合框。"u8)
            .RegisterEntry("修复了EST变更中的物品识别问题。"u8)
            .RegisterEntry("修复了模组面板有时会偏差1像素的尺寸问题。"u8)
            .RegisterEntry("修复了在GPose中其他插件破坏游戏状态假设时的重绘问题。"u8)
            .RegisterEntry("修复了高级编辑中元操作标签页的裁剪问题。"u8)
            .RegisterEntry("修复了空设置和临时设置的问题。"u8)
            .RegisterHighlight(
                "在物品交换标签页中，现在会优先排序和突出显示由当前模组更改的物品，然后是当前集合中更改的物品，最后是其他物品。（1.3.6.8）"u8)
            .RegisterHighlight(
                "默认值的元编辑现在会在导入时保留，除非未设置保留选项且没有其他选项编辑相同条目。（1.3.6.8）"u8)
            .RegisterEntry("添加了文件重定向的右键菜单，可以复制完整文件路径。（1.3.6.8）"u8)
            .RegisterEntry(
                "添加了模组导出按钮的右键菜单，可以在文件资源管理器中打开备份目录。（1.3.6.8）"u8)
            .RegisterEntry("修复了从其他插件重绘角色时的一些问题。（1.3.6.8）"u8)
            .RegisterEntry(
                "添加了一个与删除组合键分开的组合键，用于不太重要的按键检查，特别是切换匿名模式。（1.3.6.7）"u8)
            .RegisterEntry("修复了材质编辑器的一些问题（感谢Ny）。（1.3.6.6）"u8);

    private static void Add1_3_6_4(Changelog log)
        => log.NextVersion("版本 1.3.6.4"u8)
            .RegisterEntry("材质编辑器现已恢复正常功能。"u8);

    private static void Add1_3_6_0(Changelog log)
        => log.NextVersion("版本 1.3.6.0"u8)
            .RegisterImportant("更新 Penumbra 以支持游戏版本 7.20 和 Dalamud API 12。"u8)
            .RegisterEntry(
                "本次更新尚未经过充分测试，但我决定直接发布稳定版而非测试版——因为如果只发测试版，又会有大量用户为了抢先体验而涌入测试渠道，尽管他们并不适合参与测试。"u8,
                1)
            .RegisterEntry(
                "另外由于我个人并不使用 Penumbra 的大部分功能，所以很多问题可能我自己都无法发现。"u8, 1)
            .RegisterEntry("如您遇到任何问题，请立即在 Discord 上反馈。"u8,                                   1)
            .RegisterHighlight(
                "纹理编辑器现已支持 Block Compression 1/4/5 的编码格式，并添加了格式使用场景的提示说明。"u8)
            .RegisterEntry("现在支持使用 GPU 加速压缩，特别是 BC7 格式的处理速度显著提升。（感谢 Ny！）"u8, 1)
            .RegisterEntry(
                "新增通过右键点击导入按钮的上下文菜单，来导入特定模组中的 .atch 文件功能。"u8)
            .RegisterEntry("新增聊天指令用于清除 Penumbra 中的手动临时设置。"u8)
            .RegisterEntry(
                "默认情况下，用于选择首选更改项目的星标现在更加醒目，且支持自定义颜色。"u8)
            .RegisterEntry("修复了修改物品计算的一些小问题。（感谢 Anna！）"u8)
            .RegisterEntry("EQP 条目中原先标记为 Unknown 4 的项目已重命名为「隐藏手套袖口」。"u8)
            .RegisterEntry("修复了 EST 修改项的物品识别问题。"u8)
            .RegisterEntry("修复了未启用分组时，修改物品面板可能出现的显示裁剪问题。"u8);


    private static void Add1_3_5_0(Changelog log)
        => log.NextVersion("版本 1.3.5.0"u8)
            .RegisterImportant(
                "重定向不支持的文件类型（如 .atch）现在在启用时会产生警告。请更新仍包含这些文件的模组或请求其创建者更新。"u8)
            .RegisterEntry("现在可以在高级编辑的元数据部分导入 .atch 文件，将其与游戏默认值不同的更改添加到模组中。"u8)
            .RegisterHighlight("在设置和模组选项卡的合集栏中添加了始终使用临时设置的选项。"u8)
            .RegisterEntry(
                "启用此选项时，您在当前合集中所做的所有更改将作为临时更改应用，您必须使用“设为永久”将其设为永久。"u8,
                1)
            .RegisterEntry(
                "这对于尝试新模组而无需稍后重置其设置或在 Glamourer 中创建模组关联应该很有用。"u8,
                1)
            .RegisterEntry(
                "在模组选择器空白区域的上下文菜单中添加了清除所有手动临时设置的选项。"u8)
            .RegisterHighlight(
                "资源树现在考虑了一些额外的文件，如贴花，并改进了一些不应通常被修改的文件的快速导入行为。"u8)
            .RegisterHighlight("单个模组的更改项目显示已大幅改进。"u8)
            .RegisterEntry("任何更改的项目现在将在其工具提示中显示有多少个单独的编辑影响它。"u8, 1)
            .RegisterEntry("装备现在按其模型 ID 分组，减少了混乱。"u8, 1)
            .RegisterEntry(
                "显示的主要装备是受影响更改最多的那个，但可以由模组创建者和本地配置为特定项目。"u8,
                1)
            .RegisterEntry(
                "模组中存储的首选更改项目将在导出模组时共享，并用作本地首选项的默认值，这些首选项不会共享。"u8,
                2)
            .RegisterEntry(
                "您可以在设置中配置组是自动折叠还是展开，或完全删除分组。"u8, 1)
            .RegisterHighlight("修复了支持多个 UV 的模型导入/导出。"u8)
            .RegisterEntry("添加了一些与更改项目相关的 IPC。"u8)
            .RegisterEntry("骨骼和物理更改现在应该在更改项目中识别。"u8)
            .RegisterEntry("项目交换现在也会正确交换多装备槽的 EQP 条目。"u8)
            .RegisterEntry("通过 IPC 传输元数据编辑应该比以前更有效率。"u8)
            .RegisterEntry("修复了一些匿名名称在某些过场动画中的问题。"u8)
            .RegisterEntry("新提取的模组文件夹现在会尝试重命名三次，然后才被视为失败。"u8);

    private static void Add1_3_4_0(Changelog log)
        => log.NextVersion("版本 1.3.4.0"u8)
            .RegisterHighlight("为漫反射缓冲区添加HDR功能。当与 Glamourer 的高级定制功能配合使用时，可以更准确地表现非标准颜色值（例如皮肤或发色）。"u8)
            .RegisterEntry("此功能需要在卫月设置中启用“在游戏加载前等待插件初始化完成”并在启动时启用才能正常工作。默认开启但可手动关闭。"u8, 1)
            .RegisterHighlight("新增选项组类型：组合型选项组（Combining Groups）。"u8)
            .RegisterEntry("组合型选项组对用户的表现类似多选组，但不同选项的开启会导致设置组合生成唯一的配置结果。"u8, 1)
            .RegisterEntry("示例：用户可见两个复选框[+25%, +50%]，但四种选择状态实际会产生+0%、+25%、+50%或+75%（同时勾选时）。模组制作者可为每个组合单独配置不同设置。"u8, 1)
            .RegisterEntry("新增功能以更好地追踪过场动画中玩家角色的复制体（当角色被强制使用特定服装时，如玛格拉特过场动画）。可能也会改善婚礼场景的追踪，欢迎反馈。"u8)
            .RegisterEntry("在多模组选择界面添加了已选折叠组和折叠组数量的显示。"u8)
            .RegisterEntry("新增清理功能，可通过手动操作从配置和模组文件夹中移除过时或未使用的文件/备份。"u8)
            .RegisterEntry("更新了模型导入器中的骨骼和材质限制。"u8)
            .RegisterEntry("改进了异步加载IMC和材质文件的处理方式。"u8)
            .RegisterEntry("添加查询临时设置的IPC功能。"u8)
            .RegisterEntry("改进部分模组设置的IPC功能。"u8)
            .RegisterEntry("修复“画面角色”选项卡中的部分路径检测问题。"u8)
            .RegisterEntry("修复临时模组设置的相关问题。"u8)
            .RegisterEntry("修复游戏加载完成前的IPC调用问题。"u8)
            .RegisterEntry("修复材质编辑器预览中使用错误染色通道的问题。"u8)
            .RegisterEntry("当游戏加载过时材质时添加日志警告提示。"u8)
            .RegisterEntry("在解决方案中添加 Penumbra 生成/读取的部分 json 文件的 Schema 定义。"u8);

    private static void Add1_3_3_0(Changelog log)
        => log.NextVersion("版本 1.3.3.0"u8)
            .RegisterHighlight("为合集添加了临时设置。"u8)
            .RegisterEntry("在编辑模组设置时，可以通过右键菜单或设置面板中的按钮手动将设置设置为临时（并可恢复）。"u8, 1)
            .RegisterEntry("这可以用来测试模组或更改，而无需永久保存这些更改或事后恢复旧设置。"u8, 1)
            .RegisterEntry("更重要的是，其他插件可以通过IPC设置此选项，允许 Glamourer 在应用模组关联时仅设置和重置临时设置。"u8, 1)
            .RegisterEntry("作为极端示例，您可以仅在合集内启用角色的一致模组，并仅通过临时设置让 Glamourer 处理所有装备模组。"u8, 1)
            .RegisterEntry("这需要进行一些大的变更，这些变更已经测试了一段时间，但由于没人多提，所以它可能仍然存在一些错误或可用性问题。请告诉我！"u8, 1)
            .RegisterHighlight("添加了在登录事件时自动选择分配给当前角色的合集的选项。此功能默认关闭。"u8)
            .RegisterEntry("在材质编辑中，通过右键菜单项，新增了部分复制颜色集的功能。"u8)
            .RegisterHighlight("添加了对游戏缓存的 TMB 文件的处理，这应该解决了动画和 VFX 模组中 TMB 泄漏的问题。"u8)
            .RegisterEntry("启用复选框、优先级和继承按钮现在即使在滚动下拉设置时也会固定在模组设置面板顶部。"u8)
            .RegisterEntry("在使用物品交换创建新模组时，生成模组的作者信息得到了改进。"u8)
            .RegisterEntry("修复了画面角色标签页中的戒指以及通过IPC发送给其他插件的数据中的一个问题。"u8)
            .RegisterEntry("修复了写入材质文件时的一些问题，导致技术上有效的文件仍然因未知原因在游戏中引发问题。"u8)
            .RegisterEntry("修复了一些 ImGui 断言问题。"u8);

    private static void Add1_3_2_0(Changelog log)
        => log.NextVersion("版本 1.3.2.0"u8)
            .RegisterHighlight("新增 ATCH 元数据操作，允许跨多个模组对骨骼挂点进行组合编辑。"u8)
            .RegisterEntry("这些 ATCH 操作应通过 Mare Synchronos 共享。"u8, 1)
            .RegisterEntry("这是一个早期实现，可能存在问题。如果发现问题，请告知。尽管它已经测试了一段时间，但尚未收到反馈。"u8, 1)
            .RegisterEntry("在“画面角色”选项卡中通过 Ctrl + 右键单击跳转到已识别的模组，并稍微改进了其显示。"u8)
            .RegisterEntry("在文件重定向编辑器中，在路径的右键上下文菜单中增加了一些复制选项。"u8)
            .RegisterHighlight("新增通过聊天命令 '/penumbra mod settings' 更改特定模组设置的选项。"u8)
            .RegisterEntry("修复了元数据操作复制粘贴的问题。"u8)
            .RegisterEntry("修复了与数据元操作相关的其他一些问题。"u8)
            .RegisterEntry("更新了可用的 NPC 名称，并修复了某些假定不可见字符在 ImGui 中显示的问题。"u8);


    private static void Add1_3_1_0(Changelog log)
        => log.NextVersion("版本 1.3.1.0"u8)
            .RegisterEntry("Penumbra 已更新以支持 Dalamud API 11 和 7.1 游戏版本。"u8)
            .RegisterImportant("已知使用某些 VFX/SFX 模组可能导致崩溃，可能与音频文件有关。"u8)
            .RegisterEntry("如果您遇到这些问题，请在 Discord 中报告，并暂时禁用相关模组。"u8, 1)
            .RegisterImportant("已禁用修改的 .atch 文件。过期的这些文件会在加载时会导致崩溃。"u8)
            .RegisterEntry("通过元数据更改实现修改 .atch 文件的更好方法将很快在测试分支发布。"u8, 1)
            .RegisterHighlight("临时合集（如 Mare 创建的合集）现在将始终遵循所有权规则。"u8)
            .RegisterEntry("这意味着您可以关闭此设置，而 Mare 仍然可以正确处理其他玩家的宠物和坐骑。"u8, 1)
            .RegisterEntry("新的物理和动画引擎文件（.kdb 和 .bnmb）现在可以正确重定向并遵循 EST 变更。"u8)
            .RegisterEntry("修复了 EQP 条目标记错误的问题，且全局 EQP 未正确修改耳环的所有必要值的问题。"u8)
            .RegisterEntry("修复了重新加载模组时模组的全局 EQP 更改被重置的问题。"u8)
            .RegisterEntry("修复了左手戒指与 Mare 同步 / 画面角色 标签页的问题。"u8)
            .RegisterEntry("可能修复了登录画面中角色被错误识别的问题。"u8)
            .RegisterEntry("改进了调试模块的可视化功能。"u8);


    private static void Add1_3_0_0(Changelog log)
        => log.NextVersion("版本 1.3.0.0"u8)

            .RegisterHighlight("高级编辑窗口中的纹理选项卡现在可以导入和导出 .tga 文件。"u8)
            .RegisterEntry("现在也可以导入 BC4 和 BC6 纹理。"u8, 1)
            .RegisterHighlight("新增了对眼镜槽（面部配饰）进行道具交换的功能。"u8)
            .RegisterEntry("对面部配饰/额外物品进行了大量重构。如果出现任何问题，请告知。"u8, 1)
            .RegisterEntry("编辑模组选项卡现在会显示模组的导入日期，并且可以通过按钮重置。"u8)
            .RegisterEntry("还增加了一个按钮用于打开包含本地模组数据的文件。"u8, 1)
            .RegisterHighlight("现在可以将 IMC 组配置为仅应用其条目的属性标志，并从默认值中获取其他值。"u8)
            .RegisterEntry("这允许在设置属性的同时保持每个 IMC 组条目的材质索引。"u8, 1)
            .RegisterHighlight("模型导入/导出已修复并重新启用（感谢 ackwell 和 ramen）。"u8)
            .RegisterHighlight("添加了一个 hack，允许额外物品（面部配饰、眼镜）拥有 VFX。"u8)
            .RegisterEntry("还修复了之前允许饰品拥有 VFX 的 hack 不再工作的情况。"u8, 1)
            .RegisterHighlight("在高级编辑窗口中添加了对 PBD 文件的基础编辑选项。"u8)
            .RegisterEntry("现在准备高级编辑窗口中的模组不会冻结游戏，直到准备完成。"u8)
            .RegisterEntry("高级编辑窗口中的元操作现在已经排序，并且绘制时不会显著影响性能。"u8)
            .RegisterEntry("高级编辑窗口中添加了一个按钮，可以从模组中删除所有包含默认值的元数据操作。"u8)
            .RegisterEntry("现在，在从压缩包和 .pmps 导入时，如果没有在其他地方设置，包含默认值的元数据操作也会被移除，而不仅仅是 .ttmps。"u8, 1)
            .RegisterEntry("基于复选框的模组筛选器现在是三态复选框，而不是两个不相交的复选框。"u8)
            .RegisterEntry("现在可以复制资源日志中的路径。"u8)
            .RegisterEntry("在通过 Heliosphere 更新模组时，屏蔽了一些冗余的错误日志。"u8)
            .RegisterEntry("为 TexTools 互操作性添加了“Page”到导入的模组数据中。该值在 Penumbra 中不使用，只是持久化。"u8)
            .RegisterEntry("更新了所有外部依赖项。"u8)
            .RegisterEntry("修复了与亚人 IMC 条目相关的问题。"u8)
            .RegisterEntry("修复了模组导入窗口中的一些越界错误。"u8)
            .RegisterEntry("修复了有关首次创建模组元数据文件的竞态条件问题。"u8)
            .RegisterEntry("修复了合并模组选项卡中长模组标题的问题。"u8)
            .RegisterEntry("其他一些杂项修复。"u8);


    private static void Add1_2_1_0(Changelog log)
        => log.NextVersion("版本 1.2.1.0"u8)
            .RegisterHighlight("Penumbra 现在已为「金曦之遗辉」发布新版本！"u8)
            .RegisterEntry("你的模组可能需要更新。请使用TexTools的相关功能。"u8, 1)
            .RegisterEntry("对于模型文件，Penumbra提供了基本的更新功能，但尽量优先使用TexTools。"u8, 1)
            .RegisterEntry("其他文件，如材质和纹理，暂时需要通过 TexTools 更新。"u8, 1)
            .RegisterEntry("Penumbra 能够识别部分过时的模组，并防止其加载（特别是着色器，感谢 Ny）。"u8, 1)
            .RegisterImportant("很抱歉花了这么长时间，但从一开始就有大量工作要完成。"u8)
            .RegisterImportant("由于Penumbra测试时间较长，出现了许多问题和错误需要解决。"u8, 1)
            .RegisterEntry("可能仍然存在许多问题，请报告任何你发现的错误。"u8, 1)
            .RegisterImportant("但是，请确保在报告问题之前这些问题不是由过时的模组引起的。"u8, 1)
            .RegisterEntry("虽然这个更新日志看起来很短，但我省略了数百个小修复以及让 Penumbra 在「金曦之遗辉」上运行的详细工作。"u8, 1)
            .RegisterHighlight("高级编辑窗口中的材质编辑选项卡已大幅改进（感谢 Ny）。"u8)
            .RegisterEntry("特别是对于使用新着色器的「金曦之遗辉」材质，窗口提供了更深入和友好的编辑选项。"u8, 1)
            .RegisterHighlight("着色器模组和骨骼变形器的多个改进已完成。"u8)
            .RegisterHighlight("IMC组现在允许关闭默认条目中已启用的属性。"u8)
            .RegisterImportant("移除了“更新Bibo”按钮。由于旧模组需要更新，这一功能已经多余。"u8)
            .RegisterEntry("点击该按钮通常对新模组弊大于利。"u8, 1)
            .RegisterEntry("如果你仍然需要批量迁移模型中的材质，高级编辑中的材质指定选项卡仍然可以用于此操作。"u8, 1)
            .RegisterEntry("“画面角色”选项卡已更新并改进，现在可以以更有用的形式显示模组的实际路径。"u8)
            .RegisterImportant("模型导入/导出功能暂时禁用，直到完成与「金曦之遗辉」相关的更改。"u8)
            .RegisterHighlight("现在可以通过右键点击状态，在模组的合集选项卡中更改模组状态。"u8)
            .RegisterHighlight("模组中更改的物品现在会在物品交换选项卡中优先显示，并高亮显示。"u8)
            .RegisterEntry("改进了路径处理，考虑了大小写敏感性。"u8)
            .RegisterEntry("修正了在文件夹没有匹配时，负搜索匹配的问题。"u8)
            .RegisterEntry("相同优先级的模组选项组现在按反向索引顺序应用。（1.2.0.12）"u8)
            .RegisterEntry("修正了高级编辑窗口标题中显示缺失文件的问题。（1.2.0.8）"u8)
            .RegisterEntry("修正了角色在钓鱼时重绘导致的部分软锁问题。请尽量不要这样做。（1.2.0.7）"u8)
            .RegisterEntry("改进了某些职业的无效副手IMC文件的处理。（1.2.0.6）"u8)
            .RegisterEntry("为UI类别的文件添加了自动重复功能，因其不唯一时会导致崩溃。（1.2.0.5）"u8)
            .RegisterEntry("模组导入弹出窗口完成后，现在可以通过点击窗口外部关闭。（1.2.0.5）"u8)
            .RegisterEntry("修正了模组标准化跳过默认选项的问题。（1.2.0.5）"u8)
            .RegisterEntry("改进了支持信息的输出。（1.1.1.5）"u8)
            .RegisterEntry("彻底重构了元数据操作的处理。（1.1.1.3）"u8)
            .RegisterEntry("添加了禁用在登录大厅和美容师处显示模组的配置选项。（1.1.1.1）"u8)
            .RegisterEntry("修正了添加模组API和根目录的问题。（1.1.1.2）"u8)
            .RegisterEntry("修正了模组合并器文件查找和大小写的问题。（1.1.1.2）"u8)
            .RegisterEntry("修正了在某些情况下，文件保存时模组合并或物品交换无法生效的问题。（1.1.1.2）"u8);

    private static void Add1_1_1_0(Changelog log)
        => log.NextVersion("版本 1.1.1.0"u8)
            .RegisterHighlight("模组的筛选现在被标记化，现在可以同时筛选多个条件或排除特定条件。"u8)
            .RegisterEntry("鼠标悬停在筛选器上可在工具提示中查看新的可用选项。"u8, 1)
            .RegisterEntry("请注意，标记化稍微改变了之前的行为。"u8, 1)
            .RegisterEntry("此功能仍可改进，如果你有任何想法，请告诉我！"u8, 1)
            .RegisterHighlight("新增了登录界面角色按名字识别的初始功能。"u8)
            .RegisterEntry(
                "这些角色无法被重绘，并且复用了一些资源，因此可能不会始终如预期运行，但总体应该可以工作。如果遇到特殊情况，请告诉我！"u8, 1)
            .RegisterEntry("为IMC组添加了适用于所有模型变体的功能，而不是仅针对特定的变体。"u8)
            .RegisterEntry("为筛选器和隐身模式改进了资源树视图。（感谢Ny）"u8)
            .RegisterEntry("为全局EQP条件添加了工具提示。"u8)
            .RegisterEntry("修正了由于Square Enix没有将新世界公开而导致无法正确识别的问题。"u8)
            .RegisterEntry("修正了模型导入在进行权重调整时卡住的问题。（感谢ackwell）"u8)
            .RegisterEntry("修正了材质编辑器中的染色预览无法应用的问题。"u8)
            .RegisterEntry("修正了重命名时集合无法保存的问题。"u8)
            .RegisterEntry("修正了合集设置为负值时解析合集的问题，现在应将其设为0。"u8)
            .RegisterEntry("修正了配件VFX添加的问题。"u8)
            .RegisterEntry("修正了GMP动画类型条目中的问题。"u8)
            .RegisterEntry("修正了模组合并器中的另一个问题。"u8)
            .RegisterEntry("修正了IMC组和IPC的问题。"u8)
            .RegisterEntry("修正了根目录大小写的问题。"u8)
            .RegisterEntry("修正了IMC属性工具提示未出现在禁用的复选框旁边的问题。"u8)
            .RegisterEntry("为单个模组添加了获取更改项目的IPC功能。（1.1.0.2）"u8)
            .RegisterEntry("修正了创建未命名合集时的问题。（1.1.0.2）"u8)
            .RegisterEntry("修正了模组合并器的问题。（1.1.0.2）"u8)
            .RegisterEntry("修正了全局EQP条目检查戒指时错误地检查手镯的问题。（1.1.0.2）"u8)
            .RegisterEntry("修正了新创建的合集未被添加到集合列表中的问题。（1.1.0.1）"u8);

    private static void Add1_1_0_0(Changelog log)
        => log.NextVersion("版本 1.1.0.0"u8)
            .RegisterImportant(
                "此更新再次带来了大量非常重要的后端更改（合集和组），因此可能会引入新问题。"u8)
            .RegisterEntry("更新至 .NET 8 和 XIV 6.58，利用了一些新的框架功能来提高性能和稳定性。"u8)
            .RegisterHighlight(
                "新增了一个实验性的崩溃处理程序，当游戏崩溃时，它应该会写入一个 Penumbra.log 文件，其中包含特定于 Penumbra 的信息。"u8)
            .RegisterEntry("默认情况下已禁用。可以在高级设置中启用此功能。"u8, 1)
            .RegisterHighlight("合集现在具有关联的 GUID 作为标识符，而不是它们的名称，因此现在可以重命名它们。"u8)
            .RegisterEntry("迁移这些合集可能会引入问题，请在遇到任何问题时尽快告诉我。"u8, 1)
            .RegisterEntry("在迁移之前应创建永久性（非滚动性）备份，以防出现任何问题。"u8,                 1)
            .RegisterHighlight(
                "添加了可以在设置选项卡中设置并可以更轻松地应用或移除的预定义标签。（由 DZD 提供）"u8)
            .RegisterHighlight(
                "彻底重做了内部选项和组处理的方式，并引入了第一个新的组类型，变体IMC 组。"u8)
            .RegisterEntry(
                "模组创建者可以在他们的模组中添加一个 变体IMC 组，用于控制单个 变体IMC 操作，从而为其提供独立属性的选项。"u8,
                1)
            .RegisterEntry(
                "这使得组合选项变得更容易：无需定义 'A'、'B' 和 'AB'，您只需定义 'A' 和 'B'，并跳过它们的组合。"u8,
                1)
            .RegisterHighlight("新增了一种新类型的元数据操作，'全局装备参数设置 EQP 操作'。"u8)
            .RegisterEntry(
                "全局 EQP 操作允许配饰不被其他装备隐藏，例如，每当角色佩戴特定的手镯时，无论是身体还是手部物品都不会隐藏手镯。"u8,
                1)
            .RegisterEntry(
                "如果将类似夹克或披肩的物品放在配饰上，可以防止其被隐藏。"u8,
                1)
            .RegisterEntry(
                "从 TTMP 导入的单选选项组中的第一个空选项现在会保持其位置，而不是被移动到第一个选项。"u8)
            .RegisterEntry("其他空选项仍然会被移除。"u8, 1)
            .RegisterHighlight(
                "在模组选择器上下文菜单中新增了一个字段，可以直接重命名模组，而不是在文件系统中移动它们。"u8)
            .RegisterEntry("您可以在设置中选择要显示的重命名字段（无、任一或两者）。"u8, 1)
            .RegisterEntry("将 characterglass.shpk 着色文件添加到特殊着色处理以解决替换时的问题。（由 Ny 提供）"u8)
            .RegisterEntry("如果用户尚未设置根目录，则更明显地显示该信息。"u8)
            .RegisterEntry(
                "现在，只要未聚焦，您可以通过简单的右键单击将当前剪贴板文本粘贴到模组选择器筛选器中。"u8)
            .RegisterHighlight(
                "新增了选项，如果通过 变体IMC 编辑添加配饰，则可以显示配饰的 VFX，这是游戏本身不具备的功能。（由 Ocealot 提供）"u8)
            .RegisterEntry("新增对从DT基准测试读取和写入新材料和模型文件格式的支持。"u8)
            .RegisterEntry(
                "新增了在更改项目标签中隐藏机工副手的选项（因为对其进行任何更改都会同时更改所有这些项目），默认情况下开启。"u8)
            .RegisterEntry("移除了在 Penumbra 中新创建组的自动生成描述。"u8)
            .RegisterEntry(
                "对高级编辑窗口进行了一些改进，例如添加了更好且性能更佳的非结构化数据的十六进制查看器。"u8)
            .RegisterEntry("由 ackwell 进行的模型导入/导出的各种改进（在所有补丁中）。"u8)
            .RegisterEntry("在高级编辑窗口中，悬停在其他选项中的元数据操作上现在会显示这些选项的列表。"u8)
            .RegisterEntry("彻底重构了 API 和 IPC 结构。"u8)
            .RegisterImportant("这意味着一些与 Penumbra 交互的插件在更新之前可能无法正常工作。"u8, 1)
            .RegisterEntry("解决了当绘制添加项过大时，UI IPC 可能会导致所有设置移位的问题。"u8)
            .RegisterEntry("修复了重新加载模组后不能确保该模组的设置后续正确的问题。"u8)
            .RegisterEntry("修复了一些压缩文件大小的问题。"u8)
            .RegisterEntry("修复了合并和去重模组时的问题。"u8)
            .RegisterEntry("修复了扫描没有文件夹访问权限的模组时崩溃的问题。"u8)
            .RegisterEntry(
                "使插件符合 Dalamud 要求，通过添加 punchline 和另一个按钮来从安装程序中打开菜单。"u8)
            .RegisterEntry("添加了一个选项，在保存文件时自动重新绘制玩家角色。（1.0.0.8）"u8)
            .RegisterEntry("修复了操作模组不触发某些事件的问题。（1.0.0.7）"u8)
            .RegisterEntry("修复了临时模组不触发某些事件的问题。（1.0.0.6）"u8)
            .RegisterEntry("修复了在高级编辑窗口打开时重命名模组的问题。（1.0.0.6）"u8)
            .RegisterEntry("修复了空选项组的问题。（1.0.0.5）"u8)
            .RegisterEntry("修复了剧情人物识别的问题。（1.0.0.4）"u8)
            .RegisterEntry("添加了本地环境信息以更好的提交支持信息。（1.0.0.4）"u8)
            .RegisterEntry("修复了在 IPC 中复制的模组设置缺少未使用设置的问题。（1.0.0.3）"u8);

    private static void Add1_0_0_0(Changelog log)
        => log.NextVersion("Version 1.0.0.0"u8)
            .RegisterHighlight("Mods in the mod selector can now be filtered by changed item categories."u8)
            .RegisterHighlight("Model Editing options in the Advanced Editing Window have been greatly extended (by ackwell):"u8)
            .RegisterEntry("Attributes and referenced materials can now be set per mesh."u8, 1)
            .RegisterEntry(
                "Model files (.mdl) can now be exported to the well-established glTF format, which can be imported e.g. by Blender."u8,
                1)
            .RegisterEntry("glTF files can also be imported back to a .mdl file."u8, 1)
            .RegisterHighlight(
                "Model Export and Import are a work in progress and may encounter issues, not support all cases or produce wrong results, please let us know!"u8,
                1)
            .RegisterEntry("The last selected mod and the open/close state of the Advanced Editing Window are now stored across launches."u8)
            .RegisterEntry("Footsteps of certain mounts will now be associated to collections correctly."u8)
            .RegisterEntry("Save-in-Place in the texture tab now requires the configurable modifier."u8)
            .RegisterEntry("Updated OtterTex to a newer version of DirectXTex."u8)
            .RegisterEntry("Fixed an issue with horizontal scrolling if a mod title was very long."u8)
            .RegisterEntry("Fixed an issue with the mod panels header not updating its data when the selected mod updates."u8)
            .RegisterEntry("Fixed some issues with EQDP files for invalid characters."u8)
            .RegisterEntry("Fixed an issue with the FileDialog being drawn twice in certain situations."u8)
            .RegisterEntry(
                "A lot of backend changes that should not have an effect on users, but may cause issues if something got messed up."u8);

    private static void Add8_3_0(Changelog log)
        => log.NextVersion("Version 0.8.3.0"u8)
            .RegisterHighlight(
                "Improved the UI for the On-Screen tabs with highlighting of used paths, filtering and more selections. (by Ny)"u8)
            .RegisterEntry(
                "Added an option to replace non-ASCII symbols with underscores for folder paths on mod import since this causes problems on some WINE systems. This option is off by default."u8)
            .RegisterEntry(
                "Added support for the Changed Item Icons to load modded icons, but this depends on a not-yet-released Dalamud update."u8)
            .RegisterEntry(
                "Penumbra should no longer redraw characters while they are fishing, but wait for them to reel in, because that could cause soft-locks. This may cause other issues, but I have not found any."u8)
            .RegisterEntry(
                "Hopefully fixed a bug on mod import where files were being read while they were still saving, causing Penumbra to create wrong options."u8)
            .RegisterEntry("Fixed a few display issues."u8)
            .RegisterEntry("Added some IPC functionality for Xande. (by Asriel)"u8);

    private static void Add8_2_0(Changelog log)
        => log.NextVersion("Version 0.8.2.0"u8)
            .RegisterHighlight(
                "You can now redraw indoor furniture. This may not be entirely stable and might break some customizable decoration like wallpapered walls."u8)
            .RegisterEntry("The redraw bar has been slightly improved and disables currently unavailable redraw commands now."u8)
            .RegisterEntry("Redrawing players now also actively redraws any accessories they are using."u8)
            .RegisterEntry("Power-users can now redraw game objects by index via chat command."u8)
            .RegisterHighlight(
                "You can now filter for the special case 'None' for filters where that makes sense (like Tags or Changed Items)."u8)
            .RegisterHighlight("When selecting multiple mods, you can now add or remove tags from them at once."u8)
            .RegisterEntry(
                "The dye template combo in advanced material editing now displays the currently selected dye as it would appear with the respective template."u8)
            .RegisterEntry("The On-Screen tab and associated functionality has been heavily improved by Ny."u8)
            .RegisterEntry("Fixed an issue with the changed item identification for left rings."u8)
            .RegisterEntry("Updated BNPC data."u8)
            .RegisterEntry(
                "Some configuration like the currently selected tab states are now stored in a separate file that is not backed up and saved less often."u8)
            .RegisterEntry("Added option to open the Penumbra main window at game start independently of Debug Mode."u8)
            .RegisterEntry("Fixed some tooltips in the advanced editing window. (0.8.1.8)"u8)
            .RegisterEntry("Fixed clicking to linked changed items not working. (0.8.1.8)"u8)
            .RegisterEntry("Support correct handling of offhand-parts for two-handed weapons for changed items. (0.8.1.7)"u8)
            .RegisterEntry("Fixed renaming the mod directory not updating paths in the advanced window. (0.8.1.6)"u8)
            .RegisterEntry("Fixed portraits not respecting your card settings. (0.8.1.6)"u8)
            .RegisterEntry("Added ReverseResolvePlayerPathsAsync for IPC. (0.8.1.6)"u8)
            .RegisterEntry("Expanded the tooltip for Wait for Plugins on Startup. (0.8.1.5)"u8)
            .RegisterEntry("Disabled window sounds for some popup windows. (0.8.1.5)"u8)
            .RegisterEntry("Added support for middle-clicking mods to enable/disable them. (0.8.1.5)"u8);

    private static void Add8_1_2(Changelog log)
        => log.NextVersion("Version 0.8.1.2"u8)
            .RegisterEntry("Fixed an issue keeping mods selected after their deletion."u8)
            .RegisterEntry("Maybe fixed an issue causing individual assignments to get lost on game start."u8);

    private static void Add8_1_1(Changelog log)
        => log.NextVersion("Version 0.8.1.1"u8)
            .RegisterImportant(
                "Updated for 6.5 - Square Enix shuffled around a lot of things this update, so some things still might not work but have not been noticed yet. Please report any issues."u8)
            .RegisterEntry("Added support for chat commands to affect multiple individuals matching the supplied string at once."u8)
            .RegisterEntry(
                "Improved messaging: many warnings or errors appearing will stay a little longer and can now be looked at in a Messages tab (visible only if there have been any)."u8)
            .RegisterEntry("Fixed an issue with leading or trailing spaces when renaming mods."u8);


    private static void Add8_0_0(Changelog log)
        => log.NextVersion("Version 0.8.0.0"u8)
            .RegisterEntry(
                "Penumbra now uses Windows' transparent file system compression by default (on Windows systems). You can disable this functionality in the settings."u8)
            .RegisterImportant("You can retroactively compress your existing mods in the settings via the press of a button, too."u8, 1)
            .RegisterEntry(
                "In our tests, this not only was able to reduce storage space by 30-60%, it even decreased loading times since less I/O had to take place."u8,
                1)
            .RegisterEntry("Added emotes to changed item identification."u8)
            .RegisterEntry(
                "Added quick select buttons to switch to the current interface collection or the collection applying to the current player character in the mods tab, reworked their text and tooltips slightly."u8)
            .RegisterHighlight("Drag & Drop of multiple mods and folders at once is now supported by holding Control while clicking them."u8)
            .RegisterEntry("You can now disable conflicting mods from the Conflicts panel via Control + Right-click."u8)
            .RegisterEntry("Added checks for your deletion-modifiers for restoring mods from backups or deleting backups."u8)
            .RegisterEntry(
                "Penumbra now should automatically try to restore your custom sort order (mod folders) and your active collections from backups if they fail to load. No guarantees though."u8)
            .RegisterEntry("The resource watcher now displays a column providing load state information of resources."u8)
            .RegisterEntry(
                "Custom RSP scaling outside of the collection assigned to Base should now be respected for emotes that adjust your stance on height differences."u8)
            .RegisterEntry(
                "Mods that replace the skin shaders will not cause visual glitches like loss of head shadows or Free Company crest tattoos anymore (by Ny)."u8)
            .RegisterEntry("The Material editor has been improved (by Ny):"u8)
            .RegisterHighlight(
                "Live-Preview for materials yourself or entities owned by you are currently using, so you can see color set edits in real time."u8,
                1)
            .RegisterEntry(
                "Colors on the color table of a material can be highlighted on yourself or entities owned by you by hovering a button."u8, 1)
            .RegisterEntry("The color table has improved color accuracy."u8,                                                               1)
            .RegisterEntry("Materials with non-dyable color tables can be made dyable, and vice-versa."u8,                                 1)
            .RegisterEntry("The 'Advanced Shader Resources' section has been split apart into dedicated sections."u8,                      1)
            .RegisterEntry(
                "Addition and removal of shader keys, textures, constants and a color table has been automated following shader requirements and can not be done manually anymore."u8,
                1)
            .RegisterEntry(
                "Plain English names and tooltips can now be displayed instead of hexadecimal identifiers or code names by providing dev-kit files installed via certain mods."u8,
                1)
            .RegisterEntry("The Texture editor has been improved (by Ny):"u8)
            .RegisterHighlight(
                "The overlay texture can now be combined in several ways and automatically resized to match the input texture."u8,
                1)
            .RegisterEntry("New color manipulation options have been added."u8,                  1)
            .RegisterEntry("Modifications to the selected texture can now be saved in-place."u8, 1)
            .RegisterEntry("The On-Screen tab has been improved (by Ny):"u8)
            .RegisterEntry("The character list will load more quickly."u8,                           1)
            .RegisterEntry("It is now able to deal with characters under transformation effects."u8, 1)
            .RegisterEntry(
                "The headers are now color-coded to distinguish between you and other players, and between NPCs that are handled locally or on the server. Colors are customizable."u8,
                1)
            .RegisterEntry("More file types will be recognized and shown."u8,                           1)
            .RegisterEntry("The actual paths for game files will be displayed and copied correctly."u8, 1)
            .RegisterEntry("The Shader editor has been improved (by Ny):"u8)
            .RegisterEntry(
                "New sections 'Shader Resources' and 'Shader Selection' have been added, expanding on some data that was in 'Further Content' before."u8,
                1)
            .RegisterEntry("A fail-safe mode for shader decompilation on platforms that do not fully support it has been added."u8, 1)
            .RegisterEntry("Fixed invalid game paths generated for variants of customizations."u8)
            .RegisterEntry("Lots of minor improvements across the codebase."u8)
            .RegisterEntry("Some unnamed mounts were made available for actor identification. (0.7.3.2)"u8);

    private static void Add7_3_0(Changelog log)
        => log.NextVersion("Version 0.7.3.0"u8)
            .RegisterEntry(
                "Added the ability to drag and drop mod files from external sources (like a file explorer or browser) into Penumbras mod selector to import them."u8)
            .RegisterEntry("You can also drag and drop texture files into the textures tab of the Advanced Editing Window."u8, 1)
            .RegisterEntry(
                "Added a priority display to the mod selector using the currently selected collections priorities. This can be hidden in settings."u8)
            .RegisterEntry("Added IPC for texture conversion, improved texture handling backend and threading."u8)
            .RegisterEntry(
                "Added Dalamud Substitution so that other plugins can more easily use replaced icons from Penumbras Interface collection when using Dalamuds new Texture Provider."u8)
            .RegisterEntry("Added a filter to texture selection combos in the textures tab of the Advanced Editing Window."u8)
            .RegisterEntry(
                "Changed behaviour when failing to load group JSON files for mods - the pre-existing but failing files are now backed up before being deleted or overwritten."u8)
            .RegisterEntry("Further backend changes, mostly relating to the Glamourer rework."u8)
            .RegisterEntry("Fixed an issue with modded decals not loading correctly when used with the Glamourer rework."u8)
            .RegisterEntry("Fixed missing scaling with UI Scale for some combos."u8)
            .RegisterEntry("Updated the used version of SharpCompress to deal with Zip64 correctly."u8)
            .RegisterEntry("Added a toggle to not display the Changed Item categories in settings (0.7.2.2)."u8)
            .RegisterEntry("Many backend changes relating to the Glamourer rework (0.7.2.2)."u8)
            .RegisterEntry("Fixed an issue when multiple options in the same option group had the same label (0.7.2.2)."u8)
            .RegisterEntry("Fixed an issue with a GPose condition breaking animation and vfx modding in GPose (0.7.2.1)."u8)
            .RegisterEntry("Fixed some handling of decals (0.7.2.1)."u8);

    private static void Add7_2_0(Changelog log)
        => log.NextVersion("Version 0.7.2.0"u8)
            .RegisterEntry(
                "Added Changed Item Categories and icons that can filter for specific types of Changed Items, in the Changed Items Tab as well as in the Changed Items panel for specific mods.."u8)
            .RegisterEntry(
                "Icons at the top can be clicked to filter, as well as right-clicked to open a context menu with the option to inverse-filter for them"u8,
                1)
            .RegisterEntry("There is also an ALL button that can be toggled."u8, 1)
            .RegisterEntry(
                "Modded files in the Font category now resolve from the Interface assignment instead of the base assignment, despite not technically being in the UI category."u8)
            .RegisterEntry(
                "Timeline files will no longer be associated with specific characters in cutscenes, since there is no way to correctly do this, and it could cause crashes if IVCS-requiring animations were used on characters without IVCS."u8)
            .RegisterEntry("File deletion in the Advanced Editing Window now also checks for your configured deletion key combo."u8)
            .RegisterEntry(
                "The Texture tab in the Advanced Editing Window now has some quick convert buttons to just convert the selected texture to a different format in-place."u8)
            .RegisterEntry(
                "These buttons only appear if only one texture is selected on the left side, it is not otherwise manipulated, and the texture is a .tex file."u8,
                1)
            .RegisterEntry("The text part of the mod filter in the mod selector now also resets when right-clicking the drop-down arrow."u8)
            .RegisterEntry("The Dissolve Folder option in the mod selector context menu has been moved to the bottom."u8)
            .RegisterEntry("Somewhat improved IMC handling to prevent some issues."u8)
            .RegisterEntry(
                "Improved the handling of mod renames on mods with default-search names to correctly rename their search-name in (hopefully) all cases too."u8)
            .RegisterEntry("A lot of backend improvements and changes related to the pending Glamourer rework."u8)
            .RegisterEntry("Fixed an issue where the displayed active collection count in the support info was wrong."u8)
            .RegisterEntry(
                "Fixed an issue with created directories dealing badly with non-standard whitespace characters like half-width or non-breaking spaces."u8)
            .RegisterEntry("Fixed an issue with unknown animation and vfx edits not being recognized correctly."u8)
            .RegisterEntry("Fixed an issue where changing option descriptions to be empty was not working correctly."u8)
            .RegisterEntry("Fixed an issue with texture names in the resource tree of the On-Screen views."u8)
            .RegisterEntry("Fixed a bug where the game would crash when drawing folders in the mod selector that contained a '%' symbol."u8)
            .RegisterEntry("Fixed an issue with parallel algorithms obtaining the wrong number of available cores."u8)
            .RegisterEntry("Updated the available selection of Battle NPC names."u8)
            .RegisterEntry("A typo in the 0.7.1.2 Changlog has been fixed."u8)
            .RegisterEntry("Added the Sea of Stars as accepted repository. (0.7.1.4)"u8)
            .RegisterEntry(
                "Fixed an issue with collections sometimes not loading correctly, and IMC files not applying correctly. (0.7.1.3)"u8);


    private static void Add7_1_2(Changelog log)
        => log.NextVersion("Version 0.7.1.2"u8)
            .RegisterEntry(
                "Changed threaded handling of collection caches. Maybe this fixes the startup problems some people are experiencing."u8)
            .RegisterEntry(
                "This is just testing and may not be the solution, or may even make things worse. Sorry if I have to put out multiple small patches again to get this right."u8,
                1)
            .RegisterEntry("Fixed Penumbra failing to load if the main configuration file is corrupted."u8)
            .RegisterEntry("Some miscellaneous small bug fixes."u8)
            .RegisterEntry("Slight changes in behaviour for deduplicator/normalizer, mostly backend."u8)
            .RegisterEntry("A typo in the 0.7.1.0 Changelog has been fixed."u8)
            .RegisterEntry("Fixed left rings not being valid for IMC entries after validation. (7.1.1)"u8)
            .RegisterEntry(
                "Relaxed the scaling restrictions for RSP scaling values to go from 0.01 to 512.0 instead of the prior upper limit of 8.0, in interface as well as validation, to better support the fetish community. (7.1.1)"u8);

    private static void Add7_1_0(Changelog log)
        => log.NextVersion("Version 0.7.1.0"u8)
            .RegisterEntry("Updated for patch 6.4 - there may be some oversights on edge cases, but I could not find any issues myself."u8)
            .RegisterImportant(
                "This update changed some Dragoon skills that were moving the player character before to not do that anymore. If you have any mods that applied to those skills, please make sure that they do not contain any redirections for .tmb files. If skills that should no longer move your character still do that for some reason, this is detectable by the server."u8,
                1)
            .RegisterEntry(
                "Added a Mod Merging tab in the Advanced Editing Window. This can help you merge multiple mods to one, or split off specific options from an existing mod into a new mod."u8)
            .RegisterEntry(
                "Added advanced options to configure the minimum allowed window size for the main window (to reduce it). This is not quite supported and may look bad, so only use it if you really need smaller windows."u8)
            .RegisterEntry("The last tab selected in the main window is now saved and re-used when relaunching Penumbra."u8)
            .RegisterEntry("Added a hook to correctly associate some sounds that are played while weapons are drawn."u8)
            .RegisterEntry("Added a hook to correctly associate sounds that are played while dismounting."u8)
            .RegisterEntry("A hook to associate weapon-associated VFX was expanded to work in more cases."u8)
            .RegisterEntry("TMB resources now use a collection prefix to prevent retained state in some cases."u8)
            .RegisterEntry("Improved startup times a bit."u8)
            .RegisterEntry("Right-Click context menus for collections are now also ordered by name."u8)
            .RegisterEntry("Advanced Editing tabs have been reordered and renamed slightly."u8)
            .RegisterEntry("Added some validation of metadata changes to prevent stalling on load of bad IMC edits."u8)
            .RegisterEntry("Fixed an issue where collections could lose their configured inheritances during startup in some cases."u8)
            .RegisterEntry("Fixed some bugs when mods were removed from collection caches."u8)
            .RegisterEntry("Fixed some bugs with IMC files not correctly reverting to default values in some cases."u8)
            .RegisterEntry("Fixed an issue with the mod import popup not appearing (0.7.0.10)"u8)
            .RegisterEntry("Fixed an issue with the file selectors not always opening at the expected locations. (0.7.0.7)"u8)
            .RegisterEntry("Fixed some cache handling issues. (0.7.0.5 - 0.7.0.10)"u8)
            .RegisterEntry("Fixed an issue with multiple collection context menus appearing for some identifiers (0.7.0.5)"u8)
            .RegisterEntry(
                "Fixed an issue where the Update Bibo button did only work if the Advanced Editing window was opened before. (0.7.0.5)"u8);

    private static void Add7_0_4(Changelog log)
        => log.NextVersion("Version 0.7.0.4"u8)
            .RegisterEntry("Added options to the bulktag slash command to check all/local/mod tags specifically."u8)
            .RegisterEntry("Possibly improved handling of the delayed loading of individual assignments."u8)
            .RegisterEntry("Fixed a bug that caused metadata edits to apply even though mods were disabled."u8)
            .RegisterEntry("Fixed a bug that prevented material reassignments from working."u8)
            .RegisterEntry("Reverted trimming of whitespace for relative paths to only trim the end, not the start. (0.7.0.3)"u8)
            .RegisterEntry("Fixed a bug that caused an integer overflow on textures of high dimensions. (0.7.0.3)"u8)
            .RegisterEntry("Fixed a bug that caused Penumbra to enter invalid state when deleting mods. (0.7.0.2)"u8)
            .RegisterEntry("Added Notification on invalid collection names. (0.7.0.2)"u8);

    private static void Add7_0_1(Changelog log)
        => log.NextVersion("Version 0.7.0.1"u8)
            .RegisterEntry("Individual assignments can again be re-ordered by drag-and-dropping them."u8)
            .RegisterEntry("Relax the restriction of a maximum of 32 characters for collection names to 64 characters."u8)
            .RegisterEntry("Fixed a bug that showed the Your Character collection as redundant even if it was not."u8)
            .RegisterEntry("Fixed a bug that caused some required collection caches to not be built on startup and thus mods not to apply."u8)
            .RegisterEntry("Fixed a bug that showed the current collection as unused even if it was used."u8);

    private static void Add7_0_0(Changelog log)
        => log.NextVersion("Version 0.7.0.0"u8)
            .RegisterImportant(
                "The entire backend was reworked (this is still in progress). While this does not come with a lot of functionality changes, basically every file and functionality was touched."u8)
            .RegisterEntry(
                "This may have (re-)introduced some bugs that have not yet been noticed despite a long testing period - there are not many users of the testing branch."u8,
                1)
            .RegisterEntry("If you encounter any - but especially breaking or lossy - bugs, please report them immediately."u8, 1)
            .RegisterEntry("This also fixed or improved numerous bugs and issues that will not be listed here."u8,              1)
            .RegisterEntry("GitHub currently reports 321 changed files with 34541 additions and 28464 deletions."u8,            1)
            .RegisterEntry("Added Notifications on many failures that previously only wrote to log."u8)
            .RegisterEntry("Reworked the Collections Tab to hopefully be much more intuitive. It should be self-explanatory now."u8)
            .RegisterEntry("The tutorial was adapted to the new window, if you are unsure, maybe try restarting it."u8, 1)
            .RegisterEntry(
                "You can now toggle an incognito mode in the collection window so it shows shortened names of collections and players."u8, 1)
            .RegisterEntry(
                "You can get an overview about the current usage of a selected collection and its active and unused mod settings in the Collection Details panel."u8,
                1)
            .RegisterEntry("The currently selected collection is now highlighted in green (default, configurable) in multiple places."u8, 1)
            .RegisterEntry(
                "Mods now have a 'Collections' panel in the Mod Panel containing an overview about usage of the mod in all collections."u8)
            .RegisterEntry("The 'Changed Items' and 'Effective Changes' tab now contain a collection selector."u8)
            .RegisterEntry("Added the On-Screen tab to find what files a specific character is actually using (by Ny)."u8)
            .RegisterEntry("Added 3 Quick Move folders in the mod selector that can be setup in context menus for easier cleanup."u8)
            .RegisterEntry(
                "Added handling for certain animation files for mounts and fashion accessories to correctly associate them to players."u8)
            .RegisterEntry("The file selectors in the Advanced Mod Editing Window now use filterable combos."u8)
            .RegisterEntry(
                "The Advanced Mod Editing Window now shows the number of meta edits and file swaps in unselected options and highlights the option selector."u8)
            .RegisterEntry("Added API/IPC to start unpacking and installing mods from external tools (by Sebastina)."u8)
            .RegisterEntry("Hidden files and folders are now ignored for unused files in Advanced Mod Editing (by myr)"u8)
            .RegisterEntry("Paths in mods are now automatically trimmed of whitespace on loading."u8)
            .RegisterEntry("Fixed double 'by' in mod author display (by Caraxi)."u8)
            .RegisterEntry("Fixed a crash when trying to obtain names from the game data."u8)
            .RegisterEntry("Fixed some issues with tutorial windows."u8)
            .RegisterEntry("Fixed some bugs in the Resource Logger."u8)
            .RegisterEntry("Fixed Button Sizing for collapsible groups and several related bugs."u8)
            .RegisterEntry("Fixed issue with mods with default settings other than 0."u8)
            .RegisterEntry("Fixed issue with commands not registering on startup. (0.6.6.5)"u8)
            .RegisterEntry("Improved Startup Times and Time Tracking. (0.6.6.4)"u8)
            .RegisterEntry("Add Item Swapping between different types of Accessories and Hats. (0.6.6.4)"u8)
            .RegisterEntry("Fixed bugs with assignment of temporary collections and their deletion. (0.6.6.4)"u8)
            .RegisterEntry("Fixed bugs with new file loading mechanism. (0.6.6.2, 0.6.6.3)"u8)
            .RegisterEntry("Added API/IPC to open and close the main window and select specific tabs and mods. (0.6.6.2)"u8);

    private static void Add6_6_1(Changelog log)
        => log.NextVersion("Version 0.6.6.1"u8)
            .RegisterEntry("Added an option to make successful chat commands not print their success confirmations to chat."u8)
            .RegisterEntry("Fixed an issue with migration of old mods not working anymore (fixes Material UI problems)."u8)
            .RegisterEntry("Fixed some issues with using the Assign Current Player and Assign Current Target buttons."u8);

    private static void Add6_6_0(Changelog log)
        => log.NextVersion("Version 0.6.6.0"u8)
            .RegisterEntry(
                "Added new Collection Assignment Groups for Children NPC and Elderly NPC. Those take precedence before any non-individual assignments for any NPC using a child- or elderly model respectively."u8)
            .RegisterEntry(
                "Added an option to display Single Selection Groups as a group of radio buttons similar to Multi Selection Groups, when the number of available options is below the specified value. Default value is 2."u8)
            .RegisterEntry("Added a button in option groups to collapse the option list if it has more than 5 available options."u8)
            .RegisterEntry(
                "Penumbra now circumvents the games inability to read files at paths longer than 260 UTF16 characters and can also deal with generic unicode symbols in paths."u8)
            .RegisterEntry(
                "This means that Penumbra should no longer cause issues when files become too long or when there is a non-ASCII character in them."u8,
                1)
            .RegisterEntry(
                "Shorter paths are still better, so restrictions on the root directory have not been relaxed. Mod names should no longer replace non-ASCII symbols on import though."u8,
                1)
            .RegisterEntry(
                "Resource logging has been relegated to its own tab with better filtering. Please do not keep resource logging on arbitrarily or set a low record limit if you do, otherwise this eats a lot of performance and memory after a while."u8)
            .RegisterEntry(
                "Added a lot of facilities to edit the shader part of .mtrl files and .shpk files themselves in the Advanced Editing Tab (Thanks Ny and aers)."u8)
            .RegisterEntry(
                "Added splitting of Multi Selection Groups with too many options when importing .pmp files or adding mods via IPC."u8)
            .RegisterEntry("Discovery, Reloading and Unloading of a specified mod is now possible via HTTP API (Thanks Sebastina)."u8)
            .RegisterEntry("Cleaned up the HTTP API somewhat, removed currently useless options."u8)
            .RegisterEntry("Fixed an issue when extracting some textures."u8)
            .RegisterEntry("Fixed an issue with mannequins inheriting individual assignments for the current player when using ownership."u8)
            .RegisterEntry(
                "Fixed an issue with the resolving of .phyb and .sklb files for Item Swaps of head or body items with an EST entry but no unique racial model."u8);

    private static void Add6_5_2(Changelog log)
        => log.NextVersion("Version 0.6.5.2"u8)
            .RegisterEntry("Updated for game version 6.31 Hotfix."u8)
            .RegisterEntry(
                "Added option-specific descriptions for mods, instead of having just descriptions for groups of options. (Thanks Caraxi!)"u8)
            .RegisterEntry("Those are now accurately parsed from TTMPs, too."u8, 1)
            .RegisterEntry("Improved launch times somewhat through parallelization of some tasks."u8)
            .RegisterEntry(
                "Added some performance tracking for start-up durations and for real time data to Release builds. They can be seen and enabled in the Debug tab when Debug Mode is enabled."u8)
            .RegisterEntry("Fixed an issue with IMC changes and Mare Synchronos interoperability."u8)
            .RegisterEntry("Fixed an issue with housing mannequins crashing the game when resource logging was enabled."u8)
            .RegisterEntry("Fixed an issue generating Mip Maps for texture import on Wine."u8);

    private static void Add6_5_0(Changelog log)
        => log.NextVersion("Version 0.6.5.0"u8)
            .RegisterEntry("Fixed an issue with Item Swaps not using applied IMC changes in some cases."u8)
            .RegisterEntry("Improved error message on texture import when failing to create mip maps (slightly)."u8)
            .RegisterEntry("Tried to fix duty party banner identification again, also for the recommendation window this time."u8)
            .RegisterEntry("Added batched IPC to improve Mare performance."u8);

    private static void Add6_4_0(Changelog log)
        => log.NextVersion("Version 0.6.4.0"u8)
            .RegisterEntry("Fixed an issue with the identification of actors in the duty group portrait."u8)
            .RegisterEntry("Fixed some issues with wrongly cached actors and resources."u8)
            .RegisterEntry("Fixed animation handling after redraws (notably for PLD idle animations with a shield equipped)."u8)
            .RegisterEntry("Fixed an issue with collection listing API skipping one collection."u8)
            .RegisterEntry(
                "Fixed an issue with BGM files being sometimes loaded from other collections than the base collection, causing crashes."u8)
            .RegisterEntry(
                "Also distinguished file resolving for different file categories (improving performance) and disabled resolving for script files entirely."u8,
                1)
            .RegisterEntry("Some miscellaneous backend changes due to the Glamourer rework."u8);

    private static void Add6_3_0(Changelog log)
        => log.NextVersion("Version 0.6.3.0"u8)
            .RegisterEntry("Add an Assign Current Target button for individual assignments"u8)
            .RegisterEntry("Try identifying all banner actors correctly for PvE duties, Crystalline Conflict and Mahjong."u8)
            .RegisterEntry("Please let me know if this does not work for anything except identical twins."u8, 1)
            .RegisterEntry("Add handling for the 3 new screen actors (now 8 total, for PvE dutie portraits)."u8)
            .RegisterEntry("Update the Battle NPC name database for 6.3."u8)
            .RegisterEntry("Added API/IPC functions to obtain or set group or individual collections."u8)
            .RegisterEntry("Maybe fix a problem with textures sometimes not loading from their corresponding collection."u8)
            .RegisterEntry("Another try to fix a problem with the collection selectors breaking state."u8)
            .RegisterEntry("Fix a problem identifying companions."u8)
            .RegisterEntry("Fix a problem when deleting collections assigned to Groups."u8)
            .RegisterEntry(
                "Fix a problem when using the Assign Currently Played Character button and then logging onto a different character without restarting in between."u8)
            .RegisterEntry("Some miscellaneous backend changes."u8);

    private static void Add6_2_0(Changelog log)
        => log.NextVersion("Version 0.6.2.0"u8)
            .RegisterEntry("Update Penumbra for .net7, Dalamud API 8 and patch 6.3."u8)
            .RegisterEntry("Add a Bulktag chat command to toggle all mods with specific tags. (by SoyaX)"u8)
            .RegisterEntry("Add placeholder options for setting individual collections via chat command."u8)
            .RegisterEntry("Add toggles to swap left and/or right rings separately for ring item swap."u8)
            .RegisterEntry("Add handling for looping sound effects caused by animations in non-base collections."u8)
            .RegisterEntry("Add an option to not use any mods at all in the Inspect/Try-On window."u8)
            .RegisterEntry("Add handling for Mahjong actors."u8)
            .RegisterEntry("Improve hint text for File Swaps in Advanced Editing, also inverted file swap display order."u8)
            .RegisterEntry("Fix a problem where the collection selectors could get desynchronized after adding or deleting collections."u8)
            .RegisterEntry("Fix a problem that could cause setting state to get desynchronized."u8)
            .RegisterEntry("Fix an oversight where some special screen actors did not actually respect the settings made for them."u8)
            .RegisterEntry("Add collection and associated game object to Full Resource Logging."u8)
            .RegisterEntry("Add performance tracking for DEBUG-compiled versions (i.e. testing only)."u8)
            .RegisterEntry("Add some information to .mdl display and fix not respecting padding when reading them. (0.6.1.3)"u8)
            .RegisterEntry("Fix association of some vfx game objects. (0.6.1.3)"u8)
            .RegisterEntry("Stop forcing AVFX files to load synchronously. (0.6.1.3)"u8)
            .RegisterEntry("Fix an issue when incorporating deduplicated meta files. (0.6.1.2)"u8);

    private static void Add6_1_1(Changelog log)
        => log.NextVersion("Version 0.6.1.1"u8)
            .RegisterEntry(
                "Added a toggle to use all the effective changes from the entire currently selected collection for swaps, instead of the selected mod."u8)
            .RegisterEntry("Fix using equipment paths for accessory swaps and thus accessory swaps not working at all"u8)
            .RegisterEntry("Fix issues with swaps with gender-locked gear where the models for the other gender do not exist."u8)
            .RegisterEntry("Fix swapping universal hairstyles for midlanders breaking them for other races."u8)
            .RegisterEntry("Add some actual error messages on failure to create item swaps."u8)
            .RegisterEntry("Fix warnings about more than one affected item appearing for single items."u8);

    private static void Add6_1_0(Changelog log)
        => log.NextVersion("Version 0.6.1.0 (Happy New Year! Edition)"u8)
            .RegisterEntry("Add a prototype for Item Swapping."u8)
            .RegisterEntry("A new tab in Advanced Editing."u8,                                                                         1)
            .RegisterEntry("Swapping of Hair, Tail, Ears, Equipment and Accessories is supported. Weapons and Faces may be coming."u8, 1)
            .RegisterEntry("The manipulations currently in use by the selected mod with its currents settings (ignoring enabled state)"u8
              + " should be used when creating the swap, but you can also just swap unmodded things."u8, 1)
            .RegisterEntry("You can write a swap to a new mod, or to a new option in the currently selected mod."u8,                  1)
            .RegisterEntry("The swaps are not heavily tested yet, and may also be not perfectly efficient. Please leave feedback."u8, 1)
            .RegisterEntry("More detailed help or explanations will be added later."u8,                                               1)
            .RegisterEntry("Heavily improve Chat Commands. Use /penumbra help for more information."u8)
            .RegisterEntry("Penumbra now considers meta manipulations for Changed Items."u8)
            .RegisterEntry("Penumbra now tries to associate battle voices to specific actors, so that they work in collections."u8)
            .RegisterEntry(
                "Heavily improve .atex and .avfx handling, Penumbra can now associate VFX to specific actors far better, including ground effects."u8)
            .RegisterEntry("Improve some file handling for Mare-Interaction."u8)
            .RegisterEntry("Add Equipment Slots to Demihuman IMC Edits."u8)
            .RegisterEntry(
                "Add a toggle to keep metadata edits that apply the default value (and thus do not really change anything) on import from TexTools .meta files."u8)
            .RegisterEntry("Add an option to directly change the 'Wait For Plugins To Load'-Dalamud Option from Penumbra."u8)
            .RegisterEntry("Add API to copy mod settings from one mod to another."u8)
            .RegisterEntry("Fix a problem where creating individual collections did not trigger events."u8)
            .RegisterEntry("Add a Hack to support Anamnesis Redrawing better. (0.6.0.6)"u8)
            .RegisterEntry("Fix another problem with the aesthetician. (0.6.0.6)"u8)
            .RegisterEntry("Fix a problem with the export directory not being respected. (0.6.0.6)"u8);

    private static void Add6_0_5(Changelog log)
        => log.NextVersion("Version 0.6.0.5"u8)
            .RegisterEntry("Allow hyphen as last character in player and retainer names."u8)
            .RegisterEntry("Fix various bugs with ownership and GPose."u8)
            .RegisterEntry("Fix collection selectors not updating for new or deleted collections in some cases."u8)
            .RegisterEntry("Fix Chocobos not being recognized correctly."u8)
            .RegisterEntry("Fix some problems with UI actors."u8)
            .RegisterEntry("Fix problems with aesthetician again."u8);

    private static void Add6_0_2(Changelog log)
        => log.NextVersion("Version 0.6.0.2"u8)
            .RegisterEntry("Let Bell Retainer collections apply to retainer-named mannequins."u8)
            .RegisterEntry("Added a few informations to a help marker for new individual assignments."u8)
            .RegisterEntry("Fix bug with Demi Human IMC paths."u8)
            .RegisterEntry("Fix Yourself collection not applying to UI actors."u8)
            .RegisterEntry("Fix Yourself collection not applying during aesthetician."u8);

    private static void Add6_0_0(Changelog log)
        => log.NextVersion("Version 0.6.0.0"u8)
            .RegisterEntry("Revamped Individual Collections:"u8)
            .RegisterEntry("You can now specify individual collections for players (by name) of specific worlds or any world."u8, 1)
            .RegisterEntry("You can also specify NPCs (by grouped name and type of NPC), and owned NPCs (by specifying an NPC and a Player)."u8,
                1)
            .RegisterImportant(
                "Migration should move all current names that correspond to NPCs to the appropriate NPC group and all names that can be valid Player names to a Player of any world."u8,
                1)
            .RegisterImportant(
                "Please look through your Individual Collections to verify everything migrated correctly and corresponds to the game object you want. You might also want to change the 'Player (Any World)' collections to your specific homeworld."u8,
                1)
            .RegisterEntry("You can also manually sort your Individual Collections by drag and drop now."u8,                 1)
            .RegisterEntry("This new system is a pretty big rework, so please report any discrepancies or bugs you find."u8, 1)
            .RegisterEntry(
                "These changes made the specific ownership settings for Retainers and for preferring named over ownership obsolete."u8,
                1)
            .RegisterEntry(
                "General ownership can still be toggled and should apply in order of: Owned NPC > Owner (if enabled) > General NPC."u8,
                1)
            .RegisterEntry(
                "Added NPC Model Parsing, changes in NPC models should now display the names of the changed game objects for most NPCs."u8)
            .RegisterEntry("Changed Items now also display variant or subtype in addition to the model set ID where applicable."u8)
            .RegisterEntry("Collection selectors can now be filtered by name."u8)
            .RegisterEntry("Try to use Unicode normalization before replacing invalid path symbols on import for somewhat nicer paths."u8)
            .RegisterEntry("Improved interface for group settings (minimally)."u8)
            .RegisterEntry("New Special or Individual Assignments now default to your current Base assignment instead of None."u8)
            .RegisterEntry("Improved Support Info somewhat."u8)
            .RegisterEntry("Added Dye Previews for in-game dyes and dyeing templates in Material Editing."u8)
            .RegisterEntry("Colorset Editing now allows for negative values in all cases."u8)
            .RegisterEntry("Added Export buttons to .mdl and .mtrl previews in Advanced Editing."u8)
            .RegisterEntry("File Selection in the .mdl and .mtrl tabs now shows one associated game path by default and all on hover."u8)
            .RegisterEntry(
                "Added the option to reduplicate and normalize a mod, restoring all duplicates and moving the files to appropriate folders. (Duplicates Tab in Advanced Editing)"u8)
            .RegisterEntry(
                "Added an option to re-export metadata changes to TexTools-typed .meta and .rgsp files. (Meta-Manipulations Tab in Advanced Editing)"u8)
            .RegisterEntry("Fixed several bugs with the incorporation of meta changes when not done during TTMP import."u8)
            .RegisterEntry("Fixed a bug with RSP changes on non-base collections not applying correctly in some cases."u8)
            .RegisterEntry("Fixed a bug when dragging options during mod edit."u8)
            .RegisterEntry("Fixed a bug where sometimes the valid folder check caused issues."u8)
            .RegisterEntry("Fixed a bug where collections with inheritances were newly saved on every load."u8)
            .RegisterEntry("Fixed a bug where the /penumbra enable/disable command displayed the wrong message (functionality unchanged)."u8)
            .RegisterEntry("Mods without names or invalid mod folders are now warnings instead of errors."u8)
            .RegisterEntry("Added IPC events for mod deletion, addition or moves, and resolving based on game objects."u8)
            .RegisterEntry("Prevent a bug that allowed IPC to add Mods from outside the Penumbra root folder."u8)
            .RegisterEntry("A lot of big backend changes."u8);

    private static void Add5_11_1(Changelog log)
        => log.NextVersion("Version 0.5.11.1"u8)
            .RegisterEntry(
                "The 0.5.11.0 Update exposed an issue in Penumbras file-saving scheme that rarely could cause some, most or even all of your mods to lose their group information."u8)
            .RegisterEntry(
                "If this has happened to you, you will need to reimport affected mods, or manually restore their groups. I am very sorry for that."u8,
                1)
            .RegisterEntry(
                "I believe the problem is fixed with 0.5.11.1, but I can not be sure since it would occur only rarely. For the same reason, a testing build would not help (as it also did not with 0.5.11.0 itself)."u8,
                1)
            .RegisterImportant(
                "If you do encounter this or similar problems in 0.5.11.1, please immediately let me know in Discord so I can revert the update again."u8,
                1);

    private static void Add5_11_0(Changelog log)
        => log.NextVersion("Version 0.5.11.0"u8)
            .RegisterEntry(
                "Added local data storage for mods in the plugin config folder. This information is not exported together with your mod, but not dependent on collections."u8)
            .RegisterEntry("Moved the import date from mod metadata to local data."u8,                   1)
            .RegisterEntry("Added Favorites. You can declare mods as favorites and filter for them."u8,  1)
            .RegisterEntry("Added Local Tags. You can apply custom Tags to mods and filter for them."u8, 1)
            .RegisterEntry(
                "Added Mod Tags. Mod Creators (and the Edit Mod tab) can set tags that are stored in the mod meta data and are thus exported."u8)
            .RegisterEntry("Add backface and transparency toggles to .mtrl editing, as well as a info section."u8)
            .RegisterEntry("Meta Manipulation editing now highlights if the selected ID is 0 or 1."u8)
            .RegisterEntry("Fixed a bug when manually adding EQP or EQDP entries to Mods."u8)
            .RegisterEntry("Updated some tooltips and hints."u8)
            .RegisterEntry("Improved handling of IMC exception problems."u8)
            .RegisterEntry("Fixed a bug with misidentification of equipment decals."u8)
            .RegisterEntry(
                "Character collections can now be set via chat command, too. (/penumbra collection character <collection name> | <character name>)"u8)
            .RegisterEntry("Backend changes regarding API/IPC, consumers can but do not need to use the Penumbra.Api library as a submodule."u8)
            .RegisterEntry("Added API to delete mods and read and set their pseudo-filesystem paths."u8, 1)
            .RegisterEntry("Added API to check Penumbras enabled state and updates to it."u8,            1);

    private static void Add5_10_0(Changelog log)
        => log.NextVersion("Version 0.5.10.0"u8)
            .RegisterEntry("Renamed backup functionality to export functionality."u8)
            .RegisterEntry("A default export directory can now optionally be specified."u8)
            .RegisterEntry("If left blank, exports will still be stored in your mod directory."u8, 1)
            .RegisterEntry("Existing exports corresponding to existing mods will be moved automatically if the export directory is changed."u8,
                1)
            .RegisterEntry("Added buttons to export and import all color set rows at once during material editing."u8)
            .RegisterEntry("Fixed texture import being case sensitive on the extension."u8)
            .RegisterEntry("Fixed special collection selector increasing in size on non-default UI styling."u8)
            .RegisterEntry("Fixed color set rows not importing the dye values during material editing."u8)
            .RegisterEntry("Other miscellaneous small fixes."u8);

    private static void Add5_9_0(Changelog log)
        => log.NextVersion("Version 0.5.9.0"u8)
            .RegisterEntry("Special Collections are now split between male and female."u8)
            .RegisterEntry("Fix a bug where the Base and Interface Collection were set to None instead of Default on a fresh install."u8)
            .RegisterEntry("Fix a bug where cutscene actors were not properly reset and could be misidentified across multiple cutscenes."u8)
            .RegisterEntry("TexTools .meta and .rgsp files are now incorporated based on file- and game path extensions."u8);

    private static void Add5_8_7(Changelog log)
        => log.NextVersion("Version 0.5.8.7"u8)
            .RegisterEntry("Fixed some problems with metadata reloading and reverting and IMC files. (5.8.1 to 5.8.7)."u8)
            .RegisterImportant(
                "If you encounter any issues, please try completely restarting your game after updating (not just relogging), before reporting them."u8,
                1);

    private static void Add5_8_0(Changelog log)
        => log.NextVersion("Version 0.5.8.0"u8)
            .RegisterEntry("Added choices what Change Logs are to be displayed. It is recommended to just keep showing all."u8)
            .RegisterEntry("Added an Interface Collection assignment."u8)
            .RegisterEntry("All your UI mods will have to be in the interface collection."u8,                                           1)
            .RegisterEntry("Files that are categorized as UI files by the game will only check for redirections in this collection."u8, 1)
            .RegisterImportant(
                "Migration should have set your currently assigned Base Collection to the Interface Collection, please verify that."u8, 1)
            .RegisterEntry("New API / IPC for the Interface Collection added."u8, 1)
            .RegisterImportant("API / IPC consumers should verify whether they need to change resolving to the new collection."u8, 1)
            .RegisterImportant(
                "If other plugins are not using your interface collection yet, you can just keep Interface and Base the same collection for the time being."u8)
            .RegisterEntry(
                "Mods can now have default settings for each option group, that are shown while the mod is unconfigured and taken as initial values when configured."u8)
            .RegisterEntry("Default values are set when importing .ttmps from their default values, and can be changed in the Edit Mod tab."u8,
                1)
            .RegisterEntry("Files that the game loads super early should now be replaceable correctly via base or interface collection."u8)
            .RegisterEntry(
                "The 1.0 neck tattoo file should now be replaceable, even in character collections. You can also replace the transparent texture used instead. (This was ugly.)"u8)
            .RegisterEntry("Continued Work on the Texture Import/Export Tab:"u8)
            .RegisterEntry("Should work with lot more texture types for .dds and .tex files, most notably BC7 compression."u8, 1)
            .RegisterEntry("Supports saving .tex and .dds files in multiple texture types and generating MipMaps for them."u8, 1)
            .RegisterEntry("Interface reworked a bit, gives more information and the overlay side can be collapsed."u8,        1)
            .RegisterImportant(
                "May contain bugs or missing safeguards. Generally let me know what's missing, ugly, buggy, not working or could be improved. Not really feasible for me to test it all."u8,
                1)
            .RegisterEntry(
                "Added buttons for redrawing self or all as well as a tooltip to describe redraw options and a tutorial step for it."u8)
            .RegisterEntry("Collection Selectors now display None at the top if available."u8)
            .RegisterEntry(
                "Adding mods via API/IPC will now cause them to incorporate and then delete TexTools .meta and .rgsp files automatically."u8)
            .RegisterEntry("Fixed an issue with Actor 201 using Your Character collections in cutscenes."u8)
            .RegisterEntry("Fixed issues with and improved mod option editing."u8)
            .RegisterEntry(
                "Fixed some issues with and improved file redirection editing - you are now informed if you can not add a game path (because it is invalid or already in use)."u8)
            .RegisterEntry("Backend optimizations."u8)
            .RegisterEntry("Changed metadata change system again."u8, 1)
            .RegisterEntry("Improved logging efficiency."u8,          1);

    private static void Add5_7_1(Changelog log)
        => log.NextVersion("Version 0.5.7.1"u8)
            .RegisterEntry("Fixed the Changelog window not considering UI Scale correctly."u8)
            .RegisterEntry("Reworked Changelog display slightly."u8);

    private static void Add5_7_0(Changelog log)
        => log.NextVersion("Version 0.5.7.0"u8)
            .RegisterEntry("Added a Changelog!"u8)
            .RegisterEntry("Files in the UI category will no longer be deduplicated for the moment."u8)
            .RegisterImportant("If you experience UI-related crashes, please re-import your UI mods."u8, 1)
            .RegisterEntry("This is a temporary fix against those not-yet fully understood crashes and may be reworked later."u8, 1)
            .RegisterImportant(
                "There is still a possibility of UI related mods crashing the game, we are still investigating - they behave very weirdly. If you continue to experience crashing, try disabling your UI mods."u8,
                1)
            .RegisterEntry(
                "On import, Penumbra will now show files with extensions '.ttmp', '.ttmp2' and '.pmp'. You can still select showing generic archive files."u8)
            .RegisterEntry(
                "Penumbra Mod Pack ('.pmp') files are meant to be renames of any of the archive types that could already be imported that contain the necessary Penumbra meta files."u8,
                1)
            .RegisterImportant(
                "If you distribute any mod as an archive specifically for Penumbra, you should change its extension to '.pmp'. Supported base archive types are ZIP, 7-Zip and RAR."u8,
                1)
            .RegisterEntry("Penumbra will now save mod backups with the file extension '.pmp'. They still are regular ZIP files."u8, 1)
            .RegisterEntry(
                "Existing backups in your current mod directory should be automatically renamed. If you manage multiple mod directories, you may need to migrate the other ones manually."u8,
                1)
            .RegisterEntry("Fixed assigned collections not working correctly on adventurer plates."u8)
            .RegisterEntry("Fixed a wrongly displayed folder line in some circumstances."u8)
            .RegisterEntry("Fixed crash after deleting mod options."u8)
            .RegisterEntry("Fixed Inspect Window collections not working correctly."u8)
            .RegisterEntry("Made identically named options selectable in mod configuration. Do not name your options identically."u8)
            .RegisterEntry("Added some additional functionality for Mare Synchronos."u8);

    #endregion

    private static void AddDummy(Changelog log)
        => log.NextVersion(""u8);

    private (int, ChangeLogDisplayType) ConfigData()
        => (_config.Ephemeral.LastSeenVersion, _config.Main.ChangeLogDisplayType);

    private void Save(int version, ChangeLogDisplayType type)
    {
        _config.Ephemeral.LastSeenVersion = version;
        _config.Main.ChangeLogDisplayType = type;
    }
}

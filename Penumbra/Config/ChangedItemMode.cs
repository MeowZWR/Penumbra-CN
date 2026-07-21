using ImSharp;
using Luna.Generators;

namespace Penumbra;

[NamedEnum(Unknown: "错误")]
[TooltipEnum]
public enum ChangedItemMode
{
    [Name("分组（折叠）")]
    [Tooltip("按模型和槽位将项目分组显示。默认情况下将这些组折叠为单个项目。优先选择受更多更改影响或已配置的物品作为主项目。")]
    GroupedCollapsed,

    [Name("分组（展开）")]
    [Tooltip("按模型和槽位将项目分组显示。默认情况下展开这些组以显示所有项目。优先选择受更多更改影响或已配置的物品作为主项目。")]
    GroupedExpanded,

    [Name("按字母顺序")]
    [Tooltip("按字母顺序显示所有更改项目。")]
    Alphabetical,
}

public static partial class ChangedItemModeExtensions
{
    private static readonly ChangedItemModeCombo Combo = new();

    private sealed class ChangedItemModeCombo() : SimpleFilterCombo<ChangedItemMode>(SimpleFilterType.Text)
    {
        public override StringU8 DisplayString(in ChangedItemMode value)
            => new(value.ToNameU8());

        public override string FilterString(in ChangedItemMode value)
            => value.ToName();

        public override StringU8 Tooltip(in ChangedItemMode value)
            => new(value.Tooltip());

        public override IEnumerable<ChangedItemMode> GetBaseItems()
            => ChangedItemMode.Values;
    }

    public static bool DrawCombo(ReadOnlySpan<byte> label, ChangedItemMode value, float width, Action<ChangedItemMode> setter)
    {
        if (!Combo.Draw(label, ref value, StringU8.Empty, width))
            return false;

        setter(value);
        return true;
    }
}

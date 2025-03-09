using ImGuiNET;
using OtterGui.Text;

namespace Penumbra;

public enum ChangedItemMode
{
    GroupedCollapsed,
    GroupedExpanded,
    Alphabetical,
}

public static class ChangedItemModeExtensions
{
    public static ReadOnlySpan<byte> ToName(this ChangedItemMode mode)
        => mode switch
        {
            ChangedItemMode.GroupedCollapsed => "分组（折叠）"u8,
            ChangedItemMode.GroupedExpanded  => "分组（展开）"u8,
            ChangedItemMode.Alphabetical     => "按字母顺序"u8,
            _                                => "错误"u8,
        };

    public static ReadOnlySpan<byte> ToTooltip(this ChangedItemMode mode)
        => mode switch
        {
            ChangedItemMode.GroupedCollapsed =>
                "按模型和槽位将项目分组显示。默认情况下将这些组折叠为单个项目。优先选择受更多更改影响或已配置的物品作为主项目。"u8,
            ChangedItemMode.GroupedExpanded =>
                "按模型和槽位将项目分组显示。默认情况下展开这些组以显示所有项目。优先选择受更多更改影响或已配置的物品作为主项目。"u8,
            ChangedItemMode.Alphabetical => "按字母顺序显示所有更改项目。"u8,
            _                            => ""u8,
        };

    public static bool DrawCombo(ReadOnlySpan<byte> label, ChangedItemMode value, float width, Action<ChangedItemMode> setter)
    {
        ImGui.SetNextItemWidth(width);
        using var combo = ImUtf8.Combo(label, value.ToName());
        if (!combo)
            return false;

        var ret = false;
        foreach (var newValue in Enum.GetValues<ChangedItemMode>())
        {
            var selected = ImUtf8.Selectable(newValue.ToName(), newValue == value);
            if (selected)
            {
                ret = true;
                setter(newValue);
            }

            ImUtf8.HoverTooltip(newValue.ToTooltip());
        }

        return ret;
    }
}

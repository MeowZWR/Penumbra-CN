using ImSharp;
using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Settings;

public sealed class LayoutEditPopup(ModManager mods) : ObjectEditPopup, IUiService
{
    private readonly ParentCombo _parentCombo = new(mods);

    public void Open(IModObject @object)
        => Open((object)@object);

    protected override ReadOnlySpan<byte> PopupId
        => "LayoutEdit"u8;

    private void DrawGroup(IModGroup group)
    {
        DrawIdentifier(group);
        _parentCombo.Draw("父级设置"u8, group, ImEx.GuidInputWidth + Im.Style.ItemInnerSpacing.X + Im.Style.FrameHeight);
        var layout = group.Layout;
        if (Im.Checkbox("条件未满足时隐藏"u8, ref layout, ModSettingsLayout.Hide))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover(
            "勾选后，当条件未满足时，此组将完全隐藏，而不仅仅是禁用。"u8);
        if (Im.Checkbox("默认折叠"u8, ref layout, ModSettingsLayout.DefaultClosed))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover(
            "勾选后，若此组以可折叠标题显示，标题将默认收起而不是默认展开。"u8);
        if (Im.Checkbox("添加间距"u8, ref layout, ModSettingsLayout.Space))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover(
            "勾选后，无论组是否展开，都会在绘制完毕后插入空行。"u8);
        if (Im.Checkbox("挂靠到父级时隐藏组名称"u8, ref layout, ModSettingsLayout.ParentHeader))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover(
            "勾选后，若此组挂靠在父组或父选项下，将只显示其选项而不显示组标题。"u8);
    }

    private void DrawIdentifier(IModObject @object)
    {
        Guid? guid = @object.Id;
        if (ImEx.GuidInput("##guid"u8, ref guid) && guid.HasValue)
            mods.OptionEditor.ForceIdentifier(@object, guid.Value);
        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.RefreshIcon, "为此对象设置一个新的 GUID。"u8))
            mods.OptionEditor.ForceIdentifier(@object, Guid.NewGuid());
        Im.Line.SameInner();
        ImEx.TextFrameAligned("标识符（GUID）"u8);
    }

    private void DrawOption(IModOption option)
    {
        DrawIdentifier(option);
        DrawColorCombo(option);
        var layout = option.Layout;
        if (option is SingleSubMod)
        {
            using (Im.Disabled())
            {
                Im.Checkbox("条件未满足时禁用"u8, true);
            }

            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled,
                "单选选项在条件未满足时始终禁用而非隐藏。"u8);

            if (Im.Checkbox("添加分隔线"u8, ref layout, ModSettingsLayout.Separator))
                mods.OptionEditor.SetLayout(option, layout);
            Im.Tooltip.OnHover(
                "勾选后，若此选项显示在下拉框中，将在其下方绘制分隔线。"u8);
        }
        else
        {
            if (Im.Checkbox("条件未满足时隐藏"u8, ref layout, ModSettingsLayout.Hide))
                mods.OptionEditor.SetLayout(option, layout);
            Im.Tooltip.OnHover(
                "勾选后，当条件未满足时，此选项将完全隐藏，而不仅仅是禁用。"u8);
        }

        if (Im.Checkbox("添加间距"u8, ref layout, ModSettingsLayout.Space))
            mods.OptionEditor.SetLayout(option, layout);
        Im.Tooltip.OnHover(
            "勾选后，若不在下拉框内显示，将在此选项及其所有子项绘制完毕后插入空行。"u8);

        if (Im.Checkbox("隐藏选项标签（单行）"u8, ref layout, ModSettingsLayout.HideOptionLabel))
            mods.OptionEditor.SetLayout(option, layout);
        Im.Tooltip.OnHover(
            "勾选后，若此选项是与组标签同一行的单个复选框，将只显示复选框，而不显示选项名称或描述作为标签。"u8);
    }

    protected override void DrawInternal()
    {
        switch (Current)
        {
            case IModGroup group:   DrawGroup(group); break;
            case IModOption option: DrawOption(option); break;
        }
    }

    private static readonly IReadOnlyList<StringU8> ColorNames =
    [
        new("默认"u8),
        new("选项颜色 1"u8),
        new("选项颜色 2"u8),
        new("选项颜色 3"u8),
        new("选项颜色 4"u8),
        new("选项颜色 5"u8),
        new("选项颜色 6"u8),
        new("选项颜色 7"u8),
        new("选项颜色 8"u8),
    ];

    private void DrawColorCombo(IModOption option)
    {
        var       name  = ColorNames[option.ColorAsInteger];
        var       color = option.Color is 0 ? ColorParameter.Default : option.Color.Value;
        ImGuiId   popupId;
        Rectangle bb;
        using (ImGuiColor.Text.Push(color))
        {
            Im.Item.SetNextWidth(ImEx.GuidInputWidth + Im.Style.ItemInnerSpacing.X + Im.Style.FrameHeight);
            Im.Combo.DrawPreview("##Color"u8, name, out popupId, out bb, ComboFlags.HeightLargest);
        }

        Im.Tooltip.OnHover(
            "请注意这些颜色可由用户自行配置，此处预览仅显示你当前配置的颜色，可能与其他用户的配置不同。"u8);

        ImEx.TextLabel("颜色"u8);
        DrawColorPopup(option, popupId, bb);
    }

    private void DrawColorPopup(IModOption option, ImGuiId id, in Rectangle boundingBox)
    {
        using var popup = Im.Combo.DrawPopup(id, boundingBox, ComboFlags.HeightLargest);
        if (!popup)
            return;

        for (var i = 0; i < ColorNames.Count; ++i)
        {
            var       tmpName  = ColorNames[i];
            var       tmpValue = IModOption.ConvertColor(i);
            var       tmpColor = i is 0 ? ColorParameter.Default : tmpValue.Value;
            using var c        = ImGuiColor.Text.Push(tmpColor);
            if (Im.Selectable(tmpName, tmpValue == option.Color))
                mods.OptionEditor.SetColor(option, i);
        }
    }
}

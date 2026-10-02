using ImSharp;
using Luna;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Structs;

namespace Penumbra.UI.FileEditing.Materials;

public partial class MaterialEditor
{
    internal ColorTableRowPresets? RowPresets { private get; init; }

    /// <summary> Row values before the first preset was applied since the last save, and the name of the last applied preset. </summary>
    private readonly Dictionary<int, (ColorTableRow Row, string Name)> _rowPresetUndo = [];

    private string _rowPresetNewName = string.Empty;

    private bool DrawRowPresetHeader(int rowIdx, bool disabled)
    {
        if (RowPresets is null || Mtrl.Table is not ColorTable table)
            return false;

        var titleWidth = Im.Font.CalculateSize("32B"u8).X + Im.Style.FramePadding.X * 2.0f;
        ImEx.TextFramed($"{(rowIdx >> 1) + 1}{"AB"[rowIdx & 1]}", new Vector2(titleWidth, Im.Style.FrameHeight), ImGuiColor.Header);

        Im.Line.SameInner();
        Im.Item.SetNextWidth(Im.ContentRegion.Available.X - 2.0f * (Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X));
        var ret = DrawRowPresetCombo(table, rowIdx, disabled);
        Im.Line.SameInner();
        DrawRowPresetSaveButton(table, rowIdx);
        Im.Line.SameInner();
        ret |= DrawRowPresetUndoButton(table, rowIdx, disabled);
        return ret;
    }

    private bool DrawRowPresetCombo(ColorTable table, int rowIdx, bool disabled)
    {
        var applied = _rowPresetUndo.TryGetValue(rowIdx, out var undo) ? undo.Name : null;
        using var combo = Im.Combo.Begin("##rowPreset"u8, applied ?? "应用行预设...");
        if (!combo)
        {
            Im.Tooltip.OnHover("将预设参数应用到此行（不含染色设置）。\n\n悬停预设可预览参数，右键点击预设可部分应用、重命名、覆盖或删除。\n"u8
              + "来自其他着色器包的预设会以警告色显示。"u8);
            return false;
        }

        var presets = RowPresets!.Presets;
        if (presets.Count is 0)
        {
            Im.TextDisabled("暂无预设，点击右侧的保存按钮将当前行保存为预设。"u8);
            return false;
        }

        var ret = false;
        for (var i = 0; i < presets.Count; ++i)
        {
            using var id       = Im.Id.Push(i);
            var       preset   = presets[i];
            var       mismatch = IsRowPresetMismatch(preset);
            bool      clicked;
            using (ImGuiColor.Text.Push(DalamudColor.WarningForeground.Value, mismatch))
            {
                clicked = Im.Selectable(preset.Name, preset.Name == applied);
            }

            if (clicked && !disabled)
                ret |= ApplyRowPreset(table, rowIdx, preset, AllRowValues);
            if (Im.Item.Hovered())
                DrawRowPresetTooltip(preset, table[rowIdx], Mtrl.ShaderPackage.Name);
            if (DrawRowPresetContext(table, rowIdx, i, disabled, ref ret))
                break;
        }

        return ret;
    }

    private const IColorTable.ValueTypes AllRowValues = (IColorTable.ValueTypes)ulong.MaxValue;

    private bool IsRowPresetMismatch(ColorTableRowPresets.Preset preset)
        => preset.ShaderPackage.Length > 0 && preset.ShaderPackage != Mtrl.ShaderPackage.Name;

    private bool ApplyRowPreset(ColorTable table, int rowIdx, ColorTableRowPresets.Preset preset, IColorTable.ValueTypes which)
    {
        var original = _rowPresetUndo.TryGetValue(rowIdx, out var undo) ? undo.Row : table[rowIdx];
        _rowPresetUndo[rowIdx] = (original, preset.Name);
        var row = table[rowIdx];
        ((IColorTable)table).MergeSpecificValues(row, preset.Row, which);
        if (table[rowIdx] == row)
            return false;

        table[rowIdx] = row;
        return true;
    }

    /// <returns> Whether the preset list changed in a way that invalidates indices. </returns>
    private bool DrawRowPresetContext(ColorTable table, int rowIdx, int presetIdx, bool disabled, ref bool changed)
    {
        using var context = Im.Popup.BeginContextItem("context"u8);
        if (!context)
            return false;

        var preset = RowPresets!.Presets[presetIdx];
        using (Im.Disabled(disabled))
        {
            if (Im.Selectable("仅应用颜色"u8))
                changed |= ApplyRowPreset(table, rowIdx, preset, IColorTable.ValueTypes.Colors);
            if (Im.Selectable("仅应用其他值"u8))
                changed |= ApplyRowPreset(table, rowIdx, preset, ~IColorTable.ValueTypes.Colors);
        }

        Im.Separator();
        Im.Item.SetNextWidth(200 * Im.Style.GlobalScale);
        if (ImEx.InputOnDeactivation.Text("##rename"u8, preset.Name, out string newName) && newName != preset.Name)
            RowPresets.Rename(presetIdx, newName);
        Im.Tooltip.OnHover("输入新名称以重命名此预设，名称不可重复。"u8);

        if (Im.Selectable("用当前行覆盖此预设"u8))
            RowPresets.Set(preset.Name, table[rowIdx], Mtrl.ShaderPackage.Name);

        var deleted = false;
        using (Im.Disabled(!Im.Io.KeyControl))
        {
            if (Im.Selectable("删除此预设"u8))
            {
                RowPresets.Delete(presetIdx);
                deleted = true;
            }
        }

        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "按住 Ctrl 键以删除。"u8);
        return deleted;
    }

    private void DrawRowPresetSaveButton(ColorTable table, int rowIdx)
    {
        if (ImEx.Icon.Button(LunaStyle.SaveIcon, "将此行的参数保存为预设。"u8))
        {
            _rowPresetNewName = string.Empty;
            Im.Popup.Open("savePreset"u8);
        }

        using var popup = Im.Popup.Begin("savePreset"u8);
        if (!popup)
            return;

        if (Im.Window.Appearing)
            Im.Keyboard.SetFocusHere();
        Im.Item.SetNextWidth(200 * Im.Style.GlobalScale);
        var enter  = Im.Input.Text("##presetName"u8, ref _rowPresetNewName, "预设名称"u8, InputTextFlags.EnterReturnsTrue);
        var name   = _rowPresetNewName.Trim();
        var exists = RowPresets!.IndexOf(name) >= 0;
        Im.Line.SameInner();
        var clicked = ImEx.Button(exists ? "覆盖"u8 : "保存"u8, Vector2.Zero,
            exists ? "已存在同名预设，将用此行的参数覆盖它。"u8 : "保存为新预设。"u8, name.Length is 0);
        if ((clicked || enter) && name.Length > 0)
        {
            RowPresets.Set(name, table[rowIdx], Mtrl.ShaderPackage.Name);
            Im.Popup.CloseCurrent();
        }
    }

    private bool DrawRowPresetUndoButton(ColorTable table, int rowIdx, bool disabled)
    {
        var hasUndo = _rowPresetUndo.TryGetValue(rowIdx, out var undo);
        if (!ImEx.Icon.Button(LunaStyle.UndoIcon,
                hasUndo
                    ? $"撤销预设覆盖，将此行恢复为应用预设「{undo.Name}」之前的参数。\n\n应用预设后的手动修改也会被还原。"
                    : "此行没有可撤销的预设覆盖。\n\n保存到文件后将无法撤销。",
                disabled || !hasUndo))
            return false;

        _rowPresetUndo.Remove(rowIdx);
        if (table[rowIdx] == undo.Row)
            return false;

        table[rowIdx] = undo.Row;
        return true;
    }

    private static void DrawRowPresetTooltip(ColorTableRowPresets.Preset preset, in ColorTableRow current, string shaderPackage)
    {
        using var tt     = Im.Tooltip.Begin();
        var       row    = preset.Row;
        var       offset = Im.Font.CalculateSize("镜面反射颜色"u8).X + Im.Style.ItemSpacing.X * 2.0f;

        Im.Text(preset.Name);
        Im.TextDisabled(preset.ShaderPackage.Length > 0 ? $"来源着色器包：{preset.ShaderPackage}" : "来源着色器包：未知");
        if (preset.ShaderPackage.Length > 0 && preset.ShaderPackage != shaderPackage)
            Im.Text($"与当前材质的着色器包（{shaderPackage}）不同，部分参数可能不生效或含义不同。",
                DalamudColor.WarningForeground.Value);
        Im.Separator();
        RowPresetColor("漫反射颜色"u8,  offset, row.DiffuseColor,  current.DiffuseColor);
        RowPresetColor("镜面反射颜色"u8, offset, row.SpecularColor, current.SpecularColor);
        RowPresetColor("自发光颜色"u8,  offset, row.EmissiveColor, current.EmissiveColor);
        RowPresetValue("曝光值"u8, offset, row.Exposure == current.Exposure,
            row.Exposure == Half.Zero ? "-∞" : $"{MathF.Log2((float)row.Exposure) * 0.5f:F1}");
        RowPresetValue("粗糙度"u8,   offset, row.Roughness == current.Roughness,     Percent(row.Roughness));
        RowPresetValue("金属度"u8,   offset, row.Metalness == current.Metalness,     Percent(row.Metalness));
        RowPresetValue("光泽"u8,    offset, row.SheenRate == current.SheenRate,     Percent(row.SheenRate));
        RowPresetValue("光泽色调"u8,  offset, row.SheenTintRate == current.SheenTintRate, Percent(row.SheenTintRate));
        RowPresetValue("光泽粗糙度"u8, offset, row.SheenAperture == current.SheenAperture,
            $"{100.0f / (float)row.SheenAperture:F0}%");
        RowPresetValue("各向异性度"u8, offset, row.Anisotropy == current.Anisotropy, $"{(float)row.Anisotropy:F1}");
        RowPresetValue("着色器 ID"u8, offset, row.ShaderId == current.ShaderId, $"{row.ShaderId}");
        RowPresetValue("滚动参数"u8,  offset, row.Scalar23 == current.Scalar23, $"{(ushort)row.Scalar23}");
        RowPresetValue("球面贴图"u8,  offset, row.SphereMapIndex == current.SphereMapIndex && row.SphereMapMask == current.SphereMapMask,
            $"#{row.SphereMapIndex}，强度 {Percent(row.SphereMapMask)}");
        RowPresetValue("平铺"u8, offset, row.TileIndex == current.TileIndex && row.TileAlpha == current.TileAlpha,
            $"#{row.TileIndex}，透明度 {Percent(row.TileAlpha)}");
        row.TileTransform.Decompose(out var scale, out var rotation, out var shear);
        RowPresetValue("平铺变换"u8, offset, row.TileTransform == current.TileTransform,
            $"缩放 {scale.X:F2} × {scale.Y:F2}，旋转 {rotation * 180.0f / MathF.PI:F0}°，剪切 {shear * 180.0f / MathF.PI:F0}°");
        Im.Separator();
        Im.TextDisabled("灰色数值与当前行相同。"u8);
    }

    private static string Percent(Half value)
        => $"{(float)value * 100.0f:F0}%";

    private static void RowPresetValue(ReadOnlySpan<byte> label, float offset, bool same, string value)
    {
        Im.Text(label);
        Im.Line.Same(offset);
        Im.Text(value, same ? ImGuiColor.TextDisabled.Get() : ImGuiColor.Text.Get());
    }

    private static void RowPresetColor(ReadOnlySpan<byte> label, float offset, HalfColor value, HalfColor current)
    {
        Im.Text(label);
        Im.Line.Same(offset);
        Im.Dummy(new Vector2(Im.Style.TextHeight));
        CtColorRect(Im.Item.UpperLeftCorner, Im.Item.LowerRightCorner, PseudoSqrtRgb((Vector3)value));
        Im.Line.SameInner();
        Im.Text($"{(float)value.Red:F2}, {(float)value.Green:F2}, {(float)value.Blue:F2}",
            value == current ? ImGuiColor.TextDisabled.Get() : ImGuiColor.Text.Get());
    }
}

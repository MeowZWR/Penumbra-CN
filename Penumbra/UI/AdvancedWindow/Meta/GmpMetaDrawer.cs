using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class GmpMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<GmpIdentifier, GmpEntry>(editor, metaFiles), IService
{
    public override ReadOnlySpan<byte> Label
        => "面甲/可调整头部装备编辑(GMP)###GMP"u8;

    public override int NumColumns
        => 7;

    protected override void Initialize()
    {
        Identifier = new GmpIdentifier(1);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = ExpandedGmpFile.GetDefault(MetaFiles, Identifier.SetId);

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("将当前所有GMP操作复制到剪贴板。"u8, MetaDictionary.SerializeTo([], Editor.Gmp));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "编辑此项。"u8 : "此项已被编辑。"u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, Entry);

        if (DrawIdentifierInput(ref Identifier))
            UpdateEntry();

        DrawEntry(Entry, ref Entry, true);
    }

    protected override void DrawEntry(GmpIdentifier identifier, GmpEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        var defaultEntry = ExpandedGmpFile.GetDefault(MetaFiles, identifier.SetId);
        if (DrawEntry(defaultEntry, ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(GmpIdentifier, GmpEntry)> Enumerate()
        => Editor.Gmp
            .OrderBy(kvp => kvp.Key.SetId.Id)
            .Select(kvp => (kvp.Key, kvp.Value));

    protected override int Count
        => Editor.Gmp.Count;

    private static bool DrawIdentifierInput(ref GmpIdentifier identifier)
    {
        ImGui.TableNextColumn();
        return DrawPrimaryId(ref identifier);
    }

    private static void DrawIdentifier(GmpIdentifier identifier)
    {
        ImGui.TableNextColumn();
        ImUtf8.TextFramed($"{identifier.SetId.Id}", FrameColor);
        ImUtf8.HoverTooltip("模型集合ID"u8);
    }

    private static bool DrawEntry(GmpEntry defaultEntry, ref GmpEntry entry, bool disabled)
    {
        using var dis = ImRaii.Disabled(disabled);
        ImGui.TableNextColumn();
        var changes = false;
        if (Checkmark("##gmpEnabled"u8, "启用装置", entry.Enabled, defaultEntry.Enabled, out var enabled))
        {
            entry   = entry with { Enabled = enabled };
            changes = true;
        }

        ImGui.TableNextColumn();
        if (Checkmark("##gmpAnimated"u8, "装置动画", entry.Animated, defaultEntry.Animated, out var animated))
        {
            entry   = entry with { Animated = animated };
            changes = true;
        }

        var rotationWidth = 75 * ImUtf8.GlobalScale;
        ImGui.TableNextColumn();
        if (DragInput("##gmpRotationA"u8, "A方向旋转角度值"u8, rotationWidth, entry.RotationA, defaultEntry.RotationA, out var rotationA,
                (ushort)0,                (ushort)360,               0.05f,         !disabled))
        {
            entry   = entry with { RotationA = rotationA };
            changes = true;
        }

        ImUtf8.SameLineInner();
        if (DragInput("##gmpRotationB"u8, "B方向旋转角度值"u8, rotationWidth, entry.RotationB, defaultEntry.RotationB, out var rotationB,
                (ushort)0,                (ushort)360,               0.05f,         !disabled))
        {
            entry   = entry with { RotationB = rotationB };
            changes = true;
        }

        ImUtf8.SameLineInner();
        if (DragInput("##gmpRotationC"u8, "C方向旋转角度值"u8, rotationWidth, entry.RotationC, defaultEntry.RotationC, out var rotationC,
                (ushort)0,                (ushort)360,               0.05f,         !disabled))
        {
            entry   = entry with { RotationC = rotationC };
            changes = true;
        }

        var unkWidth = 50 * ImUtf8.GlobalScale;
        ImGui.TableNextColumn();
        if (DragInput("##gmpUnkA"u8, "动画类型A?（但似乎是能发光的头部装备的发光强度）"u8, unkWidth, entry.UnknownA, defaultEntry.UnknownA, out var unknownA,
                (byte)0,             (byte)15,              0.01f,    !disabled))
        {
            entry   = entry with { UnknownA = unknownA };
            changes = true;
        }

        ImUtf8.SameLineInner();
        if (DragInput("##gmpUnkB"u8, "动画类型B?"u8, unkWidth, entry.UnknownB, defaultEntry.UnknownB, out var unknownB,
                (byte)0,             (byte)15,              0.01f,    !disabled))
        {
            entry   = entry with { UnknownB = unknownB };
            changes = true;
        }

        return changes;
    }

    public static bool DrawPrimaryId(ref GmpIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = IdInput("##gmpPrimaryId"u8, unscaledWidth, identifier.SetId.Id, out var setId, 1, ExpandedEqpGmpBase.Count - 1,
            identifier.SetId.Id <= 1);
        ImUtf8.HoverTooltip(
            "模型集合ID - 通常可以在物品路径的'e####'部分找到。也可以在更改项目中查看。\n除非你明确需要，否则通常不应将此值设置为小于等于1。"u8);
        if (ret)
            identifier = new GmpIdentifier(setId);
        return ret;
    }
}

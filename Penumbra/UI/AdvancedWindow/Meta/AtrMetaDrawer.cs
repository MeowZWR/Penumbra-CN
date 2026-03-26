using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class AtrMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<AtrIdentifier, AtrEntry>(editor, metaFiles)
{
    public override ReadOnlySpan<byte> Label
        => "属性（ATR）"u8;

    public override ReadOnlySpan<byte> Tooltip
        => "属性"u8;

    private ShapeAttributeString _buffer = ShapeAttributeString.TryRead("atrx_"u8, out var s) ? s : ShapeAttributeString.Empty;
    private bool                 _identifierValid;

    public override int NumColumns
        => 7;

    public override float ColumnHeight
        => Im.Style.FrameHeight + 2 * Im.Style.CellPadding.Y;

    protected override void Initialize()
    {
        Identifier = new AtrIdentifier(HumanSlot.Unknown, null, ShapeAttributeString.Empty, GenderRace.Unknown);
        Entry      = AtrEntry.True;
    }

    protected override void DrawNew()
    {
        Im.Table.NextColumn();
        CopyToClipboardButton("复制当前所有ATR操作到剪贴板。"u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Atr)));

        Im.Table.NextColumn();
        var canAdd = !Editor.Contains(Identifier) && _identifierValid;
        var tt = canAdd
            ? "暂存此编辑。"u8
            : _identifierValid
                ? "此项不包含有效的属性。"u8
                : "此项已被编辑。"u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, AtrEntry.False);

        DrawIdentifierInput(ref Identifier);
        DrawEntry(ref Entry, true);
    }

    protected override void DrawEntry(AtrIdentifier identifier, AtrEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        if (DrawEntry(ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(AtrIdentifier, AtrEntry)> Enumerate()
        => Editor.Atr
            .OrderBy(kvp => kvp.Key.Attribute)
            .ThenBy(kvp => kvp.Key.Slot)
            .ThenBy(kvp => kvp.Key.Id)
            .Select(kvp => (kvp.Key, kvp.Value));

    public override int Count
        => Editor.Atr.Count;

    private bool DrawIdentifierInput(ref AtrIdentifier identifier)
    {
        Im.Table.NextColumn();
        var changes = DrawHumanSlot(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawGenderRaceConditionInput(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawPrimaryId(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawAttributeKeyInput(ref identifier, ref _buffer, ref _identifierValid);
        return changes;
    }

    private static void DrawIdentifier(AtrIdentifier identifier)
    {
        Im.Table.NextColumn();
        ImEx.TextFramed(ShpMetaDrawer.SlotName(identifier.Slot), default, FrameColor);
        Im.Tooltip.OnHover("模型部位"u8);

        Im.Table.NextColumn();
        if (identifier.GenderRaceCondition is not GenderRace.Unknown)
        {
            ImEx.TextFramed($"{identifier.GenderRaceCondition.ToNameU8()} ({identifier.GenderRaceCondition.ToRaceCode()})", default,
                FrameColor);
            Im.Tooltip.OnHover("设置此属性所需的性别与种族代码。");
        }
        else
        {
            ImEx.TextFramed("任意性别与种族"u8, default, FrameColor);
        }

        Im.Table.NextColumn();
        if (identifier.Id.HasValue)
            ImEx.TextFramed($"{identifier.Id.Value.Id}", default, FrameColor);
        else
            ImEx.TextFramed("全部ID"u8, default, FrameColor);
        Im.Tooltip.OnHover("主ID"u8);

        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.Attribute.AsSpan, default, FrameColor);
    }

    private static bool DrawEntry(ref AtrEntry entry, bool disabled)
    {
        using var dis = Im.Disabled(disabled);
        Im.Table.NextColumn();
        var value   = entry.Value;
        var changes = Im.Checkbox("##atrEntry"u8, ref value);
        if (changes)
            entry = new AtrEntry(value);
        Im.Tooltip.OnHover("是否为所选项目启用或禁用此属性。");
        return changes;
    }

    public static bool DrawPrimaryId(ref AtrIdentifier identifier, float unscaledWidth = 100)
    {
        var allSlots = identifier.Slot is HumanSlot.Unknown;
        var all      = !identifier.Id.HasValue;
        var ret      = false;
        using (Im.Disabled(allSlots))
        {
            if (Im.Checkbox("##atrAll"u8, ref all))
            {
                identifier = identifier with { Id = all ? null : 0 };
                ret        = true;
            }
        }

        Im.Tooltip.OnHover(allSlots
            ? "使用全部部位时，必须同时使用全部ID。"u8
            : "为所有模型ID启用此属性。"u8);

        Im.Line.SameInner();
        if (all)
        {
            using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0.05f, 0.5f));
            ImEx.TextFramed("全部ID"u8, new Vector2(unscaledWidth, 0),
                ImGuiColor.FrameBackground.Get(all || allSlots ? Im.Style.DisabledAlpha : 1f).Color, ImGuiColor.TextDisabled.Get().Color);
        }
        else
        {
            var max = identifier.Slot.ToSpecificEnum() is BodySlot ? byte.MaxValue : ExpandedEqpGmpBase.Count - 1;
            if (IdInput("##atrPrimaryId"u8, unscaledWidth, identifier.Id.GetValueOrDefault(0).Id, out var setId, 0, max, false))
            {
                identifier = identifier with { Id = setId };
                ret        = true;
            }
        }

        Im.Tooltip.OnHover("主ID - 通常可在物品路径中的 'e####' 部分或自定义内容中找到。"u8);

        return ret;
    }

    public bool DrawHumanSlot(ref AtrIdentifier identifier, float unscaledWidth = 150)
    {
        var ret = false;
        Im.Item.SetNextWidthScaled(unscaledWidth);
        using (var combo = Im.Combo.Begin("##atrSlot"u8, ShpMetaDrawer.SlotName(identifier.Slot)))
        {
            if (combo)
                foreach (var slot in ShpMetaDrawer.AvailableSlots)
                {
                    if (!Im.Selectable(ShpMetaDrawer.SlotName(slot), slot == identifier.Slot) || slot == identifier.Slot)
                        continue;

                    ret = true;
                    if (slot is HumanSlot.Unknown)
                    {
                        identifier = identifier with
                        {
                            Id = null,
                            Slot = slot,
                        };
                    }
                    else
                    {
                        identifier = identifier with
                        {
                            Id = identifier.Id.HasValue
                                ? (PrimaryId)Math.Clamp(identifier.Id.Value.Id, 0,
                                    slot.ToSpecificEnum() is BodySlot ? byte.MaxValue : ExpandedEqpGmpBase.Count - 1)
                                : null,
                            Slot = slot,
                        };
                        ret = true;
                    }
                }
        }

        Im.Tooltip.OnHover("模型部位"u8);
        return ret;
    }

    private static bool DrawGenderRaceConditionInput(ref AtrIdentifier identifier, float unscaledWidth = 250)
    {
        var ret = false;
        Im.Item.SetNextWidthScaled(unscaledWidth);

        using (var combo = Im.Combo.Begin("##shpGenderRace"u8,
                   identifier.GenderRaceCondition is GenderRace.Unknown
                       ? "任意性别与种族"u8
                       : $"{identifier.GenderRaceCondition.ToNameU8()} ({identifier.GenderRaceCondition.ToRaceCode()})"))
        {
            if (combo)
            {
                if (Im.Selectable("任意性别与种族"u8, identifier.GenderRaceCondition is GenderRace.Unknown)
                 && identifier.GenderRaceCondition is not GenderRace.Unknown)
                {
                    identifier = identifier with { GenderRaceCondition = GenderRace.Unknown };
                    ret        = true;
                }

                foreach (var gr in ShapeAttributeHashSet.GenderRaceValues.Skip(1))
                {
                    if (Im.Selectable($"{gr.ToNameU8()} ({gr.ToRaceCode()})", identifier.GenderRaceCondition == gr)
                     && identifier.GenderRaceCondition != gr)
                    {
                        identifier = identifier with { GenderRaceCondition = gr };
                        ret        = true;
                    }
                }
            }
        }

        Im.Tooltip.OnHover("仅在此性别与种族代码下激活此属性。"u8);

        return ret;
    }

    public static unsafe bool DrawAttributeKeyInput(ref AtrIdentifier identifier, ref ShapeAttributeString buffer, ref bool valid,
        float unscaledWidth = 150)
    {
        var ret  = false;
        var ptr  = Unsafe.AsPointer(ref buffer);
        var span = new Span<byte>(ptr, ShapeAttributeString.MaxLength + 1);
        using (ImStyleBorder.Frame.Push(Colors.RegexWarningBorder, Im.Style.GlobalScale, !valid))
        {
            Im.Item.SetNextWidthScaled(unscaledWidth);
            if (Im.Input.Text("##atrAttribute"u8, span, out ulong newLength, "Attribute..."u8))
            {
                buffer.ForceLength((byte)newLength);
                valid = buffer.ValidateCustomAttributeString();
                if (valid)
                    identifier = identifier with { Attribute = buffer };
                ret = true;
            }
        }

        Im.Tooltip.OnHover("支持的属性需以 `atrx_*` 格式命名，且最大长度为30个字符。"u8);
        return ret;
    }
}

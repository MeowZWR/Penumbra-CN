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

public sealed class ShpMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<ShpIdentifier, ShpEntry>(editor, metaFiles)
{
    public override ReadOnlySpan<byte> Label
        => "SHP"u8;

    public override ReadOnlySpan<byte> Tooltip
        => "形态键"u8;

    private ShapeAttributeString _buffer = ShapeAttributeString.TryRead("shpx_"u8, out var s) ? s : ShapeAttributeString.Empty;
    private bool                 _identifierValid;

    public override int NumColumns
        => 8;

    public override float ColumnHeight
        => Im.Style.FrameHeight + 2 * Im.Style.CellPadding.Y;

    protected override void Initialize()
        => Identifier = new ShpIdentifier(HumanSlot.Unknown, null, ShapeAttributeString.Empty, ShapeConnectorCondition.None,
            GenderRace.Unknown);

    protected override void DrawNew()
    {
        Im.Table.NextColumn();
        CopyToClipboardButton("将当前所有SHP操作复制到剪贴板。"u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Shp)));

        Im.Table.NextColumn();
        var canAdd = !Editor.Contains(Identifier) && _identifierValid;
        var tt = canAdd
            ? "编辑此项。"u8
            : _identifierValid
                ? "此项不包含有效的形态键。"u8
                : "此项已被编辑。"u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, ShpEntry.True);

        DrawIdentifierInput(ref Identifier);
        DrawEntry(ref Entry, true);
    }

    protected override void DrawEntry(ShpIdentifier identifier, ShpEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        if (DrawEntry(ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(ShpIdentifier, ShpEntry)> Enumerate()
        => Editor.Shp
            .OrderBy(kvp => kvp.Key.Shape)
            .ThenBy(kvp => kvp.Key.Slot)
            .ThenBy(kvp => kvp.Key.Id)
            .ThenBy(kvp => kvp.Key.ConnectorCondition)
            .Select(kvp => (kvp.Key, kvp.Value));

    public override int Count
        => Editor.Shp.Count;

    private bool DrawIdentifierInput(ref ShpIdentifier identifier)
    {
        Im.Table.NextColumn();
        var changes = DrawHumanSlot(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawGenderRaceConditionInput(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawPrimaryId(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawShapeKeyInput(ref identifier, ref _buffer, ref _identifierValid);

        Im.Table.NextColumn();
        changes |= DrawConnectorConditionInput(ref identifier);
        return changes;
    }

    private static void DrawIdentifier(ShpIdentifier identifier)
    {
        Im.Table.NextColumn();

        ImEx.TextFramed(SlotName(identifier.Slot), default, FrameColor);
        Im.Tooltip.OnHover("模型部位"u8);

        Im.Table.NextColumn();
        if (identifier.GenderRaceCondition is not GenderRace.Unknown)
        {
            ImEx.TextFramed($"{identifier.GenderRaceCondition.ToNameU8()} ({identifier.GenderRaceCondition.ToRaceCode()})", default,
                FrameColor);
            Im.Tooltip.OnHover("性别与种族代码用于设置此形态键。");
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
        Im.Tooltip.OnHover("主要ID"u8);

        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.Shape.AsSpan, default, FrameColor);

        Im.Table.NextColumn();
        if (identifier.ConnectorCondition is not ShapeConnectorCondition.None)
        {
            ImEx.TextFramed($"{identifier.ConnectorCondition}", default, FrameColor);
            Im.Tooltip.OnHover("连接器条件用于激活此形态键。"u8);
        }
    }

    private static bool DrawEntry(ref ShpEntry entry, bool disabled)
    {
        using var dis = Im.Disabled(disabled);
        Im.Table.NextColumn();
        var value   = entry.Value;
        var changes = Im.Checkbox("##shpEntry"u8, ref value);
        if (changes)
            entry = new ShpEntry(value);
        Im.Tooltip.OnHover("是否启用或禁用此形态键用于选定物品。"u8);
        return changes;
    }

    public static bool DrawPrimaryId(ref ShpIdentifier identifier, float unscaledWidth = 100)
    {
        var allSlots = identifier.Slot is HumanSlot.Unknown;
        var all      = !identifier.Id.HasValue;
        var ret      = false;
        using (Im.Disabled(allSlots))
        {
            if (Im.Checkbox("##shpAll"u8, ref all))
            {
                identifier = identifier with { Id = all ? null : 0 };
                ret        = true;
            }
        }

        Im.Tooltip.OnHover(allSlots ? "使用所有部位时，您还需要使用所有ID。"u8 : "启用此形态键用于所有模型ID。"u8);

        Im.Line.SameInner();
        if (all)
        {
            using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0.05f, 0.5f));
            ImEx.TextFramed("全部ID"u8, new Vector2(unscaledWidth, 0),
                ImGuiColor.FrameBackground.Get(all || allSlots ? Im.Style.DisabledAlpha : 1f).Color,
                ImGuiColor.TextDisabled.Get().Color);
        }
        else
        {
            var max = identifier.Slot.ToSpecificEnum() is BodySlot ? byte.MaxValue : ExpandedEqpGmpBase.Count - 1;
            if (IdInput("##shpPrimaryId"u8, unscaledWidth, identifier.Id.GetValueOrDefault(0).Id, out var setId, 0, max, false))
            {
                identifier = identifier with { Id = setId };
                ret        = true;
            }
        }

        Im.Tooltip.OnHover("主要ID - 通常可以在物品路径的'e####'部分或自定义内容中找到。"u8);

        return ret;
    }

    public bool DrawHumanSlot(ref ShpIdentifier identifier, float unscaledWidth = 170)
    {
        var ret = false;
        Im.Item.SetNextWidthScaled(unscaledWidth);
        using (var combo = Im.Combo.Begin("##shpSlot"u8, SlotName(identifier.Slot)))
        {
            if (combo)
                foreach (var slot in AvailableSlots)
                {
                    if (!Im.Selectable(SlotName(slot), slot == identifier.Slot) || slot == identifier.Slot)
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
                            ConnectorCondition = Identifier.ConnectorCondition switch
                            {
                                ShapeConnectorCondition.Wrists when slot is HumanSlot.Body or HumanSlot.Hands => ShapeConnectorCondition.Wrists,
                                ShapeConnectorCondition.Waist when slot is HumanSlot.Body or HumanSlot.Legs   => ShapeConnectorCondition.Waist,
                                ShapeConnectorCondition.Ankles when slot is HumanSlot.Legs or HumanSlot.Feet  => ShapeConnectorCondition.Ankles,
                                _                                                                             => ShapeConnectorCondition.None,
                            },
                        };
                        ret = true;
                    }
                }
        }

        Im.Tooltip.OnHover("模型部位"u8);
        return ret;
    }

    public static unsafe bool DrawShapeKeyInput(ref ShpIdentifier identifier, ref ShapeAttributeString buffer, ref bool valid,
        float unscaledWidth = 200)
    {
        var ret  = false;
        var ptr  = Unsafe.AsPointer(ref buffer);
        var span = new Span<byte>(ptr, ShapeAttributeString.MaxLength + 1);
        using (ImStyleBorder.Frame.Push(Colors.RegexWarningBorder, Im.Style.GlobalScale, !valid))
        {
            Im.Item.SetNextWidthScaled(unscaledWidth);
            if (Im.Input.Text("##shpShape"u8, span, out ulong newLength, "形态键..."u8))
            {
                buffer.ForceLength((byte)newLength);
                valid = buffer.ValidateCustomShapeString();
                if (valid)
                    identifier = identifier with { Shape = buffer };
                ret = true;
            }
        }

        Im.Tooltip.OnHover("支持的形态键需要以 `shpx_*` 格式命名，且最大长度为30个字符。"u8);
        return ret;
    }

    private static bool DrawConnectorConditionInput(ref ShpIdentifier identifier, float unscaledWidth = 80)
    {
        var ret = false;
        Im.Item.SetNextWidth(unscaledWidth * Im.Style.GlobalScale);
        var (showWrists, showWaist, showAnkles, disable) = identifier.Slot switch
        {
            HumanSlot.Unknown => (true, true, true, false),
            HumanSlot.Body    => (true, true, false, false),
            HumanSlot.Legs    => (false, true, true, false),
            HumanSlot.Hands   => (true, false, false, false),
            HumanSlot.Feet    => (false, false, true, false),
            _                 => (false, false, false, true),
        };
        using var disabled = Im.Disabled(disable);
        using (var combo = Im.Combo.Begin("##shpCondition"u8, $"{identifier.ConnectorCondition}"))
        {
            if (combo)
            {
                if (Im.Selectable("无"u8, identifier.ConnectorCondition is ShapeConnectorCondition.None))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.None };

                if (showWrists && Im.Selectable("手腕"u8, identifier.ConnectorCondition is ShapeConnectorCondition.Wrists))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.Wrists };

                if (showWaist && Im.Selectable("腰部"u8, identifier.ConnectorCondition is ShapeConnectorCondition.Waist))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.Waist };

                if (showAnkles && Im.Selectable("脚踝"u8, identifier.ConnectorCondition is ShapeConnectorCondition.Ankles))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.Ankles };
            }
        }

        Im.Tooltip.OnHover(
            "仅在任何自定义连接器形态键 (shpx_[wr|wa|an]_*) 通过匹配属性启用时激活此形态键。"u8);
        return ret;
    }

    private static bool DrawGenderRaceConditionInput(ref ShpIdentifier identifier, float unscaledWidth = 250)
    {
        var ret = false;
        Im.Item.SetNextWidth(unscaledWidth * Im.Style.GlobalScale);

        using (var combo = Im.Combo.Begin("##shpGenderRace"u8, identifier.GenderRaceCondition is GenderRace.Unknown
                   ? "任意性别与种族"
                   : $"{identifier.GenderRaceCondition.ToName()} ({identifier.GenderRaceCondition.ToRaceCode()})"))
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

        Im.Tooltip.OnHover("仅为此性别与种族代码激活此形态键。"u8);

        return ret;
    }

    public static ReadOnlySpan<HumanSlot> AvailableSlots
        =>
        [
            HumanSlot.Unknown,
            HumanSlot.Head,
            HumanSlot.Body,
            HumanSlot.Hands,
            HumanSlot.Legs,
            HumanSlot.Feet,
            HumanSlot.Ears,
            HumanSlot.Neck,
            HumanSlot.Wrists,
            HumanSlot.RFinger,
            HumanSlot.LFinger,
            HumanSlot.Glasses,
            HumanSlot.Hair,
            HumanSlot.Face,
            HumanSlot.Ear,
        ];

    public static ReadOnlySpan<byte> SlotName(HumanSlot slot)
        => slot switch
        {
            HumanSlot.Unknown => "全部部位"u8,
            HumanSlot.Head    => "装备：头部"u8,
            HumanSlot.Body    => "装备：身体"u8,
            HumanSlot.Hands   => "装备：手部"u8,
            HumanSlot.Legs    => "装备：腿部"u8,
            HumanSlot.Feet    => "装备：脚部"u8,
            HumanSlot.Ears    => "装备：耳朵"u8,
            HumanSlot.Neck    => "装备：颈部"u8,
            HumanSlot.Wrists  => "装备：手腕"u8,
            HumanSlot.RFinger => "装备：右手指"u8,
            HumanSlot.LFinger => "装备：左手指"u8,
            HumanSlot.Glasses => "装备：眼镜"u8,
            HumanSlot.Hair    => "外貌：头发"u8,
            HumanSlot.Face    => "外貌：面部"u8,
            HumanSlot.Ear     => "外貌：耳朵"u8,
            _                 => "未知"u8,
        };
}

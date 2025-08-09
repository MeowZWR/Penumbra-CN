using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json.Linq;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
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
    : MetaDrawer<ShpIdentifier, ShpEntry>(editor, metaFiles), IService
{
    public override ReadOnlySpan<byte> Label
        => "形状键 (SHP)###SHP"u8;

    private ShapeAttributeString _buffer = ShapeAttributeString.TryRead("shpx_"u8, out var s) ? s : ShapeAttributeString.Empty;
    private bool                 _identifierValid;

    public override int NumColumns
        => 8;

    public override float ColumnHeight
        => ImUtf8.FrameHeightSpacing;

    protected override void Initialize()
    {
        Identifier = new ShpIdentifier(HumanSlot.Unknown, null, ShapeAttributeString.Empty, ShapeConnectorCondition.None, GenderRace.Unknown);
    }

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("将当前所有SHP操作复制到剪贴板。"u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Shp)));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier) && _identifierValid;
        var tt = canAdd
            ? "暂存此更改。"u8
            : _identifierValid
                ? "该条目不包含有效的形状键。"u8
                : "该条目已被编辑。"u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
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

    protected override int Count
        => Editor.Shp.Count;

    private bool DrawIdentifierInput(ref ShpIdentifier identifier)
    {
        ImGui.TableNextColumn();
        var changes = DrawHumanSlot(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawGenderRaceConditionInput(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawShapeKeyInput(ref identifier, ref _buffer, ref _identifierValid);

        ImGui.TableNextColumn();
        changes |= DrawConnectorConditionInput(ref identifier);
        return changes;
    }

    private static void DrawIdentifier(ShpIdentifier identifier)
    {
        ImGui.TableNextColumn();

        ImUtf8.TextFramed(SlotName(identifier.Slot), FrameColor);
        ImUtf8.HoverTooltip("模型部位"u8);

        ImGui.TableNextColumn();
        if (identifier.GenderRaceCondition is not GenderRace.Unknown)
        {
            ImUtf8.TextFramed($"{identifier.GenderRaceCondition.ToName()} ({identifier.GenderRaceCondition.ToRaceCode()})", FrameColor);
            ImUtf8.HoverTooltip("此形状键生效的性别与种族代码。");
        }
        else
        {
            ImUtf8.TextFramed("任意性别与种族"u8, FrameColor);
        }

        ImGui.TableNextColumn();
        if (identifier.Id.HasValue)
            ImUtf8.TextFramed($"{identifier.Id.Value.Id}", FrameColor);
        else
            ImUtf8.TextFramed("全部ID"u8, FrameColor);
        ImUtf8.HoverTooltip("主ID"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Shape.AsSpan, FrameColor);

        ImGui.TableNextColumn();
        if (identifier.ConnectorCondition is not ShapeConnectorCondition.None)
        {
            ImUtf8.TextFramed($"{identifier.ConnectorCondition}", FrameColor);
            ImUtf8.HoverTooltip("激活此形状所需的连接条件。");
        }
    }

    private static bool DrawEntry(ref ShpEntry entry, bool disabled)
    {
        using var dis = ImRaii.Disabled(disabled);
        ImGui.TableNextColumn();
        var value   = entry.Value;
        var changes = ImUtf8.Checkbox("##shpEntry"u8, ref value);
        if (changes)
            entry = new ShpEntry(value);
        ImUtf8.HoverTooltip("是否为所选项目启用或禁用此形状键。");
        return changes;
    }

    public static bool DrawPrimaryId(ref ShpIdentifier identifier, float unscaledWidth = 100)
    {
        var allSlots = identifier.Slot is HumanSlot.Unknown;
        var all      = !identifier.Id.HasValue;
        var ret      = false;
        using (ImRaii.Disabled(allSlots))
        {
            if (ImUtf8.Checkbox("##shpAll"u8, ref all))
            {
                identifier = identifier with { Id = all ? null : 0 };
                ret        = true;
            }
        }

        ImUtf8.HoverTooltip(allSlots ? "使用全部部位时，必须同时使用全部ID。"u8 : "为所有模型ID启用此形状键。"u8);

        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        if (all)
        {
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.05f, 0.5f));
            ImUtf8.TextFramed("全部ID"u8, ImGui.GetColorU32(ImGuiCol.FrameBg, all || allSlots ? ImGui.GetStyle().DisabledAlpha : 1f),
                new Vector2(unscaledWidth, 0), ImGui.GetColorU32(ImGuiCol.TextDisabled));
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

        ImUtf8.HoverTooltip("主ID - 通常可在物品路径中的 'e####' 部分或自定义内容中找到。"u8);

        return ret;
    }

    public bool DrawHumanSlot(ref ShpIdentifier identifier, float unscaledWidth = 170)
    {
        var ret = false;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
        using (var combo = ImUtf8.Combo("##shpSlot"u8, SlotName(identifier.Slot)))
        {
            if (combo)
                foreach (var slot in AvailableSlots)
                {
                    if (!ImUtf8.Selectable(SlotName(slot), slot == identifier.Slot) || slot == identifier.Slot)
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

        ImUtf8.HoverTooltip("模型部位"u8);
        return ret;
    }

    public static unsafe bool DrawShapeKeyInput(ref ShpIdentifier identifier, ref ShapeAttributeString buffer, ref bool valid,
        float unscaledWidth = 200)
    {
        var ret  = false;
        var ptr  = Unsafe.AsPointer(ref buffer);
        var span = new Span<byte>(ptr, ShapeAttributeString.MaxLength + 1);
        using (new ImRaii.ColorStyle().Push(ImGuiCol.Border, Colors.RegexWarningBorder, !valid).Push(ImGuiStyleVar.FrameBorderSize, 1f, !valid))
        {
            ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
            if (ImUtf8.InputText("##shpShape"u8, span, out int newLength, "形状键..."u8))
            {
                buffer.ForceLength((byte)newLength);
                valid = buffer.ValidateCustomShapeString();
                if (valid)
                    identifier = identifier with { Shape = buffer };
                ret = true;
            }
        }

        ImUtf8.HoverTooltip("支持的形状键需以 `shpx_*` 格式命名，且最大长度为30个字符。"u8);
        return ret;
    }

    private static bool DrawConnectorConditionInput(ref ShpIdentifier identifier, float unscaledWidth = 80)
    {
        var ret = false;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
        var (showWrists, showWaist, showAnkles, disable) = identifier.Slot switch
        {
            HumanSlot.Unknown => (true, true, true, false),
            HumanSlot.Body    => (true, true, false, false),
            HumanSlot.Legs    => (false, true, true, false),
            HumanSlot.Hands   => (true, false, false, false),
            HumanSlot.Feet    => (false, false, true, false),
            _                 => (false, false, false, true),
        };
        using var disabled = ImRaii.Disabled(disable);
        using (var combo = ImUtf8.Combo("##shpCondition"u8, $"{identifier.ConnectorCondition}"))
        {
            if (combo)
            {
                if (ImUtf8.Selectable("无"u8, identifier.ConnectorCondition is ShapeConnectorCondition.None))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.None };

                if (showWrists && ImUtf8.Selectable("手腕"u8, identifier.ConnectorCondition is ShapeConnectorCondition.Wrists))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.Wrists };

                if (showWaist && ImUtf8.Selectable("腰部"u8, identifier.ConnectorCondition is ShapeConnectorCondition.Waist))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.Waist };

                if (showAnkles && ImUtf8.Selectable("脚踝"u8, identifier.ConnectorCondition is ShapeConnectorCondition.Ankles))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.Ankles };
            }
        }

        ImUtf8.HoverTooltip(
            "仅当有自定义连接器形状键（shpx_[wr|wa|an]_*) 通过匹配属性被启用时，才激活此形状键。"u8);
        return ret;
    }

    private static bool DrawGenderRaceConditionInput(ref ShpIdentifier identifier, float unscaledWidth = 250)
    {
        var ret = false;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);

        using (var combo = ImUtf8.Combo("##shpGenderRace"u8, identifier.GenderRaceCondition is GenderRace.Unknown
                   ? "任意性别与种族"
                   : $"{identifier.GenderRaceCondition.ToName()} ({identifier.GenderRaceCondition.ToRaceCode()})"))
        {
            if (combo)
            {
                if (ImUtf8.Selectable("任意性别与种族"u8, identifier.GenderRaceCondition is GenderRace.Unknown)
                 && identifier.GenderRaceCondition is not GenderRace.Unknown)
                {
                    identifier = identifier with { GenderRaceCondition = GenderRace.Unknown };
                    ret        = true;
                }

                foreach (var gr in ShapeAttributeHashSet.GenderRaceValues.Skip(1))
                {
                    if (ImUtf8.Selectable($"{gr.ToName()} ({gr.ToRaceCode()})", identifier.GenderRaceCondition == gr)
                     && identifier.GenderRaceCondition != gr)
                    {
                        identifier = identifier with { GenderRaceCondition = gr };
                        ret        = true;
                    }
                }
            }
        }

        ImUtf8.HoverTooltip(
            "仅为指定的性别与种族代码激活此形状键。"u8);

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
            HumanSlot.Feet    => "装备：足部"u8,
            HumanSlot.Ears    => "装备：耳饰"u8,
            HumanSlot.Neck    => "装备：项链"u8,
            HumanSlot.Wrists  => "装备：手腕"u8,
            HumanSlot.RFinger => "装备：右手指"u8,
            HumanSlot.LFinger => "装备：左手指"u8,
            HumanSlot.Glasses => "装备：眼镜"u8,
            HumanSlot.Hair    => "自定义：发型"u8,
            HumanSlot.Face    => "自定义：脸部"u8,
            HumanSlot.Ear     => "自定义：耳朵"u8,
            _                 => "未知"u8,
        };
}

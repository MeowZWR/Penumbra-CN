using Dalamud.Interface;
using ImGuiNET;
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

public sealed class AtrMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<AtrIdentifier, AtrEntry>(editor, metaFiles), IService
{
    public override ReadOnlySpan<byte> Label
        => "属性(ATR)###ATR"u8;

    private ShapeAttributeString _buffer = ShapeAttributeString.TryRead("atrx_"u8, out var s) ? s : ShapeAttributeString.Empty;
    private bool                 _identifierValid;

    public override int NumColumns
        => 7;

    public override float ColumnHeight
        => ImUtf8.FrameHeightSpacing;

    protected override void Initialize()
    {
        Identifier = new AtrIdentifier(HumanSlot.Unknown, null, ShapeAttributeString.Empty, GenderRace.Unknown);
        Entry      = AtrEntry.True;
    }

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("将当前所有ATR操作复制到剪贴板。"u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Atr)));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier) && _identifierValid;
        var tt = canAdd
            ? "暂存此更改。"u8
            : _identifierValid
                ? "此条目不包含有效的属性。"u8
                : "此条目已被编辑。"u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
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

    protected override int Count
        => Editor.Atr.Count;

    private bool DrawIdentifierInput(ref AtrIdentifier identifier)
    {
        ImGui.TableNextColumn();
        var changes = DrawHumanSlot(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawGenderRaceConditionInput(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawAttributeKeyInput(ref identifier, ref _buffer, ref _identifierValid);
        return changes;
    }

    private static void DrawIdentifier(AtrIdentifier identifier)
    {
        ImGui.TableNextColumn();

        ImUtf8.TextFramed(ShpMetaDrawer.SlotName(identifier.Slot), FrameColor);
        ImUtf8.HoverTooltip("模型部位"u8);

        ImGui.TableNextColumn();
        if (identifier.GenderRaceCondition is not GenderRace.Unknown)
        {
            ImUtf8.TextFramed($"{identifier.GenderRaceCondition.ToName()} ({identifier.GenderRaceCondition.ToRaceCode()})", FrameColor);
            ImUtf8.HoverTooltip("设置此属性所需的性别与种族代码。");
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
        ImUtf8.TextFramed(identifier.Attribute.AsSpan, FrameColor);
    }

    private static bool DrawEntry(ref AtrEntry entry, bool disabled)
    {
        using var dis = ImRaii.Disabled(disabled);
        ImGui.TableNextColumn();
        var value   = entry.Value;
        var changes = ImUtf8.Checkbox("##atrEntry"u8, ref value);
        if (changes)
            entry = new AtrEntry(value);
        ImUtf8.HoverTooltip("是否为所选项目启用或禁用此属性。");
        return changes;
    }

    public static bool DrawPrimaryId(ref AtrIdentifier identifier, float unscaledWidth = 100)
    {
        var allSlots = identifier.Slot is HumanSlot.Unknown;
        var all      = !identifier.Id.HasValue;
        var ret      = false;
        using (ImRaii.Disabled(allSlots))
        {
            if (ImUtf8.Checkbox("##atrAll"u8, ref all))
            {
                identifier = identifier with { Id = all ? null : 0 };
                ret        = true;
            }
        }

        ImUtf8.HoverTooltip(allSlots
            ? "使用全部部位时，必须同时使用全部ID。"u8
            : "为所有模型ID启用此属性。"u8);

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
            if (IdInput("##atrPrimaryId"u8, unscaledWidth, identifier.Id.GetValueOrDefault(0).Id, out var setId, 0, max, false))
            {
                identifier = identifier with { Id = setId };
                ret        = true;
            }
        }

        ImUtf8.HoverTooltip("主ID - 通常可在物品路径中的 'e####' 部分或自定义内容中找到。"u8);

        return ret;
    }

    public bool DrawHumanSlot(ref AtrIdentifier identifier, float unscaledWidth = 150)
    {
        var ret = false;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
        using (var combo = ImUtf8.Combo("##atrSlot"u8, ShpMetaDrawer.SlotName(identifier.Slot)))
        {
            if (combo)
                foreach (var slot in ShpMetaDrawer.AvailableSlots)
                {
                    if (!ImUtf8.Selectable(ShpMetaDrawer.SlotName(slot), slot == identifier.Slot) || slot == identifier.Slot)
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

        ImUtf8.HoverTooltip("模型部位"u8);
        return ret;
    }
     
    private static bool DrawGenderRaceConditionInput(ref AtrIdentifier identifier, float unscaledWidth = 250)
    {
        var ret = false;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);

        using (var combo = ImUtf8.Combo("##shpGenderRace"u8,
                   identifier.GenderRaceCondition is GenderRace.Unknown
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
            "仅在此性别与种族代码下激活此属性。"u8);

        return ret;
    }

    public static unsafe bool DrawAttributeKeyInput(ref AtrIdentifier identifier, ref ShapeAttributeString buffer, ref bool valid,
        float unscaledWidth = 150)
    {
        var ret  = false;
        var ptr  = Unsafe.AsPointer(ref buffer);
        var span = new Span<byte>(ptr, ShapeAttributeString.MaxLength + 1);
        using (new ImRaii.ColorStyle().Push(ImGuiCol.Border, Colors.RegexWarningBorder, !valid).Push(ImGuiStyleVar.FrameBorderSize, 1f, !valid))
        {
            ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
            if (ImUtf8.InputText("##atrAttribute"u8, span, out int newLength, "属性..."u8))
            {
                buffer.ForceLength((byte)newLength);
                valid = buffer.ValidateCustomAttributeString();
                if (valid)
                    identifier = identifier with { Attribute = buffer };
                ret = true;
            }
        }

        ImUtf8.HoverTooltip("支持的属性需以 `atrx_*` 格式命名，且最大长度为30个字符。"u8);
        return ret;
    }
}

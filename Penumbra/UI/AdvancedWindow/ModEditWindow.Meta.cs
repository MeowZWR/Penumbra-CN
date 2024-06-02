using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private const string ModelSetIdTooltip =
        "模型ID - 通常可以在物品路径部分找到（比如在'文件重定向'或'材质指定'里，装备是'e####'，武器是'w####'）。\n当然也可以借助Anamnesis或Textools等工具来获取。\n这个ID一般不应该保持为默认值'1'(<= 1)，除非你明确需要这个值。";

    private const string ModelSetIdTooltipShort = "模型ID";
    private const string EquipSlotTooltip       = "装备槽";
    private const string ModelRaceTooltip       = "模型种族";
    private const string GenderTooltip          = "性别";
    private const string ObjectTypeTooltip      = "对象类型";
    private const string SecondaryIdTooltip     = "次要ID";
    private const string PrimaryIdTooltipShort  = "主ID";
    private const string VariantIdTooltip       = "变体ID";
    private const string EstTypeTooltip         = "EST类型";
    private const string RacialTribeTooltip     = "种族";
    private const string ScalingTypeTooltip     = "缩放类型";

    private void DrawMetaTab()
    {
        using var tab = ImRaii.TabItem( "元数据操作" );
        if (!tab)
            return;

        DrawOptionSelectHeader();

        var setsEqual = !_editor.MetaEditor.Changes;
        var tt        = setsEqual ? "没有进行任何更改。" : "应用当前暂存的更改。";
        ImGui.NewLine();
        if (ImGuiUtil.DrawDisabledButton("应用更改", Vector2.Zero, tt, setsEqual))
            _editor.MetaEditor.Apply(_editor.Option!);

        ImGui.SameLine();
        tt = setsEqual ? "没有进行任何更改。" : "撤销当前进行的所有更改。";
        if( ImGuiUtil.DrawDisabledButton( "撤销更改", Vector2.Zero, tt, setsEqual ) )
            _editor.MetaEditor.Load(_editor.Mod!, _editor.Option!);

        ImGui.SameLine();
        AddFromClipboardButton();
        ImGui.SameLine();
        SetFromClipboardButton();
        ImGui.SameLine();
        CopyToClipboardButton("将当前的所有操作复制到剪贴板。", _iconSize, _editor.MetaEditor.Recombine());
        ImGui.SameLine();
        if( ImGui.Button( "写入为TexTools文件" ) )
            _metaFileManager.WriteAllTexToolsMeta(Mod!);

        using var child = ImRaii.Child("##meta", -Vector2.One, true);
        if (!child)
            return;

        DrawEditHeader(_editor.MetaEditor.Eqp, "装备参数设置(设置可见性)(EQP)###EQP", 5, EqpRow.Draw, EqpRow.DrawNew,
            _editor.MetaEditor.OtherData[MetaManipulation.Type.Eqp]);
        DrawEditHeader(_editor.MetaEditor.Eqdp, "种族模型编辑(EQDP)###EQDP", 7, EqdpRow.Draw, EqdpRow.DrawNew,
            _editor.MetaEditor.OtherData[MetaManipulation.Type.Eqdp]);
        DrawEditHeader(_editor.MetaEditor.Imc, "变体(变量)设置(IMC)###IMC", 10, ImcRow.Draw, ImcRow.DrawNew,
            _editor.MetaEditor.OtherData[MetaManipulation.Type.Imc]);
        DrawEditHeader(_editor.MetaEditor.Est, "额外骨骼参数(EST)###EST", 7, EstRow.Draw, EstRow.DrawNew,
            _editor.MetaEditor.OtherData[MetaManipulation.Type.Est]);
        DrawEditHeader(_editor.MetaEditor.Gmp, "面罩/面具(可调整头部装备)编辑(GMP)###GMP", 7, GmpRow.Draw, GmpRow.DrawNew,
            _editor.MetaEditor.OtherData[MetaManipulation.Type.Gmp]);
        DrawEditHeader(_editor.MetaEditor.Rsp, "种族缩放编辑(全局修改)(RSP)###RSP", 5, RspRow.Draw, RspRow.DrawNew,
            _editor.MetaEditor.OtherData[MetaManipulation.Type.Rsp]);
        DrawEditHeader(_editor.MetaEditor.GlobalEqp, "全局装备参数编辑 (Global EQP)###GEQP", 4, GlobalEqpRow.Draw,
            GlobalEqpRow.DrawNew,                    _editor.MetaEditor.OtherData[MetaManipulation.Type.GlobalEqp]);
    }


    /// <summary> The headers for the different meta changes all have basically the same structure for different types.</summary>
    private void DrawEditHeader<T>(IReadOnlyCollection<T> items, string label, int numColumns,
        Action<MetaFileManager, T, ModEditor, Vector2> draw, Action<MetaFileManager, ModEditor, Vector2> drawNew,
        ModMetaEditor.OtherOptionData otherOptionData)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV;

        var oldPos = ImGui.GetCursorPosY();
        var header = ImGui.CollapsingHeader($"{items.Count} {label}");
        var newPos = ImGui.GetCursorPos();
        if (otherOptionData.TotalCount > 0)
        {
            var text = $"在其他选项中有{otherOptionData.TotalCount}个修改";
            var size = ImGui.CalcTextSize(text).X;
            ImGui.SetCursorPos(new Vector2(ImGui.GetContentRegionAvail().X - size, oldPos + ImGui.GetStyle().FramePadding.Y));
            ImGuiUtil.TextColored(ColorId.RedundantAssignment.Value() | 0xFF000000, text);
            if (ImGui.IsItemHovered())
            {
                using var tt = ImUtf8.Tooltip();
                foreach (var name in otherOptionData)
                    ImUtf8.Text(name);
            }

            ImGui.SetCursorPos(newPos);
        }

        if (!header)
            return;

        using (var table = ImRaii.Table(label, numColumns, flags))
        {
            if (table)
            {
                drawNew(_metaFileManager, _editor, _iconSize);
                foreach (var (item, index) in items.ToArray().WithIndex())
                {
                    using var id = ImRaii.PushId(index);
                    draw(_metaFileManager, item, _editor, _iconSize);
                }
            }
        }

        ImGui.NewLine();
    }

    private static class EqpRow
    {
        private static EqpManipulation _new = new(Eqp.DefaultEntry, EquipSlot.Head, 1);

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton( "将所有 EQP 操作复制到剪贴板。", iconSize,
                editor.MetaEditor.Eqp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd       = editor.MetaEditor.CanAdd(_new);
            var tt           = canAdd ? "进行此条目的编辑。" : "这个条目已经在编辑了。";
            var defaultEntry = ExpandedEqpFile.GetDefault(metaFileManager, _new.SetId);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (IdInput("##eqpId", IdWidth, _new.SetId.Id, out var setId, 1, ExpandedEqpGmpBase.Count - 1, _new.SetId <= 1))
                _new = new EqpManipulation(ExpandedEqpFile.GetDefault(metaFileManager, setId), _new.Slot, setId);

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            ImGui.TableNextColumn();
            if (Combos.EqpEquipSlot("##eqpSlot", _new.Slot, out var slot))
                _new = new EqpManipulation(ExpandedEqpFile.GetDefault(metaFileManager, setId), slot, _new.SetId);

            ImGuiUtil.HoverTooltip(EquipSlotTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                new Vector2(3 * UiHelpers.Scale, ImGui.GetStyle().ItemSpacing.Y));
            foreach (var flag in Eqp.EqpAttributes[_new.Slot])
            {
                var value = defaultEntry.HasFlag(flag);
                Checkmark("##eqp", flag.ToLocalName(), value, value, out _);
                ImGui.SameLine();
            }

            ImGui.NewLine();
        }

        public static void Draw(MetaFileManager metaFileManager, EqpManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SetId.ToString());
            ImGuiUtil.HoverTooltip(ModelSetIdTooltipShort);
            var defaultEntry = ExpandedEqpFile.GetDefault(metaFileManager, meta.SetId);

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Slot.ToName());
            ImGuiUtil.HoverTooltip(EquipSlotTooltip);

            // Values
            ImGui.TableNextColumn();
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                new Vector2(3 * UiHelpers.Scale, ImGui.GetStyle().ItemSpacing.Y));
            var idx = 0;
            foreach (var flag in Eqp.EqpAttributes[meta.Slot])
            {
                using var id           = ImRaii.PushId(idx++);
                var       defaultValue = defaultEntry.HasFlag(flag);
                var       currentValue = meta.Entry.HasFlag(flag);
                if (Checkmark("##eqp", flag.ToLocalName(), currentValue, defaultValue, out var value))
                    editor.MetaEditor.Change(meta.Copy(value ? meta.Entry | flag : meta.Entry & ~flag));

                ImGui.SameLine();
            }

            ImGui.NewLine();
        }
    }


    private static class EqdpRow
    {
        private static EqdpManipulation _new = new(EqdpEntry.Invalid, EquipSlot.Head, Gender.Male, ModelRace.Midlander, 1);

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton( "将当前所有 EQDP 操作复制到剪贴板。", iconSize,
                editor.MetaEditor.Eqdp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var raceCode      = Names.CombinedRace(_new.Gender, _new.Race);
            var validRaceCode = CharacterUtilityData.EqdpIdx(raceCode, false) >= 0;
            var canAdd        = validRaceCode && editor.MetaEditor.CanAdd(_new);
            var tt = canAdd   ? "进行此项编辑。" :
                validRaceCode ? "此项已经编辑过了。" : "这个种族不能和这个性别组合使用。";
            var defaultEntry = validRaceCode
                ? ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, _new.Race), _new.Slot.IsAccessory(), _new.SetId)
                : 0;
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (IdInput("##eqdpId", IdWidth, _new.SetId.Id, out var setId, 0, ExpandedEqpGmpBase.Count - 1, _new.SetId <= 1))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, _new.Race),
                    _new.Slot.IsAccessory(), setId);
                _new = new EqdpManipulation(newDefaultEntry, _new.Slot, _new.Gender, _new.Race, setId);
            }

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            ImGui.TableNextColumn();
            if (Combos.Race("##eqdpRace", _new.Race, out var race))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, race),
                    _new.Slot.IsAccessory(), _new.SetId);
                _new = new EqdpManipulation(newDefaultEntry, _new.Slot, _new.Gender, race, _new.SetId);
            }

            ImGuiUtil.HoverTooltip(ModelRaceTooltip);

            ImGui.TableNextColumn();
            if (Combos.Gender("##eqdpGender", _new.Gender, out var gender))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(gender, _new.Race),
                    _new.Slot.IsAccessory(), _new.SetId);
                _new = new EqdpManipulation(newDefaultEntry, _new.Slot, gender, _new.Race, _new.SetId);
            }

            ImGuiUtil.HoverTooltip(GenderTooltip);

            ImGui.TableNextColumn();
            if (Combos.EqdpEquipSlot("##eqdpSlot", _new.Slot, out var slot))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, _new.Race),
                    slot.IsAccessory(), _new.SetId);
                _new = new EqdpManipulation(newDefaultEntry, slot, _new.Gender, _new.Race, _new.SetId);
            }

            ImGuiUtil.HoverTooltip(EquipSlotTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            var (bit1, bit2) = defaultEntry.ToBits(_new.Slot);
            Checkmark( "材质##eqdpCheck1", string.Empty, bit1, bit1, out _ );
            ImGui.SameLine();
            Checkmark( "模型##eqdpCheck2", string.Empty, bit2, bit2, out _ );
        }

        public static void Draw(MetaFileManager metaFileManager, EqdpManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SetId.ToString());
            ImGuiUtil.HoverTooltip(ModelSetIdTooltipShort);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Race.ToName());
            ImGuiUtil.HoverTooltip(ModelRaceTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Gender.ToName());
            ImGuiUtil.HoverTooltip(GenderTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Slot.ToName());
            ImGuiUtil.HoverTooltip(EquipSlotTooltip);

            // Values
            var defaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(meta.Gender, meta.Race), meta.Slot.IsAccessory(),
                meta.SetId);
            var (defaultBit1, defaultBit2) = defaultEntry.ToBits(meta.Slot);
            var (bit1, bit2)               = meta.Entry.ToBits(meta.Slot);
            ImGui.TableNextColumn();
            if( Checkmark( "材质##eqdpCheck1", string.Empty, bit1, defaultBit1, out var newBit1 ) )
                editor.MetaEditor.Change(meta.Copy(Eqdp.FromSlotAndBits(meta.Slot, newBit1, bit2)));

            ImGui.SameLine();
            if( Checkmark( "模型##eqdpCheck2", string.Empty, bit2, defaultBit2, out var newBit2 ) )
                editor.MetaEditor.Change(meta.Copy(Eqdp.FromSlotAndBits(meta.Slot, bit1, newBit2)));
        }
    }

    private static class ImcRow
    {
        private static ImcIdentifier _newIdentifier = ImcIdentifier.Default;

        private static float IdWidth
            => 80 * UiHelpers.Scale;

        private static float SmallIdWidth
            => 45 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton( "将当前所有IMC操作复制到剪贴板。", iconSize,
                editor.MetaEditor.Imc.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var (defaultEntry, fileExists, _) = metaFileManager.ImcChecker.GetDefaultEntry(_newIdentifier, true);
            var manip  = (MetaManipulation)new ImcManipulation(_newIdentifier, defaultEntry);
            var canAdd = fileExists && editor.MetaEditor.CanAdd(manip);
            var tt     = canAdd ? "进行此项编辑。" : !fileExists ? "此IMC文件不存在。" : "此选项已经修改过了。";
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(manip);

            // Identifier
            ImGui.TableNextColumn();
            var change = ImcManipulationDrawer.DrawObjectType(ref _newIdentifier);

            ImGui.TableNextColumn();
            change |= ImcManipulationDrawer.DrawPrimaryId(ref _newIdentifier);
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                new Vector2(3 * UiHelpers.Scale, ImGui.GetStyle().ItemSpacing.Y));

            ImGui.TableNextColumn();
            // Equipment and accessories are slightly different imcs than other types.
            if (_newIdentifier.ObjectType is ObjectType.Equipment or ObjectType.Accessory)
                change |= ImcManipulationDrawer.DrawSlot(ref _newIdentifier);
            else
                change |= ImcManipulationDrawer.DrawSecondaryId(ref _newIdentifier);

            ImGui.TableNextColumn();
            change |= ImcManipulationDrawer.DrawVariant(ref _newIdentifier);

            ImGui.TableNextColumn();
            if (_newIdentifier.ObjectType is ObjectType.DemiHuman)
                change |= ImcManipulationDrawer.DrawSlot(ref _newIdentifier, 70);
            else
                ImGui.Dummy(new Vector2(70 * UiHelpers.Scale, 0));

            if (change)
                defaultEntry = metaFileManager.ImcChecker.GetDefaultEntry(_newIdentifier, true).Entry;
            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            ImcManipulationDrawer.DrawMaterialId(defaultEntry, ref defaultEntry, false);
            ImGui.SameLine();
            ImcManipulationDrawer.DrawMaterialAnimationId(defaultEntry, ref defaultEntry, false);
            ImGui.TableNextColumn();
            ImcManipulationDrawer.DrawDecalId(defaultEntry, ref defaultEntry, false);
            ImGui.SameLine();
            ImcManipulationDrawer.DrawVfxId(defaultEntry, ref defaultEntry, false);
            ImGui.SameLine();
            ImcManipulationDrawer.DrawSoundId(defaultEntry, ref defaultEntry, false);
            ImGui.TableNextColumn();
            ImcManipulationDrawer.DrawAttributes(defaultEntry, ref defaultEntry);
        }

        public static void Draw(MetaFileManager metaFileManager, ImcManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.ObjectType.ToName());
            ImGuiUtil.HoverTooltip(ObjectTypeTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.PrimaryId.ToString());
            ImGuiUtil.HoverTooltip(PrimaryIdTooltipShort);

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            if (meta.ObjectType is ObjectType.Equipment or ObjectType.Accessory)
            {
                ImGui.TextUnformatted(meta.EquipSlot.ToName());
                ImGuiUtil.HoverTooltip(EquipSlotTooltip);
            }
            else
            {
                ImGui.TextUnformatted(meta.SecondaryId.ToString());
                ImGuiUtil.HoverTooltip(SecondaryIdTooltip);
            }

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Variant.ToString());
            ImGuiUtil.HoverTooltip(VariantIdTooltip);

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            if (meta.ObjectType is ObjectType.DemiHuman)
            {
                ImGui.TextUnformatted(meta.EquipSlot.ToName());
                ImGuiUtil.HoverTooltip(EquipSlotTooltip);
            }

            // Values
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                new Vector2(3 * UiHelpers.Scale, ImGui.GetStyle().ItemSpacing.Y));
            ImGui.TableNextColumn();
            var defaultEntry = metaFileManager.ImcChecker.GetDefaultEntry(meta.Identifier, true).Entry;
            var newEntry     = meta.Entry;
            var changes      = ImcManipulationDrawer.DrawMaterialId(defaultEntry, ref newEntry, true);
            ImGui.SameLine();
            changes |= ImcManipulationDrawer.DrawMaterialAnimationId(defaultEntry, ref newEntry, true);
            ImGui.TableNextColumn();
            changes |= ImcManipulationDrawer.DrawDecalId(defaultEntry, ref newEntry, true);
            ImGui.SameLine();
            changes |= ImcManipulationDrawer.DrawVfxId(defaultEntry, ref newEntry, true);
            ImGui.SameLine();
            changes |= ImcManipulationDrawer.DrawSoundId(defaultEntry, ref newEntry, true);
            ImGui.TableNextColumn();
            changes |= ImcManipulationDrawer.DrawAttributes(defaultEntry, ref newEntry);

            if (changes)
                editor.MetaEditor.Change(meta.Copy(newEntry));
        }
    }

    private static class EstRow
    {
        private static EstManipulation _new = new(Gender.Male, ModelRace.Midlander, EstManipulation.EstType.Body, 1, 0);

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton( "将当前所有 EST 操作复制到剪贴板。", iconSize,
                editor.MetaEditor.Est.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd       = editor.MetaEditor.CanAdd(_new);
            var tt           = canAdd ? "进行此项编辑" : "此项已经在编辑了。";
            var defaultEntry = EstFile.GetDefault(metaFileManager, _new.Slot, Names.CombinedRace(_new.Gender, _new.Race), _new.SetId);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (IdInput("##estId", IdWidth, _new.SetId.Id, out var setId, 0, ExpandedEqpGmpBase.Count - 1, _new.SetId <= 1))
            {
                var newDefaultEntry = EstFile.GetDefault(metaFileManager, _new.Slot, Names.CombinedRace(_new.Gender, _new.Race), setId);
                _new = new EstManipulation(_new.Gender, _new.Race, _new.Slot, setId, newDefaultEntry);
            }

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            ImGui.TableNextColumn();
            if (Combos.Race("##estRace", _new.Race, out var race))
            {
                var newDefaultEntry = EstFile.GetDefault(metaFileManager, _new.Slot, Names.CombinedRace(_new.Gender, race), _new.SetId);
                _new = new EstManipulation(_new.Gender, race, _new.Slot, _new.SetId, newDefaultEntry);
            }

            ImGuiUtil.HoverTooltip(ModelRaceTooltip);

            ImGui.TableNextColumn();
            if (Combos.Gender("##estGender", _new.Gender, out var gender))
            {
                var newDefaultEntry = EstFile.GetDefault(metaFileManager, _new.Slot, Names.CombinedRace(gender, _new.Race), _new.SetId);
                _new = new EstManipulation(gender, _new.Race, _new.Slot, _new.SetId, newDefaultEntry);
            }

            ImGuiUtil.HoverTooltip(GenderTooltip);

            ImGui.TableNextColumn();
            if (Combos.EstSlot("##estSlot", _new.Slot, out var slot))
            {
                var newDefaultEntry = EstFile.GetDefault(metaFileManager, slot, Names.CombinedRace(_new.Gender, _new.Race), _new.SetId);
                _new = new EstManipulation(_new.Gender, _new.Race, slot, _new.SetId, newDefaultEntry);
            }

            ImGuiUtil.HoverTooltip(EstTypeTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            IntDragInput( "##estSkeleton", "骨骼索引", IdWidth, _new.Entry, defaultEntry, out _, 0, ushort.MaxValue, 0.05f );
        }

        public static void Draw(MetaFileManager metaFileManager, EstManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SetId.ToString());
            ImGuiUtil.HoverTooltip(ModelSetIdTooltipShort);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Race.ToName());
            ImGuiUtil.HoverTooltip(ModelRaceTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Gender.ToName());
            ImGuiUtil.HoverTooltip(GenderTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Slot.ToString());
            ImGuiUtil.HoverTooltip(EstTypeTooltip);

            // Values
            var defaultEntry = EstFile.GetDefault(metaFileManager, meta.Slot, Names.CombinedRace(meta.Gender, meta.Race), meta.SetId);
            ImGui.TableNextColumn();
            if( IntDragInput( "##estSkeleton", $"骨骼索引\n默认值： {defaultEntry}", IdWidth, meta.Entry, defaultEntry,
                    out var entry,            0,                                                ushort.MaxValue, 0.05f))
                editor.MetaEditor.Change(meta.Copy((ushort)entry));
        }
    }

    private static class GmpRow
    {
        private static GmpManipulation _new = new(GmpEntry.Default, 1);

        private static float RotationWidth
            => 75 * UiHelpers.Scale;

        private static float UnkWidth
            => 50 * UiHelpers.Scale;

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton( "将当前所有GMP操作复制到剪贴板。", iconSize,
                editor.MetaEditor.Gmp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd       = editor.MetaEditor.CanAdd(_new);
            var tt           = canAdd ? "进行此项编辑。" : "此项已经在编辑了。";
            var defaultEntry = ExpandedGmpFile.GetDefault(metaFileManager, _new.SetId);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (IdInput("##gmpId", IdWidth, _new.SetId.Id, out var setId, 1, ExpandedEqpGmpBase.Count - 1, _new.SetId <= 1))
                _new = new GmpManipulation(ExpandedGmpFile.GetDefault(metaFileManager, setId), setId);

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            Checkmark( "##gmpEnabled", "调整启用", defaultEntry.Enabled, defaultEntry.Enabled, out _ );
            ImGui.TableNextColumn();
            Checkmark( "##gmpAnimated", "调整动画(过渡/瞬间)", defaultEntry.Animated, defaultEntry.Animated, out _ );
            ImGui.TableNextColumn();
            IntDragInput( "##gmpRotationA", "A方向旋转角度值", RotationWidth, defaultEntry.RotationA, defaultEntry.RotationA, out _, 0,
                360,                       0f);
            ImGui.SameLine();
            IntDragInput( "##gmpRotationB", "B方向旋转角度值", RotationWidth, defaultEntry.RotationB, defaultEntry.RotationB, out _, 0,
                360,                       0f);
            ImGui.SameLine();
            IntDragInput( "##gmpRotationC", "C方向旋转角度值", RotationWidth, defaultEntry.RotationC, defaultEntry.RotationC, out _, 0,
                360,                       0f);
            ImGui.TableNextColumn();
            IntDragInput( "##gmpUnkA", "动画类型 A?（但似乎是能发光的头部装备的发光强度）", UnkWidth, defaultEntry.UnknownA, defaultEntry.UnknownA, out _, 0, 15, 0f );
            ImGui.SameLine();
            IntDragInput( "##gmpUnkB", "动画类型 B?", UnkWidth, defaultEntry.UnknownB, defaultEntry.UnknownB, out _, 0, 15, 0f );
        }

        public static void Draw(MetaFileManager metaFileManager, GmpManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SetId.ToString());
            ImGuiUtil.HoverTooltip(ModelSetIdTooltipShort);

            // Values
            var defaultEntry = ExpandedGmpFile.GetDefault(metaFileManager, meta.SetId);
            ImGui.TableNextColumn();
            if( Checkmark( "##gmpEnabled", "调整启用", meta.Entry.Enabled, defaultEntry.Enabled, out var enabled ) )
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { Enabled = enabled }));

            ImGui.TableNextColumn();
            if( Checkmark( "##gmpAnimated", "调整动画", meta.Entry.Animated, defaultEntry.Animated, out var animated ) )
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { Animated = animated }));

            ImGui.TableNextColumn();
            if( IntDragInput( "##gmpRotationA", $"A方向旋转角度值\n默认值： {defaultEntry.RotationA}", RotationWidth,
                    meta.Entry.RotationA,      defaultEntry.RotationA, out var rotationA, 0, 360, 0.05f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { RotationA = (ushort)rotationA }));

            ImGui.SameLine();
            if( IntDragInput( "##gmpRotationB", $"B方向旋转角度值\n默认值： {defaultEntry.RotationB}", RotationWidth,
                    meta.Entry.RotationB,      defaultEntry.RotationB, out var rotationB, 0, 360, 0.05f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { RotationB = (ushort)rotationB }));

            ImGui.SameLine();
            if( IntDragInput( "##gmpRotationC", $"C方向旋转角度值\n默认值： {defaultEntry.RotationC}", RotationWidth,
                    meta.Entry.RotationC,      defaultEntry.RotationC, out var rotationC, 0, 360, 0.05f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { RotationC = (ushort)rotationC }));

            ImGui.TableNextColumn();
            if( IntDragInput( "##gmpUnkA", $"动画类型A?\n默认值： {defaultEntry.UnknownA}", UnkWidth, meta.Entry.UnknownA,
                    defaultEntry.UnknownA, out var unkA,                                                 0,        15, 0.01f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { UnknownA = (byte)unkA }));

            ImGui.SameLine();
            if( IntDragInput( "##gmpUnkB", $"动画类型B?\n默认值： {defaultEntry.UnknownB}", UnkWidth, meta.Entry.UnknownB,
                    defaultEntry.UnknownB, out var unkB,                                                 0,        15, 0.01f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { UnknownA = (byte)unkB }));
        }
    }

    private static class RspRow
    {
        private static RspManipulation _new = new(SubRace.Midlander, RspAttribute.MaleMinSize, 1f);

        private static float FloatWidth
            => 150 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton( "复制所有RSP操作到剪贴板.", iconSize,
                editor.MetaEditor.Rsp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd       = editor.MetaEditor.CanAdd(_new);
            var tt           = canAdd ? "进行此项编辑。" : "此选项已经在编辑了。";
            var defaultEntry = CmpFile.GetDefault(metaFileManager, _new.SubRace, _new.Attribute);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (Combos.SubRace("##rspSubRace", _new.SubRace, out var subRace))
                _new = new RspManipulation(subRace, _new.Attribute, CmpFile.GetDefault(metaFileManager, subRace, _new.Attribute));

            ImGuiUtil.HoverTooltip(RacialTribeTooltip);

            ImGui.TableNextColumn();
            if (Combos.RspAttribute("##rspAttribute", _new.Attribute, out var attribute))
                _new = new RspManipulation(_new.SubRace, attribute, CmpFile.GetDefault(metaFileManager, subRace, attribute));

            ImGuiUtil.HoverTooltip(ScalingTypeTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(FloatWidth);
            ImGui.DragFloat("##rspValue", ref defaultEntry, 0f);
        }

        public static void Draw(MetaFileManager metaFileManager, RspManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SubRace.ToName());
            ImGuiUtil.HoverTooltip(RacialTribeTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Attribute.ToFullString());
            ImGuiUtil.HoverTooltip(ScalingTypeTooltip);
            ImGui.TableNextColumn();

            // Values
            var def   = CmpFile.GetDefault(metaFileManager, meta.SubRace, meta.Attribute);
            var value = meta.Entry;
            ImGui.SetNextItemWidth(FloatWidth);
            using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
                def < value ? ColorId.IncreasedMetaValue.Value() : ColorId.DecreasedMetaValue.Value(),
                def != value);
            if (ImGui.DragFloat("##rspValue", ref value, 0.001f, RspManipulation.MinValue, RspManipulation.MaxValue)
             && value is >= RspManipulation.MinValue and <= RspManipulation.MaxValue)
                editor.MetaEditor.Change(meta.Copy(value));

            ImGuiUtil.HoverTooltip($"Default Value: {def:0.###}");
        }
    }

    private static class GlobalEqpRow
    {
        private static GlobalEqpManipulation _new = new()
        {
            Type      = GlobalEqpType.DoNotHideEarrings,
            Condition = 1,
        };

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("复制所有全局EQP操作到剪贴板。", iconSize,
                editor.MetaEditor.GlobalEqp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd = editor.MetaEditor.CanAdd(_new);
            var tt     = canAdd ? "进行此项编辑。" : "这个条目已经被操作过了。";
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(250 * ImUtf8.GlobalScale);
            using (var combo = ImUtf8.Combo("##geqpType"u8, _new.Type.ToName()))
            {
                if (combo)
                    foreach (var type in Enum.GetValues<GlobalEqpType>())
                    {
                        if (ImUtf8.Selectable(type.ToName(), type == _new.Type))
                            _new = new GlobalEqpManipulation
                            {
                                Type      = type,
                                Condition = type.HasCondition() ? _new.Type.HasCondition() ? _new.Condition : 1 : 0,
                            };
                        ImUtf8.HoverTooltip(type.ToDescription());
                    }
            }

            ImUtf8.HoverTooltip(_new.Type.ToDescription());

            ImGui.TableNextColumn();
            if (!_new.Type.HasCondition())
                return;

            if (IdInput("##geqpCond", 100 * ImUtf8.GlobalScale, _new.Condition.Id, out var newId, 1, ushort.MaxValue, _new.Condition.Id <= 1))
                _new = _new with { Condition = newId };
        }

        public static void Draw(MetaFileManager metaFileManager, GlobalEqpManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImUtf8.Text(meta.Type.ToName());
            ImUtf8.HoverTooltip(meta.Type.ToDescription());
            ImGui.TableNextColumn();
            if (meta.Type.HasCondition())
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
                ImUtf8.Text($"{meta.Condition.Id}");
            }
        }
    }

    // A number input for ids with a optional max id of given width.
    // Returns true if newId changed against currentId.
    private static bool IdInput(string label, float width, ushort currentId, out ushort newId, int minId, int maxId, bool border)
    {
        int tmp = currentId;
        ImGui.SetNextItemWidth(width);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, UiHelpers.Scale, border);
        using var color = ImRaii.PushColor(ImGuiCol.Border, Colors.RegexWarningBorder, border);
        if (ImGui.InputInt(label, ref tmp, 0))
            tmp = Math.Clamp(tmp, minId, maxId);

        newId = (ushort)tmp;
        return newId != currentId;
    }

    // A checkmark that compares against a default value and shows a tooltip.
    // Returns true if newValue is changed against currentValue.
    private static bool Checkmark(string label, string tooltip, bool currentValue, bool defaultValue, out bool newValue)
    {
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
            defaultValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue);
        newValue = currentValue;
        ImGui.Checkbox(label, ref newValue);
        ImGuiUtil.HoverTooltip(tooltip, ImGuiHoveredFlags.AllowWhenDisabled);
        return newValue != currentValue;
    }

    // A dragging int input of given width that compares against a default value, shows a tooltip and clamps against min and max.
    // Returns true if newValue changed against currentValue.
    private static bool IntDragInput(string label, string tooltip, float width, int currentValue, int defaultValue, out int newValue,
        int minValue, int maxValue, float speed)
    {
        newValue = currentValue;
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
            defaultValue > currentValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue);
        ImGui.SetNextItemWidth(width);
        if (ImGui.DragInt(label, ref newValue, speed, minValue, maxValue))
            newValue = Math.Clamp(newValue, minValue, maxValue);

        ImGuiUtil.HoverTooltip(tooltip, ImGuiHoveredFlags.AllowWhenDisabled);

        return newValue != currentValue;
    }

    private static void CopyToClipboardButton(string tooltip, Vector2 iconSize, IEnumerable<MetaManipulation> manipulations)
    {
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clipboard.ToIconString(), iconSize, tooltip, false, true))
            return;

        var text = Functions.ToCompressedBase64(manipulations, MetaManipulation.CurrentVersion);
        if (text.Length > 0)
            ImGui.SetClipboardText(text);
    }

    private void AddFromClipboardButton()
    {
        if( ImGui.Button( "添加剪贴板中的设置" ) )
        {
            var clipboard = ImGuiUtil.GetClipboardText();

            var version = Functions.FromCompressedBase64<MetaManipulation[]>(clipboard, out var manips);
            if (version == MetaManipulation.CurrentVersion && manips != null)
                foreach (var manip in manips.Where(m => m.ManipulationType != MetaManipulation.Type.Unknown))
                    _editor.MetaEditor.Set(manip);
        }

        ImGuiUtil.HoverTooltip(
            "尝试将存储在剪贴板中的元数据操作添加到当前设置。\n会覆盖已存在的操作，不会移除此模组中做过的其他操作。" );
    }

    private void SetFromClipboardButton()
    {
        if( ImGui.Button( "应用剪贴板中的设置" ) )
        {
            var clipboard = ImGuiUtil.GetClipboardText();
            var version   = Functions.FromCompressedBase64<MetaManipulation[]>(clipboard, out var manips);
            if (version == MetaManipulation.CurrentVersion && manips != null)
            {
                _editor.MetaEditor.Clear();
                foreach (var manip in manips.Where(m => m.ManipulationType != MetaManipulation.Type.Unknown))
                    _editor.MetaEditor.Set(manip);
            }
        }

        ImGuiUtil.HoverTooltip(
            "尝试将剪贴板中存储的元数据操作应用到当前的设置中。\n会移除此模组中做过的其他元数据操作。" );
    }

    private static void DrawMetaButtons(MetaManipulation meta, ModEditor editor, Vector2 iconSize)
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton( "将此操作复制到剪贴板。", iconSize, Array.Empty< MetaManipulation >().Append( meta ) );

        ImGui.TableNextColumn();
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), iconSize, "删除这个元数据操作。", false, true ) )
            editor.MetaEditor.Delete(meta);
    }
}

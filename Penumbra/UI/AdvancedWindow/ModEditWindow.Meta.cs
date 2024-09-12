using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Api.Api;
using Penumbra.Meta.Manipulations;
using Penumbra.UI.AdvancedWindow.Meta;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly MetaDrawers _metaDrawers;

    private void DrawMetaTab()
    {
        using var tab = ImUtf8.TabItem("元数据操作"u8);
        if (!tab)
            return;

        DrawOptionSelectHeader();

        var setsEqual = !_editor.MetaEditor.Changes;
        var tt        = setsEqual ? "没有进行任何更改。"u8 : "应用当前暂存的更改。"u8;
        ImGui.NewLine();
        if (ImUtf8.ButtonEx("应用更改"u8, tt, Vector2.Zero, setsEqual))
            _editor.MetaEditor.Apply(_editor.Option!);

        ImGui.SameLine();
        tt = setsEqual ? "没有进行任何更改。"u8 : "撤销当前进行的所有更改。"u8;
        if (ImUtf8.ButtonEx("撤销更改"u8, tt, Vector2.Zero, setsEqual))
            _editor.MetaEditor.Load(_editor.Mod!, _editor.Option!);

        ImGui.SameLine();
        AddFromClipboardButton();
        ImGui.SameLine();
        SetFromClipboardButton();
        ImGui.SameLine();
        CopyToClipboardButton("将当前的所有操作复制到剪贴板。", _iconSize, _editor.MetaEditor);
        ImGui.SameLine();
        if (ImUtf8.Button("写入为TexTools文件"u8))
            _metaFileManager.WriteAllTexToolsMeta(Mod!);
        ImGui.SameLine();
        if (ImUtf8.ButtonEx("移除所有默认值", "删除列表中所有被设置为默认值的条目。"u8))
            _editor.MetaEditor.DeleteDefaultValues();

        using var child = ImRaii.Child("##meta", -Vector2.One, true);
        if (!child)
            return;

        DrawEditHeader(MetaManipulationType.Eqp);
        DrawEditHeader(MetaManipulationType.Eqdp);
        DrawEditHeader(MetaManipulationType.Imc);
        DrawEditHeader(MetaManipulationType.Est);
        DrawEditHeader(MetaManipulationType.Gmp);
        DrawEditHeader(MetaManipulationType.Rsp);
        DrawEditHeader(MetaManipulationType.GlobalEqp);
    }

    private void DrawEditHeader(MetaManipulationType type)
    {
        var drawer = _metaDrawers.Get(type);
        if (drawer == null)
            return;

        var oldPos = ImGui.GetCursorPosY();
        var header = ImUtf8.CollapsingHeader($"{_editor.MetaEditor.GetCount(type)} {drawer.Label}");
        DrawOtherOptionData(type, oldPos, ImGui.GetCursorPos());
        if (!header)
            return;

        DrawTable(drawer);
    }

    private static void DrawTable(IMetaDrawer drawer)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV;
        using var             table = ImUtf8.Table(drawer.Label, drawer.NumColumns, flags);
        if (!table)
            return;

        drawer.Draw();
        ImGui.NewLine();
    }

    private void DrawOtherOptionData(MetaManipulationType type, float oldPos, Vector2 newPos)
    {
        var otherOptionData = _editor.MetaEditor.OtherData[type];
        if (otherOptionData.TotalCount <= 0)
            return;

        var text = $"{otherOptionData.TotalCount} Edits in other Options";
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

    private static void CopyToClipboardButton(string tooltip, Vector2 iconSize, MetaDictionary manipulations)
    {
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clipboard.ToIconString(), iconSize, tooltip, false, true))
            return;

        var text = Functions.ToCompressedBase64(manipulations, MetaApi.CurrentVersion);
        if (text.Length > 0)
            ImGui.SetClipboardText(text);
    }

    private void AddFromClipboardButton()
    {
        if (ImGui.Button("添加剪贴板中的设置"))
        {
            var clipboard = ImGuiUtil.GetClipboardText();

            var version = Functions.FromCompressedBase64<MetaDictionary>(clipboard, out var manips);
            if (version == MetaApi.CurrentVersion && manips != null)
            {
                _editor.MetaEditor.UpdateTo(manips);
                _editor.MetaEditor.Changes = true;
            }
        }

        ImGuiUtil.HoverTooltip(
            "尝试将存储在剪贴板中的元数据操作添加到当前设置。\n会覆盖已存在的操作，不会移除此模组中做过的其他操作。");
    }

    private void SetFromClipboardButton()
    {
        if (ImGui.Button("应用剪贴板中的设置"))
        {
            var clipboard = ImGuiUtil.GetClipboardText();
            var version   = Functions.FromCompressedBase64<MetaDictionary>(clipboard, out var manips);
            if (version == MetaApi.CurrentVersion && manips != null)
            {
                _editor.MetaEditor.SetTo(manips);
                _editor.MetaEditor.Changes = true;
            }
        }

        ImGuiUtil.HoverTooltip(
            "尝试将剪贴板中存储的元数据操作应用到当前的设置中。\n会移除此模组中做过的其他元数据操作。");
    }
}

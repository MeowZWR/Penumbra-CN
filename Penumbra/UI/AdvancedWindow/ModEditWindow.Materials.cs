using ImSharp;
using Luna;
using Penumbra.Api.Enums;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private void DrawMaterialReassignmentTab()
    {
        if (_editor.Files.GetByType(ResourceType.Mdl).Count is 0)
            return;

        using var tab = Im.TabBar.BeginItem("材质指定"u8);
        if (!tab)
            return;

        Im.Line.Same();
        MaterialSuffix.Draw(_editor, ImEx.ScaledVector(175, 0));

        Im.Line.New();
        using var child = Im.Child.Begin("##mdlFiles"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        using var table = Im.Table.Begin("##files"u8, 4, TableFlags.RowBackground | TableFlags.SizingFixedFit, Im.ContentRegion.Available);
        if (!table)
            return;

        foreach (var (idx, info) in _editor.MdlMaterialEditor.ModelFiles.Index())
        {
            using var id = Im.Id.Push(idx);
            table.NextColumn();
            if (ImEx.Icon.Button(LunaStyle.SaveIcon, "保存修改过的mdl文件。\n请注意此操作有风险！"u8, !info.Changed))
                info.Save(_editor.Compactor);

            table.NextColumn();
            if (ImEx.Icon.Button(LunaStyle.RefreshIcon, "还原当前修改到默认状态。"u8, !info.Changed))
                info.Restore();

            table.NextColumn();
            Im.Text(info.Path.InternalName.Span[(Mod!.ModPath.FullName.Length + 1)..]);
            table.NextColumn();
            Im.Item.SetNextWidthScaled(400);
            var tmp = info.CurrentMaterials[0];
            if (Im.Input.Text("##0"u8, ref tmp))
                info.SetMaterial(tmp, 0);

            for (var i = 1; i < info.Count; ++i)
            {
                using var id2 = Im.Id.Push(i);
                table.NextColumn();
                table.NextColumn();
                table.NextColumn();
                table.NextColumn();
                Im.Item.SetNextWidthScaled(400);
                tmp = info.CurrentMaterials[i];
                if (Im.Input.Text(""u8, ref tmp))
                    info.SetMaterial(tmp, i);
            }
        }
    }
}

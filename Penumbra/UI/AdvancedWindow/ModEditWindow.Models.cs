﻿using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Custom;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.Import.Models;
using Penumbra.Mods;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private const int MdlMaterialMaximum = 4;

    private const string MdlImportDocumentation =
        @"https://github.com/xivdev/Penumbra/wiki/Model-IO#user-content-9b49d296-23ab-410a-845b-a3be769b71ea";

    private const string MdlExportDocumentation =
        @"https://github.com/xivdev/Penumbra/wiki/Model-IO#user-content-25968400-ebe5-4861-b610-cb1556db7ec4";

    private readonly FileEditor<MdlTab> _modelTab;
    private readonly ModelManager       _models;

    private          string           _modelNewMaterial           = string.Empty;
    private readonly List<TagButtons> _subMeshAttributeTagWidgets = [];
    private          string           _customPath                 = string.Empty;
    private          Utf8GamePath     _customGamePath             = Utf8GamePath.Empty;
    private          MdlFile          _lastFile                   = null!;
    private          long[]           _lodTriCount                = [];

    private void UpdateFile(MdlFile file, bool force)
    {
        if (file == _lastFile && !force)
            return;

        _lastFile = file;
        var subMeshTotal = file.Meshes.Aggregate(0, (count, mesh) => count + mesh.SubMeshCount);
        if (_subMeshAttributeTagWidgets.Count != subMeshTotal)
        {
            _subMeshAttributeTagWidgets.Clear();
            _subMeshAttributeTagWidgets.AddRange(
                Enumerable.Range(0, subMeshTotal).Select(_ => new TagButtons())
            );
        }

        _lodTriCount = Enumerable.Range(0, file.Lods.Length).Select(l => GetTriangleCountForLod(file, l)).ToArray();
    }

    private bool DrawModelPanel(MdlTab tab, bool disabled)
    {
        var ret = tab.Dirty;
        UpdateFile(tab.Mdl, ret);
        DrawImportExport(tab, disabled);

        ret |= DrawModelMaterialDetails(tab, disabled);

        if (ImGui.CollapsingHeader($"网格 ({_lastFile.Meshes.Length})###meshes"))
            for (var i = 0; i < _lastFile.LodCount; ++i)
                ret |= DrawModelLodDetails(tab, i, disabled);

        ret |= DrawOtherModelDetails(disabled);

        return !disabled && ret;
    }

    private void DrawImportExport(MdlTab tab, bool disabled)
    {
        if (!ImGui.CollapsingHeader("导入 / 导出"))
            return;

        var childSize = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);

        DrawImport(tab, childSize, disabled);
        ImGui.SameLine();
        DrawExport(tab, childSize, disabled);

        DrawIoExceptions(tab);
        DrawIoWarnings(tab);
    }

    private void DrawImport(MdlTab tab, Vector2 size, bool _1)
    {
        using var id = ImRaii.PushId("import");

        _dragDropManager.CreateImGuiSource("ModelDragDrop",
            m => m.Extensions.Any(e => ValidModelExtensions.Contains(e.ToLowerInvariant())), m =>
            {
                if (!GetFirstModel(m.Files, out var file))
                    return false;

                ImGui.TextUnformatted($"拖拽模型到此处进行编辑： {Path.GetFileName(file)}");
                return true;
            });

        using (var frame = ImRaii.FramedGroup("导入", size, headerPreIcon: FontAwesomeIcon.FileImport))
        {
            ImGui.Checkbox("保持当前材质", ref tab.ImportKeepMaterials);
            ImGui.Checkbox("保持当前属性", ref tab.ImportKeepAttributes);

            if (ImGuiUtil.DrawDisabledButton("导入glTF格式", Vector2.Zero, "导入一个glTF文件，覆盖此mdl的内容。",
                    tab.PendingIo))
                _fileDialog.OpenFilePicker("加载来自glTF的模型。", "glTF{.gltf,.glb}", (success, paths) =>
                {
                    if (success && paths.Count > 0)
                        tab.Import(paths[0]);
                }, 1, Mod!.ModPath.FullName, false);

            ImGui.SameLine();
            DrawDocumentationLink(MdlImportDocumentation);
        }

        if (_dragDropManager.CreateImGuiTarget("ModelDragDrop", out var files, out _) && GetFirstModel(files, out var importFile))
            tab.Import(importFile);
    }

    private void DrawExport(MdlTab tab, Vector2 size, bool _)
    {
        using var id    = ImRaii.PushId("export");
        using var frame = ImRaii.FramedGroup("导出", size, headerPreIcon: FontAwesomeIcon.FileExport);

        if (tab.GamePaths == null)
        {
            ImGui.TextUnformatted(tab.IoExceptions.Count == 0 ? "解析模型游戏路径。" : "解析模型游戏路径失败。");

            return;
        }

        DrawGamePathCombo(tab);

        ImGui.Checkbox("##exportGeneratedMissingBones", ref tab.ExportConfig.GenerateMissingBones);
        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker("生成丢失的骨骼",
            "警告：启用此选项可能导致导出的网格不可用。\n"
          + "它的主要目的是允许导出模型权重到不存在的骨骼。\n"
          + "在启用之前，请确保在当前合集中启用了依赖项，并且正确配置了EST元数据。");

        var gamePath = tab.GamePathIndex >= 0 && tab.GamePathIndex < tab.GamePaths.Count
            ? tab.GamePaths[tab.GamePathIndex]
            : _customGamePath;

        if (ImGuiUtil.DrawDisabledButton("导出为glTF格式", Vector2.Zero, "将此mdl文件导出到glTF，以便在3D创作应用程序中使用。",
                tab.PendingIo || gamePath.IsEmpty))
            _fileDialog.OpenSavePicker("将模型保存为glTF。", ".gltf", Path.GetFileNameWithoutExtension(gamePath.Filename().ToString()),
                ".gltf", (valid, path) =>
                {
                    if (!valid)
                        return;

                    tab.Export(path, gamePath);
                },
                Mod!.ModPath.FullName,
                false
            );

        ImGui.SameLine();
        DrawDocumentationLink(MdlExportDocumentation);
    }

    private static void DrawIoExceptions(MdlTab tab)
    {
        if (tab.IoExceptions.Count == 0)
            return;

        var size = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        using var frame = ImRaii.FramedGroup("Exceptions", size, headerPreIcon: FontAwesomeIcon.TimesCircle,
            borderColor: Colors.RegexWarningBorder);

        var spaceAvail = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X - 100;
        foreach (var (exception, index) in tab.IoExceptions.WithIndex())
        {
            using var id       = ImRaii.PushId(index);
            var       message  = $"{exception.GetType().Name}: {exception.Message}";
            var       textSize = ImGui.CalcTextSize(message).X;
            if (textSize > spaceAvail)
                message = message[..(int)Math.Floor(message.Length * (spaceAvail / textSize))] + "...";

            using var exceptionNode = ImRaii.TreeNode(message);
            if (exceptionNode)
            {
                using var indent = ImRaii.PushIndent();
                ImGuiUtil.TextWrapped(exception.ToString());
            }
        }
    }

    private static void DrawIoWarnings(MdlTab tab)
    {
        if (tab.IoWarnings.Count == 0)
            return;

        var       size  = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        using var frame = ImRaii.FramedGroup("警告", size, headerPreIcon: FontAwesomeIcon.ExclamationCircle, borderColor: 0xFF40FFFF);

        var spaceAvail = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X - 100;
        foreach (var (warning, index) in tab.IoWarnings.WithIndex())
        {
            using var id       = ImRaii.PushId(index);
            var       textSize = ImGui.CalcTextSize(warning).X;

            if (textSize <= spaceAvail)
            {
                ImRaii.TreeNode(warning, ImGuiTreeNodeFlags.Leaf).Dispose();
                continue;
            }

            var firstLine = warning[..(int)Math.Floor(warning.Length * (spaceAvail / textSize))] + "...";

            using var warningNode = ImRaii.TreeNode(firstLine);
            if (warningNode)
            {
                using var indent = ImRaii.PushIndent();
                ImGuiUtil.TextWrapped(warning);
            }
        }
    }

    private void DrawGamePathCombo(MdlTab tab)
    {
        if (tab.GamePaths!.Count != 0)
        {
            DrawComboButton(tab);
            return;
        }

        ImGui.TextUnformatted("未检测到关联的游戏路径。有效的游戏路径是当前导出所必需的。");
        if (!ImGui.InputTextWithHint("##customInput", "输入自定义游戏路径...", ref _customPath, 256))
            return;

        if (!Utf8GamePath.FromString(_customPath, out _customGamePath, false))
            _customGamePath = Utf8GamePath.Empty;
    }

    /// <summary> I disliked the combo with only one selection so turn it into a button in that case. </summary>
    private static void DrawComboButton(MdlTab tab)
    {
        const string label       = "游戏路径";
        var          preview     = tab.GamePaths![tab.GamePathIndex].ToString();
        var          labelWidth  = ImGui.CalcTextSize(label).X + ImGui.GetStyle().ItemInnerSpacing.X;
        var          buttonWidth = ImGui.GetContentRegionAvail().X - labelWidth - ImGui.GetStyle().ItemSpacing.X;
        if (tab.GamePaths!.Count == 1)
        {
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));
            using var color = ImRaii.PushColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.FrameBg))
                .Push(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.FrameBgHovered))
                .Push(ImGuiCol.ButtonActive,  ImGui.GetColorU32(ImGuiCol.FrameBgActive));
            using var group = ImRaii.Group();
            ImGui.Button(preview, new Vector2(buttonWidth, 0));
            ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.TextUnformatted("游戏路径");
        }
        else
        {
            ImGui.SetNextItemWidth(buttonWidth);
            using var combo = ImRaii.Combo("游戏路径", preview);
            if (combo.Success)
                foreach (var (path, index) in tab.GamePaths.WithIndex())
                {
                    if (!ImGui.Selectable(path.ToString(), index == tab.GamePathIndex))
                        continue;

                    tab.GamePathIndex = index;
                }
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.SetClipboardText(preview);
        ImGuiUtil.HoverTooltip("右键单击可复制到剪贴板。", ImGuiHoveredFlags.AllowWhenDisabled);
    }

    private void DrawDocumentationLink(string address)
    {
        const string text = "说明文档（英文） →";

        var framePadding = ImGui.GetStyle().FramePadding;
        var width        = ImGui.CalcTextSize(text).X + framePadding.X * 2;

        // Draw the link button. We set the background colour to transparent to mimic the look of a link.
        using var color = ImRaii.PushColor(ImGuiCol.Button, 0x00000000);
        CustomGui.DrawLinkButton(Penumbra.Messager, text, address, width);

        // Draw an underline for the text.
        var lineStart = ImGui.GetItemRectMax();
        lineStart -= framePadding;
        var lineEnd = lineStart with { X = ImGui.GetItemRectMin().X + framePadding.X };
        ImGui.GetWindowDrawList().AddLine(lineStart, lineEnd, 0xFFFFFFFF);
    }

    private bool DrawModelMaterialDetails(MdlTab tab, bool disabled)
    {
        if (!ImGui.CollapsingHeader("材质"))
            return false;

        using var table = ImRaii.Table(string.Empty, disabled ? 2 : 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return false;

        var ret       = false;
        var materials = tab.Mdl.Materials;

        ImGui.TableSetupColumn("index", ImGuiTableColumnFlags.WidthFixed,   80 * UiHelpers.Scale);
        ImGui.TableSetupColumn("path",  ImGuiTableColumnFlags.WidthStretch, 1);
        if (!disabled)
            ImGui.TableSetupColumn("actions", ImGuiTableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);

        var inputFlags = disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None;
        for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            ret |= DrawMaterialRow(tab, disabled, materials, materialIndex, inputFlags);

        if (materials.Length >= MdlMaterialMaximum || disabled)
            return ret;

        ImGui.TableNextColumn();

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##newMaterial", "添加新材质...", ref _modelNewMaterial, Utf8GamePath.MaxGamePathLength, inputFlags);
        var validName = _modelNewMaterial.Length > 0 && _modelNewMaterial[0] == '/';
        ImGui.TableNextColumn();
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), UiHelpers.IconButtonSize, string.Empty, !validName, true))
            return ret;

        tab.Mdl.Materials = materials.AddItem(_modelNewMaterial);
        _modelNewMaterial = string.Empty;
        return true;
    }

    private bool DrawMaterialRow(MdlTab tab, bool disabled, string[] materials, int materialIndex, ImGuiInputTextFlags inputFlags)
    {
        using var id  = ImRaii.PushId(materialIndex);
        var       ret = false;
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"材质 #{materialIndex + 1}");

        var temp = materials[materialIndex];
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText($"##material{materialIndex}", ref temp, Utf8GamePath.MaxGamePathLength, inputFlags)
         && temp.Length > 0
         && temp != materials[materialIndex]
           )
        {
            materials[materialIndex] = temp;
            ret                      = true;
        }

        if (disabled)
            return ret;

        ImGui.TableNextColumn();

        // Need to have at least one material.
        if (materials.Length <= 1)
            return ret;

        var tt             = "删除此材料。\n以该材质为目标的任何网格都将更新为使用材质 #1.";
        var modifierActive = _config.DeleteModModifier.IsActive();
        if (!modifierActive)
            tt += $"\n按住{_config.DeleteModModifier}进行删除操作。";
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize, tt, !modifierActive, true))
            return ret;

        tab.RemoveMaterial(materialIndex);
        return true;
    }

    private bool DrawModelLodDetails(MdlTab tab, int lodIndex, bool disabled)
    {
        using var lodNode = ImRaii.TreeNode($"细节层次 #{lodIndex + 1}", ImGuiTreeNodeFlags.DefaultOpen);
        if (!lodNode)
            return false;

        var lod = tab.Mdl.Lods[lodIndex];
        var ret = false;

        for (var meshOffset = 0; meshOffset < lod.MeshCount; meshOffset++)
            ret |= DrawModelMeshDetails(tab, lod.MeshIndex + meshOffset, disabled);

        return ret;
    }

    private bool DrawModelMeshDetails(MdlTab tab, int meshIndex, bool disabled)
    {
        using var meshNode = ImRaii.TreeNode($"网格 #{meshIndex + 1}", ImGuiTreeNodeFlags.DefaultOpen);
        if (!meshNode)
            return false;

        using var id    = ImRaii.PushId(meshIndex);
        using var table = ImRaii.Table(string.Empty, 2, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return false;

        ImGui.TableSetupColumn("name",  ImGuiTableColumnFlags.WidthFixed,   100 * UiHelpers.Scale);
        ImGui.TableSetupColumn("field", ImGuiTableColumnFlags.WidthStretch, 1);

        var file = tab.Mdl;
        var mesh = file.Meshes[meshIndex];

        // Mesh material
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("材质");

        ImGui.TableNextColumn();
        var ret = DrawMaterialCombo(tab, meshIndex, disabled);

        // Sub meshes
        for (var subMeshOffset = 0; subMeshOffset < mesh.SubMeshCount; subMeshOffset++)
            ret |= DrawSubMeshAttributes(tab, meshIndex, subMeshOffset, disabled);

        return ret;
    }

    private static bool DrawMaterialCombo(MdlTab tab, int meshIndex, bool disabled)
    {
        var       mesh = tab.Mdl.Meshes[meshIndex];
        using var _    = ImRaii.Disabled(disabled);
        ImGui.SetNextItemWidth(-1);
        using var materialCombo = ImRaii.Combo("##material", tab.Mdl.Materials[mesh.MaterialIndex]);

        if (!materialCombo)
            return false;

        var ret = false;
        foreach (var (material, materialIndex) in tab.Mdl.Materials.WithIndex())
        {
            if (!ImGui.Selectable(material, mesh.MaterialIndex == materialIndex))
                continue;

            tab.Mdl.Meshes[meshIndex].MaterialIndex = (ushort)materialIndex;
            ret                                     = true;
        }

        return ret;
    }

    private bool DrawSubMeshAttributes(MdlTab tab, int meshIndex, int subMeshOffset, bool disabled)
    {
        using var _ = ImRaii.PushId(subMeshOffset);

        var mesh         = tab.Mdl.Meshes[meshIndex];
        var subMeshIndex = mesh.SubMeshIndex + subMeshOffset;

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"属性 #{subMeshOffset + 1}");

        ImGui.TableNextColumn();
        var widget     = _subMeshAttributeTagWidgets[subMeshIndex];
        var attributes = tab.GetSubMeshAttributes(subMeshIndex);

        if (attributes == null)
        {
            attributes = ["invalid attribute data"];
            disabled   = true;
        }

        var tagIndex = widget.Draw(string.Empty, string.Empty, attributes,
            out var editedAttribute, !disabled);
        if (tagIndex < 0)
            return false;

        var oldName = tagIndex < attributes.Count ? attributes[tagIndex] : null;
        var newName = editedAttribute.Length > 0 ? editedAttribute : null;
        tab.UpdateSubMeshAttribute(subMeshIndex, oldName, newName);

        return true;
    }

    private bool DrawOtherModelDetails(bool _)
    {
        using var header = ImRaii.CollapsingHeader("更多内容");
        if (!header)
            return false;

        using (var table = ImRaii.Table("##data", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (table)
            {
                ImGuiUtil.DrawTableColumn("Version");
                ImGuiUtil.DrawTableColumn(_lastFile.Version.ToString());
                ImGuiUtil.DrawTableColumn("Radius");
                ImGuiUtil.DrawTableColumn(_lastFile.Radius.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("Model Clip Out Distance");
                ImGuiUtil.DrawTableColumn(_lastFile.ModelClipOutDistance.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("Shadow Clip Out Distance");
                ImGuiUtil.DrawTableColumn(_lastFile.ShadowClipOutDistance.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("LOD Count");
                ImGuiUtil.DrawTableColumn(_lastFile.LodCount.ToString());
                ImGuiUtil.DrawTableColumn("Enable Index Buffer Streaming");
                ImGuiUtil.DrawTableColumn(_lastFile.EnableIndexBufferStreaming.ToString());
                ImGuiUtil.DrawTableColumn("Enable Edge Geometry");
                ImGuiUtil.DrawTableColumn(_lastFile.EnableEdgeGeometry.ToString());
                ImGuiUtil.DrawTableColumn("Flags 1");
                ImGuiUtil.DrawTableColumn(_lastFile.Flags1.ToString());
                ImGuiUtil.DrawTableColumn("Flags 2");
                ImGuiUtil.DrawTableColumn(_lastFile.Flags2.ToString());
                ImGuiUtil.DrawTableColumn("Vertex Declarations");
                ImGuiUtil.DrawTableColumn(_lastFile.VertexDeclarations.Length.ToString());
                ImGuiUtil.DrawTableColumn("Bone Bounding Boxes");
                ImGuiUtil.DrawTableColumn(_lastFile.BoneBoundingBoxes.Length.ToString());
                ImGuiUtil.DrawTableColumn("Bone Tables");
                ImGuiUtil.DrawTableColumn(_lastFile.BoneTables.Length.ToString());
                ImGuiUtil.DrawTableColumn("Element IDs");
                ImGuiUtil.DrawTableColumn(_lastFile.ElementIds.Length.ToString());
                ImGuiUtil.DrawTableColumn("Extra LoDs");
                ImGuiUtil.DrawTableColumn(_lastFile.ExtraLods.Length.ToString());
                ImGuiUtil.DrawTableColumn("Meshes");
                ImGuiUtil.DrawTableColumn(_lastFile.Meshes.Length.ToString());
                ImGuiUtil.DrawTableColumn("Shape Meshes");
                ImGuiUtil.DrawTableColumn(_lastFile.ShapeMeshes.Length.ToString());
                ImGuiUtil.DrawTableColumn("LoDs");
                ImGuiUtil.DrawTableColumn(_lastFile.Lods.Length.ToString());
                ImGuiUtil.DrawTableColumn("Vertex Declarations");
                ImGuiUtil.DrawTableColumn(_lastFile.VertexDeclarations.Length.ToString());
                ImGuiUtil.DrawTableColumn("Stack Size");
                ImGuiUtil.DrawTableColumn(_lastFile.StackSize.ToString());
                foreach (var (triCount, lod) in _lodTriCount.WithIndex())
                {
                    ImGuiUtil.DrawTableColumn($"LOD #{lod + 1} Triangle Count");
                    ImGuiUtil.DrawTableColumn(triCount.ToString());
                }
            }
        }

        using (var materials = ImRaii.TreeNode("Materials", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (materials)
                foreach (var material in _lastFile.Materials)
                    ImRaii.TreeNode(material, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var attributes = ImRaii.TreeNode("Attributes", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (attributes)
                foreach (var attribute in _lastFile.Attributes)
                    ImRaii.TreeNode(attribute, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var bones = ImRaii.TreeNode("Bones", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (bones)
                foreach (var bone in _lastFile.Bones)
                    ImRaii.TreeNode(bone, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var shapes = ImRaii.TreeNode("Shapes", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (shapes)
                foreach (var shape in _lastFile.Shapes)
                    ImRaii.TreeNode(shape.ShapeName, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        if (_lastFile.RemainingData.Length > 0)
        {
            using var t = ImRaii.TreeNode($"（小心卡）Additional Data (Size: {_lastFile.RemainingData.Length})###AdditionalData");
            if (t)
                ImGuiUtil.TextWrapped(string.Join(' ', _lastFile.RemainingData.Select(c => $"{c:X2}")));
        }

        return false;
    }

    private static bool GetFirstModel(IEnumerable<string> files, [NotNullWhen(true)] out string? file)
    {
        file = files.FirstOrDefault(f => ValidModelExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        return file != null;
    }

    private static long GetTriangleCountForLod(MdlFile model, int lod)
    {
        var vertSum   = 0u;
        var meshIndex = model.Lods[lod].MeshIndex;
        var meshCount = model.Lods[lod].MeshCount;

        for (var i = meshIndex; i < meshIndex + meshCount; i++)
            vertSum += model.Meshes[i].IndexCount;

        return vertSum / 3;
    }

    private static readonly string[] ValidModelExtensions =
    [
        ".gltf",
        ".glb",
    ];
}

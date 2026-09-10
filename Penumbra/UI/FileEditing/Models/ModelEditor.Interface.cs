using Dalamud.Interface;
using ImSharp;
using Lumina.Data.Parsing;
using Luna;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.Import.Models.Import;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Models;

public partial class ModelEditor
{
    public const int OffByOneOffset = 0;
    private const int MdlMaterialMaximum = ModelImporter.MaterialLimit;

    private const string MdlImportDocumentation =
        "https://github.com/xivdev/Penumbra/wiki/Model-IO#user-content-9b49d296-23ab-410a-845b-a3be769b71ea";

    private const string MdlExportDocumentation =
        "https://github.com/xivdev/Penumbra/wiki/Model-IO#user-content-25968400-ebe5-4861-b610-cb1556db7ec4";

    private string _modelNewMaterial = string.Empty;

    private string       _customPath     = string.Empty;
    private Utf8GamePath _customGamePath = Utf8GamePath.Empty;

    private bool   _loadedData;
    public  long[] LodTriCount = [];

    public bool DrawToolbar(bool disabled)
    {
        DrawVersionUpdate(disabled);

        return false;
    }

    private void UpdateFile(bool force)
    {
        if (_loadedData && !force)
            return;

        _loadedData = true;
        LodTriCount = Enumerable.Range(0, Mdl.Lods.Length).Select(l => GetTriangleCountForLod(Mdl, l)).ToArray();
    }

    public bool DrawPanel(bool disabled)
    {
        var ret = Dirty;
        UpdateFile(ret);
        DrawImportExport(disabled);

        ret |= DrawModelMaterialDetails(disabled);

        if (Im.Tree.Header($"网格 ({Mdl.Meshes.Length})###meshes"))
            for (var i = 0; i < Mdl.LodCount; ++i)
                ret |= DrawModelLodDetails(i, disabled);

        ret |= DrawOtherModelDetails();

        return !disabled && ret;
    }

    private void DrawVersionUpdate(bool disabled)
    {
        if (disabled || Mdl.Version is not MdlFile.V5)
            return;

        if (!ImEx.Button("将 MDL 版本从 V5 更新至 V6"u8, Colors.PressEnterWarningBg, default, Im.ContentRegion.Available with { Y = 0 },
                "如果发现 7.0 版本之前的模型骨骼权重显示异常，请尝试使用此功能。\n\n注意：此操作不可撤销。"u8))
            return;

        Mdl.ConvertV5ToV6();
        SaveRequested?.Invoke();
    }

    private void DrawImportExport(bool disabled)
    {
        if (!Im.Tree.Header("导入/导出"u8))
            return;

        var childSize = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X) / 2, 0);

        DrawImport(childSize, disabled);
        Im.Line.Same();
        DrawExport(childSize, disabled);

        DrawIoExceptions();
        DrawIoWarnings();
    }

    private void DrawImport(Vector2 size, bool _1)
    {
        using var id = Im.Id.Push("import"u8);

        _dragDropManager.CreateImGuiSource("ModelDragDrop",
            m => m.Extensions.Any(e => ValidModelExtensions.Contains(e.ToLowerInvariant())), m =>
            {
                if (!GetFirstModel(m.Files, out var file))
                    return false;

                Im.Text($"拖拽模型进行编辑: {Path.GetFileName(file)}");
                return true;
            });

        using (ImEx.FramedGroup("导入"u8, LunaStyle.ImportIcon, default, StringU8.Empty, ColorParameter.Default, ColorParameter.Default,
                   size))
        {
            Im.Checkbox("保留当前材质"u8,  ref ImportKeepMaterials);
            Im.Checkbox("保留当前属性"u8, ref ImportKeepAttributes);

            if (ImEx.Button("从 glTF 导入"u8, Vector2.Zero, "导入一个 glTF 文件，覆盖当前 mdl 的内容。"u8, PendingIo))
                _fileDialog.OpenFilePicker("从 glTF 加载模型。", "glTF{.gltf,.glb}", (success, paths) =>
                {
                    if (success && paths.Count > 0)
                        Import(paths[0]);
                }, 1, _context?.Mod?.ModPath.FullName, false);

            Im.Line.Same();
            DrawDocumentationLink(MdlImportDocumentation);
        }

        if (_dragDropManager.CreateImGuiTarget("ModelDragDrop", out var files, out _) && GetFirstModel(files, out var importFile))
            Import(importFile);
    }

    private void DrawExport(Vector2 size, bool _)
    {
        using var id = Im.Id.Push("export"u8);
        using var frame = ImEx.FramedGroup("导出"u8, LunaStyle.FileExportIcon, default, StringU8.Empty, ColorParameter.Default,
            ColorParameter.Default, size);

        if (GamePaths is null)
        {
            Im.Text(IoExceptions.Count is 0 ? "解析模型游戏路径。"u8 : "解析模型游戏路径失败。"u8);

            return;
        }

        DrawGamePathCombo();

        Im.Checkbox("##exportGeneratedMissingBones"u8, ref ExportConfig.GenerateMissingBones);
        LunaStyle.DrawAlignedHelpMarkerLabel("生成缺失骨骼"u8,
            "警告：启用此选项可能导致导出的网格无法使用。\n"u8
          + "此功能主要用于导出那些绑定了不存在骨骼权重的模型。\n"u8
          + "在启用之前，请确保当前合集中启用了依赖项，并且已正确配置 EST 元数据。"u8);

        var gamePath = GamePathIndex >= 0 && GamePathIndex < GamePaths.Count
            ? GamePaths[GamePathIndex]
            : _customGamePath;

        if (ImEx.Button("导出为 glTF"u8, Vector2.Zero, "将此 mdl 文件导出为 glTF，用于 3D 创作应用程序。"u8,
                PendingIo || gamePath.IsEmpty))
            _fileDialog.OpenSavePicker("保存模型为 glTF。", ".glb", Path.GetFileNameWithoutExtension(gamePath.Filename().ToString()),
                ".glb", (valid, path) =>
                {
                    if (!valid)
                        return;

                    Export(path, gamePath);
                },
                _context?.Mod?.ModPath.FullName,
                false
            );

        Im.Line.Same();
        DrawDocumentationLink(MdlExportDocumentation);
    }

    private void DrawIoExceptions()
    {
        if (IoExceptions.Count is 0)
            return;

        var size = Im.ContentRegion.Available with { Y = 0 };
        using var frame = ImEx.FramedGroup("异常"u8, LunaStyle.ErrorIcon, default, StringU8.Empty, ColorParameter.Default,
            LunaStyle.ErrorForeground, size);

        var spaceAvail = Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - 100;
        foreach (var (index, exception) in IoExceptions.Index())
        {
            using var id       = Im.Id.Push(index);
            var       message  = new StringU8($"{exception.GetType().Name}: {exception.Message}");
            var       textSize = Im.Font.CalculateSize(message).X;
            if (textSize > spaceAvail)
                message = new StringU8($"{message.Span[..(int)Math.Floor(message.Length * (spaceAvail / textSize))]}...");

            using var exceptionNode = Im.Tree.Node(message);
            if (exceptionNode)
            {
                using var indent = Im.Indent();
                Im.TextWrapped($"{exception}");
            }
        }
    }

    private void DrawIoWarnings()
    {
        if (IoWarnings.Count is 0)
            return;

        var size = Im.ContentRegion.Available with { Y = 0 };
        using var frame = ImEx.FramedGroup("警告"u8, LunaStyle.WarningIcon, default, StringU8.Empty, ColorParameter.Default,
            LunaStyle.WarningForeground, size);

        var spaceAvail = Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - 100;
        foreach (var (index, warning) in IoWarnings.Index())
        {
            using var id       = Im.Id.Push(index);
            var       textSize = Im.Font.CalculateSize(warning).X;

            if (textSize <= spaceAvail)
            {
                Im.Tree.Leaf(warning);
                continue;
            }

            var firstLine = new StringU8($"{warning.AsSpan(0, (int)Math.Floor(warning.Length * (spaceAvail / textSize)))}...");

            using var warningNode = Im.Tree.Node(firstLine);
            if (warningNode)
            {
                using var indent = Im.Indent();
                Im.TextWrapped(warning);
            }
        }
    }

    private void DrawGamePathCombo()
    {
        if (GamePaths!.Count != 0)
        {
            DrawComboButton();
            return;
        }

        Im.Text("未检测到关联的游戏路径。目前导出仍需要有效的游戏路径。"u8);
        if (!Im.Input.Text("##customInput"u8, ref _customPath, "输入自定义游戏路径..."u8))
            return;

        if (!Utf8GamePath.FromString(_customPath, out _customGamePath))
            _customGamePath = Utf8GamePath.Empty;
    }

    /// <summary> I disliked the combo with only one selection so turn it into a button in that case. </summary>
    private void DrawComboButton()
    {
        var preview     = GamePaths![GamePathIndex].Path.Span;
        var labelWidth  = Im.Font.CalculateSize("游戏路径"u8).X + Im.Style.ItemInnerSpacing.X;
        var buttonWidth = Im.ContentRegion.Available.X - labelWidth - Im.Style.ItemSpacing.X;
        if (GamePaths!.Count == 1)
        {
            using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0, 0.5f));
            using var color = ImGuiColor.Button.Push(Im.Style[ImGuiColor.FrameBackground])
                .Push(ImGuiColor.ButtonHovered, Im.Style[ImGuiColor.FrameBackgroundHovered])
                .Push(ImGuiColor.ButtonActive,  Im.Style[ImGuiColor.FrameBackgroundActive]);
            using var group = Im.Group();
            Im.Button(preview, new Vector2(buttonWidth, 0));
            Im.Line.Same(0, Im.Style.ItemInnerSpacing.X);
            Im.Text("游戏路径"u8);
        }
        else
        {
            Im.Item.SetNextWidth(buttonWidth);
            using var combo = Im.Combo.Begin("游戏路径"u8, preview);
            if (combo.Success)
                foreach (var (index, path) in GamePaths.Index())
                {
                    if (!Im.Selectable(path.Path.Span, index == GamePathIndex))
                        continue;

                    GamePathIndex = index;
                }
        }

        if (Im.Item.RightClicked())
            Im.Clipboard.Set(preview);
        Im.Tooltip.OnHover("右键点击复制到剪贴板。"u8, HoveredFlags.AllowWhenDisabled);
    }

    private static void DrawDocumentationLink(string address)
    {
        var text  = "文档 →"u8;
        var width = Im.Font.CalculateButtonSize(text).X;
        // Draw the link button. We set the background colour to transparent to mimic the look of a link.
        using var color = ImGuiColor.Button.Push(Vector4.Zero);
        SupportButton.Link(Penumbra.Messager, text, address, width, ""u8);

        // Draw an underline for the text.
        var lineStart = Im.Item.LowerRightCorner;
        lineStart -= Im.Style.FramePadding;
        var lineEnd = lineStart with { X = Im.Item.UpperLeftCorner.X + Im.Style.FramePadding.X };
        Im.Window.DrawList.Shape.Line(lineStart, lineEnd, 0xFFFFFFFF);
    }

    private bool DrawModelMaterialDetails(bool disabled)
    {
        var invalidMaterialCount = Mdl.Materials.Count(material => !ValidateMaterial(material));

        var oldPos = Im.Cursor.Y;
        var header = Im.Tree.Header("材质"u8);
        var newPos = Im.Cursor.Position;
        if (invalidMaterialCount > 0)
        {
            var text = new StringU8($"{invalidMaterialCount} invalid material{(invalidMaterialCount > 1 ? "s" : "")}");
            var size = Im.Font.CalculateSize(text).X;
            Im.Cursor.Position = new Vector2(Im.ContentRegion.Available.X - size, oldPos + Im.Style.FramePadding.Y);
            Im.Text(text, Rgba32.Red);
            Im.Cursor.Position = newPos;
        }

        if (!header)
            return false;

        using var table = Im.Table.Begin(StringU8.Empty, disabled ? 2 : 4, TableFlags.SizingFixedFit);
        if (!table)
            return false;

        var ret       = false;
        var materials = Mdl.Materials;

        table.SetupColumn("index"u8, TableColumnFlags.WidthFixed,   80 * Im.Style.GlobalScale);
        table.SetupColumn("path"u8,  TableColumnFlags.WidthStretch, 1);
        if (!disabled)
        {
            table.SetupColumn("actions"u8, TableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
            table.SetupColumn("help"u8,    TableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
        }

        var inputFlags = disabled ? InputTextFlags.ReadOnly : InputTextFlags.None;
        for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            ret |= DrawMaterialRow(table, disabled, materials, materialIndex, inputFlags);

        if (materials.Length >= MdlMaterialMaximum || disabled)
            return ret;

        table.NextColumn();

        table.NextColumn();
        Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
        Im.Input.Text("##newMaterial"u8, ref _modelNewMaterial, "添加新材质..."u8, maxLength: Utf8GamePath.MaxGamePathLength,
            flags: inputFlags);
        var validName = ValidateMaterial(_modelNewMaterial);
        table.NextColumn();
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, StringU8.Empty, !validName))
        {
            ret               = true;
            Mdl.Materials     = materials.AddItem(_modelNewMaterial);
            _modelNewMaterial = string.Empty;
        }

        table.NextColumn();
        if (!validName && _modelNewMaterial.Length > 0)
            DrawInvalidMaterialMarker();

        return ret;
    }

    private bool DrawMaterialRow(in Im.TableDisposable table, bool disabled, string[] materials, int materialIndex,
        InputTextFlags inputFlags)
    {
        using var id  = Im.Id.Push(materialIndex);
        var       ret = false;
        table.DrawFrameColumn($"材质 #{materialIndex + 1}");

        var temp = materials[materialIndex];
        table.NextColumn();
        Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
        if (Im.Input.Text($"##material{materialIndex}", ref temp, maxLength: Utf8GamePath.MaxGamePathLength, flags: inputFlags)
         && temp.Length > 0
         && temp != materials[materialIndex]
           )
        {
            materials[materialIndex] = temp;
            ret                      = true;
        }

        if (disabled)
            return ret;

        table.NextColumn();
        // Need to have at least one material.
        if (materials.Length > 1)
        {
            var modifierActive = LunaStyle.Modifier.Destructive.Active;
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon,
                    "删除此材质。\n任何使用此材质的网格都将更新为使用材质 #1。"u8, !modifierActive))
            {
                RemoveMaterial(materialIndex);
                ret = true;
            }

            if (!modifierActive)
                Im.Tooltip.OnHover($"\n按住 {LunaStyle.Modifier.Destructive} 删除。");
        }

        table.NextColumn();
        // Add markers to invalid materials.
        if (!ValidateMaterial(temp))
            DrawInvalidMaterialMarker();

        return ret;
    }

    private static void DrawInvalidMaterialMarker()
    {
        ImEx.Icon.Draw(FontAwesomeIcon.TimesCircle.Icon(), Rgba32.Red);
        Im.Tooltip.OnHover(
            "材质路径必须是相对路径（如 \"/filename.mtrl\")\n"u8
          + "或绝对路径（如 \"bg/full/path/to/filename.mtrl\"），\n"u8
          + "且必须以 \".mtrl\" 结尾。"u8);
    }

    private bool DrawModelLodDetails(int lodIndex, bool disabled)
    {
        using var lodNode = Im.Tree.Node($"细节层次 #{lodIndex + OffByOneOffset}", TreeNodeFlags.DefaultOpen);
        if (!lodNode)
            return false;

        var lod = Mdl.Lods[lodIndex];
        var ret = false;

        for (var meshOffset = 0; meshOffset < lod.MeshCount; meshOffset++)
            ret |= DrawModelMeshDetails(lod.MeshIndex + meshOffset, disabled);

        return ret;
    }

    private bool DrawModelMeshDetails(int meshIndex, bool disabled)
    {
        using var meshNode = Im.Tree.Node($"网格 #{meshIndex + OffByOneOffset}", TreeNodeFlags.DefaultOpen);
        if (!meshNode)
            return false;

        using var id    = Im.Id.Push(meshIndex);
        using var table = Im.Table.Begin(StringU8.Empty, 2, TableFlags.SizingFixedFit);
        if (!table)
            return false;

        table.SetupColumn("name"u8,  TableColumnFlags.WidthFixed,   150 * Im.Style.GlobalScale);
        table.SetupColumn("field"u8, TableColumnFlags.WidthStretch, 1);

        var file = Mdl;
        var mesh = file.Meshes[meshIndex];

        // Vertex elements
        table.DrawFrameColumn("顶点元素"u8);

        table.NextColumn();
        DrawVertexElementDetails(file.VertexDeclarations[meshIndex].VertexElements);

        // Mesh material
        table.DrawFrameColumn("材质"u8);

        table.NextColumn();
        var ret = DrawMaterialCombo(meshIndex, disabled);

        // Sub meshes
        for (var subMeshOffset = 0; subMeshOffset < mesh.SubMeshCount; subMeshOffset++)
            ret |= DrawSubMeshAttributes(table, meshIndex, subMeshOffset, disabled);

        return ret;
    }

    private static void DrawVertexElementDetails(MdlStructs.VertexElement[] vertexElements)
    {
        using var node = Im.Tree.Node("点击展开"u8);
        if (!node)
            return;

        const TableFlags flags = TableFlags.SizingFixedFit
          | TableFlags.RowBackground
          | TableFlags.Borders
          | TableFlags.NoHostExtendX;
        using var table = Im.Table.Begin(StringU8.Empty, 4, flags);
        if (!table)
            return;

        table.SetupColumn("用途"u8);
        table.SetupColumn("类型"u8);
        table.SetupColumn("Stream"u8);
        table.SetupColumn("Offset"u8);
        table.HeaderRow();

        foreach (var element in vertexElements)
        {
            table.DrawColumn($"{(MdlFile.VertexUsage)element.Usage}");
            table.DrawColumn($"{(MdlFile.VertexType)element.Type}");
            table.DrawColumn($"{element.Stream}");
            table.DrawColumn($"{element.Offset}");
        }
    }

    private bool DrawMaterialCombo(int meshIndex, bool disabled)
    {
        var       mesh = Mdl.Meshes[meshIndex];
        using var _    = Im.Disabled(disabled);
        Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
        using var materialCombo = Im.Combo.Begin("##material"u8, Mdl.Materials[mesh.MaterialIndex]);

        if (!materialCombo)
            return false;

        var ret = false;
        foreach (var (materialIndex, material) in Mdl.Materials.Index())
        {
            if (!Im.Selectable(material, mesh.MaterialIndex == materialIndex))
                continue;

            Mdl.Meshes[meshIndex].MaterialIndex = (ushort)materialIndex;
            ret                                 = true;
        }

        return ret;
    }

    private bool DrawSubMeshAttributes(in Im.TableDisposable table, int meshIndex, int subMeshOffset, bool disabled)
    {
        using var _ = Im.Id.Push(subMeshOffset);

        var mesh         = Mdl.Meshes[meshIndex];
        var subMeshIndex = mesh.SubMeshIndex + subMeshOffset;

        table.DrawFrameColumn($"子网格 #{subMeshOffset + OffByOneOffset} 属性 ");

        table.NextColumn();
        var attributes = GetSubMeshAttributes(subMeshIndex);

        if (attributes is null)
        {
            attributes = ["无效的属性数据"];
            disabled   = true;
        }

        var tagIndex = TagButtons.Draw(StringU8.Empty, StringU8.Empty, attributes, out var editedAttribute, !disabled);
        if (tagIndex < 0)
            return false;

        var oldName = tagIndex < attributes.Count ? attributes[tagIndex] : null;
        var newName = editedAttribute.Length > 0 ? editedAttribute : null;
        UpdateSubMeshAttribute(subMeshIndex, oldName, newName);

        return true;
    }

    private bool DrawOtherModelDetails()
    {
        using var header = Im.Tree.HeaderId("其他内容"u8);
        if (!header)
            return false;

        var ret = false;
        using (var table = Im.Table.Begin("##data"u8, 2, TableFlags.SizingFixedFit))
        {
            if (table)
            {
                table.DrawDataPair("版本"u8,                       $"0x{Mdl.Version:X}");
                table.DrawDataPair("Radius"u8,                        Mdl.Radius.ToString(CultureInfo.InvariantCulture));
                table.DrawDataPair("模型裁剪距离"u8,       Mdl.ModelClipOutDistance.ToString(CultureInfo.InvariantCulture));
                table.DrawDataPair("阴影裁剪距离"u8,      Mdl.ShadowClipOutDistance.ToString(CultureInfo.InvariantCulture));
                table.DrawDataPair("细节层次数量"u8,                     Mdl.LodCount);
                table.DrawDataPair("启用索引缓冲流"u8, Mdl.EnableIndexBufferStreaming);
                table.DrawDataPair("启用边缘几何"u8,          Mdl.EnableEdgeGeometry);
                table.DrawDataPair("Flags 1"u8,                       Mdl.Flags1);
                table.DrawDataPair("Flags 2"u8,                       Mdl.Flags2);
                table.DrawDataPair("顶点声明"u8,           Mdl.VertexDeclarations.Length);
                table.DrawDataPair("骨骼包围盒"u8,           Mdl.BoneBoundingBoxes.Length);
                table.DrawDataPair("骨骼表"u8,                   Mdl.BoneTables.Length);
                table.DrawDataPair("元素 ID"u8,                   Mdl.ElementIds.Length);
                table.DrawDataPair("额外细节层次"u8,                    Mdl.ExtraLods.Length);
                table.DrawDataPair("网格"u8,                        Mdl.Meshes.Length);
                table.DrawDataPair("形状网格"u8,                  Mdl.ShapeMeshes.Length);
                table.DrawDataPair("细节层次"u8,                          Mdl.Lods.Length);
                table.DrawDataPair("顶点声明"u8,           Mdl.VertexDeclarations.Length);
                table.DrawDataPair("堆栈大小"u8,                    Mdl.StackSize);
                foreach (var (lod, triCount) in LodTriCount.Index())
                    table.DrawDataPair($"细节层次 #{lod + 1} 三角形数量", triCount);
            }
        }

        using (var materials = Im.Tree.Node("材质"u8, TreeNodeFlags.DefaultOpen))
        {
            if (materials)
                foreach (var material in Mdl.Materials)
                    Im.Tree.Leaf(material);
        }

        using (var attributes = Im.Tree.Node("属性"u8, TreeNodeFlags.DefaultOpen))
        {
            if (attributes)
                for (var i = 0; i < Mdl.Attributes.Length; ++i)
                {
                    using var id        = Im.Id.Push(i);
                    ref var   attribute = ref Mdl.Attributes[i];
                    var       name      = attribute;
                    if (Im.Input.Text("##attribute"u8, ref name, "属性名称..."u8) && name.Length > 0 && name != attribute)
                    {
                        attribute = name;
                        ret       = true;
                    }
                }
        }

        using (var bones = Im.Tree.Node("骨骼"u8, TreeNodeFlags.DefaultOpen))
        {
            if (bones)
                for (var i = 0; i < Mdl.Bones.Length; ++i)
                {
                    using var id   = Im.Id.Push(i);
                    ref var   bone = ref Mdl.Bones[i];
                    var       name = bone;
                    if (Im.Input.Text("##bone"u8, ref name, "骨骼名称..."u8) && name.Length > 0 && name != bone)
                    {
                        bone = name;
                        ret  = true;
                    }
                }
        }

        using (var shapes = Im.Tree.Node("形状"u8, TreeNodeFlags.DefaultOpen))
        {
            if (shapes)
                for (var i = 0; i < Mdl.Shapes.Length; ++i)
                {
                    using var id    = Im.Id.Push(i);
                    ref var   shape = ref Mdl.Shapes[i];
                    var       name  = shape.ShapeName;
                    if (Im.Input.Text("##shape"u8, ref name, "形状名称..."u8) && name.Length > 0 && name != shape.ShapeName)
                    {
                        shape.ShapeName = name;
                        ret             = true;
                    }
                }
        }

        if (Mdl.RemainingData.Length > 0)
        {
            using var t = Im.Tree.Node($"额外数据 (大小: {Mdl.RemainingData.Length})###AdditionalData");
            if (t)
                ImEx.HexViewer(Mdl.RemainingData);
        }

        return ret;
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

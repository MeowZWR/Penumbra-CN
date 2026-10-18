using Dalamud.Plugin.Services;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Gui;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.Interop.ResourceTree;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Materials;

public sealed class MaterialEditorFactory(
    IDataManager gameData,
    IFramework framework,
    ObjectManager objects,
    CharacterBaseDestructor characterBaseDestructor,
    StainService stainService,
    ResourceTreeFactory resourceTreeFactory,
    FileDialogService fileDialog,
    ShaderIdPicker shaderIdPicker,
    TextureArraySlicePickers textureArraySlicePickers,
    Configuration config,
    ColorTableRowPresets rowPresets,                // 合并上游时保留：颜色表行预设
    Interop.Services.ModSphereMapManager sphereMaps) : BaseFileEditorFactory(gameData), IUiService // 合并上游时保留：模组级球面贴图
{
    public override string Identifier
        => typeof(MaterialEditor).FullName!;

    public override string DisplayName
        => "Penumbra Material Editor";

    public override IEnumerable<ResourceType> SupportedResourceTypes
        => [ResourceType.Mtrl];

    public override IFileEditor CreateForData(byte[] data, string path, bool writable, string? gamePath, FileEditingContext? context)
        => CreateForData((ReadOnlySpan<byte>)data, path, writable, gamePath, context);

    public override IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, string? gamePath,
        FileEditingContext? context)
        => new MaterialEditor(GameData, framework, objects, characterBaseDestructor, stainService, resourceTreeFactory, fileDialog,
            shaderIdPicker, textureArraySlicePickers, config, context, new MtrlFile(data), path, writable)
        {
            RowPresets = rowPresets, // 合并上游时保留：颜色表行预设
            SphereMaps = sphereMaps, // 合并上游时保留：模组级球面贴图
        };
}

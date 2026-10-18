using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.Interop;
using Luna;
using Penumbra.GameData;
using Penumbra.Interop.Hooks.Resources;
using Penumbra.Interop.Services;
using CSModelRenderer = FFXIVClientStructs.FFXIV.Client.Graphics.Render.ModelRenderer;

namespace Penumbra.Interop.Hooks.PostProcessing;

/// <summary> Binds the sphere map array of the mod providing a material, per material. </summary>
/// <remarks>
///   The CharacterBase render callback writes CharacterUtility slot 97 into the thread-local Kernel.Context texture table once per character.
///   ModelRenderer.OnRenderMaterial runs right before each material's draw is recorded and overwrites that entry per material,
///   with the default texture for materials not provided by a mod with a slot table.
/// </remarks>
public sealed unsafe class ModSphereMapBinder : IDisposable, IRequiredService
{
    private const int ContextTextureTableOffset  = 0x1140;
    private const int ContextTextureEntrySize    = 24;
    private const int ContextTextureEntryTexture = 8;
    private const int ContextTextureTableSize    = 256;
    private const int SphereMapTableIndexOffset  = 0x53C;

    private delegate ushort* ModelRendererOnRenderMaterialDelegate(CSModelRenderer* modelRenderer,
        CSModelRenderer.OnRenderMaterialParams2* param, Material* material, uint materialIndex);

    private readonly Hook<ModelRendererOnRenderMaterialDelegate>? _hook;
    private readonly ModSphereMapManager                          _manager;
    private readonly CharacterUtility                             _utility;
    private readonly ResourceHandleDestructor                     _resourceHandleDestructor;

    public ModSphereMapBinder(HookManager hooks, ModSphereMapManager manager, CharacterUtility utility,
        ResourceHandleDestructor resourceHandleDestructor)
    {
        _manager                  = manager;
        _utility                  = utility;
        _resourceHandleDestructor = resourceHandleDestructor;
        _hook = hooks.CreateHook<ModelRendererOnRenderMaterialDelegate>("ModelRenderer.OnRenderMaterial.SphereMap",
            Sigs.ModelRendererOnRenderMaterial, Detour, !HookOverrides.Instance.PostProcessing.ModelRendererOnRenderMaterial).Result;
        _resourceHandleDestructor.Subscribe(OnResourceHandleDestructor, ResourceHandleDestructor.Priority.ModSphereMapBinder);
    }

    public void Dispose()
    {
        _hook?.Dispose();
        _resourceHandleDestructor.Unsubscribe(OnResourceHandleDestructor);
    }

    private void OnResourceHandleDestructor(in ResourceHandleDestructor.Arguments arguments)
    {
        if (arguments.ResourceHandle->FileType is Api.Enums.ResourceType.Mtrl)
            _manager.RemoveMaterial((nint)arguments.ResourceHandle);
    }

    private ushort* Detour(CSModelRenderer* modelRenderer, CSModelRenderer.OnRenderMaterialParams2* param, Material* material,
        uint materialIndex)
    {
        var ret = _hook!.Original(modelRenderer, param, material, materialIndex);
        if (_manager.Active && material is not null && material->MaterialResourceHandle is not null)
            Bind(material);
        return ret;
    }

    private void Bind(Material* material)
    {
        var utility = _utility.Address;
        if (utility is null || utility->SphereDArrayTexResource is null)
            return;

        var threadLocals = ThreadLocals.ThreadLocalInstance();
        if (threadLocals is null || !threadLocals->IsInitialized || threadLocals->GraphicsKernelContext is null)
            return;

        var index = *(uint*)((byte*)utility + SphereMapTableIndexOffset);
        if (index >= ContextTextureTableSize)
            return;

        var slot = (nint*)((byte*)threadLocals->GraphicsKernelContext
          + ContextTextureTableOffset
          + index * ContextTextureEntrySize
          + ContextTextureEntryTexture);
        var defaultTexture = (nint)utility->SphereDArrayTexResource->CsHandle.Texture;
        var texture        = _manager.GetTexture(material->MaterialResourceHandle);
        if (texture == nint.Zero)
            texture = defaultTexture;

        var current = *slot;
        if (current == texture)
            return;

        // Entries holding neither the default texture nor a mod array are left untouched.
        if (current == defaultTexture || _manager.IsOwnTexture(current))
            *slot = texture;
    }
}

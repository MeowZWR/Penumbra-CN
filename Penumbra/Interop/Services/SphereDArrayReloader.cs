using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Services;

/// <summary> Loads the Base-collection <c>sphere_d_array.tex</c> into CharacterUtility for global use (materials + editor). </summary>
public sealed unsafe class SphereDArrayReloader : IDisposable, IRequiredService
{
    private readonly CharacterUtility         _utility;
    private readonly ResourceLoader           _resources;
    private readonly ActiveCollectionData     _active;
    private readonly Configuration            _config;
    private readonly CharacterUtilityFinished _finished;

    private SafeResourceHandle? _custom;

    public SphereDArrayReloader(CharacterUtility utility, ResourceLoader resources, ActiveCollectionData active, Configuration config,
        CharacterUtilityFinished finished)
    {
        _utility   = utility;
        _resources = resources;
        _active    = active;
        _config    = config;
        _finished  = finished;
        finished.Subscribe(Reload, CharacterUtilityFinished.Priority.SphereDArrayReloader);
    }

    public void Dispose()
    {
        _finished.Unsubscribe(Reload);
        RestoreDefault();
    }

    /// <summary> Resolve <c>sphere_d_array.tex</c> through the Base collection and swap it into CharacterUtility. </summary>
    public void Reload()
    {
        if (!_utility.Ready || _utility.Address is null)
            return;

        RestoreDefault();
        if (!_config.Main.EnableMods || !_config.Advanced.EnableExtendedFeatures)
            return;

        var resolveData = _active.Default.ToResolveData();
        var handle = _resources.LoadResolvedSafeResource(ResourceCategory.Chara, ResourceType.Tex, GamePaths.Tex.SphereDArrayUtf8.Path,
            resolveData);
        if (handle.IsInvalid)
        {
            Penumbra.Log.Warning("Failed to load sphere_d_array.tex for CharacterUtility.");
            handle.Dispose();
            return;
        }

        if ((nint)handle.ResourceHandle == _utility.DefaultSphereDArrayResource)
        {
            handle.Dispose();
            Penumbra.Log.Debug("sphere_d_array.tex reload kept the default resource.");
            return;
        }

        _custom                                   = handle;
        _utility.Address->SphereDArrayTexResource = (TextureResourceHandle*)handle.ResourceHandle;
        Penumbra.Log.Debug($"sphere_d_array.tex replaced in CharacterUtility with 0x{(nint)handle.ResourceHandle:X}.");
    }

    private void RestoreDefault()
    {
        if (_utility.Address is not null && _utility.DefaultSphereDArrayResource != nint.Zero)
            _utility.Address->SphereDArrayTexResource = (TextureResourceHandle*)_utility.DefaultSphereDArrayResource;

        _custom?.Dispose();
        _custom = null;
    }
}

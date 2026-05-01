using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Luna;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public sealed unsafe class RspSetupCharacter : FastHook<RspSetupCharacter.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public RspSetupCharacter(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState)
    {
        _collectionResolver = collectionResolver;
        _metaState          = metaState;
        Task                = hooks.CreateHook<Delegate>("RSP Setup Character", Sigs.RspSetupCharacter, Detour, !HookOverrides.Instance.Meta.RspSetupCharacter);
    }

    public delegate void Delegate(DrawObject* drawObject, byte unk1);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject, byte unk1)
    {
        Penumbra.Log.Excessive($"[RSP Setup Character] Invoked on {(nint)drawObject:X} with {unk1:X}.");
        // Skip if we are coming from ChangeCustomize.
        if (_metaState.CustomizeChangeCollection.Valid)
        {
            Task.Result!.Original.Invoke(drawObject, unk1);
            return;
        }

        var collection = _collectionResolver.IdentifyCollection(drawObject, true);
        _metaState.RspCollection.Push(collection);
        Task.Result!.Original.Invoke(drawObject, unk1);
        _metaState.RspCollection.Pop();
    }
}

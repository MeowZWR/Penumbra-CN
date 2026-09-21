using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Services;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Processing;

public sealed class PapPathPreProcessor(AnimationResourceCache cache) : IPathPreProcessor
{
    public ResourceType Type
        => ResourceType.Pap;

    public FullPath? PreProcess(ResolveData resolveData, CiByteString path, Utf8GamePath _, bool nonDefault, FullPath? resolved)
        => Apply(resolveData, resolved);

    /// <summary> Apply PAP cache-busting without going through <see cref="GamePathPreProcessService"/>. </summary>
    public FullPath? Apply(ResolveData resolveData, FullPath? resolved)
    {
        if (!cache.Contains(resolved))
            return resolved;

        return PathDataHandler.CreatePap(resolved!.Value.InternalName, resolveData.ModCollection, cache.Generation);
    }
}

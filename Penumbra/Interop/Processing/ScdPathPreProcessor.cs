using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Services;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Processing;

public sealed class ScdPathPreProcessor(AnimationResourceCache cache) : IPathPreProcessor
{
    public ResourceType Type
        => ResourceType.Scd;

    public FullPath? PreProcess(ResolveData resolveData, CiByteString path, Utf8GamePath _, bool nonDefault, FullPath? resolved)
    {
        if (cache.Contains(resolved))
            return PathDataHandler.CreateScd(path, resolveData.ModCollection, cache.Generation);

        return resolved;
    }
}

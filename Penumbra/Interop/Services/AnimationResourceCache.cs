using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Services;

/// <summary>
/// Session-local cache-bust targets for animation files from the currently selected mod.
/// Only PAP/TMB/SCD redirections that are actually applied by that mod's current options are stored.
/// Generation 0 and an empty target set keep existing load behavior.
/// </summary>
public sealed class AnimationResourceCache : Luna.IService
{
    private static readonly CiByteString PapExtension = new(".pap"u8, MetaDataComputation.All);
    private static readonly CiByteString TmbExtension = new(".tmb"u8, MetaDataComputation.All);
    private static readonly CiByteString ScdExtension = new(".scd"u8, MetaDataComputation.All);

    private readonly ConcurrentDictionary<CiByteString, byte> _paths = new();

    private int _generation;

    /// <summary> Current generation. 0 means no successful refresh this session. </summary>
    public int Generation
        => Volatile.Read(ref _generation);

    /// <summary> True when the given replacement file was targeted by the last refresh. </summary>
    public bool Contains(FullPath? resolved)
        => resolved is { IsRooted: true } path && _paths.ContainsKey(path.InternalName);

    /// <summary>
    /// Replace the target set with PAP/TMB/SCD files from the given redirections.
    /// Returns 0 files without changing state when none match.
    /// </summary>
    public int Refresh(IReadOnlyDictionary<Utf8GamePath, FullPath> redirections)
    {
        var paths = new List<CiByteString>();
        foreach (var (gamePath, fullPath) in redirections)
        {
            if (!fullPath.IsRooted)
                continue;

            var extension = gamePath.Extension();
            if (!extension.Equals(PapExtension) && !extension.Equals(TmbExtension) && !extension.Equals(ScdExtension))
                continue;

            paths.Add(fullPath.InternalName.Clone());
        }

        if (paths.Count is 0)
            return 0;

        Interlocked.Increment(ref _generation);
        _paths.Clear();
        foreach (var path in paths)
            _paths.TryAdd(path, 0);

        return paths.Count;
    }
}

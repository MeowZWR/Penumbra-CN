using Luna;
using Penumbra.Api;
using Penumbra.Interop.Services;

namespace Penumbra.Communication;

/// <summary> Triggered when the Character Utility becomes ready. </summary>
public sealed class CharacterUtilityFinished(LunaLogger log) : EventBase<CharacterUtilityFinished.Priority>(nameof(CharacterUtilityFinished), log)
{
    public enum Priority
    {
        /// <seealso cref="CharacterUtility"/>
        OnFinishedLoading = int.MaxValue,

        /// <seealso cref="IpcProviders.OnCharacterUtilityReady"/>
        IpcProvider = int.MinValue,

        /// <seealso cref="Collections.Cache.CollectionCacheManager"/>
        CollectionCacheManager = 0,

        /// <seealso cref="SphereDArrayReloader"/>
        SphereDArrayReloader = -1, // 合并上游时保留：球面贴图阵列重载优先级
    }
}

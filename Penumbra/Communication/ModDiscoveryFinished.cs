using Luna;

namespace Penumbra.Communication;

/// <summary> Triggered whenever a new mod discovery has finished. </summary>
public sealed class ModDiscoveryFinished(LunaLogger log) : EventBase<ModDiscoveryFinished.Priority>(nameof(ModDiscoveryFinished), log)
{
    public enum Priority
    {
        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.RestoreLastSelection"/>
        ModFileSystemSelector = -200,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModDiscoveryFinished"/>
        CollectionCacheManager = -100,

        /// <seealso cref="Collections.Manager.CollectionStorage.OnModDiscoveryFinished"/>
        CollectionStorage = 0,

        /// <seealso cref="Mods.Manager.ModCacheManager.OnModDiscoveryFinished"/>
        ModCacheManager = 0,

        /// <seealso cref="Mods.Manager.ModFileSystem.Reload"/>
        ModFileSystem = 0,

        /// <seealso cref="Interop.Services.ModSphereMapManager"/>
        ModSphereMapManager = 0, // 合并上游时保留：模组级球面贴图
    }
}

using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Shared;
using ProjectM.Scripting;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace LilWu.SSRecharge;

internal static class RechargeService
{
    // TM_Castle_RefinementStation_Soulshard on the current V Rising server build.
    private static readonly PrefabGUID SoulShardContainerPrefab = new(1794206684);

    private static EntityManager _entityManager;
    private static ServerGameSettingsSystem? _gameSettings;
    private static EntityQuery _shardQuery;
    private static EntityQuery _containerQuery;
    private static bool _initialized;
    private static DateTime _lastScanUtc;
    private static DateTime _lastSuccessfulScanUtc;
    private static int _lastAppliedSlotLimit = -1;

    internal static void Tick()
    {
        if (!Plugin.Enabled.Value)
            return;

        var now = DateTime.UtcNow;
        var interval = Math.Max(1, Plugin.ScanIntervalSeconds.Value);
        if (_lastScanUtc != default && (now - _lastScanUtc).TotalSeconds < interval)
            return;

        _lastScanUtc = now;

        try
        {
            if (!_initialized && !TryInitialize())
                return;

            var elapsedSeconds = _lastSuccessfulScanUtc == default
                ? interval
                : Math.Clamp((now - _lastSuccessfulScanUtc).TotalSeconds, 0, interval * 3.0);
            _lastSuccessfulScanUtc = now;

            RechargeStoredShards(elapsedSeconds);
            ApplyContainerSlotLimit();
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Soul Shard recharge scan failed: {exception}");
        }
    }

    private static bool TryInitialize()
    {
        var server = FindServerWorld();
        if (server is null)
            return false;

        var prefabCollection = server.GetExistingSystemManaged<PrefabCollectionSystem>();
        if (prefabCollection is null || prefabCollection.SpawnableNameToPrefabGuidDictionary.Count == 0)
            return false;

        _entityManager = server.EntityManager;
        _gameSettings = server.GetExistingSystemManaged<ServerGameSettingsSystem>();
        if (_gameSettings is null)
            return false;

        var shardBuilder = new EntityQueryBuilder(Allocator.Temp)
            .AddAll(new ComponentType(Il2CppType.Of<Relic>(), ComponentType.AccessMode.ReadOnly))
            .AddAll(new ComponentType(Il2CppType.Of<Durability>(), ComponentType.AccessMode.ReadWrite))
            .AddAll(new ComponentType(Il2CppType.Of<InventoryItem>(), ComponentType.AccessMode.ReadOnly));
        _shardQuery = _entityManager.CreateEntityQuery(ref shardBuilder);
        shardBuilder.Dispose();

        var containerBuilder = new EntityQueryBuilder(Allocator.Temp)
            .AddAll(new ComponentType(Il2CppType.Of<PrefabGUID>(), ComponentType.AccessMode.ReadOnly))
            .AddAll(new ComponentType(Il2CppType.Of<InventoryInstanceElement>(), ComponentType.AccessMode.ReadWrite));
        _containerQuery = _entityManager.CreateEntityQuery(ref containerBuilder);
        containerBuilder.Dispose();

        _initialized = true;
        Plugin.ModLog.LogInfo("Soul Shard recharge service initialized.");
        return true;
    }

    private static void RechargeStoredShards(double elapsedSeconds)
    {
        var hours = Plugin.RechargeHours.Value;
        if (hours <= 0 || elapsedSeconds <= 0)
            return;

        var entities = _shardQuery.ToEntityArray(Allocator.Temp);
        var changed = 0;

        foreach (var shard in entities)
        {
            var relic = _entityManager.GetComponentData<Relic>(shard);
            if (relic.RelicType == RelicType.None || !IsInSoulShardContainer(shard))
                continue;

            var durability = _entityManager.GetComponentData<Durability>(shard);
            if (durability.MaxDurability <= 0 || durability.Value >= durability.MaxDurability)
                continue;

            var rechargePerSecond = durability.MaxDurability / (hours * 60.0 * 60.0);
            durability.Value = Math.Min(durability.MaxDurability,
                durability.Value + (float)(rechargePerSecond * elapsedSeconds));
            durability.IsBroken = durability.Value <= 0;
            _entityManager.SetComponentData(shard, durability);
            changed++;
        }

        entities.Dispose();
        if (changed > 0)
            Plugin.ModLog.LogDebug($"Recharged {changed} stored Soul Shard(s) for {elapsedSeconds:0.0} seconds.");
    }

    private static bool IsInSoulShardContainer(Entity shard)
    {
        var inventoryItem = _entityManager.GetComponentData<InventoryItem>(shard);
        var inventoryEntity = inventoryItem.ContainerEntity;
        if (inventoryEntity == Entity.Null || !_entityManager.Exists(inventoryEntity) ||
            !_entityManager.HasComponent<InventoryConnection>(inventoryEntity))
            return false;

        var owner = _entityManager.GetComponentData<InventoryConnection>(inventoryEntity).InventoryOwner;
        return owner != Entity.Null && _entityManager.Exists(owner) &&
               _entityManager.HasComponent<PrefabGUID>(owner) &&
               _entityManager.GetComponentData<PrefabGUID>(owner) == SoulShardContainerPrefab;
    }

    private static void ApplyContainerSlotLimit()
    {
        if (!Plugin.LimitSlotsToClanSize.Value || _gameSettings is null)
            return;

        var clanSize = Math.Max(1, _gameSettings._Settings.ClanSize);
        var entities = _containerQuery.ToEntityArray(Allocator.Temp);
        var containersChanged = 0;

        foreach (var entity in entities)
        {
            if (_entityManager.GetComponentData<PrefabGUID>(entity) != SoulShardContainerPrefab)
                continue;

            var inventories = _entityManager.GetBuffer<InventoryInstanceElement>(entity);
            var changed = false;
            for (var i = 0; i < inventories.Length; i++)
            {
                var inventory = inventories[i];
                if (inventory.Slots == clanSize && inventory.MaxSlots == clanSize)
                    continue;

                inventory.Slots = clanSize;
                inventory.MaxSlots = clanSize;
                inventories[i] = inventory;
                changed = true;
            }

            if (changed)
                containersChanged++;
        }

        entities.Dispose();
        if (containersChanged > 0 || _lastAppliedSlotLimit != clanSize)
            Plugin.ModLog.LogInfo($"Soul Shard container slot limit set to server clan size: {clanSize}. Updated {containersChanged} container(s).");
        _lastAppliedSlotLimit = clanSize;
    }

    private static World? FindServerWorld()
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == "Server")
                return world;
        }

        return null;
    }

    internal static void Dispose()
    {
        if (!_initialized)
            return;

        _shardQuery.Dispose();
        _containerQuery.Dispose();
        _initialized = false;
    }
}

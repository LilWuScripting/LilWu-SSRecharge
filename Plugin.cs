using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace LilWu.SSRecharge;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "LilWu.SSRecharge";
    public const string PluginName = "LilWu's Recharged Soul Shards";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource ModLog { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<double> RechargeHours { get; private set; } = null!;
    internal static ConfigEntry<int> ScanIntervalSeconds { get; private set; } = null!;
    internal static ConfigEntry<bool> LimitSlotsToClanSize { get; private set; } = null!;

    private Harmony? _harmony;

    public override void Load()
    {
        if (Application.productName != "VRisingServer")
        {
            Log.LogWarning($"{PluginName} is server-side only; skipping load on the game client.");
            return;
        }

        ModLog = Log;
        Enabled = Config.Bind("General", "Enabled", true,
            "Enable Soul Shard container recharging.");
        RechargeHours = Config.Bind("Recharge", "FullRechargeHours", 2.0,
            "Hours required to recharge a completely empty Soul Shard to full while it remains stored.");
        ScanIntervalSeconds = Config.Bind("Recharge", "ScanIntervalSeconds", 10,
            "How often the server scans Soul Shard containers. Minimum: 1 second.");
        LimitSlotsToClanSize = Config.Bind("Storage", "LimitSlotsToClanSize", true,
            "Set every Soul Shard container's available slots to the server ClanSize setting.");

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();
        Log.LogInfo($"Loaded {PluginName} v{PluginVersion}. Full recharge time: {RechargeHours.Value:0.##} hours.");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        RechargeService.Dispose();
        return true;
    }
}

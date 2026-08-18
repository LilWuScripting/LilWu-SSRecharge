# LilWu's Recharged Soul Shards

`LilWu.SSRecharge` is a server-side V Rising mod that turns the built-in Soul Shard container into a slow recharge station.

## Features

- Soul Shards recharge only while stored in `TM_Castle_RefinementStation_Soulshard`.
- A fully depleted shard takes 2 hours to recharge by default.
- Partially depleted shards recharge proportionally (for example, 50% to full takes 1 hour).
- Soul Shard container slots automatically match the server's `ClanSize` setting.
- Recharge duration, scan interval, and slot limiting are configurable through BepInEx.
- Server-side only; players do not need to install the mod.

The slot limit follows the configured maximum clan/team size, not the number of members currently online or currently present in one clan. A server with `ClanSize = 4` gets four slots per Soul Shard container; `ClanSize = 8` gets eight.

## Build

The project targets .NET 6 and references the assemblies from the local V Rising dedicated server installation.

```powershell
dotnet build -c Release
```

If the dedicated server is installed elsewhere:

```powershell
dotnet build -c Release -p:VRisingServerPath="D:\Servers\VRisingDedicatedServer"
```

Copy `bin/Release/net6.0/LilWu.SSRecharge.dll` into the server's `BepInEx/plugins` directory and restart the server.

## Configuration

After the first launch, edit `BepInEx/config/LilWu.SSRecharge.cfg`:

- `General.Enabled = true`
- `Recharge.FullRechargeHours = 2.0`
- `Recharge.ScanIntervalSeconds = 10`
- `Storage.LimitSlotsToClanSize = true`

## Compatibility note

The Soul Shard container prefab ID is tied to the current game build. After a major V Rising update, verify the mod log for initialization errors and rebuild against the updated server interop assemblies.

## Local RCON administration

The scripts under `tools` enable localhost-only RCON and store its generated password using Windows DPAPI. After configuration and one server restart, announce a restart from PowerShell with:

```powershell
.\tools\Send-VRisingRcon.ps1 'announcerestart 10'
```

Or send a custom message:

```powershell
.\tools\Send-VRisingRcon.ps1 'announce Server restart in 10 minutes. Please return to a safe place.'
```

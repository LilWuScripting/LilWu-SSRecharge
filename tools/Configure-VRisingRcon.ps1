[CmdletBinding()]
param(
    [string] $SettingsPath = 'C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer\save-data\Saves\v4\BloodBucketSave\ServerHostSettings.json',
    [int] $Port = 25575,
    [string] $BindAddress = '127.0.0.1',
    [string] $CredentialPath = (Join-Path $PSScriptRoot 'rcon-password.clixml')
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $SettingsPath)) {
    throw "Server settings not found: $SettingsPath"
}
$passwordBytes = [byte[]]::new(32)
[Security.Cryptography.RandomNumberGenerator]::Fill($passwordBytes)
$password = [Convert]::ToBase64String($passwordBytes)
$securePassword = ConvertTo-SecureString -String $password -AsPlainText -Force
$settings = Get-Content -Raw -LiteralPath $SettingsPath | ConvertFrom-Json
if (-not $settings.Rcon) {
    $settings | Add-Member -NotePropertyName Rcon -NotePropertyValue ([pscustomobject]@{})
}
$settings.Rcon.Enabled = $true
$settings.Rcon.Port = $Port
$settings.Rcon.Password = $password
if ($settings.Rcon.PSObject.Properties.Name -contains 'BindAddress') {
    $settings.Rcon.BindAddress = $BindAddress
}
else {
    $settings.Rcon | Add-Member -NotePropertyName BindAddress -NotePropertyValue $BindAddress
}
$backupPath = "$SettingsPath.pre-rcon-backup"
Copy-Item -LiteralPath $SettingsPath -Destination $backupPath -Force
$settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $SettingsPath -Encoding UTF8
$securePassword | Export-Clixml -LiteralPath $CredentialPath -Force
Write-Host ("RCON enabled on " + $BindAddress + ":" + $Port + ".")
Write-Host "Settings backup: $backupPath"
Write-Host 'Restart the V Rising server once to activate RCON.'

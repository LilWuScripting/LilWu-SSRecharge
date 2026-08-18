[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string] $Command,
    [string] $HostName = '127.0.0.1',
    [int] $Port = 25575,
    [string] $CredentialPath = (Join-Path $PSScriptRoot 'rcon-password.clixml'),
    [int] $TimeoutSeconds = 10
)
$ErrorActionPreference = 'Stop'
function New-RconPacket {
    param([int] $Id, [int] $Type, [string] $Body)
    $bodyBytes = [Text.Encoding]::UTF8.GetBytes($Body)
    $size = 4 + 4 + $bodyBytes.Length + 2
    $packet = [byte[]]::new($size + 4)
    [BitConverter]::GetBytes($size).CopyTo($packet, 0)
    [BitConverter]::GetBytes($Id).CopyTo($packet, 4)
    [BitConverter]::GetBytes($Type).CopyTo($packet, 8)
    $bodyBytes.CopyTo($packet, 12)
    return $packet
}
function Read-ExactBytes {
    param([IO.Stream] $Stream, [int] $Count)
    $result = [byte[]]::new($Count)
    $offset = 0
    while ($offset -lt $Count) {
        $read = $Stream.Read($result, $offset, $Count - $offset)
        if ($read -le 0) { throw 'RCON connection closed unexpectedly.' }
        $offset += $read
    }
    return $result
}
function Read-RconPacket {
    param([IO.Stream] $Stream)
    $sizeBytes = Read-ExactBytes -Stream $Stream -Count 4
    $size = [BitConverter]::ToInt32($sizeBytes, 0)
    if ($size -lt 10 -or $size -gt 4MB) { throw "Invalid RCON packet size: $size" }
    $payload = Read-ExactBytes -Stream $Stream -Count $size
    [pscustomobject]@{
        Id = [BitConverter]::ToInt32($payload, 0)
        Type = [BitConverter]::ToInt32($payload, 4)
        Body = [Text.Encoding]::UTF8.GetString($payload, 8, $size - 10)
    }
}
if (-not (Test-Path -LiteralPath $CredentialPath)) {
    throw "RCON credential not found: $CredentialPath. Run Configure-VRisingRcon.ps1 first."
}
$securePassword = Import-Clixml -LiteralPath $CredentialPath
$credential = [Management.Automation.PSCredential]::new('rcon', $securePassword)
$password = $credential.GetNetworkCredential().Password
$client = [Net.Sockets.TcpClient]::new()
try {
    $connect = $client.ConnectAsync($HostName, $Port)
    if (-not $connect.Wait([TimeSpan]::FromSeconds($TimeoutSeconds))) {
        throw ("Timed out connecting to RCON at " + $HostName + ":" + $Port + ".")
    }
    $client.ReceiveTimeout = $TimeoutSeconds * 1000
    $client.SendTimeout = $TimeoutSeconds * 1000
    $stream = $client.GetStream()
    $authPacket = New-RconPacket -Id 1 -Type 3 -Body $password
    $stream.Write($authPacket, 0, $authPacket.Length)
    $authResponse = Read-RconPacket -Stream $stream
    if ($authResponse.Id -eq -1) { throw 'RCON authentication failed.' }
    $commandPacket = New-RconPacket -Id 2 -Type 2 -Body $Command
    $stream.Write($commandPacket, 0, $commandPacket.Length)
    $response = Read-RconPacket -Stream $stream
    if ($response.Id -ne 2) { throw "Unexpected RCON response id: $($response.Id)" }
    if ($response.Body) { $response.Body }
    else { "Command sent: $Command" }
}
finally {
    $password = $null
    $client.Dispose()
}

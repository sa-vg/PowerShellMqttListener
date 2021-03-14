Import-Module   "$PSScriptRoot/../Module/PowerShellMqtt.Listener.dll" -Force

$filePath = $PSScriptRoot + "\output.txt"
$filePath

Start-MqttListener `
-Server "localhost" `
-ClientId "ClientId-001" `
-Port 1883 `
-Username "username" `
-Password "password" `
-Topic 'brand/type/group/code' `
-OnlyPayload `
-CleanSession:$false `
-Verbose > $filePath
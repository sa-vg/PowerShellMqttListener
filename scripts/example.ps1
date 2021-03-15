$module = Import-Module "$PSScriptRoot/../PowerShellMqtt" -Force

$filePath = $PSScriptRoot + "\output.txt"
$filePath

$args = @{
    Server = "localhost"
    ClientId = "ClientId-001"
    Port = 1883
    Username = "username"
    Password = "password"
    Topic = 'brand/type/group/code'
    OnlyPayload = $true
    CleanSession = $false
    Verbose = $true
}

Start-MqttListener @args | Out-File -FilePath $filePath -Append
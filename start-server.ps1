$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = $scriptRoot

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget\packages"
$env:DOTNET_ROLL_FORWARD = "Major"

Write-Host "Server BACKEND dang khoi dong..."
Write-Host "API local: http://localhost:5055"
Write-Host "API cho thiet bi khac: http://<IP-hoac-domain-cua-server>:5055"
Write-Host "SignalR hub: /hubs/incidents"
Write-Host "Cau hinh DB bang BACKEND/appsettings.json hoac bien moi truong POLICE_DATABASE_PROVIDER, POLICE_SQLSERVER_CONNECTION, POLICE_POSTGRES_CONNECTION"

dotnet run --project (Join-Path $scriptRoot "PoliceBackend.csproj") --urls "http://0.0.0.0:5055"

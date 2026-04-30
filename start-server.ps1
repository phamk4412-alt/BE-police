$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = $scriptRoot
$projectPath = Join-Path $repoRoot "PoliceBackend.csproj"
$appDll = Join-Path $repoRoot "bin\Debug\net9.0\PoliceBackend.dll"
$localDotnet = Join-Path $repoRoot ".dotnet9\dotnet.exe"

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget\packages"

Write-Host "Server BACKEND dang khoi dong..."
Write-Host "API local: http://localhost:5055"
Write-Host "API cho thiet bi khac: http://<IP-hoac-domain-cua-server>:5055"
Write-Host "SignalR hub: /hubs/incidents"
Write-Host "Cau hinh DB bang BACKEND/appsettings.json hoac bien moi truong POLICE_DATABASE_PROVIDER, POLICE_SQLSERVER_CONNECTION, POLICE_POSTGRES_CONNECTION"

dotnet build $projectPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$env:ASPNETCORE_URLS = "http://0.0.0.0:5055"

if (Test-Path $localDotnet) {
    $env:DOTNET_ROOT = Split-Path -Parent $localDotnet
    & $localDotnet $appDll
} else {
    dotnet $appDll
}

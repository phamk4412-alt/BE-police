$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = $scriptRoot

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget\packages"
$env:DOTNET_ROLL_FORWARD = "Major"

if ([string]::IsNullOrWhiteSpace($env:POLICE_DATABASE_PROVIDER)) {
    $env:POLICE_DATABASE_PROVIDER = "sqlserver"
}

if ($env:POLICE_DATABASE_PROVIDER -eq "sqlserver" -and [string]::IsNullOrWhiteSpace($env:POLICE_SQLSERVER_CONNECTION)) {
    $server = if ([string]::IsNullOrWhiteSpace($env:POLICE_SQLSERVER_SERVER)) { "161.248.147.174,10001" } else { $env:POLICE_SQLSERVER_SERVER }
    $database = if ([string]::IsNullOrWhiteSpace($env:POLICE_SQLSERVER_DATABASE)) { "police" } else { $env:POLICE_SQLSERVER_DATABASE }
    $username = if ([string]::IsNullOrWhiteSpace($env:POLICE_SQLSERVER_USER)) { "sa" } else { $env:POLICE_SQLSERVER_USER }

    if ([string]::IsNullOrWhiteSpace($env:POLICE_SQLSERVER_PASSWORD)) {
        $securePassword = Read-Host "Nhap SQL Server password cho user '$username'" -AsSecureString
        $password = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
        )
    } else {
        $password = $env:POLICE_SQLSERVER_PASSWORD
    }

    $env:POLICE_SQLSERVER_CONNECTION = "Server=$server;Database=$database;User Id=$username;Password=$password;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=True"
}

Write-Host "Server BACKEND dang khoi dong..."
Write-Host "API local: http://localhost:5055"
Write-Host "API cho thiet bi khac: http://<IP-hoac-domain-cua-server>:5055"
Write-Host "SignalR hub: /hubs/incidents"
Write-Host "Database provider: $env:POLICE_DATABASE_PROVIDER"
Write-Host "Cau hinh DB bang bien moi truong POLICE_DATABASE_PROVIDER, POLICE_SQLSERVER_CONNECTION, POLICE_POSTGRES_CONNECTION"

dotnet run --project (Join-Path $scriptRoot "PoliceBackend.csproj") --urls "http://0.0.0.0:5055"

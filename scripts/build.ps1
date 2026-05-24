param(
    [string]$ValheimDir = $env:VALHEIM_DIR,
    [string]$Configuration = "Release"
)

if ([string]::IsNullOrWhiteSpace($ValheimDir)) {
    Write-Error "Set VALHEIM_DIR or pass -ValheimDir."
    exit 1
}

$env:VALHEIM_DIR = $ValheimDir
dotnet build "$PSScriptRoot\..\SkadiNet.csproj" -c $Configuration

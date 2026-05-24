param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestFile,

    [Parameter(Mandatory = $true)]
    [string]$Version
)

if (-not (Test-Path -LiteralPath $ManifestFile)) {
    throw "Manifest file not found: $ManifestFile"
}

$content = Get-Content -LiteralPath $ManifestFile -Raw
$updated = $content -replace '"version_number"\s*:\s*"[^"]*"', ('"version_number": "' + $Version + '"')
Set-Content -LiteralPath $ManifestFile -Value $updated -NoNewline

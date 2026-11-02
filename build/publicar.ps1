<#
  Arma el instalador en tu PC (sin GitHub Actions).

  Uso:
    .\build\publicar.ps1 -Version 1.2.0              # solo arma el instalador en .\Releases
    .\build\publicar.ps1 -Version 1.2.0 -Subir       # además lo publica en GitHub Releases

  Para -Subir necesitás un token de GitHub con permiso "contents: write"
  en la variable de entorno GITHUB_TOKEN.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$Subir
)

$ErrorActionPreference = 'Stop'
$repo = 'https://github.com/Reiseng/stock-manager-wpf'
Set-Location (Split-Path $PSScriptRoot -Parent)

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    dotnet tool install -g vpk --version 1.2.161
}

Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue
dotnet publish StockControl.csproj -c Release -r win-x64 --self-contained -p:Version=$Version -o publish

if ($Subir) {
    vpk download github --repoUrl $repo --token $env:GITHUB_TOKEN -o Releases
}

$notes = @()
if (Test-Path notas-de-version.md) { $notes = @('--releaseNotes', 'notas-de-version.md') }
vpk pack --packId StockManager --packVersion $Version --packDir publish --mainExe StockManager.exe `
    --packTitle "Stock Manager" --packAuthors "Reiseng Software" -o Releases @notes

if ($Subir) {
    vpk upload github --repoUrl $repo --token $env:GITHUB_TOKEN -o Releases --publish `
        --releaseName "Stock Manager $Version" --tag "v$Version"
}

Write-Host ""
Write-Host "Listo. Instalador: Releases\StockManager-win-Setup.exe" -ForegroundColor Green

# PowerShell script to switch between local and NuGet references
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("local", "nuget")]
    [string]$Mode,
    
    [string]$Version = "1.1.1"
)

Write-Host "MapPilote Reference Switcher" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan
Write-Host

if ($Mode -eq "local") {
    Write-Host "Switching to LOCAL PROJECT references..." -ForegroundColor Green
    Write-Host "• Enables debugging into MapPiloteGeopackageHelper source" -ForegroundColor Gray
    Write-Host "• Changes to library immediately available" -ForegroundColor Gray
    Write-Host "• Use this during development" -ForegroundColor Gray
    Write-Host
    
    dotnet build --property:UseLocalProjects=true
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Successfully switched to LOCAL PROJECT references" -ForegroundColor Green
    }
} else {
    Write-Host "Switching to NUGET PACKAGE references..." -ForegroundColor Yellow
    Write-Host "• Uses published version $Version from NuGet" -ForegroundColor Gray
    Write-Host "• Library debugging not available (optimized)" -ForegroundColor Gray
    Write-Host "• Use this for distribution/testing" -ForegroundColor Gray
    Write-Host
    
    dotnet build --property:UseLocalProjects=false --property:MapPiloteGeopackageHelperVersion=$Version
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Successfully switched to NUGET PACKAGE references (v$Version)" -ForegroundColor Yellow
    }
}

Write-Host
Write-Host "Usage examples:" -ForegroundColor Cyan
Write-Host "  .\SwitchReferences.ps1 -Mode local" -ForegroundColor White
Write-Host "  .\SwitchReferences.ps1 -Mode nuget" -ForegroundColor White
Write-Host "  .\SwitchReferences.ps1 -Mode nuget -Version 1.2.0" -ForegroundColor White
@echo off
REM Switch to using NuGet package references
echo Switching to NUGET PACKAGE references...
echo This uses the published version from NuGet.
echo.

dotnet build --property:UseLocalProjects=false

echo.
echo ? Now using NUGET PACKAGE references
echo ? Using published version %MapPiloteGeopackageHelperVersion%
echo ? Library debugging not available (optimized release build)
echo.
pause
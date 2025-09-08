@echo off
REM Switch to using local project references for debugging
echo Switching to LOCAL PROJECT references...
echo This allows debugging into MapPiloteGeopackageHelper source code.
echo.

dotnet build --property:UseLocalProjects=true

echo.
echo ? Now using LOCAL PROJECT references
echo ? You can debug into MapPiloteGeopackageHelper source
echo ? Changes to the library will be immediately available
echo.
pause
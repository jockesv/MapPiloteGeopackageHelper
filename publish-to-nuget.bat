@echo off
REM ====================================================================
REM Publish MapPiloteGeopackageHelper to NuGet.org
REM ====================================================================
REM 
REM Prerequisites:
REM 1. Set NUGET_API_KEY environment variable with your API key
REM    OR edit this file and uncomment the SET NUGET_API_KEY line below
REM 2. Build the project in Release mode first
REM
REM Usage:
REM   publish-to-nuget.bat [version]
REM
REM Example:
REM   publish-to-nuget.bat 1.2.2
REM ====================================================================

setlocal enabledelayedexpansion

REM Uncomment and set your API key here if not using environment variable
REM SET NUGET_API_KEY=your-api-key-here

REM Check if API key is set
if "%NUGET_API_KEY%"=="" (
    echo ERROR: NUGET_API_KEY environment variable is not set!
    echo.
    echo Please either:
    echo   1. Set environment variable: SET NUGET_API_KEY=your-key
    echo   2. Edit this script and set the key directly
    echo.
    echo Get your API key from: https://www.nuget.org/account/apikeys
    exit /b 1
)

REM Get version from parameter or detect from latest package
if "%1"=="" (
    echo Detecting latest package version...
    for /f "delims=" %%i in ('dir /b /o-d MapPiloteGeopackageHelper\bin\Release\MapPiloteGeopackageHelper.*.nupkg 2^>nul') do (
        set PACKAGE_FILE=%%i
        goto :found
    )
    echo ERROR: No package found in MapPiloteGeopackageHelper\bin\Release\
    echo Please build the project in Release mode first:
    echo   dotnet build MapPiloteGeopackageHelper\MapPiloteGeopackageHelper.csproj -c Release
    exit /b 1
    :found
) else (
    set PACKAGE_FILE=MapPiloteGeopackageHelper.%1.nupkg
)

set PACKAGE_PATH=MapPiloteGeopackageHelper\bin\Release\%PACKAGE_FILE%

REM Check if package exists
if not exist "%PACKAGE_PATH%" (
    echo ERROR: Package not found: %PACKAGE_PATH%
    echo.
    echo Please build the project in Release mode first:
    echo   dotnet build MapPiloteGeopackageHelper\MapPiloteGeopackageHelper.csproj -c Release
    exit /b 1
)

echo.
echo ====================================================================
echo Publishing to NuGet.org
echo ====================================================================
echo Package: %PACKAGE_FILE%
echo Path:    %PACKAGE_PATH%
echo.

REM Confirm before publishing
set /p CONFIRM="Are you sure you want to publish this package? (Y/N): "
if /i not "%CONFIRM%"=="Y" (
    echo Publish cancelled.
    exit /b 0
)

echo.
echo Publishing...
dotnet nuget push "%PACKAGE_PATH%" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json --skip-duplicate

if %ERRORLEVEL% equ 0 (
    echo.
    echo ====================================================================
    echo SUCCESS! Package published to NuGet.org
    echo ====================================================================
    echo.
    echo View your package at:
    echo https://www.nuget.org/packages/MapPiloteGeopackageHelper/
    echo.
    echo Note: It may take a few minutes for the package to be indexed
    echo and available for download.
    echo ====================================================================
) else (
    echo.
    echo ====================================================================
    echo ERROR: Failed to publish package!
    echo ====================================================================
    echo.
    echo Common issues:
    echo   - Invalid or expired API key
    echo   - Version already exists (use --skip-duplicate flag)
    echo   - Network connectivity issues
    echo   - Package validation failed
    echo.
    echo Check the error message above for details.
    echo ====================================================================
    exit /b 1
)

endlocal

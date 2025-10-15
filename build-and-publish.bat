@echo off
REM ====================================================================
REM Build and Publish MapPiloteGeopackageHelper NuGet Package
REM ====================================================================
REM 
REM This script:
REM 1. Cleans previous builds
REM 2. Builds the project in Release mode (generates .nupkg)
REM 3. Runs all tests
REM 4. Optionally publishes to NuGet.org
REM
REM Prerequisites:
REM - .NET 8 and .NET 9 SDKs installed
REM - NUGET_API_KEY environment variable set (for publishing)
REM ====================================================================

setlocal

echo.
echo ====================================================================
echo Building MapPiloteGeopackageHelper Package
echo ====================================================================
echo.

REM Step 1: Clean
echo [1/4] Cleaning previous builds...
dotnet clean MapPiloteGeopackageHelper\MapPiloteGeopackageHelper.csproj -c Release
if %ERRORLEVEL% neq 0 (
    echo ERROR: Clean failed!
    exit /b 1
)
echo Clean completed.
echo.

REM Step 2: Build in Release mode
echo [2/4] Building project in Release mode...
dotnet build MapPiloteGeopackageHelper\MapPiloteGeopackageHelper.csproj -c Release
if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed!
    exit /b 1
)
echo Build completed.
echo.

REM Step 3: Run tests
echo [3/4] Running tests...
dotnet test TestMapPiloteGeoPackageHandler\TestMapPiloteGeoPackageHandler.csproj -c Release --no-build --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo ERROR: Tests failed!
    echo Please fix the failing tests before publishing.
    exit /b 1
)
echo All tests passed.
echo.

REM Step 4: Show generated packages
echo [4/4] Generated packages:
echo.
dir /b MapPiloteGeopackageHelper\bin\Release\*.nupkg
echo.

REM Find the latest package
for /f "delims=" %%i in ('dir /b /o-d MapPiloteGeopackageHelper\bin\Release\MapPiloteGeopackageHelper.*.nupkg 2^>nul') do (
    set LATEST_PACKAGE=%%i
    goto :found_package
)
:found_package

echo Latest package: %LATEST_PACKAGE%
echo Location: MapPiloteGeopackageHelper\bin\Release\%LATEST_PACKAGE%
echo.

REM Ask if user wants to publish
set /p PUBLISH="Do you want to publish this package to NuGet.org? (Y/N): "
if /i "%PUBLISH%"=="Y" (
    echo.
    call publish-to-nuget.bat
) else (
    echo.
    echo Package built successfully but not published.
    echo To publish later, run: publish-to-nuget.bat
)

echo.
echo ====================================================================
echo Done!
echo ====================================================================

endlocal

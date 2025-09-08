# Reference Mode Configuration
# 
# This file shows the current reference mode for test/example projects:
#
# LOCAL   = Uses project references (for debugging and development)
# NUGET   = Uses NuGet package references (for distribution testing)
#
# To change modes:
# - Edit Directory.Build.props and change <UseLocalProjects> value
# - Or run: dotnet build --property:UseLocalProjects=true/false
# - Or use the provided scripts: switch-to-local.bat / switch-to-nuget.bat

Current Mode: LOCAL (UseLocalProjects=true)
NuGet Version: 1.1.1

Projects affected:
- MapPiloteNugetHelloWorld
- MapPiloteLargeDatasetUploadExample  
- MapPiloteFluentApiExample
- MapPiloteGeopackageHellerHelloWorld
- MapPiloteGeopackageHelperSchemaBrowser
- MapPiloteBulkLoadPerformaceTester
- TestMapPiloteGeoPackageHandler
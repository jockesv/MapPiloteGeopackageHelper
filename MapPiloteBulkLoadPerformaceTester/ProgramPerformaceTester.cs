/* Licence...
 * MIT License
 *
 * Copyright (c) 2025 Anders Dahlgren
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy 
 * of this software and associated documentation files (the "Software"), to deal 
 * in the Software without restriction, including without limitation the rights 
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is 
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
 * SOFTWARE.
 */
using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;
using System.Diagnostics;
using System.Globalization;

// =============================================================
// Bulk Load Performance Tester for MapPiloteGeopackageHelper
// -------------------------------------------------------------
// This performance comparison example demonstrates:
//  1) Single-row insert approach (traditional, slower)
//  2) Bulk insert approach (modern, faster)
//  3) Performance metrics and timing comparisons
//  4) File size analysis between methods
//  5) Configurable test dataset generation
// Perfect for benchmarking different insertion strategies!
// =============================================================

// Performance comparison between single inserts and bulk insert

Console.WriteLine("=== MapPilote Bulk Load Performance Tester ===");

// Show reference mode at runtime - check the LIBRARY we're testing, not this project
#if DEBUG
var helperAssembly = typeof(CMPGeopackageCreateHelper).Assembly;
var assemblyLocation = helperAssembly.Location;
var assemblyDirectory = Path.GetDirectoryName(assemblyLocation) ?? "";

Console.WriteLine($"  Checking MapPiloteGeopackageHelper library location...");
Console.WriteLine($"   Assembly: {helperAssembly.FullName}");
Console.WriteLine($"   Location: {assemblyLocation}");

// Check if we're in a local development environment but using copied assemblies
var isInTestProjectBin = assemblyLocation.Contains("\\bin\\Debug\\") || 
                         assemblyLocation.Contains("/bin/Debug/") ||
                         assemblyLocation.Contains("\\bin\\Release\\") || 
                         assemblyLocation.Contains("/bin/Release/");

// Read the MSBuild UseLocalProjects setting from the project
var projectDir = Path.GetDirectoryName(Environment.CurrentDirectory) ?? "";
var useLocalProjectsFromBuild = Environment.GetEnvironmentVariable("UseLocalProjects");
var assemblySize = new FileInfo(assemblyLocation).Length;

// Better detection logic - check multiple indicators
var isFromNuGet = false;
var nuGetIndicators = new List<string>();

// Check 1: Direct NuGet cache path
if (assemblyLocation.Contains("\\.nuget\\packages\\") || 
    assemblyLocation.Contains("/.nuget/packages/") ||
    assemblyLocation.Contains("\\packages\\mappilotegeopackagehelper\\") ||
    assemblyLocation.Contains("/packages/mappilotegeopackagehelper/"))
{
    isFromNuGet = true;
    nuGetIndicators.Add("Direct NuGet cache path");
}

// Check 2: Look for NuGet restore files in obj folder
var objFolder = Path.Combine(Environment.CurrentDirectory, "obj");
if (Directory.Exists(objFolder))
{
    var nugetRestoreFiles = Directory.GetFiles(objFolder, "*.nuget.*", SearchOption.AllDirectories);
    if (nugetRestoreFiles.Length > 0)
    {
        nuGetIndicators.Add($"NuGet restore files in obj ({nugetRestoreFiles.Length} files)");
        
        // Check if any restore file mentions our package
        foreach (var file in nugetRestoreFiles.Take(5)) // Check first 5 files
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains("MapPiloteGeopackageHelper", StringComparison.OrdinalIgnoreCase))
                {
                    isFromNuGet = true;
                    nuGetIndicators.Add($"Package found in {Path.GetFileName(file)}");
                    break;
                }
            }
            catch { /* ignore file read errors */ }
        }
    }
}

// Check 3: Look for .deps.json file which indicates NuGet resolution
var depsJsonPath = Path.Combine(Path.GetDirectoryName(assemblyLocation) ?? "", 
                                Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "") + ".deps.json");
if (File.Exists(depsJsonPath))
{
    try
    {
        var depsContent = File.ReadAllText(depsJsonPath);
        if (depsContent.Contains("MapPiloteGeopackageHelper", StringComparison.OrdinalIgnoreCase))
        {
            nuGetIndicators.Add("Found in .deps.json (NuGet dependency)");
            if (depsContent.Contains("\"type\": \"package\""))
            {
                isFromNuGet = true;
                nuGetIndicators.Add("Confirmed as package dependency in .deps.json");
            }
        }
    }
    catch { /* ignore file read errors */ }
}

Console.WriteLine($"   MSBuild UseLocalProjects: {useLocalProjectsFromBuild ?? "not set in environment"}");
Console.WriteLine($"   NuGet Detection Indicators: {string.Join(", ", nuGetIndicators)}");

if (isFromNuGet)
{
    Console.WriteLine("  LIBRARY MODE: Using NUGET MapPiloteGeopackageHelper package");  
    Console.WriteLine("     Testing against published NuGet package from nuget.org");
}
else if (isInTestProjectBin)
{
    // When in test project bin, we need to check the build configuration
    // If UseLocalProjects=false in Directory.Build.props, it's likely NuGet even if copied
    var directoryBuildProps = Path.Combine(projectDir, "Directory.Build.props");
    bool useLocalFromProps = true; // default assumption
    
    if (File.Exists(directoryBuildProps))
    {
        var propsContent = File.ReadAllText(directoryBuildProps);
        if (propsContent.Contains("<UseLocalProjects") && propsContent.Contains(">false<"))
        {
            useLocalFromProps = false;
        }
    }
    
    if (!useLocalFromProps)
    {
        Console.WriteLine("  LIBRARY MODE: Using NUGET MapPiloteGeopackageHelper package");  
        Console.WriteLine("     Testing against published NuGet package (copied to bin folder)");
        Console.WriteLine("      Assembly copied from NuGet cache to local bin during build");
    }
    else
    {
        Console.WriteLine("  LIBRARY MODE: Using LOCAL MapPiloteGeopackageHelper");
        Console.WriteLine("     Testing against your development code");
    }
}
else
{
    Console.WriteLine("  LIBRARY MODE: UNKNOWN - Please check manually");
    Console.WriteLine($"   Location: {assemblyDirectory}");
}

// Show additional diagnostic info
Console.WriteLine($"   Diagnostic info:");
Console.WriteLine($"   - Is in NuGet cache: {isFromNuGet}");
Console.WriteLine($"   - Is in bin folder: {isInTestProjectBin}");
Console.WriteLine($"   - Assembly file size: {assemblySize / 1024.0:F1} KB");
#else
Console.WriteLine("  LIBRARY MODE: Using NUGET MapPiloteGeopackageHelper package (Release build)");
#endif

const int srid = 3006;
const string layerName = "points";
const int count = 1_000; // start with 1000 points until everything is running      

var normalPath = Path.Combine(@"C:\temp\", "normal_insert.gpkg");
var bulkPath = Path.Combine(@"C:\temp\", "bulk_insert.gpkg");

// Clean up old files
TryDelete(normalPath);
TryDelete(bulkPath);

// Attribute schema (order matters for per-row insert)
var attributeOrder = new List<string> { "name", "age", "height", "note" };
var headers = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["name"] = "TEXT",
    ["age"] = "INTEGER",
    ["height"] = "REAL",
    ["note"] = "TEXT"
};

Console.WriteLine("Generating random features...");
var rnd = new Random(42);
var features = GenerateRandomFeatures(count, rnd);

// NORMAL INSERT (per feature)
Console.WriteLine("\n--- Normal insert ---");
CreateGpkgWithLayer(normalPath, layerName, headers, srid);
var sw = Stopwatch.StartNew();
int i = 0;
foreach (var f in features)
{
    // Use empty string for nulls to be treated as NULL by helper
    var values = new string[attributeOrder.Count];
    values[0] = f.Attributes.TryGetValue("name", out var name) && name != null ? name : string.Empty;
    values[1] = f.Attributes.TryGetValue("age", out var age) && age != null ? age : string.Empty;
    values[2] = f.Attributes.TryGetValue("height", out var height) && height != null ? height : string.Empty;
    values[3] = f.Attributes.TryGetValue("note", out var note) && note != null ? note : string.Empty;

    CGeopackageAddDataHelper.AddPointToGeoPackage(normalPath, layerName, (Point)f.Geometry!, values);

    i++;
    if (i % 2000 == 0)
        Console.Write('.');
}
sw.Stop();
Console.WriteLine();
Report(sw.Elapsed, count, normalPath);

// BULK INSERT
Console.WriteLine("\n--- Bulk insert ---");
CreateGpkgWithLayer(bulkPath, layerName, headers, srid);
sw.Restart();
CGeopackageAddDataHelper.BulkInsertFeatures(bulkPath, layerName, features, srid: srid, batchSize: 500);
sw.Stop();
Report(sw.Elapsed, count, bulkPath);

Console.WriteLine("\nDone.");

static List<FeatureRecord> GenerateRandomFeatures(int n, Random rnd)
{
    var list = new List<FeatureRecord>(n);

    // Swedish national grid extent (SWEREF99 TM)
    const double minX = 181750.0, minY = 6090250.0, maxX = 1086500.0, maxY = 7689500.0;

    for (int idx = 0; idx < n; idx++)
    {
        var x = minX + rnd.NextDouble() * (maxX - minX);
        var y = minY + rnd.NextDouble() * (maxY - minY);
        var p = new Point(x, y);

        var age = (18 + rnd.Next(63)).ToString(CultureInfo.InvariantCulture); // 18-80
        var height = (1.50 + rnd.NextDouble() * 0.6).ToString("0.00", CultureInfo.InvariantCulture); // 1.50-2.10
        string? note = rnd.NextDouble() < 0.2 ? null : (rnd.NextDouble() < 0.5 ? "A" : "B");

        var attrs = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["name"] = $"Person_{idx:D5}",
            ["age"] = age,
            ["height"] = height,
            ["note"] = note
        };

        list.Add(new FeatureRecord(p, attrs));
    }

    return list;
}

static void CreateGpkgWithLayer(string path, string layerName, Dictionary<string, string> headers, int srid)
{
    CMPGeopackageCreateHelper.CreateGeoPackage(path, srid);
    GeopackageLayerCreateHelper.CreateGeopackageLayer(path, layerName, headers, geometryType: "POINT", srid: srid);
}

static void Report(TimeSpan elapsed, int n, string filePath)
{
    var rps = n / Math.Max(elapsed.TotalSeconds, 1e-9);
    var size = new FileInfo(filePath).Length;
    Console.WriteLine($"Inserted {n:N0} rows in {elapsed.TotalSeconds:F2}s ({rps:N0} rows/s). File size: {size / 1024.0 / 1024.0:F2} MB");
}

static void TryDelete(string path)
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
}

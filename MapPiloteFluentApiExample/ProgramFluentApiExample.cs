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
using System.Globalization;

// =============================================================
// Modern Fluent API Example for MapPiloteGeopackageHelper
// -------------------------------------------------------------
// This comprehensive example demonstrates the modern API:
//  1) Create/open GeoPackage with fluent syntax
//  2) Define schemas and ensure layers exist
//  3) Generate sample Swedish cities data
//  4) Bulk insert with progress reporting and options
//  5) Query data with filtering, sorting and streaming
//  6) Demonstrate CRUD operations (Create, Read, Update, Delete)
//  7) Extract comprehensive metadata and statistics
// Perfect for learning the recommended async/await patterns!
// =============================================================

const string gpkgPath = "FluentAPIFileExample.gpkg";
const int srid = 3006;

// Clean up
if (File.Exists(gpkgPath)) File.Delete(gpkgPath);

Console.WriteLine("=== MapPilote Fluent API Example ===");

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
Console.WriteLine();
#else
Console.WriteLine("  LIBRARY MODE: Using NUGET MapPiloteGeopackageHelper package (Release build)");
Console.WriteLine();
#endif

try
{
    // 1. Create/open GeoPackage
    Console.WriteLine("Creating GeoPackage...");
    using var geoPackage = await GeoPackage.OpenAsync(gpkgPath, srid);
    
    // 2. Define schema for different feature types
    var pointSchema = new Dictionary<string, string>
    {
        ["name"] = "TEXT",
        ["population"] = "INTEGER",
        ["area_km2"] = "REAL",
        ["country"] = "TEXT"
    };

    // 3. Ensure layer exists
    Console.WriteLine("Creating layer 'cities'...");
    var citiesLayer = await geoPackage.EnsureLayerAsync("cities", pointSchema, srid);

    // 4. Generate sample data (Swedish cities)
    Console.WriteLine("Generating sample cities...");
    var cities = GenerateSampleCities();
    
    // 5. Bulk insert with progress reporting
    Console.WriteLine("\nBulk inserting features...");
    var progress = new Progress<BulkProgress>(p =>
    {
        var bar = new string('#', (int)(p.PercentComplete / 5));
        var empty = new string('.', Math.Max(0, 20 - bar.Length));
        Console.Write($"\r[{bar}{empty}] {p.Processed}/{p.Total} ({p.PercentComplete.ToString("F1", CultureInfo.InvariantCulture)}%) - {p.Remaining} remaining");
        if (p.Processed >= p.Total)
        {
            // Finish the progress line with a newline once complete
            Console.WriteLine();
        }
    }
    );
    

    await citiesLayer.BulkInsertAsync(
        cities,
        new BulkInsertOptions(
            BatchSize: 100,
            CreateSpatialIndex: true,
            ConflictPolicy: ConflictPolicy.Ignore
        ),
        progress);

    // Ensure we start on a fresh line after progress output
    Console.WriteLine();
    
    Console.WriteLine("Insert completed!");

    // 6. Query data back with various options
    Console.WriteLine("\nQuerying data back...");
    
    // Count all features
    var totalCount = await citiesLayer.CountAsync();
    Console.WriteLine($"Total cities: {totalCount}");
    
    // Count large cities
    var largeCities = await citiesLayer.CountAsync("population > 100000");
    Console.WriteLine($"Large cities (>100k): {largeCities}");
    
    // 7. Stream features with filters
    Console.WriteLine("\nTop 5 largest cities:");
    var readOptions = new ReadOptions(
        IncludeGeometry: true,
        WhereClause: "population > 50000",
        OrderBy: "population DESC",
        Limit: 5
    );
    
    int rank = 1;
    await foreach (var city in citiesLayer.ReadFeaturesAsync(readOptions))
    {
        var point = city.Geometry as Point;
        var name = city.Attributes["name"];
        var pop = city.Attributes["population"];
        var country = city.Attributes["country"];
        
        Console.WriteLine($"  {rank}. {name}, {country} - {pop:N0} people at ({point?.X:F0}, {point?.Y:F0})");
        rank++;
    }

    // 8. Demonstrate update/delete operations
    Console.WriteLine("\nCleaning up small towns...");
    var deleted = await citiesLayer.DeleteAsync("population < 15000");
    Console.WriteLine($"Deleted {deleted} small towns");

    var remainingCount = await citiesLayer.CountAsync();
    Console.WriteLine($"Remaining cities: {remainingCount}");

    // 9. Get comprehensive metadata
    Console.WriteLine("\nGeoPackage metadata:");
    var info = await geoPackage.GetInfoAsync();
    
    foreach (var layer in info.Layers)
    {
        Console.WriteLine($"  Layer: {layer.TableName}");
        Console.WriteLine($"    Type: {layer.GeometryType ?? "Non-spatial"}");
        Console.WriteLine($"    SRID: {layer.Srid}");
        Console.WriteLine($"    Columns: {layer.AttributeColumns.Count} attributes");
        
        if (layer.MinX.HasValue)
        {
            Console.WriteLine($"    Extent: [{layer.MinX:F0}, {layer.MinY:F0}] -> [{layer.MaxX:F0}, {layer.MaxY:F0}]");
        }
    }

    Console.WriteLine($"\nDemo completed! GeoPackage saved to: {Path.GetFullPath(gpkgPath)}");
    Console.WriteLine("You can open this file in QGIS or other GIS software.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
}

static List<FeatureRecord> GenerateSampleCities()
{
    var cities = new List<(string Name, double X, double Y, int Population, double Area, string Country)>
    {
        // Major cities
        ("Stockholm", 683527, 6579433, 975551, 188.0, "Sweden"),
        ("Gothenburg", 317773, 6394498, 579281, 203.6, "Sweden"),
        ("Malmö", 375040, 6163000, 350963, 158.4, "Sweden"),
        ("Uppsala", 646138, 6636722, 230767, 48.8, "Sweden"),
        ("Västerås", 587902, 6611234, 127799, 48.2, "Sweden"),
        ("Örebro", 511954, 6569151, 126009, 58.2, "Sweden"),
        ("Linköping", 537341, 6473261, 165618, 56.6, "Sweden"),
        ("Helsingborg", 358240, 6212773, 149280, 38.4, "Sweden"),
        ("Jönköping", 450430, 6400662, 98659, 38.2, "Sweden"),
        ("Norrköping", 568715, 6494377, 95618, 45.8, "Sweden"),
        ("Lund", 386905, 6175041, 94703, 22.6, "Sweden"),
        ("Umeå", 757988, 7088793, 89607, 33.4, "Sweden"),
        ("Gävle", 616308, 6729788, 78331, 62.7, "Sweden"),
        ("Borås", 377137, 6400313, 72169, 40.2, "Sweden"),
        ("Eskilstuna", 584568, 6580834, 69948, 53.6, "Sweden"),
        
        // Smaller towns and villages to demonstrate deletion
        ("Mariefred", 626618, 6571605, 3783, 12.4, "Sweden"),
        ("Trosa", 646980, 6531411, 5192, 18.7, "Sweden"),
        ("Vaxholm", 689191, 6590230, 4312, 8.9, "Sweden"),
        ("Sigtuna", 652732, 6612047, 8444, 21.3, "Sweden"),
        ("Åmål", 367901, 6548074, 9380, 15.6, "Sweden"),
        ("Lysekil", 292103, 6465552, 7568, 19.2, "Sweden"),
        ("Marstrand", 297277, 6421146, 1432, 7.4, "Sweden"),
        ("Strömstad", 280575, 6540319, 6288, 11.8, "Sweden"),
        ("Ystad", 425090, 6144121, 18350, 9.9, "Sweden"),
        ("Simrishamn", 458718, 6156142, 6327, 22.1, "Sweden")
    };

    return cities.Select(city => new FeatureRecord(
        new Point(city.X, city.Y),
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["name"] = city.Name,
            ["population"] = city.Population.ToString(CultureInfo.InvariantCulture),
            ["area_km2"] = city.Area.ToString("F1", CultureInfo.InvariantCulture),
            ["country"] = city.Country
        }
    )).ToList();
}
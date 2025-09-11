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
//  1) Create/open GeoPackage with fluent syntax and optional status callbacks
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
Console.WriteLine("Using local MapPiloteGeopackageHelper library");
Console.WriteLine();

Console.WriteLine("MapPiloteGeopackageHelper - Hello World");
Console.WriteLine("========================================\n");

// Run both examples
await RunTraditionalApiExample(gpkgPath, srid);
Console.WriteLine();
await RunFluentApiExample(gpkgPath, srid);

Console.WriteLine("\nBoth examples completed successfully!");
Console.WriteLine("You can open the .gpkg files in QGIS or other GIS software.");

// =============================================================
// TRADITIONAL API EXAMPLE (Legacy - synchronous) with Status Callbacks
// =============================================================
static Task RunTraditionalApiExample(string workDir, int srid)
{
    Console.WriteLine("1. TRADITIONAL API EXAMPLE (with Status Callbacks)");
    Console.WriteLine("---------------------------------------------------");
    
    string gpkgPath = "traditional_hello.gpkg";
    string layerName = "traditional_points";
    
    // Clean up
    TryDelete(gpkgPath);
    
    // 1) Create GeoPackage with status callback
    Console.WriteLine("Creating GeoPackage with traditional API...");
    CMPGeopackageCreateHelper.CreateGeoPackage(gpkgPath, srid, onStatus: Console.WriteLine);
    
    // 2) Define schema
    var headers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["name"] = "TEXT",
        ["age"] = "INTEGER", 
        ["city"] = "TEXT"
    };
    
    // 3) Create layer with status and error callbacks
    GeopackageLayerCreateHelper.CreateGeopackageLayer(
        gpkgPath, layerName, headers, "POINT", srid,
        onStatus: Console.WriteLine,
        onError: msg => Console.WriteLine($"ERROR: {msg}"));
    
    // 4) Insert single point with warning callback
    var point = new Point(500000, 6400000);
    var attributes = new[] { "Alice", "30", "Stockholm" };
    CGeopackageAddDataHelper.AddPointToGeoPackage(
        gpkgPath, layerName, point, attributes,
        onWarning: Console.WriteLine);
    
    // 5) Bulk insert features with warning callback
    var features = new List<FeatureRecord>
    {
        new FeatureRecord(
            new Point(500100, 6400100),
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["name"] = "Bob",
                ["age"] = "25", 
                ["city"] = "Gothenburg"
            }),
        new FeatureRecord(
            new Point(500200, 6400200),
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["name"] = "Charlie",
                ["age"] = "35",
                ["city"] = "Malm�"
            })
    };
    
    CGeopackageAddDataHelper.BulkInsertFeatures(
        gpkgPath, layerName, features, srid,
        onWarning: Console.WriteLine);
    
    // 6) Read features back
    Console.WriteLine("Reading features with traditional API:");
    var readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkgPath, layerName);
    
    foreach (var feature in readBack.Take(3))
    {
        var p = feature.Geometry as Point;
        var name = feature.Attributes.GetValueOrDefault("name");
        var city = feature.Attributes.GetValueOrDefault("city");
        Console.WriteLine($"  - {name} from {city} at ({p?.X:F0}, {p?.Y:F0})");
    }
    
    Console.WriteLine($"? Traditional API: Created {Path.GetFileName(gpkgPath)} with {features.Count + 1} features");
    return Task.CompletedTask;
}

// =============================================================
// FLUENT API EXAMPLE (Modern - async/await, recommended) with Status Callbacks
// =============================================================
static async Task RunFluentApiExample(string workDir, int srid)
{
    Console.WriteLine("2. MODERN FLUENT API EXAMPLE (Recommended)");
    Console.WriteLine("-------------------------------------------");
    
    string gpkgPath = "fluent_hello.gpkg";
    
    // Clean up
    TryDelete(gpkgPath);
    
    try
    {
        // 1) Create/open GeoPackage with using statement (automatic disposal) and status callback
        Console.WriteLine("Creating GeoPackage with fluent API...");
        using var geoPackage = await GeoPackage.OpenAsync(gpkgPath, srid, onStatus: Console.WriteLine);
        
        // 2) Define schema and ensure layer exists
        var schema = new Dictionary<string, string>
        {
            ["name"] = "TEXT",
            ["age"] = "INTEGER",
            ["city"] = "TEXT",
            ["active"] = "INTEGER"
        };
        
        var layer = await geoPackage.EnsureLayerAsync("fluent_points", schema, srid);
        
        // 3) Prepare sample data
        var people = new List<FeatureRecord>
        {
            new FeatureRecord(
                new Point(600000, 6500000),
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["name"] = "Emma",
                    ["age"] = "28",
                    ["city"] = "Uppsala", 
                    ["active"] = "1"
                }),
            new FeatureRecord(
                new Point(600100, 6500100),
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["name"] = "David",
                    ["age"] = "42",
                    ["city"] = "V�ster�s",
                    ["active"] = "1"
                }),
            new FeatureRecord(
                new Point(600200, 6500200),
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["name"] = "Lisa",
                    ["age"] = "19",
                    ["city"] = "�rebro",
                    ["active"] = "0"
                }),
            new FeatureRecord(
                new Point(600300, 6500300), 
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["name"] = "Marcus",
                    ["age"] = "55",
                    ["city"] = "Link�ping",
                    ["active"] = "1"
                })
        };
        
        // 4) Bulk insert with progress reporting
        Console.WriteLine("Inserting features with progress...");
        var progress = new Progress<BulkProgress>(p =>
        {
            if (p.IsComplete)
                Console.WriteLine($"  ? Inserted {p.Total} features");
        });
        
        await layer.BulkInsertAsync(people, new BulkInsertOptions(BatchSize: 2), progress);
        
        // 5) Query with various options
        var totalCount = await layer.CountAsync();
        var activeCount = await layer.CountAsync("active = 1");
        Console.WriteLine($"Total people: {totalCount}, Active: {activeCount}");
        
        // 6) Read with filtering and ordering
        Console.WriteLine("Active people (sorted by age):");
        var readOptions = new ReadOptions(
            WhereClause: "active = 1", 
            OrderBy: "age ASC",
            IncludeGeometry: true
        );
        
        await foreach (var person in layer.ReadFeaturesAsync(readOptions))
        {
            var p = person.Geometry as Point;
            var name = person.Attributes["name"];
            var age = person.Attributes["age"];
            var city = person.Attributes["city"];
            Console.WriteLine($"  - {name}, {age} from {city} at ({p?.X:F0}, {p?.Y:F0})");
        }
        
        // 7) Demonstrate delete operation
        var deletedCount = await layer.DeleteAsync("active = 0");
        var remainingCount = await layer.CountAsync();
        Console.WriteLine($"Deleted {deletedCount} inactive people, {remainingCount} remaining");
        
        // 8) Get metadata
        var info = await geoPackage.GetInfoAsync();
        var layerInfo = info.Layers.First();
        Console.WriteLine($"Layer '{layerInfo.TableName}' has {layerInfo.AttributeColumns.Count} attribute columns");
        
        Console.WriteLine($"? Fluent API: Created {Path.GetFileName(gpkgPath)} with advanced features");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in Fluent API example: {ex.Message}");
    }
}

// --- Helper ---
static void TryDelete(string path)
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
}
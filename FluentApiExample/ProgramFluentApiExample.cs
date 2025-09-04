using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;
using System.Globalization;

// =============================================================
// Comprehensive Modern Fluent API Example
// =============================================================

const string gpkgPath = "modern_comprehensive.gpkg";
const int srid = 3006;

// Clean up
if (File.Exists(gpkgPath)) File.Delete(gpkgPath);

Console.WriteLine("Modern GeoPackage API Demo");
Console.WriteLine("===============================\n");

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
    var deleted = await citiesLayer.DeleteAsync("population < 10000");
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
        ("Stockholm", 674032, 6580383, 975551, 188.0, "Sweden"),
        ("Gothenburg", 529268, 6397848, 579281, 203.6, "Sweden"),
        ("Malmö", 373340, 6161503, 350963, 158.4, "Sweden"),
        ("Uppsala", 598624, 6654006, 230767, 48.8, "Sweden"),
        ("Västerås", 565077, 6631421, 127799, 48.2, "Sweden"),
        ("Örebro", 566848, 6519948, 126009, 58.2, "Sweden"),
        ("Linköping", 628983, 6484916, 165618, 56.6, "Sweden"),
        ("Helsingborg", 382988, 6222702, 149280, 38.4, "Sweden"),
        ("Jönköping", 599133, 6400906, 98659, 38.2, "Sweden"),
        ("Norrköping", 630483, 6467813, 95618, 45.8, "Sweden"),
        ("Lund", 375782, 6179652, 94703, 22.6, "Sweden"),
        ("Umeå", 723932, 7042944, 89607, 33.4, "Sweden"),
        ("Gävle", 608132, 6828584, 78331, 62.7, "Sweden"),
        ("Borås", 517298, 6374003, 72169, 40.2, "Sweden"),
        ("Eskilstuna", 578992, 6558644, 69948, 53.6, "Sweden"),
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
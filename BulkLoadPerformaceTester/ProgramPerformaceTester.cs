using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;

// Performance comparison between single inserts and bulk insert

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

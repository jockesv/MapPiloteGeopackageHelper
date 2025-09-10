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
using MapPiloteGeopackageHelper;           // <- This is the library namespace (our API lives here)
using NetTopologySuite.Geometries;         // <- Geometry types (Point etc.) come from NetTopologySuite

Console.WriteLine("=== MapPilote GeoPackage Helper Hello World ===");
Console.WriteLine("Using local MapPiloteGeopackageHelper library");
Console.WriteLine();

// =============================================================
// Hello World for MapPiloteGeopackageHelper
// -------------------------------------------------------------
// This minimal example shows how to:
//  1) Create a GeoPackage file
//  2) Create a spatial layer (table) with attribute columns
//  3) Insert features (points) with attributes
//  4) Bulk-insert many features efficiently
//  5) Read features back from the file
// Each step is small, explicit and well-commented so you can copy/paste.
// =============================================================

// 0) Choose a working directory and filenames
string workDir = Environment.CurrentDirectory;
string gpkgPath = Path.Combine(workDir, "hello_world.gpkg");
string layerName = "hello_points";
const int srid = 3006; // SWEREF99 TM (Sweden), included in the helper by default

// 1) Start fresh: delete any previous file to get repeatable runs
TryDelete(gpkgPath);

// 2) Create a new, empty GeoPackage database with required metadata
CMPGeopackageCreateHelper.CreateGeoPackage(gpkgPath, srid);

// 3) Define a simple table schema: four attribute columns + a geometry column (added automatically)
//    - id (autoincrement) is always added by the helper when creating a layer
//    - geom column (BLOB) is also added automatically and registered in gpkg tables
var headers = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["name"] = "TEXT",     // string text
    ["age"] = "INTEGER",   // integer (stored as number)
    ["height"] = "REAL",   // floating point number
    ["note"] = "TEXT"      // optional string (we'll use null/empty sometimes)
};

// 4) Create the spatial layer in the GeoPackage (POINT geometry type)
GeopackageLayerCreateHelper.CreateGeopackageLayer(
    gpkgPath,
    layerName,
    headers,
    geometryType: "POINT",
    srid: srid);

Console.WriteLine($"Created GeoPackage: {gpkgPath}");

// 5) Insert a single point with attributes
var singlePoint = new Point(500000, 6400000); // X/Y in SWEREF99 TM
var attributeOrder = new[] { "name", "age", "height", "note" };
var singleAttributes = new[] { "Alice", "30", "1.72", "hello" };
CGeopackageAddDataHelper.AddPointToGeoPackage(gpkgPath, layerName, singlePoint, singleAttributes);
Console.WriteLine("Inserted one point using the single-insert API.");

// 6) Prepare a few features using the standard FeatureRecord type
//    FeatureRecord takes an NTS Geometry and a string dictionary of attributes by column name
var bulkFeatures = new List<FeatureRecord>
{
    new FeatureRecord(
        Geometry: new Point(500100, 6400100),
        Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["name"] = "Bob",
            ["age"] = "41",
            ["height"] = "1.83",
            ["note"] = null
        }),
    new FeatureRecord(
        Geometry: new Point(500200, 6400200),
        Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["name"] = "Charlie",
            ["age"] = "25",
            ["height"] = "1.67",
            ["note"] = "demo"
        }),
};

// 7) Bulk insert for speed when writing many rows
CGeopackageAddDataHelper.BulkInsertFeatures(
    geoPackagePath: gpkgPath,
    layerName: layerName,
    features: bulkFeatures,
    srid: srid,
    batchSize: 500); // batch size may be tuned for your environment
Console.WriteLine($"Bulk inserted {bulkFeatures.Count} more points.");

// 8) Read features back as a stream; geometry is optional (we include it here)
var readBack = CMPGeopackageReadDataHelper.ReadFeatures(
    geoPackageFilePath: gpkgPath,
    tableName: layerName,
    geometryColumn: "geom",
    includeGeometry: true);

Console.WriteLine("\nRows in layer (first few shown):");
int shown = 0;
foreach (var feature in readBack)
{
    // Geometry is a NetTopologySuite Geometry; cast if you need X/Y on points
    var p = feature.Geometry as Point;
    var name = feature.Attributes.GetValueOrDefault("name");
    var age = feature.Attributes.GetValueOrDefault("age");
    var height = feature.Attributes.GetValueOrDefault("height");
    var note = feature.Attributes.GetValueOrDefault("note") ?? "<null>";

    Console.WriteLine($"- name={name}, age={age}, height={height}, note={note}, geom=({p?.X:F1},{p?.Y:F1})");

    if (++shown >= 5) break; // keep output short in Hello World
}

Console.WriteLine("\nSuccess! You created a GeoPackage, wrote points, and read them back.");
Console.WriteLine("You can now open the .gpkg in QGIS or other GIS tools.");

// --- Small helper ---
static void TryDelete(string path)
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
}

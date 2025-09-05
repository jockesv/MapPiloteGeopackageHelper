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
using Microsoft.Data.Sqlite;

// =============================================================
// Large Dataset Upload Example with Spatial Index Performance Test
// -------------------------------------------------------------
// This example demonstrates:
//  1) Generating 10,000 random points across Sweden (SWEREF99 TM)
//  2) Bulk loading with and without spatial indexes
//  3) Performance comparison for spatial queries
// =============================================================

const int POINT_COUNT = 10000;
const int SRID = 3006; // SWEREF99 TM (Sweden)
const double BUFFER_DISTANCE = 10000.0; // 10km buffer
const string LAYER_NAME = "air_pollution_points";

string workDir = Environment.CurrentDirectory;
string gpkgWithoutIndex = Path.Combine(workDir, "AirpolutionPointsWithoutSpatialIndex.gpkg");
string gpkgWithIndex = Path.Combine(workDir, "AirpolutionPointsWithSpatialIndex.gpkg");

Console.WriteLine("MapPilote Large Dataset Upload Example");
Console.WriteLine("======================================");
Console.WriteLine($"Generating {POINT_COUNT:N0} random points across Sweden...");
Console.WriteLine();

// Step 1: Create list with 10,000 random points across Sweden
var airPollutionData = await GenerateRandomAirPollutionDataAsync(POINT_COUNT);
Console.WriteLine($"✓ Generated {airPollutionData.Count:N0} air pollution data points");

// Clean up existing files
TryDelete(gpkgWithoutIndex);
TryDelete(gpkgWithIndex);

// Step 2: Bulk load WITHOUT spatial index
Console.WriteLine("\n2. Bulk loading WITHOUT spatial index...");
var stopwatch = Stopwatch.StartNew();
await BulkLoadDataAsync(gpkgWithoutIndex, airPollutionData, createSpatialIndex: false);
stopwatch.Stop();
Console.WriteLine($"   ✓ Bulk load completed in {stopwatch.ElapsedMilliseconds:N0} ms");

// Step 3: Bulk load WITH spatial index
Console.WriteLine("\n3. Bulk loading WITH spatial index...");
stopwatch.Restart();
await BulkLoadDataAsync(gpkgWithIndex, airPollutionData, createSpatialIndex: true);
stopwatch.Stop();
Console.WriteLine($"   ✓ Bulk load completed in {stopwatch.ElapsedMilliseconds:N0} ms");

// Step 4: Pick a random point and create buffer
var random = new Random(); // Different every time - uses current time as seed
var randomPoint = airPollutionData[random.Next(airPollutionData.Count)];
var bufferGeometry = randomPoint.Geometry!.Buffer(BUFFER_DISTANCE);

Console.WriteLine($"\n4. Selected random point for spatial query:");
var randomPointGeom = randomPoint.Geometry as Point;
Console.WriteLine($"   Point: ({randomPointGeom?.X:F0}, {randomPointGeom?.Y:F0})");
Console.WriteLine($"   Name: {randomPoint.Attributes["name"]}");
Console.WriteLine($"   Buffer: {BUFFER_DISTANCE:N0}m radius");

// Step 5: Query WITHOUT spatial index
Console.WriteLine("\n5. Querying points within buffer WITHOUT spatial index...");
var resultsWithoutIndex = await QueryPointsInBufferAsync(gpkgWithoutIndex, bufferGeometry);
Console.WriteLine($"   ✓ Found {resultsWithoutIndex.Count:N0} points in {resultsWithoutIndex.QueryTime:N0} ms");

// Step 6: Query WITH spatial index
Console.WriteLine("\n6. Querying points within buffer WITH spatial index...");
var resultsWithIndex = await QueryPointsInBufferAsync(gpkgWithIndex, bufferGeometry);
Console.WriteLine($"   ✓ Found {resultsWithIndex.Count:N0} points in {resultsWithIndex.QueryTime:N0} ms");

// Step 7: Evaluate timing results
Console.WriteLine("\n=== PERFORMANCE COMPARISON ===");
Console.WriteLine($"Points found without index: {resultsWithoutIndex.Count:N0}");
Console.WriteLine($"Points found with index:    {resultsWithIndex.Count:N0}");
Console.WriteLine($"Query time without index:   {resultsWithoutIndex.QueryTime:N0} ms");
Console.WriteLine($"Query time with index:      {resultsWithIndex.QueryTime:N0} ms");

if (resultsWithoutIndex.QueryTime > 0)
{
    var speedup = (double)resultsWithoutIndex.QueryTime / resultsWithIndex.QueryTime;
    Console.WriteLine($"Performance improvement:    {speedup:F1}x faster with spatial index");
}

Console.WriteLine($"\nResult validation: {(resultsWithoutIndex.Count == resultsWithIndex.Count ? "✓ PASSED" : "✗ FAILED")}");
Console.WriteLine("\nFiles created:");
Console.WriteLine($"  - {Path.GetFileName(gpkgWithoutIndex)}");
Console.WriteLine($"  - {Path.GetFileName(gpkgWithIndex)}");
Console.WriteLine("\nYou can open these .gpkg files in QGIS to visualize the data!");

// =============================================================
// Helper Methods
// =============================================================

static async Task<List<FeatureRecord>> GenerateRandomAirPollutionDataAsync(int count)
{
    var random = new Random(); // Different every time - uses current time as seed
    var features = new List<FeatureRecord>();
    
    // Read the actual Swedish boundary from sverige.gpkg
    string sverigeGpkgPath = Path.Combine(Environment.CurrentDirectory, "data", "sverige.gpkg");
    if (!File.Exists(sverigeGpkgPath))
    {
        throw new FileNotFoundException($"Swedish boundary file not found: {sverigeGpkgPath}");
    }
    
    Console.WriteLine("   Loading Swedish boundary geometry...");
    
    // Read the Swedish boundary geometry
    Geometry? swedenGeometry = null;
    var swedenFeatures = CMPGeopackageReadDataHelper.ExecuteSpatialQuery(sverigeGpkgPath, GetFirstTableName(sverigeGpkgPath));
    swedenGeometry = swedenFeatures.FirstOrDefault()?.Geometry;
    
    if (swedenGeometry == null)
    {
        throw new InvalidOperationException("Could not load Swedish boundary geometry from sverige.gpkg");
    }
    
    Console.WriteLine($"   ✓ Loaded Swedish boundary (geometry type: {swedenGeometry.GeometryType})");
    
    // Get the envelope for initial random point generation
    var envelope = swedenGeometry.EnvelopeInternal;
    Console.WriteLine($"   Generating {count:N0} points within Swedish territory...");
    
    var cities = new[] { "Stockholm", "Göteborg", "Malmö", "Uppsala", "Västerås", "Örebro", "Linköping", "Helsingborg", "Jönköping", "Norrköping" };
    
    await Task.Run(() =>
    {
        int generated = 0;
        int attempts = 0;
        int maxAttempts = count * 10; // Prevent infinite loops - this is now calculated inside the lambda
        
        while (generated < count && attempts < maxAttempts)
        {
            attempts++;
            
            // Generate random coordinates within Sweden's bounding box
            double x = envelope.MinX + random.NextDouble() * (envelope.MaxX - envelope.MinX);
            double y = envelope.MinY + random.NextDouble() * (envelope.MaxY - envelope.MinY);
            var candidatePoint = new Point(x, y);
            
            // Check if the point is actually within Swedish territory
            if (swedenGeometry.Contains(candidatePoint) || swedenGeometry.Intersects(candidatePoint))
            {
                // Generate attributes
                var attributes = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["rowid"] = (generated + 1).ToString(),
                    ["name"] = $"Station_{generated + 1:D5}_{cities[random.Next(cities.Length)]}",
                    ["airpolutionLevel"] = random.Next(1, 151).ToString() // 1-150 pollution level randomly
                };
                
                features.Add(new FeatureRecord(candidatePoint, attributes));
                generated++;
                
                // Progress indicator for large datasets
                if (generated % 1000 == 0)
                {
                    Console.WriteLine($"   Generated {generated:N0}/{count:N0} points within Swedish boundary...");
                }
            }
        }
        
        if (generated < count)
        {
            Console.WriteLine($"   Warning: Only generated {generated:N0} of {count:N0} requested points after {attempts:N0} attempts");
        }
    });
    
    return features;
}

// Helper method to get the first table name from a GeoPackage
static string GetFirstTableName(string geoPackagePath)
{
    var info = CMPGeopackageReadDataHelper.GetGeopackageInfo(geoPackagePath);
    var firstLayer = info.Layers.FirstOrDefault();
    if (firstLayer == null)
    {
        throw new InvalidOperationException($"No layers found in {geoPackagePath}");
    }
    return firstLayer.TableName;
}

static async Task BulkLoadDataAsync(string geoPackagePath, List<FeatureRecord> features, bool createSpatialIndex)
{
    var schema = new Dictionary<string, string>
    {
        ["rowid"] = "INTEGER",
        ["name"] = "TEXT",
        ["airpolutionLevel"] = "INTEGER"
    };
    
    using var geoPackage = await GeoPackage.OpenAsync(geoPackagePath, SRID);
    var layer = await geoPackage.EnsureLayerAsync(LAYER_NAME, schema, SRID);
    
    var options = new BulkInsertOptions(
        BatchSize: 1000,
        CreateSpatialIndex: createSpatialIndex,
        Srid: SRID
    );
    
    var progress = new Progress<BulkProgress>(p =>
    {
        if (p.Processed % 2000 == 0 || p.IsComplete)
            Console.WriteLine($"   Progress: {p.Processed:N0}/{p.Total:N0} ({p.PercentComplete:F1}%)");
    });
    
    await layer.BulkInsertAsync(features, options, progress);
    
    // Create spatial index after bulk insert if requested
    if (createSpatialIndex)
    {
        Console.WriteLine("   Creating spatial index...");
        await layer.CreateSpatialIndexAsync();
    }
}

static async Task<(int Count, long QueryTime)> QueryPointsInBufferAsync(string geoPackagePath, Geometry bufferGeometry)
{
    var stopwatch = Stopwatch.StartNew();
    var results = new List<FeatureRecord>();
    
    // Option 1: Use the new simplified spatial query method
    var allFeatures = CMPGeopackageReadDataHelper.ExecuteSpatialQuery(geoPackagePath, LAYER_NAME);
    
    int candidateCount = 0;
    int actualCount = 0;
    
    foreach (var feature in allFeatures)
    {
        candidateCount++;
        
        if (feature.Geometry != null)
        {
            // Perform precise geometric test - no manual header stripping needed!
            if (bufferGeometry.Contains(feature.Geometry))
            {
                results.Add(feature);
                actualCount++;
            }
        }
    }
    
    stopwatch.Stop();
    Console.WriteLine($"   Examined {candidateCount:N0} candidate points, {actualCount:N0} within buffer");
    
    return (results.Count, stopwatch.ElapsedMilliseconds);
}

// Alternative: If you still need direct SQL access, use the geometry helper
static async Task<(int Count, long QueryTime)> QueryPointsInBufferDirectSqlAsync(string geoPackagePath, Geometry bufferGeometry)
{
    var stopwatch = Stopwatch.StartNew();
    var results = new List<FeatureRecord>();
    
    using var connection = new SqliteConnection($"Data Source={geoPackagePath}");
    await connection.OpenAsync();
    
    var sql = $"SELECT rowid, name, airpolutionLevel, geom FROM {LAYER_NAME} WHERE geom IS NOT NULL";
    using var command = new SqliteCommand(sql, connection);
    using var reader = await command.ExecuteReaderAsync();
    
    int candidateCount = 0;
    int actualCount = 0;
    
    while (await reader.ReadAsync())
    {
        candidateCount++;
        
        if (!reader.IsDBNull(3)) // geom column
        {
            var gpkgBlob = (byte[])reader.GetValue(3);
            // Use library method - one line, no manual header handling!
            var geometry = CMPGeopackageReadDataHelper.ReadGeometryFromGpkgBlob(gpkgBlob);
            
            if (geometry != null && bufferGeometry.Contains(geometry))
            {
                var attributes = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["rowid"] = reader.GetInt32(0).ToString(),
                    ["name"] = reader.GetString(1),
                    ["airpolutionLevel"] = reader.GetInt32(2).ToString()
                };
                
                results.Add(new FeatureRecord(geometry, attributes));
                actualCount++;
            }
        }
    }
    
    stopwatch.Stop();
    Console.WriteLine($"   Examined {candidateCount:N0} candidate points, {actualCount:N0} within buffer");
    
    return (results.Count, stopwatch.ElapsedMilliseconds);
}

static void TryDelete(string path)
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
}

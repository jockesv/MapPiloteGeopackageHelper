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
using NetTopologySuite.IO;

// =============================================================
// Large Dataset Upload Example with Spatial Index Performance Test
// -------------------------------------------------------------
// This example demonstrates:
//  1) Generating random points across Sweden (SWEREF99 TM) - OPTIMIZED for speed
//  2) Bulk loading with and without spatial indexes
//  3) Performance comparison for spatial queries
// =============================================================

const int POINT_COUNT = 100000; // Reduced for faster debugging - change back to 50000 for final runs
const int SRID = 3006; // SWEREF99 TM (Sweden)
const double BUFFER_DISTANCE = 200000.0;
const string LAYER_NAME_WITHOUT_INDEX = "air_pollution_points_withOutSpatialIndex";
const string LAYER_NAME_WITH_INDEX = "air_pollution_points_withSpatialIndex";

string workDir = Environment.CurrentDirectory;
string gpkgWithoutIndex = Path.Combine(workDir, "AirpolutionPointsWithoutSpatialIndex.gpkg");
string gpkgWithIndex = Path.Combine(workDir, "AirpolutionPointsWithSpatialIndex.gpkg");

Console.WriteLine("MapPilote Large Dataset Upload Example (OPTIMIZED)");
Console.WriteLine("==================================================");
Console.WriteLine($"Generating {POINT_COUNT:N0} random points across Sweden...");
Console.WriteLine();

// Step 1: Create list with random points across Sweden
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

// DEBUG: Show buffer envelope for debugging
var bufferEnvelope = bufferGeometry.EnvelopeInternal;
Console.WriteLine($"   Buffer envelope: [{bufferEnvelope.MinX:F0}, {bufferEnvelope.MinY:F0}] to [{bufferEnvelope.MaxX:F0}, {bufferEnvelope.MaxY:F0}]");
Console.WriteLine($"   Buffer area: {bufferGeometry.Area / 1000000:F0} km²");

// DEBUG: Show some sample points for comparison
Console.WriteLine($"   Sample points for comparison:");
for (int i = 0; i < Math.Min(5, airPollutionData.Count); i++)
{
    var samplePoint = airPollutionData[i].Geometry as Point;
    var distance = randomPointGeom?.Distance(samplePoint!) ?? 0;
    Console.WriteLine($"     Point {i+1}: ({samplePoint?.X:F0}, {samplePoint?.Y:F0}) - Distance: {distance:F0}m");
}

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
// Helper Methods - OPTIMIZED FOR PERFORMANCE
// =============================================================

static async Task<List<FeatureRecord>> GenerateRandomAirPollutionDataAsync(int count)
{
    var random = new Random(); // Different every time - uses current time as seed
    var features = new List<FeatureRecord>();

    // Read the actual Swedish boundary from SwedenBorder.gpkg
    string sverigeGpkgPath = Path.Combine(Environment.CurrentDirectory, "Data", "SwedenBorder.gpkg");
    if (!File.Exists(sverigeGpkgPath))
    {
        throw new FileNotFoundException($"Swedish boundary file not found: {sverigeGpkgPath}");
    }
    
    Console.WriteLine("   Loading Swedish boundary geometry...");
    
    // Read the Swedish boundary geometry
    Geometry? swedenGeometry = null;
    var swedenFeatures = CMPGeopackageReadDataHelper.ReadFeatures(sverigeGpkgPath, GetFirstTableName(sverigeGpkgPath));
    swedenGeometry = swedenFeatures.FirstOrDefault()?.Geometry;
    
    if (swedenGeometry == null)
    {
        throw new InvalidOperationException("Could not load Swedish boundary geometry from SwedenBorder.gpkg");
    }
    
    Console.WriteLine($"   ✓ Loaded Swedish boundary (geometry type: {swedenGeometry.GeometryType})");
    
    // Get the envelope for initial random point generation
    var envelope = swedenGeometry.EnvelopeInternal;
    Console.WriteLine($"   Generating {count:N0} points within Swedish territory...");
    Console.WriteLine($"     Using optimized two-stage filtering for faster generation...");
    
    var cities = new[] { "Stockholm", "Göteborg", "Malmö", "Uppsala", "Västerås", "Örebro", "Linköping", "Helsingborg", "Jönköping", "Norrköping" };
    
    await Task.Run(() =>
    {
        int generated = 0;
        int attempts = 0;
        int envelopeRejects = 0;
        int maxAttempts = count * 10; // Prevent infinite loops
        
        while (generated < count && attempts < maxAttempts)
        {
            attempts++;
            
            // Generate random coordinates within Sweden's bounding box
            double x = envelope.MinX + random.NextDouble() * (envelope.MaxX - envelope.MinX);
            double y = envelope.MinY + random.NextDouble() * (envelope.MaxY - envelope.MinY);
            var candidatePoint = new Point(x, y);
            
            // OPTIMIZATION 1: Fast envelope check first (much faster than full geometry test)
            if (!swedenGeometry.EnvelopeInternal.Contains(candidatePoint.X, candidatePoint.Y))
            {
                envelopeRejects++;
                continue; // Point is outside Sweden's bounding box, skip expensive geometry test
            }
            
            // OPTIMIZATION 2: Only do expensive geometry test for points within bounding box
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
                
                // Progress indicator for large datasets - more frequent updates
                if (generated % 500 == 0 && generated > 0)
                {
                    var hitRate = (double)generated / attempts * 100;
                    Console.WriteLine($"   Generated {generated:N0}/{count:N0} points ({hitRate:F1}% hit rate, {envelopeRejects:N0} envelope rejects)");
                }
            }
        }
        
        if (generated < count)
        {
            Console.WriteLine($"   Warning: Only generated {generated:N0} of {count:N0} requested points after {attempts:N0} attempts");
        }
        else
        {
            var finalHitRate = (double)generated / attempts * 100;
            Console.WriteLine($"   ✓ Generation complete! Hit rate: {finalHitRate:F1}% ({envelopeRejects:N0} envelope rejects saved expensive geometry tests)");
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
    
    // Use different layer names based on whether spatial index is created
    var layerName = createSpatialIndex ? LAYER_NAME_WITH_INDEX : LAYER_NAME_WITHOUT_INDEX;
    
    using var geoPackage = await GeoPackage.OpenAsync(geoPackagePath, SRID);
    var layer = await geoPackage.EnsureLayerAsync(layerName, schema, SRID);
    
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
    
    // Create QGIS-compatible spatial index manually
    if (createSpatialIndex)
    {
        Console.WriteLine("   Creating QGIS-compatible spatial index...");
        using var connection = new SqliteConnection($"Data Source={geoPackagePath}");
        await connection.OpenAsync();
        
        await CreateGeoPackageSpatialIndexAsync(connection, layerName);
    }
}

static async Task CreateGeoPackageSpatialIndexAsync(SqliteConnection connection, string layerName)
{
    try
    {
        // 1. Create GeoPackage extensions table if it doesn't exist
        var createExtensionsSql = @"
            CREATE TABLE IF NOT EXISTS gpkg_extensions (
                table_name TEXT,
                column_name TEXT,
                extension_name TEXT NOT NULL,
                definition TEXT NOT NULL,
                scope TEXT NOT NULL,
                CONSTRAINT ge_tce UNIQUE (table_name, column_name, extension_name)
            )";
        using var createExtCmd = new SqliteCommand(createExtensionsSql, connection);
        await createExtCmd.ExecuteNonQueryAsync();
        
        // 2. Create the RTree spatial index with GeoPackage naming convention
        var createIndexSql = $@"
            CREATE VIRTUAL TABLE rtree_{layerName}_geom USING rtree(
                id,
                minx, maxx,
                miny, maxy
            )";
        using var createCmd = new SqliteCommand(createIndexSql, connection);
        await createCmd.ExecuteNonQueryAsync();
        
        // 3. Register the spatial index extension
        var registerExtensionSql = @"
            INSERT OR REPLACE INTO gpkg_extensions 
            (table_name, column_name, extension_name, definition, scope)
            VALUES (@table_name, @column_name, 'gpkg_rtree_index', 'GeoPackage 1.0 Specification Annex L', 'write-only')";
        
        using var registerCmd = new SqliteCommand(registerExtensionSql, connection);
        registerCmd.Parameters.AddWithValue("@table_name", layerName);
        registerCmd.Parameters.AddWithValue("@column_name", "geom");
        await registerCmd.ExecuteNonQueryAsync();
        
        // 4. Populate the spatial index
        Console.WriteLine("   Populating spatial index...");
        
        // First, count total records to enable progress reporting
        var countSql = $"SELECT COUNT(*) FROM {layerName} WHERE geom IS NOT NULL";
        using var countCmd = new SqliteCommand(countSql, connection);
        var totalRecords = (long)(await countCmd.ExecuteScalarAsync() ?? 0);
        
        var selectSql = $"SELECT rowid, geom FROM {layerName} WHERE geom IS NOT NULL";
        using var selectCmd = new SqliteCommand(selectSql, connection);
        using var dataReader = await selectCmd.ExecuteReaderAsync();
        
        var insertSql = $"INSERT INTO rtree_{layerName}_geom (id, minx, maxx, miny, maxy) VALUES (@id, @minx, @maxx, @miny, @maxy)";
        using var insertCmd = new SqliteCommand(insertSql, connection);
        
        insertCmd.Parameters.Add("@id", SqliteType.Integer);
        insertCmd.Parameters.Add("@minx", SqliteType.Real);
        insertCmd.Parameters.Add("@maxx", SqliteType.Real);
        insertCmd.Parameters.Add("@miny", SqliteType.Real);
        insertCmd.Parameters.Add("@maxy", SqliteType.Real);
        
        int indexedCount = 0;
        int processedCount = 0;
        var progressStopwatch = Stopwatch.StartNew();
        var wkbReader = new WKBReader();
        
        while (await dataReader.ReadAsync())
        {
            processedCount++;
            var rowid = dataReader.GetInt32(0);
            var gpkgBlob = (byte[])dataReader.GetValue(1);
            
            try
            {
                Geometry? geometry = null;
                try
                {
                    // Skip the GeoPackage header and read the WKB part
                    if (gpkgBlob.Length > 8) // GeoPackage header is at least 8 bytes
                    {
                        // Find WKB start - typically after the header
                        var wkbStart = 8; // Standard GeoPackage header size
                        var wkbData = new byte[gpkgBlob.Length - wkbStart];
                        Array.Copy(gpkgBlob, wkbStart, wkbData, 0, wkbData.Length);
                        geometry = wkbReader.Read(wkbData);
                    }
                }
                catch
                {
                    // Skip invalid geometries
                }
                
                if (geometry != null)
                {
                    var env = geometry.EnvelopeInternal;
                    insertCmd.Parameters["@id"].Value = rowid;
                    insertCmd.Parameters["@minx"].Value = env.MinX;
                    insertCmd.Parameters["@maxx"].Value = env.MaxX;
                    insertCmd.Parameters["@miny"].Value = env.MinY;
                    insertCmd.Parameters["@maxy"].Value = env.MaxY;
                    await insertCmd.ExecuteNonQueryAsync();
                    indexedCount++;
                }
            }
            catch
            {
                // Skip invalid geometries
            }
            
            // Progress reporting - show progress every 1000 records or every 2 seconds
            if (processedCount % 1000 == 0 || progressStopwatch.ElapsedMilliseconds > 2000)
            {
                var percentage = totalRecords > 0 ? (double)processedCount / totalRecords * 100 : 0;
                var rate = progressStopwatch.ElapsedMilliseconds > 0 ? processedCount * 1000.0 / progressStopwatch.ElapsedMilliseconds : 0;
                
                Console.WriteLine($"     Spatial index progress: {processedCount:N0}/{totalRecords:N0} ({percentage:F1}%) - {rate:F0} records/sec");
                progressStopwatch.Restart();
            }
        }
        
        Console.WriteLine($"   ✓ Spatial index created with {indexedCount:N0} entries ({processedCount - indexedCount} invalid geometries skipped)");
        Console.WriteLine("   ✓ Registered GeoPackage RTree extension");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   ⚠ Error creating spatial index: {ex.Message}");
    }
}

static async Task<(int Count, long QueryTime)> QueryPointsInBufferAsync(string geoPackagePath, Geometry bufferGeometry)
{
    var stopwatch = Stopwatch.StartNew();
    var results = new List<FeatureRecord>();
    
    using var connection = new SqliteConnection($"Data Source={geoPackagePath}");
    await connection.OpenAsync();
    
    // Get the buffer's bounding box for spatial index optimization
    var envelope = bufferGeometry.EnvelopeInternal;
    
    // Determine which layer exists and whether it has a spatial index
    var layerNameWithIndex = LAYER_NAME_WITH_INDEX;
    var layerNameWithoutIndex = LAYER_NAME_WITHOUT_INDEX;
    
    // Check if spatial index layer exists
    var checkIndexedLayerSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{layerNameWithIndex}'";
    using var checkIndexedCmd = new SqliteCommand(checkIndexedLayerSql, connection);
    var indexedLayerExists = await checkIndexedCmd.ExecuteScalarAsync() != null;
    
    // Check if non-indexed layer exists
    var checkNonIndexedLayerSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{layerNameWithoutIndex}'";
    using var checkNonIndexedCmd = new SqliteCommand(checkNonIndexedLayerSql, connection);
    var nonIndexedLayerExists = await checkNonIndexedCmd.ExecuteScalarAsync() != null;
    
    string layerName;
    bool hasSpatialIndex = false;
    
    if (indexedLayerExists)
    {
        layerName = layerNameWithIndex;
        // Check if this layer actually has a spatial index
        var checkIndexSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='rtree_{layerNameWithIndex}_geom'";
        using var checkCmd = new SqliteCommand(checkIndexSql, connection);
        hasSpatialIndex = await checkCmd.ExecuteScalarAsync() != null;
    }
    else if (nonIndexedLayerExists)
    {
        layerName = layerNameWithoutIndex;
        hasSpatialIndex = false;
    }
    else
    {
        throw new InvalidOperationException("No air pollution layer found in the GeoPackage");
    }
    
    string sql;
    if (hasSpatialIndex)
    {
        // More efficient approach: Use the spatial index to get IDs first, then fetch features
        sql = $@"
            SELECT f.rowid, f.name, f.airpolutionLevel, f.geom 
            FROM {layerName} f
            WHERE f.rowid IN (
                SELECT r.id FROM rtree_{layerName}_geom r
                WHERE r.minx <= {envelope.MaxX.ToString(System.Globalization.CultureInfo.InvariantCulture)} 
                AND r.maxx >= {envelope.MinX.ToString(System.Globalization.CultureInfo.InvariantCulture)}
                AND r.miny <= {envelope.MaxY.ToString(System.Globalization.CultureInfo.InvariantCulture)} 
                AND r.maxy >= {envelope.MinY.ToString(System.Globalization.CultureInfo.InvariantCulture)}
            )
            AND f.geom IS NOT NULL";
        Console.WriteLine($"   Using spatial index (R*Tree) for fast filtering on layer '{layerName}'...");
        Console.WriteLine($"   DEBUG: R*Tree query envelope: [{envelope.MinX:F0}, {envelope.MinY:F0}] to [{envelope.MaxX:F0}, {envelope.MaxY:F0}]");
    }
    else
    {
        // Fallback: full table scan when no spatial index
        sql = $@"
            SELECT rowid, name, airpolutionLevel, geom 
            FROM {layerName} 
            WHERE geom IS NOT NULL";
        Console.WriteLine($"   Using full table scan (no spatial index available) on layer '{layerName}'...");
        Console.WriteLine($"   DEBUG: Full table scan - should examine all {POINT_COUNT} points");
    }
    
    using var command = new SqliteCommand(sql, connection);
    using var queryReader = await command.ExecuteReaderAsync();
    
    int candidateCount = 0;
    int actualCount = 0;
    int geometryParseErrors = 0;
    var sampleDistances = new List<double>();
    var wkbReader = new WKBReader();
    
    while (await queryReader.ReadAsync())
    {
        candidateCount++;
        
        if (!queryReader.IsDBNull(3)) // geom column
        {
            var gpkgBlob = (byte[])queryReader.GetValue(3);
            try
            {
                Geometry? geometry = null;
                try
                {
                    // Skip the GeoPackage header and read the WKB part
                    if (gpkgBlob.Length > 8) // GeoPackage header is at least 8 bytes
                    {
                        // Find WKB start - typically after the header
                        var wkbStart = 8; // Standard GeoPackage header size
                        var wkbData = new byte[gpkgBlob.Length - wkbStart];
                        Array.Copy(gpkgBlob, wkbStart, wkbData, 0, wkbData.Length);
                        geometry = wkbReader.Read(wkbData);
                    }
                }
                catch
                {
                    // Skip invalid geometries
                }
                
                if (geometry != null)
                {
                    // Calculate distance for debugging
                    var distance = bufferGeometry.Centroid.Distance(geometry);
                    if (sampleDistances.Count < 10) // Keep first 10 distances for debugging
                    {
                        sampleDistances.Add(distance);
                    }
                    
                    if (bufferGeometry.Contains(geometry))
                    {
                        var attributes = new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            ["rowid"] = queryReader.GetInt32(0).ToString(),
                            ["name"] = queryReader.GetString(1),
                            ["airpolutionLevel"] = queryReader.GetInt32(2).ToString()
                        };
                        
                        results.Add(new FeatureRecord(geometry, attributes));
                        actualCount++;
                        
                        // Debug: Show details of points found within buffer
                        if (actualCount <= 5) // Show first 5 matches
                        {
                            var point = geometry as Point;
                            Console.WriteLine($"     DEBUG: Found point {actualCount}: ({point?.X:F0}, {point?.Y:F0}) - Distance: {distance:F0}m");
                        }
                    }
                }
            }
            catch
            {
                geometryParseErrors++;
            }
        }
    }
    
    stopwatch.Stop();
    Console.WriteLine($"   Examined {candidateCount:N0} candidate points, {actualCount:N0} within buffer");
    Console.WriteLine($"   DEBUG: Geometry parse errors: {geometryParseErrors}");
    if (sampleDistances.Count > 0)
    {
        var distanceStrings = sampleDistances.Select(d => $"{d:F0}m").ToArray();
        Console.WriteLine($"   DEBUG: Sample distances (first {sampleDistances.Count}): {string.Join(", ", distanceStrings)}");
        Console.WriteLine($"   DEBUG: Min distance: {sampleDistances.Min():F0}m, Max distance: {sampleDistances.Max():F0}m");
    }
    
    return (results.Count, stopwatch.ElapsedMilliseconds);
}

static void TryDelete(string path)
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
}
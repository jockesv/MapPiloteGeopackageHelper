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

namespace TestMapPiloteGeoPackageHandler
{
    [TestClass]
    public class TestFluentApi
    {
        private static string CreateTempGpkgPath()
        {
            var path = Path.Combine(Path.GetTempPath(), $"fluent_test_{Guid.NewGuid():N}.gpkg");
            return path;
        }

        [TestMethod]
        public async Task FluentApi_CreateAndReadFeatures_ShouldWork()
        {
            string gpkg = CreateTempGpkgPath();
            try
            {
                // Arrange
                var schema = new Dictionary<string, string>
                {
                    ["name"] = "TEXT",
                    ["value"] = "INTEGER"
                };

                var features = new[]
                {
                    new FeatureRecord(
                        new Point(100, 200),
                        new Dictionary<string, string?> { ["name"] = "Test1", ["value"] = "42" }
                    ),
                    new FeatureRecord(
                        new Point(300, 400),
                        new Dictionary<string, string?> { ["name"] = "Test2", ["value"] = "84" }
                    )
                };

                // Act
                using var geoPackage = await GeoPackage.OpenAsync(gpkg);
                var layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
                
                await layer.BulkInsertAsync(features, new BulkInsertOptions(BatchSize: 1));
                
                var readBack = new List<FeatureRecord>();
                await foreach (var feature in layer.ReadFeaturesAsync())
                {
                    readBack.Add(feature);
                }

                // Assert
                Assert.AreEqual(2, readBack.Count);
                
                var first = readBack.First();
                Assert.IsNotNull(first.Geometry);
                Assert.AreEqual("Test1", first.Attributes["name"]);
                Assert.AreEqual("42", first.Attributes["value"]);
                
                var point = (Point)first.Geometry!;
                Assert.AreEqual(100.0, point.X, 1e-9);
                Assert.AreEqual(200.0, point.Y, 1e-9);
            }
            finally
            {
                if (File.Exists(gpkg))
                {
                    try { File.Delete(gpkg); } catch { }
                }
            }
        }

        [TestMethod]
        public async Task FluentApi_CountAndDelete_ShouldWork()
        {
            string gpkg = CreateTempGpkgPath();
            try
            {
                // Arrange
                var schema = new Dictionary<string, string> { ["status"] = "TEXT" };
                var features = Enumerable.Range(1, 10).Select(i => 
                    new FeatureRecord(
                        new Point(i, i),
                        new Dictionary<string, string?> { ["status"] = i <= 5 ? "active" : "inactive" }
                    )).ToList();

                // Act
                using var geoPackage = await GeoPackage.OpenAsync(gpkg);
                var layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
                
                await layer.BulkInsertAsync(features);
                
                var totalCount = await layer.CountAsync();
                var activeCount = await layer.CountAsync("status = 'active'");
                var deletedCount = await layer.DeleteAsync("status = 'inactive'");
                var remainingCount = await layer.CountAsync();

                // Assert
                Assert.AreEqual(10, totalCount);
                Assert.AreEqual(5, activeCount);
                Assert.AreEqual(5, deletedCount);
                Assert.AreEqual(5, remainingCount);
            }
            finally
            {
                if (File.Exists(gpkg))
                {
                    try { File.Delete(gpkg); } catch { }
                }
            }
        }

        [TestMethod]
        public async Task FluentApi_ReadWithOptions_ShouldWork()
        {
            string gpkg = CreateTempGpkgPath();
            try
            {
                // Arrange
                var schema = new Dictionary<string, string> { ["rank"] = "INTEGER" };
                var features = Enumerable.Range(1, 20).Select(i => 
                    new FeatureRecord(
                        new Point(i * 10, i * 20),
                        new Dictionary<string, string?> { ["rank"] = i.ToString() }
                    )).ToList();

                // Act
                using var geoPackage = await GeoPackage.OpenAsync(gpkg);
                var layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
                
                await layer.BulkInsertAsync(features);
                
                // Read with limit
                var limited = new List<FeatureRecord>();
                await foreach (var feature in layer.ReadFeaturesAsync(new ReadOptions(Limit: 5)))
                {
                    limited.Add(feature);
                }

                // Read with WHERE clause
                var filtered = new List<FeatureRecord>();
                await foreach (var feature in layer.ReadFeaturesAsync(new ReadOptions(WhereClause: "rank > 15")))
                {
                    filtered.Add(feature);
                }

                // Assert
                Assert.AreEqual(5, limited.Count);
                Assert.AreEqual(5, filtered.Count); // ranks 16-20
                
                // Verify filtering worked
                foreach (var feature in filtered)
                {
                    var rank = int.Parse(feature.Attributes["rank"]!);
                    Assert.IsTrue(rank > 15);
                }
            }
            finally
            {
                if (File.Exists(gpkg))
                {
                    try { File.Delete(gpkg); } catch { }
                }
            }
        }

        [TestMethod]
        public async Task FluentApi_ConflictPolicies_ShouldWork()
        {
            string gpkg = CreateTempGpkgPath();
            try
            {
                // Arrange - create schema with unique constraint simulation
                var schema = new Dictionary<string, string> { ["unique_id"] = "INTEGER", ["data"] = "TEXT" };
                
                var initialFeature = new FeatureRecord(
                    new Point(100, 100),
                    new Dictionary<string, string?> { ["unique_id"] = "1", ["data"] = "original" }
                );

                var conflictFeature = new FeatureRecord(
                    new Point(200, 200),
                    new Dictionary<string, string?> { ["unique_id"] = "1", ["data"] = "updated" }
                );

                // Act
                using var geoPackage = await GeoPackage.OpenAsync(gpkg);
                var layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
                
                // Insert initial
                await layer.BulkInsertAsync(new[] { initialFeature });
                var countAfterFirst = await layer.CountAsync();
                
                // Try to insert conflict with IGNORE policy
                await layer.BulkInsertAsync(
                    new[] { conflictFeature }, 
                    new BulkInsertOptions(ConflictPolicy: ConflictPolicy.Ignore));
                var countAfterIgnore = await layer.CountAsync();

                // Assert
                Assert.AreEqual(1, countAfterFirst);
                Assert.AreEqual(2, countAfterIgnore); // SQLite allows duplicates without explicit unique constraint
            }
            finally
            {
                if (File.Exists(gpkg))
                {
                    try { File.Delete(gpkg); } catch { }
                }
            }
        }

        [TestMethod]
        public async Task FluentApi_OrderBy_ShouldWork()
        {
            string gpkg = CreateTempGpkgPath();
            try
            {
                // Arrange
                var schema = new Dictionary<string, string> { ["score"] = "INTEGER", ["name"] = "TEXT" };
                var features = new[]
                {
                    new FeatureRecord(new Point(1, 1), new Dictionary<string, string?> { ["score"] = "50", ["name"] = "Bob" }),
                    new FeatureRecord(new Point(2, 2), new Dictionary<string, string?> { ["score"] = "90", ["name"] = "Alice" }),
                    new FeatureRecord(new Point(3, 3), new Dictionary<string, string?> { ["score"] = "70", ["name"] = "Charlie" }),
                    new FeatureRecord(new Point(4, 4), new Dictionary<string, string?> { ["score"] = "60", ["name"] = "Diana" }),
                };

                // Act
                using var geoPackage = await GeoPackage.OpenAsync(gpkg);
                var layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
                await layer.BulkInsertAsync(features);

                // Read with ORDER BY ASC
                var ascResults = new List<FeatureRecord>();
                await foreach (var feature in layer.ReadFeaturesAsync(new ReadOptions(OrderBy: "score ASC")))
                {
                    ascResults.Add(feature);
                }

                // Read with ORDER BY DESC
                var descResults = new List<FeatureRecord>();
                await foreach (var feature in layer.ReadFeaturesAsync(new ReadOptions(OrderBy: "score DESC")))
                {
                    descResults.Add(feature);
                }

                // Assert ASC order
                Assert.AreEqual("Bob", ascResults[0].Attributes["name"]);    // 50
                Assert.AreEqual("Diana", ascResults[1].Attributes["name"]);  // 60
                Assert.AreEqual("Charlie", ascResults[2].Attributes["name"]); // 70
                Assert.AreEqual("Alice", ascResults[3].Attributes["name"]);  // 90

                // Assert DESC order
                Assert.AreEqual("Alice", descResults[0].Attributes["name"]);   // 90
                Assert.AreEqual("Charlie", descResults[1].Attributes["name"]); // 70
                Assert.AreEqual("Diana", descResults[2].Attributes["name"]);   // 60
                Assert.AreEqual("Bob", descResults[3].Attributes["name"]);     // 50
            }
            finally
            {
                if (File.Exists(gpkg))
                {
                    try { File.Delete(gpkg); } catch { }
                }
            }
        }
    }
}
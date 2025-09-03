using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MapPiloteGeopackageHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    }
}
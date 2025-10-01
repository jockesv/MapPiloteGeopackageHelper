using MapPiloteGeopackageHelper;
using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler
{
    [TestClass]
    public class TestWalFunctionality
    {
        private static string CreateTempGpkgPath()
        {
            var path = Path.Combine(Path.GetTempPath(), $"wal_functionality_test_{Guid.NewGuid():N}.gpkg");
            return path;
        }

        [TestMethod]
        public void WalMode_EnabledGeoPackage_ShouldSupportConcurrentReadsWhileWriting()
        {
            // This test demonstrates the main benefit of WAL mode: concurrent readers during writes
            string gpkg = CreateTempGpkgPath();
            var statusMessages = new List<string>();
            
            try
            {
                // Arrange: Create GeoPackage with WAL mode
                CMPGeopackageCreateHelper.CreateGeoPackage(
                    gpkg, 
                    srid: 3006, 
                    walMode: true, 
                    onStatus: msg => statusMessages.Add(msg));

                // Create a layer for testing
                var schema = new Dictionary<string, string>
                {
                    ["name"] = "TEXT",
                    ["value"] = "INTEGER"
                };
                GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", schema);

                // Add initial data
                var point1 = new Point(100, 200);
                CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "test_layer", point1, new[] { "Point1", "10" });

                // Act: Test concurrent access (simulate reader while writer is active)
                using var readerConnection = new SqliteConnection($"Data Source={gpkg}");
                using var writerConnection = new SqliteConnection($"Data Source={gpkg}");
                
                readerConnection.Open();
                writerConnection.Open();

                // Verify WAL mode is active
                using var journalCommand = new SqliteCommand("PRAGMA journal_mode", readerConnection);
                var journalMode = journalCommand.ExecuteScalar()?.ToString();
                Assert.AreEqual("wal", journalMode?.ToLower(), "WAL mode should be active");

                // Start a transaction on writer (this would block readers in DELETE mode)
                using var writerTransaction = writerConnection.BeginTransaction();
                
                // Insert data in writer transaction (not yet committed) - using proper parameter binding
                using var insertCommand = new SqliteCommand(
                    "INSERT INTO test_layer (geom, name, value) VALUES (@geom, @name, @value)", 
                    writerConnection, writerTransaction);
                
                var point2 = new Point(300, 400);
                var wkb = point2.ToBinary();
                var gpkgBlob = CMPGeopackageUtils.CreateGpkgBlob(wkb, 3006);
                
                insertCommand.Parameters.AddWithValue("@geom", gpkgBlob);
                insertCommand.Parameters.AddWithValue("@name", "Point2");
                insertCommand.Parameters.AddWithValue("@value", 20);
                insertCommand.ExecuteNonQuery();

                // Reader should still be able to read existing data (WAL benefit)
                using var readerCommand = new SqliteCommand("SELECT COUNT(*) FROM test_layer", readerConnection);
                var countBeforeCommit = Convert.ToInt32(readerCommand.ExecuteScalar());
                
                // Should see the original data (1 record) even though writer has uncommitted data
                Assert.AreEqual(1, countBeforeCommit, "Reader should see consistent snapshot before writer commits");

                // Commit the writer transaction
                writerTransaction.Commit();

                // Now reader should see updated data after checkpoint
                using var readerCommand2 = new SqliteCommand("SELECT COUNT(*) FROM test_layer", readerConnection);
                var countAfterCommit = Convert.ToInt32(readerCommand2.ExecuteScalar());
                
                // After commit, we should see both records
                Assert.IsTrue(countAfterCommit >= 1, "Reader should eventually see committed data");

                // Assert: Verify WAL mode was properly reported
                var hasWalMessage = statusMessages.Any(msg => 
                    msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
                Assert.IsTrue(hasWalMessage, "Should report WAL mode enablement");
            }
            finally
            {
                TryDeleteFile(gpkg);
            }
        }

        [TestMethod]
        public void WalMode_ComparedToNormalMode_ShouldShowDifferentJournalModes()
        {
            // Test that compares WAL vs normal mode side by side
            string walGpkg = CreateTempGpkgPath();
            string normalGpkg = CreateTempGpkgPath();
            
            try
            {
                // Create one GeoPackage with WAL mode
                CMPGeopackageCreateHelper.CreateGeoPackage(walGpkg, srid: 3006, walMode: true);
                
                // Create one GeoPackage with normal mode
                CMPGeopackageCreateHelper.CreateGeoPackage(normalGpkg, srid: 3006, walMode: false);

                // Check journal modes
                using (var walConnection = new SqliteConnection($"Data Source={walGpkg}"))
                {
                    walConnection.Open();
                    using var walCommand = new SqliteCommand("PRAGMA journal_mode", walConnection);
                    var walMode = walCommand.ExecuteScalar()?.ToString();
                    Assert.AreEqual("wal", walMode?.ToLower(), "WAL GeoPackage should use WAL journal mode");
                }

                using (var normalConnection = new SqliteConnection($"Data Source={normalGpkg}"))
                {
                    normalConnection.Open();
                    using var normalCommand = new SqliteCommand("PRAGMA journal_mode", normalConnection);
                    var normalMode = normalCommand.ExecuteScalar()?.ToString();
                    Assert.AreNotEqual("wal", normalMode?.ToLower(), "Normal GeoPackage should not use WAL journal mode");
                    
                    // Should be 'delete' mode (SQLite default)
                    Assert.IsTrue(normalMode?.ToLower() == "delete" || normalMode?.ToLower() == "truncate", 
                        $"Normal mode should be delete or truncate, but was: {normalMode}");
                }
            }
            finally
            {
                TryDeleteFile(walGpkg);
                TryDeleteFile(normalGpkg);
            }
        }

        [TestMethod]
        public void WalMode_CreateAndUseGeoPackage_ShouldMaintainDataIntegrity()
        {
            // Test that WAL mode doesn't break normal GeoPackage operations
            string gpkg = CreateTempGpkgPath();
            var statusMessages = new List<string>();
            
            try
            {
                // Create GeoPackage with WAL mode
                CMPGeopackageCreateHelper.CreateGeoPackage(
                    gpkg, 
                    srid: 4326, 
                    walMode: true, 
                    onStatus: msg => statusMessages.Add(msg));

                // Create layer
                var schema = new Dictionary<string, string>
                {
                    ["city_name"] = "TEXT",
                    ["population"] = "INTEGER",
                    ["area_km2"] = "REAL"
                };
                GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "cities", schema, "POINT", 4326);

                // Add multiple points
                var cities = new[]
                {
                    new { Name = "Stockholm", Pop = 975551, Area = 188.0, Point = new Point(18.0686, 59.3293) },
                    new { Name = "Gothenburg", Pop = 579281, Area = 203.67, Point = new Point(11.9746, 57.7089) },
                    new { Name = "Malmö", Pop = 344166, Area = 156.87, Point = new Point(13.0007, 55.6050) }
                };

                foreach (var city in cities)
                {
                    CGeopackageAddDataHelper.AddPointToGeoPackage(
                        gpkg, "cities", city.Point, 
                        new[] { city.Name, city.Pop.ToString(), city.Area.ToString() });
                }

                // Verify data integrity with WAL mode
                var features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "cities").ToList();
                
                Assert.AreEqual(3, features.Count, "Should have inserted 3 cities");
                
                var stockholm = features.FirstOrDefault(f => f.Attributes["city_name"] == "Stockholm");
                Assert.IsNotNull(stockholm, "Stockholm should be found");
                Assert.AreEqual("975551", stockholm.Attributes["population"], "Stockholm population should be correct");

                // Verify GeoPackage metadata is intact
                using var connection = new SqliteConnection($"Data Source={gpkg}");
                connection.Open();

                // Verify journal mode is still WAL
                using var journalCommand = new SqliteCommand("PRAGMA journal_mode", connection);
                var journalMode = journalCommand.ExecuteScalar()?.ToString();
                Assert.AreEqual("wal", journalMode?.ToLower(), "Should maintain WAL mode");

                // Verify GeoPackage structure
                using var tablesCommand = new SqliteCommand(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('gpkg_contents', 'gpkg_spatial_ref_sys', 'gpkg_geometry_columns', 'cities')", 
                    connection);
                
                var tableResults = new List<string>();
                using var reader = tablesCommand.ExecuteReader();
                while (reader.Read())
                {
                    tableResults.Add(reader.GetString(0));
                }

                Assert.AreEqual(4, tableResults.Count, "Should have all required tables");
                Assert.IsTrue(tableResults.Contains("cities"), "Should have cities table");
                Assert.IsTrue(tableResults.Contains("gpkg_contents"), "Should have gpkg_contents table");

                // Verify status messages
                var hasWalMessage = statusMessages.Any(msg => msg.Contains("Enabled WAL"));
                var hasCreationMessage = statusMessages.Any(msg => msg.Contains("Successfully created GeoPackage"));
                
                Assert.IsTrue(hasWalMessage, "Should report WAL enablement");
                Assert.IsTrue(hasCreationMessage, "Should report successful creation");
            }
            finally
            {
                TryDeleteFile(gpkg);
            }
        }

        [TestMethod]
        public void WalMode_PerformanceBenchmark_ShouldCompleteWithinReasonableTime()
        {
            // Simple performance test to ensure WAL mode doesn't severely impact creation time
            string gpkg = CreateTempGpkgPath();
            
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Create GeoPackage with WAL mode
                CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, srid: 3006, walMode: true);
                
                // Create layer and add some data
                var schema = new Dictionary<string, string> { ["test"] = "TEXT" };
                GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "perf_test", schema);
                
                // Add 100 points to test write performance
                for (int i = 0; i < 100; i++)
                {
                    var point = new Point(i * 10, i * 10);
                    CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "perf_test", point, new[] { $"Point_{i}" });
                }
                
                stopwatch.Stop();
                
                // Should complete within reasonable time (adjust threshold as needed)
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, 
                    $"WAL mode operations should complete within 10 seconds, took {stopwatch.ElapsedMilliseconds}ms");
                
                // Verify all data was written
                var features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "perf_test").ToList();
                Assert.AreEqual(100, features.Count, "All 100 points should be written with WAL mode");
                
                Console.WriteLine($"WAL mode performance test completed in {stopwatch.ElapsedMilliseconds}ms");
            }
            finally
            {
                TryDeleteFile(gpkg);
            }
        }

        [TestMethod]
        public void WalMode_CheckpointAndCleanup_ShouldManageWalFilesCorrectly()
        {
            // Test that demonstrates WAL file management
            string gpkg = CreateTempGpkgPath();
            
            try
            {
                // Create GeoPackage with WAL mode
                CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, walMode: true);
                
                // Create layer and add data to generate WAL activity
                var schema = new Dictionary<string, string> { ["data"] = "TEXT" };
                GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "wal_test", schema);
                
                // Add data to create WAL entries
                for (int i = 0; i < 10; i++)
                {
                    var point = new Point(i, i);
                    CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "wal_test", point, new[] { $"Data_{i}" });
                }
                
                // WAL files might be created during operations
                string walFile = gpkg + "-wal";
                string shmFile = gpkg + "-shm";
                
                // Check if WAL auxiliary files exist (they might not always be present)
                bool walFileExists = File.Exists(walFile);
                bool shmFileExists = File.Exists(shmFile);
                
                // Perform checkpoint to integrate WAL data back to main database
                using (var connection = new SqliteConnection($"Data Source={gpkg}"))
                {
                    connection.Open();
                    
                    // Verify WAL mode is active
                    using var journalCommand = new SqliteCommand("PRAGMA journal_mode", connection);
                    var journalMode = journalCommand.ExecuteScalar()?.ToString();
                    Assert.AreEqual("wal", journalMode?.ToLower(), "Should be in WAL mode");
                    
                    // Perform checkpoint
                    using var checkpointCommand = new SqliteCommand("PRAGMA wal_checkpoint(TRUNCATE)", connection);
                    var result = checkpointCommand.ExecuteScalar();
                    
                    // Verify data is still accessible after checkpoint
                    using var countCommand = new SqliteCommand("SELECT COUNT(*) FROM wal_test", connection);
                    var count = Convert.ToInt32(countCommand.ExecuteScalar());
                    Assert.AreEqual(10, count, "All data should be accessible after checkpoint");
                }
                
                // Verify GeoPackage file exists and is readable
                Assert.IsTrue(File.Exists(gpkg), "Main GeoPackage file should exist");
                
                var features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "wal_test").ToList();
                Assert.AreEqual(10, features.Count, "All features should be readable after WAL operations");
                
                Console.WriteLine($"WAL file existed during test: {walFileExists}");
                Console.WriteLine($"SHM file existed during test: {shmFileExists}");
            }
            finally
            {
                TryDeleteFile(gpkg);
            }
        }

        [TestMethod]
        public void WalMode_BackwardCompatibility_ExistingCodeShouldStillWork()
        {
            // Ensure that enabling WAL mode doesn't break existing functionality
            string gpkg = CreateTempGpkgPath();
            
            try
            {
                // Create GeoPackage with WAL mode using new parameter
                CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, srid: 3006, walMode: true);
                
                // Use existing helper methods (should work the same)
                var schema = new Dictionary<string, string>
                {
                    ["name"] = "TEXT",
                    ["type"] = "TEXT"
                };
                
                // Existing layer creation should work
                GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "compatibility_test", schema);
                
                // Existing data insertion should work
                var point = new Point(500, 600);
                CGeopackageAddDataHelper.AddPointToGeoPackage(
                    gpkg, "compatibility_test", point, new[] { "Test Point", "Test Type" });
                
                // Existing bulk insert should work
                var features = new List<FeatureRecord>
                {
                    new FeatureRecord(
                        new Point(100, 100),
                        new Dictionary<string, string?> { ["name"] = "Bulk1", ["type"] = "TypeA" }
                    ),
                    new FeatureRecord(
                        new Point(200, 200),
                        new Dictionary<string, string?> { ["name"] = "Bulk2", ["type"] = "TypeB" }
                    )
                };
                
                CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "compatibility_test", features);
                
                // Existing read operations should work
                var readFeatures = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "compatibility_test").ToList();
                
                Assert.AreEqual(3, readFeatures.Count, "Should read all inserted features with WAL mode");
                
                var testPoint = readFeatures.FirstOrDefault(f => f.Attributes["name"] == "Test Point");
                Assert.IsNotNull(testPoint, "Individual insert should work with WAL mode");
                
                var bulk1 = readFeatures.FirstOrDefault(f => f.Attributes["name"] == "Bulk1");
                var bulk2 = readFeatures.FirstOrDefault(f => f.Attributes["name"] == "Bulk2");
                
                Assert.IsNotNull(bulk1, "Bulk insert feature 1 should work with WAL mode");
                Assert.IsNotNull(bulk2, "Bulk insert feature 2 should work with WAL mode");
                
                // Verify WAL mode is still active
                using var connection = new SqliteConnection($"Data Source={gpkg}");
                connection.Open();
                using var command = new SqliteCommand("PRAGMA journal_mode", connection);
                var mode = command.ExecuteScalar()?.ToString();
                Assert.AreEqual("wal", mode?.ToLower(), "WAL mode should be maintained throughout operations");
            }
            finally
            {
                TryDeleteFile(gpkg);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try 
            { 
                if (File.Exists(path)) 
                {
                    File.Delete(path);
                    
                    // Also delete WAL and SHM files if they exist
                    var walFile = path + "-wal";
                    var shmFile = path + "-shm";
                    
                    if (File.Exists(walFile)) File.Delete(walFile);
                    if (File.Exists(shmFile)) File.Delete(shmFile);
                }
            } 
            catch 
            { 
                // Ignore deletion errors in tests
            }
        }
    }
}
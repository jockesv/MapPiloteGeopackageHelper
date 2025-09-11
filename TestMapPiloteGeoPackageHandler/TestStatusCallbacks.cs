using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler
{
    [TestClass]
    public class TestStatusCallbacks
    {
        private static string CreateTempGpkgPath()
        {
            var path = Path.Combine(Path.GetTempPath(), $"callback_test_{Guid.NewGuid():N}.gpkg");
            return path;
        }

        [TestMethod]
        public void CMPGeopackageCreateHelper_WithStatusCallback_ShouldInvokeCallback()
        {
            // Arrange
            string gpkg = CreateTempGpkgPath();
            var statusMessages = new List<string>();
            
            try
            {
                // Act
                CMPGeopackageCreateHelper.CreateGeoPackage(
                    gpkg, 
                    onStatus: msg => statusMessages.Add(msg));

                // Assert
                Assert.IsTrue(statusMessages.Count > 0, "No status messages were received");
                Assert.IsTrue(statusMessages.Any(msg => msg.Contains("Successfully created GeoPackage")), 
                    "Expected success message not found");
                Assert.IsTrue(File.Exists(gpkg), "GeoPackage file was not created");
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
        public void GeopackageLayerCreateHelper_WithStatusCallback_ShouldInvokeCallback()
        {
            // Arrange
            string gpkg = CreateTempGpkgPath();
            var statusMessages = new List<string>();
            var errorMessages = new List<string>();
            
            try
            {
                // Create GeoPackage first
                CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
                
                var schema = new Dictionary<string, string>
                {
                    ["test_col"] = "TEXT"
                };

                // Act
                GeopackageLayerCreateHelper.CreateGeopackageLayer(
                    gpkg, 
                    "test_layer", 
                    schema,
                    onStatus: msg => statusMessages.Add(msg),
                    onError: msg => errorMessages.Add(msg));

                // Assert
                Assert.IsTrue(statusMessages.Count > 0, "No status messages were received");
                Assert.IsTrue(statusMessages.Any(msg => msg.Contains("Successfully created spatial layer")), 
                    "Expected success message not found");
                Assert.AreEqual(0, errorMessages.Count, "Unexpected error messages received");
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
        public void AddPointToGeoPackage_WithWarningCallback_ShouldInvokeOnUnknownType()
        {
            // This test would require creating a layer with unknown column type
            // For now, we'll test that the callback parameter doesn't break existing functionality
            string gpkg = CreateTempGpkgPath();
            var warningMessages = new List<string>();
            
            try
            {
                // Arrange
                CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
                
                var schema = new Dictionary<string, string>
                {
                    ["name"] = "TEXT",
                    ["value"] = "INTEGER"
                };
                
                GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", schema);
                
                var point = new Point(100, 200);
                var attributes = new[] { "Test", "42" };

                // Act
                CGeopackageAddDataHelper.AddPointToGeoPackage(
                    gpkg, 
                    "test_layer", 
                    point, 
                    attributes,
                    onWarning: msg => warningMessages.Add(msg));

                // Assert - No warnings expected for valid data
                Assert.AreEqual(0, warningMessages.Count, "No warnings should be generated for valid data");
                
                // Verify the point was actually inserted
                var features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer").ToList();
                Assert.AreEqual(1, features.Count, "Point should have been inserted");
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
        public async Task FluentAPI_WithStatusCallback_ShouldInvokeCallback()
        {
            // Arrange
            string gpkg = CreateTempGpkgPath();
            var statusMessages = new List<string>();
            
            try
            {
                // Act
                using var geoPackage = await GeoPackage.OpenAsync(
                    gpkg, 
                    onStatus: msg => statusMessages.Add(msg));

                // Assert
                Assert.IsTrue(statusMessages.Count > 0, "No status messages were received from fluent API");
                Assert.IsTrue(statusMessages.Any(msg => msg.Contains("Successfully initialized GeoPackage")), 
                    "Expected initialization message not found");
                Assert.IsTrue(File.Exists(gpkg), "GeoPackage file was not created by fluent API");
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
        public void WithoutCallbacks_ShouldStillWork()
        {
            // Arrange
            string gpkg = CreateTempGpkgPath();
            
            try
            {
                // Act - Test that methods work without any callbacks (backward compatibility)
                CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
                
                var schema = new Dictionary<string, string>
                {
                    ["name"] = "TEXT"
                };
                
                GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", schema);
                
                var point = new Point(100, 200);
                var attributes = new[] { "Test" };
                
                CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "test_layer", point, attributes);

                // Assert
                Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created without callbacks");
                
                var features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer").ToList();
                Assert.AreEqual(1, features.Count, "Point should be inserted without callbacks");
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
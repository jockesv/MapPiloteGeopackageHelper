using System.IO;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using MapPiloteGeopackageHelper;

namespace TestMapPiloteGeoPackageHandler
{
    [TestClass]
    
    public sealed class TestCreateGeoPackage
    {

        [TestMethod]
        //Create an empty geopackage
        public void TestMethod1SimpleCreate()
        {
            // Arrange
            string filePath = @"C:\\temp\\1SimpleCreate.gpkg";
            if (File.Exists(filePath))
                File.Delete(filePath);

            // Act
            CMPGeopackageCreateHelper.CreateGeoPackage(filePath);

            // Assert
            Assert.IsTrue(File.Exists(filePath), "GeoPackage file was not created.");
        }

        [TestMethod]
        //Create a geopackage, create a layer for points and add a point with attributes
        public void TestMethod2CreateAddPoint()
        {
            // Arrange
            string filePath = @"C:\\temp\\2CreateAndAddPoint.gpkg";
            if (File.Exists(filePath))
                File.Delete(filePath);

            // Act
            // Create the geopackage
            CMPGeopackageCreateHelper.CreateGeoPackage(filePath);

            //Create a layer for points
            var allColumns = new Dictionary<string, string>
                {
                    { "test1integer", "INTEGER" },
                    { "test2integer", "INTEGER" }
                };
            GeopackageLayerCreateHelper.CreateGeopackageLayer(filePath, "MyTestPoint", allColumns, "POINT");

            // 
            //Create a point and add it to the geopackage - Östersund coordinates in SRID 3006 (SWEREF99 TM)
            var point = new Point(415000, 7045000);
            String[] attributdata = new String[] { "4211","42"  };
            CGeopackageAddDataHelper.AddPointToGeoPackage(filePath, "MyTestPoint", point, attributdata);

            // Assert
            Assert.IsTrue(File.Exists(filePath), "GeoPackage file was not created.");
        }
    }
}

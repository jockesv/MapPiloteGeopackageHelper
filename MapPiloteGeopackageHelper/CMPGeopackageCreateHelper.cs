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
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace MapPiloteGeopackageHelper
{
    public class CMPGeopackageCreateHelper
    {
        static CMPGeopackageCreateHelper()
        {
            // Initialize SQLite
            Batteries.Init();
        }
        
        /// <summary>
        /// Creates a new GeoPackage file with proper spatial metadata and tables
        /// </summary>
        /// <param name="geoPackageFilePath">Path where the GeoPackage file will be created</param>
        /// <param name="srid">Spatial Reference System Identifier (default 3006 for SWEREF99 TM)</param>
        public static void CreateGeoPackage(string geoPackageFilePath, int srid = 3006)
        {
            // Delete existing file if it exists
            if (File.Exists(geoPackageFilePath))
            {
                File.Delete(geoPackageFilePath);
                Console.WriteLine($"Deleted existing GeoPackage file: {geoPackageFilePath}");
            }

            using (var connection = new SqliteConnection($"Data Source={geoPackageFilePath}"))
            {
                connection.Open();

                // Create required GeoPackage system tables
                CMPGeopackageUtils.CreateGeoPackageMetadataTables(connection);

                // Set up spatial reference system
                CMPGeopackageUtils.SetupSpatialReferenceSystem(connection, srid);

                Console.WriteLine($"Successfully created GeoPackage: {geoPackageFilePath}");
            }
        }
       
    }
}

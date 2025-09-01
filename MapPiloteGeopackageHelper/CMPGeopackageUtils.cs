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
using NetTopologySuite.Geometries;

namespace MapPiloteGeopackageHelper
{
    // / <summary>
    /// Utility class for creating and managing GeoPackage files
    /// Note that all methods are internal as this is a helper class
    /// Note if adding new methods: Even if class is internal best practice is to have all methods internal
    internal class CMPGeopackageUtils
    {
        public const string GEOPACKAGE_MIME_TYPE = "application/geopackage+sqlite3";
        public const string GEOPACKAGE_FILE_EXTENSION = ".gpkg";
        #region Private methods for GeoPackage setup
        internal static void CreateGeoPackageMetadataTables(SqliteConnection connection)
        {
            // First, set the application_id to identify this as a GeoPackage
            ExecuteCommand(connection, "PRAGMA application_id = 1196444487"); // 'GPKG' in ASCII

            // Create gpkg_spatial_ref_sys table
            string createSrsTable = @"
            CREATE TABLE gpkg_spatial_ref_sys (
                srs_name TEXT NOT NULL,
                srs_id INTEGER NOT NULL PRIMARY KEY,
                organization TEXT NOT NULL,
                organization_coordsys_id INTEGER NOT NULL,
                definition TEXT NOT NULL,
                description TEXT
            )";
            ExecuteCommand(connection, createSrsTable);

            // Create gpkg_contents table
            string createContentsTable = @"
            CREATE TABLE gpkg_contents (
                table_name TEXT NOT NULL PRIMARY KEY,
                data_type TEXT NOT NULL,
                identifier TEXT UNIQUE,
                description TEXT DEFAULT '',
                last_change DATETIME NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                min_x DOUBLE,
                min_y DOUBLE,
                max_x DOUBLE,
                max_y DOUBLE,
                srs_id INTEGER,
                CONSTRAINT fk_gc_r_srs_id FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id)
            )";
            ExecuteCommand(connection, createContentsTable);

            // Create gpkg_geometry_columns table
            string createGeomColumnsTable = @"
            CREATE TABLE gpkg_geometry_columns (
                table_name TEXT NOT NULL,
                column_name TEXT NOT NULL,
                geometry_type_name TEXT NOT NULL,
                srs_id INTEGER NOT NULL,
                z TINYINT NOT NULL,
                m TINYINT NOT NULL,
                CONSTRAINT pk_geom_cols PRIMARY KEY (table_name, column_name),
                CONSTRAINT uk_gc_table_name UNIQUE (table_name),
                CONSTRAINT fk_gc_tn FOREIGN KEY (table_name) REFERENCES gpkg_contents(table_name),
                CONSTRAINT fk_gc_srs FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id)
            )";
            ExecuteCommand(connection, createGeomColumnsTable);
        }

        internal static void SetupSpatialReferenceSystem(SqliteConnection connection, int srid)
        {
            // Insert SWEREF99 TM (EPSG:3006) - common Swedish coordinate system
            string insertSrs = @"
            INSERT OR REPLACE INTO gpkg_spatial_ref_sys 
            (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
            VALUES 
            ('SWEREF99 TM', 3006, 'EPSG', 3006, 
            'PROJCS[""SWEREF99 TM"",GEOGCS[""SWEREF99"",DATUM[""SWEREF99"",SPHEROID[""GRS 1980"",6378137,298.257222101,AUTHORITY[""EPSG"",""7019""]],TOWGS84[0,0,0,0,0,0,0],AUTHORITY[""EPSG"",""6619""]],PRIMEM[""Greenwich"",0,AUTHORITY[""EPSG"",""8901""]],UNIT[""degree"",0.01745329251994328,AUTHORITY[""EPSG"",""9122""]],AUTHORITY[""EPSG"",""4619""]],UNIT[""metre"",1,AUTHORITY[""EPSG"",""9001""]],PROJECTION[""Transverse_Mercator""],PARAMETER[""latitude_of_origin"",0],PARAMETER[""central_meridian"",15],PARAMETER[""scale_factor"",0.9996],PARAMETER[""false_easting"",500000],PARAMETER[""false_northing"",0],AUTHORITY[""EPSG"",""3006""],AXIS[""Y"",NORTH],AXIS[""X"",EAST]]',
            'Swedish national coordinate system')";
            ExecuteCommand(connection, insertSrs);

            // Also add WGS84 (EPSG:4326) as it's commonly needed
            string insertWgs84 = @"
            INSERT OR REPLACE INTO gpkg_spatial_ref_sys 
            (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
            VALUES 
            ('WGS 84', 4326, 'EPSG', 4326, 
            'GEOGCS[""WGS 84"",DATUM[""WGS_1984"",SPHEROID[""WGS 84"",6378137,298.257223563,AUTHORITY[""EPSG"",""7030""]],AUTHORITY[""EPSG"",""6326""]],PRIMEM[""Greenwich"",0,AUTHORITY[""EPSG"",""8901""]],UNIT[""degree"",0.01745329251994328,AUTHORITY[""EPSG"",""9122""]],AUTHORITY[""EPSG"",""4326""]]',
            'World Geodetic System 1984')";
            ExecuteCommand(connection, insertWgs84);

            // Add the undefined SRS (required by GeoPackage spec)
            string insertUndefined = @"
            INSERT OR REPLACE INTO gpkg_spatial_ref_sys 
            (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
            VALUES 
            ('Undefined cartesian SRS', -1, 'NONE', -1, 'undefined', 'undefined cartesian coordinate reference system')";
            ExecuteCommand(connection, insertUndefined);

            // Add the undefined geographic SRS (required by GeoPackage spec)
            string insertUndefinedGeographic = @"
            INSERT OR REPLACE INTO gpkg_spatial_ref_sys 
            (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
            VALUES 
            ('Undefined geographic SRS', 0, 'NONE', 0, 'undefined', 'undefined geographic coordinate reference system')";
            ExecuteCommand(connection, insertUndefinedGeographic);
        }

        internal static void RegisterTableInContents(SqliteConnection connection, string tableName,
            string geometryColumn, string geometryType, int srid, double? minX = null, double? minY = null,
            double? maxX = null, double? maxY = null)
        {
            string insertContents = @"
            INSERT INTO gpkg_contents 
            (table_name, data_type, identifier, description, srs_id, min_x, min_y, max_x, max_y)
            VALUES (@table_name, 'features', @table_name, @description, @srs_id, @min_x, @min_y, @max_x, @max_y)";

            using (var command = new SqliteCommand(insertContents, connection))
            {
                command.Parameters.AddWithValue("@table_name", tableName);
                command.Parameters.AddWithValue("@description", $"Spatial table {tableName}");
                command.Parameters.AddWithValue("@srs_id", srid);
                command.Parameters.AddWithValue("@min_x", minX ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@min_y", minY ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@max_x", maxX ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@max_y", maxY ?? (object)DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        internal static void RegisterGeometryColumn(SqliteConnection connection, string tableName,
            string geometryColumn, string geometryType, int srid)
        {
            string insertGeomColumn = @"
            INSERT INTO gpkg_geometry_columns 
            (table_name, column_name, geometry_type_name, srs_id, z, m)
            VALUES (@table_name, @column_name, @geometry_type, @srs_id, 0, 0)";

            using (var command = new SqliteCommand(insertGeomColumn, connection))
            {
                command.Parameters.AddWithValue("@table_name", tableName);
                command.Parameters.AddWithValue("@column_name", geometryColumn);
                command.Parameters.AddWithValue("@geometry_type", geometryType);
                command.Parameters.AddWithValue("@srs_id", srid);
                command.ExecuteNonQuery();
            }
        }

        internal static void ExecuteCommand(SqliteConnection connection, string sql)
        {
            using (var command = new SqliteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        internal static Polygon CreatePolygonFromPiposId(int piposId)
        {
            // Use the existing logic from CGeotools to create geometry
            long x = SpatialConnUtils.CGeotools.XFromId(piposId);
            long y = SpatialConnUtils.CGeotools.YFromId(piposId);

            // Create a 250x250 meter tile polygon
            var coordinates = new[]
            {
            new Coordinate(x, y),
            new Coordinate(x + 250, y),
            new Coordinate(x + 250, y + 250),
            new Coordinate(x, y + 250),
            new Coordinate(x, y) // Close the polygon
        };

            var factory = new GeometryFactory();
            return factory.CreatePolygon(coordinates);
        }

        internal static byte[] CreateGpkgBlob(byte[] wkb, int srid)
        {
            // Create GeoPackage binary header according to spec
            var header = new List<byte>();

            // Magic number "GP" (0x47, 0x50)
            header.AddRange(new byte[] { 0x47, 0x50 });

            // Version (0x00)
            header.Add(0x00);

            // Flags (0x00 = no envelope, little endian, binary type = 0)
            header.Add(0x00);

            // SRID (4 bytes, little endian)
            header.AddRange(BitConverter.GetBytes(srid));

            // WKB data
            header.AddRange(wkb);

            return header.ToArray();
        }

        internal static (double minX, double minY, double maxX, double maxY) CalculateSwedishExtent()
        {
            // Swedish national grid extent (SWEREF99 TM)
            return (181750.0, 6090250.0, 1086500.0, 7689500.0);
        }

        /*   private static void UpdateSpatialExtent(SqliteConnection connection, string tableName, List<CActivitytile> activityTiles)
           {
               if (activityTiles == null || activityTiles.Count == 0)
                   return;

               double minX = double.MaxValue, minY = double.MaxValue;
               double maxX = double.MinValue, maxY = double.MinValue;

               foreach (var tile in activityTiles)
               {
                   long x = SpatialConnUtils.CGeotools.XFromId(tile.PiposId);
                   long y = SpatialConnUtils.CGeotools.YFromId(tile.PiposId);

                   minX = Math.Min(minX, x);
                   minY = Math.Min(minY, y);
                   maxX = Math.Max(maxX, x + 250);
                   maxY = Math.Max(maxY, y + 250);
               }

               string updateExtent = @"
               UPDATE gpkg_contents 
               SET min_x = @min_x, min_y = @min_y, max_x = @max_x, max_y = @max_y, 
                   last_change = strftime('%Y-%m-%dT%H:%M:%fZ','now')
               WHERE table_name = @table_name";

               using (var command = new SqliteCommand(updateExtent, connection))
               {
                   command.Parameters.AddWithValue("@table_name", tableName);
                   command.Parameters.AddWithValue("@min_x", minX);
                   command.Parameters.AddWithValue("@min_y", minY);
                   command.Parameters.AddWithValue("@max_x", maxX);
                   command.Parameters.AddWithValue("@max_y", maxY);
                   command.ExecuteNonQuery();
               }

           }*/
        #endregion
    }
}

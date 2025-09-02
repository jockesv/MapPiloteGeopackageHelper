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
using NetTopologySuite.IO;
using System.Globalization;

namespace MapPiloteGeopackageHelper
{

    public static class CMPGeopackageReadDataHelper
    {
        // Returns all user table names registered in gpkg_contents
        internal static List<string> GetContentTableNames(string geoPackageFilePath)
        {
            var tableNames = new List<string>();
            using var connection = new SqliteConnection($"Data Source={geoPackageFilePath}");
            connection.Open();

            const string queryFile = "SELECT table_name FROM gpkg_contents";
            using var commandFile = new SqliteCommand(queryFile, connection);
            using var readerFile = commandFile.ExecuteReader();
            while (readerFile.Read())
            {
                tableNames.Add(readerFile.GetString(0));
            }

            return tableNames;
        }

        // New: Summarize GeoPackage metadata to guide data reading
        public static GeopackageInfo GetGeopackageInfo(string geoPackageFilePath)
        {
            using var connection = new SqliteConnection($"Data Source={geoPackageFilePath}");
            connection.Open();

            // 1) Read SRS table
            var srsList = new List<SrsInfo>();
            const string srsSql = "SELECT srs_id, srs_name, organization, organization_coordsys_id, definition, description FROM gpkg_spatial_ref_sys";
            using (var srsCmd = new SqliteCommand(srsSql, connection))
            using (var srsReader = srsCmd.ExecuteReader())
            {
                while (srsReader.Read())
                {
                    srsList.Add(new SrsInfo(
                        srsReader.GetInt32(0),
                        srsReader.GetString(1),
                        srsReader.GetString(2),
                        srsReader.GetInt32(3),
                        srsReader.GetString(4),
                        srsReader.IsDBNull(5) ? null : srsReader.GetString(5)));
                }
            }

            // 2) Read geometry columns once (map by table)
            var geomInfoByTable = new Dictionary<string, (string Column, string Type, int Srid)>();
            const string geomSql = "SELECT table_name, column_name, geometry_type_name, srs_id FROM gpkg_geometry_columns";
            using (var gCmd = new SqliteCommand(geomSql, connection))
            using (var gReader = gCmd.ExecuteReader())
            {
                while (gReader.Read())
                {
                    var t = gReader.GetString(0);
                    var c = gReader.GetString(1);
                    var gt = gReader.GetString(2);
                    var s = gReader.GetInt32(3);
                    geomInfoByTable[t] = (c, gt, s);
                }
            }

            // 3) Read contents and per-layer column info
            var layers = new List<LayerInfo>();
            const string contentsSql = "SELECT table_name, data_type, srs_id, min_x, min_y, max_x, max_y FROM gpkg_contents ORDER BY table_name";
            using (var cCmd = new SqliteCommand(contentsSql, connection))
            using (var cReader = cCmd.ExecuteReader())
            {
                while (cReader.Read())
                {
                    var tableName = cReader.GetString(0);
                    var dataType = cReader.GetString(1);
                    int? srid = cReader.IsDBNull(2) ? null : cReader.GetInt32(2);
                    double? minX = cReader.IsDBNull(3) ? null : cReader.GetDouble(3);
                    double? minY = cReader.IsDBNull(4) ? null : cReader.GetDouble(4);
                    double? maxX = cReader.IsDBNull(5) ? null : cReader.GetDouble(5);
                    double? maxY = cReader.IsDBNull(6) ? null : cReader.GetDouble(6);

                    // Query PRAGMA table_info for column metadata
                    var columns = new List<ColumnInfo>();
                    using (var tCmd = new SqliteCommand($"PRAGMA table_info({tableName})", connection))
                    using (var tReader = tCmd.ExecuteReader())
                    {
                        while (tReader.Read())
                        {
                            // PRAGMA table_info columns: cid, name, type, notnull (0/1), dflt_value, pk (0/1)
                            var colName = tReader.GetString(1);
                            var colType = tReader.IsDBNull(2) ? string.Empty : tReader.GetString(2);
                            var notNull = !tReader.IsDBNull(3) && tReader.GetInt32(3) == 1;
                            var isPk = !tReader.IsDBNull(5) && tReader.GetInt32(5) == 1;
                            columns.Add(new ColumnInfo(colName, colType, notNull, isPk));
                        }
                    }

                    string? geomColumn = null;
                    string? geomType = null;
                    int? geomSrid = null;
                    if (geomInfoByTable.TryGetValue(tableName, out var gi))
                    {
                        geomColumn = gi.Column;
                        geomType = gi.Type;
                        geomSrid = gi.Srid;
                        // Prefer geometry column SRID if contents.srs_id is null
                        if (srid is null) srid = geomSrid;
                    }

                    var attributeColumns = columns
                        .Where(c => !string.Equals(c.Name, "id", StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(c.Name, geomColumn, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    layers.Add(new LayerInfo(
                        TableName: tableName,
                        DataType: dataType,
                        Srid: srid,
                        GeometryColumn: geomColumn,
                        GeometryType: geomType,
                        MinX: minX,
                        MinY: minY,
                        MaxX: maxX,
                        MaxY: maxY,
                        Columns: columns,
                        AttributeColumns: attributeColumns));
                }
            }

            return new GeopackageInfo(layers, srsList);
        }

        // Enumerates features as (pipos_id, wkb) from a given table. Geometry is optional; if not found, returns null.
        internal static IEnumerable<(int PiposId, byte[]? GeometryWkb)> ReadFeaturesWithGeometryWkb(string geoPackageFilePath, string tableName)
        {
            using var connection = new SqliteConnection($"Data Source={geoPackageFilePath}");
            connection.Open();

            var sql = $"SELECT * FROM {tableName} ORDER BY pipos_id";
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                // Require pipos_id
                var piposIndex = reader.GetOrdinal("pipos_id");
                var piposId = reader.GetInt32(piposIndex);

                byte[]? wkb = null;
                var geomOrdinal = reader.GetOrdinal("geom");
                if (geomOrdinal >= 0)
                {
                    var geomBlob = reader.GetValue(geomOrdinal) as byte[];
                    if (geomBlob != null)
                    {
                        try
                        {
                            wkb = StripGpkgHeader(geomBlob);
                        }
                        catch
                        {
                            // Ignore geometry parsing errors in outline helper
                            wkb = null;
                        }
                    }
                }

                yield return (piposId, wkb);
            }
        }

        // Helper: strips the GeoPackage geometry header and returns WKB payload
        internal static byte[] StripGpkgHeader(byte[] gpkgBinary)
        {
            if (gpkgBinary.Length < 8)
                throw new ArgumentException("Invalid GPKG geometry header");

            byte flags = gpkgBinary[3];
            int envelopeIndicator = (flags >> 1) & 0x07;
            int envelopeBytes = envelopeIndicator switch
            {
                0 => 0,
                1 => 32,
                2 => 48,
                3 => 64,
                _ => throw new ArgumentException("Invalid envelope indicator in GPKG header")
            };

            int headerSize = 8 + envelopeBytes;
            if (gpkgBinary.Length < headerSize)
                throw new ArgumentException("Incomplete GPKG geometry header.");

            var wkb = new byte[gpkgBinary.Length - headerSize];
            Array.Copy(gpkgBinary, headerSize, wkb, 0, wkb.Length);
            return wkb;
        }

        /*
         * ColumnInfo
         *  Describes a single column in a GeoPackage user table. Values come from SQLite PRAGMA table_info.
         *  - Name: the column identifier as declared in the schema (case preserved).
         *  - Type: the declared SQL type (e.g. INTEGER, REAL, TEXT, BLOB, VARCHAR). May be empty if no type was declared.
         *  - NotNull: true when a NOT NULL constraint exists (table_info.notnull == 1).
         *  - IsPrimaryKey: true when the column participates in the primary key (table_info.pk == 1).
         *  Note: geometry columns are BLOBs and also appear in gpkg_geometry_columns.
         */
        public sealed record ColumnInfo(string Name, string Type, bool NotNull, bool IsPrimaryKey);

        /*
         * LayerInfo
         *  Consolidated metadata for a single GeoPackage layer (user table).
         *  Combines gpkg_contents, gpkg_geometry_columns and PRAGMA table_info.
         *  - TableName: gpkg_contents.table_name / SQLite table name.
         *  - DataType: gpkg_contents.data_type (typically "features" for spatial layers).
         *  - Srid: spatial reference identifier; geometry_columns.srs_id is preferred if contents.srs_id is null.
         *  - GeometryColumn: name of the geometry column (often "geom"). Null for non-spatial tables.
         *  - GeometryType: geometry type (POINT, LINESTRING, POLYGON, MULTIPOINT, ...). Null for non-spatial tables.
         *  - MinX/MinY/MaxX/MaxY: optional extent values from gpkg_contents; may be null.
         *  - Columns: all declared columns including PK and geometry.
         *  - AttributeColumns: Columns without PK and geometry; convenient for attribute I/O.
         */
        public sealed record LayerInfo(
            string TableName,
            string DataType,
            int? Srid,
            string? GeometryColumn,
            string? GeometryType,
            double? MinX,
            double? MinY,
            double? MaxX,
            double? MaxY,
            List<ColumnInfo> Columns,
            List<ColumnInfo> AttributeColumns);

        /*
         * SrsInfo
         *  Entry from gpkg_spatial_ref_sys (spatial reference systems present in the GeoPackage).
         *  Includes standard EPSG codes (e.g. 4326/WGS84, 3006/SWEREF99 TM) and required undefined entries (-1 and 0).
         *  - SrsId: gpkg_spatial_ref_sys.srs_id
         *  - SrsName: human-readable name
         *  - Organization: 
         *  •   EPSG
         *  •	OGC (e.g., OGC:CRS84)
         *  •	ESRI (Esri WKIDs, e.g., ESRI:102100)
         *  •	IGNF (Institut Géographique National France)
         *  •	SR-ORG (spatialreference.org community codes)
         *  •	IAU / IAU_2015 (planetary coordinate systems)
         *  •	NONE (used for the required undefined SRS entries: -1 and 0)
         *  •	CUSTOM (vendor- or project-defined)
         *  - OrganizationCoordsysId: organization-specific code (e.g. 4326, 3006)
         *  - Definition: WKT definition string
         *  - Description: optional description
         */
        public sealed record SrsInfo(
            int SrsId,
            string SrsName,
            string Organization,
            int OrganizationCoordsysId,
            string Definition,
            string? Description);

        /*
         * GeopackageInfo
         *  High-level summary of a GeoPackage.
         *  - Layers: per-layer metadata required for reading/writing features.
         *  - SpatialRefSystems: all SRS rows available in the package for SRID checks and CRS mapping.
         */
        public sealed record GeopackageInfo(
            List<LayerInfo> Layers,
            List<SrsInfo> SpatialRefSystems);

        // Stream features as FeatureRecord for a table; decode geometry optionally
        public static IEnumerable<FeatureRecord> ReadFeatures(
            string geoPackageFilePath,
            string tableName,
            string geometryColumn = "geom",
            bool includeGeometry = true)
        {
            using var connection = new SqliteConnection($"Data Source={geoPackageFilePath}");
            connection.Open();

            // Discover columns and pick attribute columns (exclude id and geometry)
            var columns = new List<ColumnInfo>();
            using (var tCmd = new SqliteCommand($"PRAGMA table_info({tableName})", connection))
            using (var tReader = tCmd.ExecuteReader())
            {
                while (tReader.Read())
                {
                    var colName = tReader.GetString(1);
                    var colType = tReader.IsDBNull(2) ? string.Empty : tReader.GetString(2);
                    var notNull = !tReader.IsDBNull(3) && tReader.GetInt32(3) == 1;
                    var isPk = !tReader.IsDBNull(5) && tReader.GetInt32(5) == 1;
                    columns.Add(new ColumnInfo(colName, colType, notNull, isPk));
                }
            }

            var attributeColumns = columns
                .Where(c => !string.Equals(c.Name, "id", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(c.Name, geometryColumn, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Name)
                .ToList();

            using var cmd = new SqliteCommand($"SELECT * FROM {tableName}", connection);
            using var reader = cmd.ExecuteReader();

            var geomOrdinal = -1;
            try { geomOrdinal = reader.GetOrdinal(geometryColumn); } catch { geomOrdinal = -1; }

            var wkbReader = includeGeometry ? new WKBReader() : null;

            while (reader.Read())
            {
                var attrs = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var name in attributeColumns)
                {
                    var ord = reader.GetOrdinal(name);
                    attrs[name] = reader.IsDBNull(ord) ? null : Convert.ToString(reader.GetValue(ord), CultureInfo.InvariantCulture);
                }

                Geometry? geometry = null;
                if (includeGeometry && geomOrdinal >= 0 && !reader.IsDBNull(geomOrdinal))
                {
                    var gpkgBlob = (byte[])reader.GetValue(geomOrdinal);
                    var wkb = StripGpkgHeader(gpkgBlob);
                    geometry = wkbReader!.Read(wkb);
                }

                yield return new FeatureRecord(geometry, attrs);
            }
        }
    }
}

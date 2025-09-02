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
using SQLitePCL;
using System.Globalization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestMapPiloteGeoPackageHandler")]

namespace MapPiloteGeopackageHelper
{
    public class CGeopackageAddDataHelper
    {
        static CGeopackageAddDataHelper()
        {
            // Initialize SQLite
            Batteries.Init();
        }

        /// <summary>
        /// Adds a point to a GeoPackage layer with attribute data and enhanced validation
        /// </summary>
        /// <param name="geoPackagePath">Path to the GeoPackage file</param>
        /// <param name="layerName">Name of the layer to add the point to</param>
        /// <param name="point">Point geometry to add</param>
        /// <param name="attributeData">Array of attribute values corresponding to the non-geometry columns</param>
        public static void AddPointToGeoPackage(string geoPackagePath, string layerName, Point point, string[] attributeData)
        {
            // Ensure the GeoPackage file exists
            if (!File.Exists(geoPackagePath))
            {
                throw new FileNotFoundException($"GeoPackage file not found: {geoPackagePath}");
            }

            const int defaultSrid = 3006; // SWEREF99 TM
            const string geometryColumn = "geom";

            using (var connection = new SqliteConnection($"Data Source={geoPackagePath}"))
            {
                connection.Open();

                // Get column information for the table (excluding id and geom columns)
                var columnInfo = GetColumnInfoWithTypes(connection, layerName);

                // Validate that we have the right number of attribute values
                if (attributeData.Length != columnInfo.Count)
                {
                    var expectedColumns = string.Join(", ", columnInfo.Select(c => $"{c.Name}({c.Type})"));
                    throw new ArgumentException(
                        $"Column count mismatch for table '{layerName}'. " +
                        $"Expected {columnInfo.Count} attribute values for columns: {expectedColumns}, " +
                        $"but received {attributeData.Length} values.");
                }

                // Enhanced validation: Check data type compatibility
                for (int i = 0; i < attributeData.Length; i++)
                {
                    ValidateDataTypeCompatibility(columnInfo[i], attributeData[i], i);
                }

                // Build the INSERT statement
                var columnNames = columnInfo.Select(c => c.Name).ToList();
                var columnList = string.Join(", ", columnNames.Concat(new[] { geometryColumn }));
                var parameterList = string.Join(", ", columnNames.Select(c => $"@{c}").Concat(new[] { "@geom" }));
                
                string insertQuery = $"INSERT INTO {layerName} ({columnList}) VALUES ({parameterList})";

                using (var command = new SqliteCommand(insertQuery, connection))
                {
                    // Add parameters for attribute data with type conversion
                    for (int i = 0; i < columnNames.Count; i++)
                    {
                        var convertedValue = ConvertValueToSqliteType(columnInfo[i], attributeData[i]);
                        command.Parameters.AddWithValue($"@{columnNames[i]}", convertedValue);
                    }

                    // Convert point geometry to GeoPackage binary format
                    var wkbWriter = new WKBWriter();
                    var wkb = wkbWriter.Write(point);
                    var gpkgBlob = CreateGpkgBlob(wkb, defaultSrid);
                    command.Parameters.AddWithValue("@geom", gpkgBlob);

                    command.ExecuteNonQuery();
                }

                Console.WriteLine($"Successfully added point to layer '{layerName}' in GeoPackage");
            }
        }

        /// <summary>
        /// Bulk insert features (attributes + optional geometry) with transactional batching.
        /// </summary>
        public static void BulkInsertFeatures(
            string geoPackagePath,
            string layerName,
            IEnumerable<FeatureRecord> features,
            int srid = 3006,
            int batchSize = 1000,
            string geometryColumn = "geom")
        {
            if (!File.Exists(geoPackagePath))
                throw new FileNotFoundException($"GeoPackage file not found: {geoPackagePath}");

            using var connection = new SqliteConnection($"Data Source={geoPackagePath}");
            connection.Open();

            // Get column information for attributes (excludes id and geometry)
            var columnInfo = GetColumnInfoWithTypes(connection, layerName);
            var columnNames = columnInfo.Select(c => c.Name).ToList();

            var columnList = string.Join(", ", columnNames.Concat(new[] { geometryColumn }));
            var parameterList = string.Join(", ", columnNames.Select(c => $"@{c}").Concat(new[] { "@geom" }));
            var insertSql = $"INSERT INTO {layerName} ({columnList}) VALUES ({parameterList})";

            using var command = new SqliteCommand(insertSql, connection);

            // Create parameters once
            foreach (var name in columnNames)
                command.Parameters.AddWithValue($"@{name}", DBNull.Value);
            command.Parameters.AddWithValue("@geom", DBNull.Value);

            // Begin first transaction
            SqliteTransaction? txn = connection.BeginTransaction();
            command.Transaction = txn;

            var wkbWriter = new WKBWriter();
            int i = 0;

            foreach (var feature in features)
            {
                // Bind attribute values from the provided dictionary; missing keys treated as NULL
                for (int idx = 0; idx < columnNames.Count; idx++)
                {
                    var col = columnInfo[idx];
                    feature.Attributes.TryGetValue(col.Name, out var raw);
                    var valForValidation = raw ?? string.Empty;
                    ValidateDataTypeCompatibility(col, valForValidation, idx);

                    var converted = ConvertValueToSqliteType(col, valForValidation);
                    command.Parameters[$"@{col.Name}"].Value = converted ?? DBNull.Value;
                }

                // Bind geometry (optional)
                if (feature.Geometry is null)
                {
                    command.Parameters["@geom"].Value = DBNull.Value;
                }
                else
                {
                    var wkb = wkbWriter.Write(feature.Geometry);
                    var gpkgBlob = CreateGpkgBlob(wkb, srid);
                    command.Parameters["@geom"].Value = gpkgBlob;
                }

                command.ExecuteNonQuery();

                i++;
                if (i % batchSize == 0)
                {
                    txn!.Commit();
                    txn.Dispose();
                    txn = connection.BeginTransaction();
                    command.Transaction = txn;
                }
            }

            // Final commit if there are pending operations in the current transaction
            txn?.Commit();
            txn?.Dispose();
        }

        /// <summary>
        /// Enhanced method to get column information including data types
        /// </summary>
        private static List<ColumnInfo> GetColumnInfoWithTypes(SqliteConnection connection, string tableName)
        {
            var columnInfo = new List<ColumnInfo>();
            
            string query = $"PRAGMA table_info({tableName})";
            using (var command = new SqliteCommand(query, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string columnName = reader.GetString(1); // Column name is at index 1
                    string columnType = reader.GetString(2); // Column type is at index 2
                    
                    // Exclude auto-increment id and geometry columns
                    if (!columnName.Equals("id", StringComparison.OrdinalIgnoreCase) && 
                        !columnName.Equals("geom", StringComparison.OrdinalIgnoreCase))
                    {
                        columnInfo.Add(new ColumnInfo(columnName, columnType));
                    }
                }
            }

            return columnInfo;
        }

        /// <summary>
        /// Validates that the provided data value is compatible with the column type
        /// Made internal for testing purposes
        /// </summary>
        internal static void ValidateDataTypeCompatibility(ColumnInfo columnInfo, string value, int index)
        {
            if (string.IsNullOrEmpty(value))
                return; // Allow NULL values

            var columnType = columnInfo.Type.ToUpperInvariant();
            
            try
            {
                switch (columnType)
                {
                    case "INTEGER":
                    case "INT":
                        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        {
                            throw new ArgumentException(
                                $"Data type mismatch at index {index}: Column '{columnInfo.Name}' expects INTEGER, " +
                                $"but received '{value}' which cannot be converted to an integer.");
                        }
                        break;

                    case "REAL":
                    case "FLOAT":
                    case "DOUBLE":
                        if (!double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
                        {
                            throw new ArgumentException(
                                $"Data type mismatch at index {index}: Column '{columnInfo.Name}' expects REAL/FLOAT, " +
                                $"but received '{value}' which cannot be converted to a number.");
                        }
                        break;

                    case "TEXT":
                    case "VARCHAR":
                    case "CHAR":
                        // Text values are always acceptable
                        break;

                    case "BLOB":
                        throw new ArgumentException(
                            $"Column '{columnInfo.Name}' is of type BLOB and cannot be inserted via string array. " +
                            "BLOB columns require special handling.");

                    default:
                        // For unknown types, log warning but allow (SQLite is flexible)
                        Console.WriteLine($"Warning: Unknown column type '{columnType}' for column '{columnInfo.Name}'. Proceeding with string value.");
                        break;
                }
            }
            catch (ArgumentException)
            {
                throw; // Re-throw validation errors
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Error validating data for column '{columnInfo.Name}' at index {index}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts string value to appropriate SQLite type
        /// </summary>
        private static object ConvertValueToSqliteType(ColumnInfo columnInfo, string value)
        {
            if (string.IsNullOrEmpty(value))
                return DBNull.Value;

            var columnType = columnInfo.Type.ToUpperInvariant();
            
            return columnType switch
            {
                "INTEGER" or "INT" => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "REAL" or "FLOAT" or "DOUBLE" => double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture),
                "TEXT" or "VARCHAR" or "CHAR" => value,
                _ => value // Default to string for unknown types
            };
        }

        /// <summary>
        /// Helper class to store column information
        /// Made internal for testing purposes
        /// </summary>
        internal class ColumnInfo
        {
            public string Name { get; }
            public string Type { get; }

            public ColumnInfo(string name, string type)
            {
                Name = name;
                Type = type;
            }
        }

        /// <summary>
        /// Gets the names of non-geometry columns from a table (excluding id and geom)
        /// DEPRECATED: Use GetColumnInfoWithTypes for better validation
        /// </summary]
        private static List<string> GetNonGeometryColumnNames(SqliteConnection connection, string tableName)
        {
            var columnNames = new List<string>();
            
            string query = $"PRAGMA table_info({tableName})";
            using (var command = new SqliteCommand(query, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string columnName = reader.GetString(1); // Column name is at index 1
                    // Exclude auto-increment id and geometry columns
                    if (!columnName.Equals("id", StringComparison.OrdinalIgnoreCase) && 
                        !columnName.Equals("geom", StringComparison.OrdinalIgnoreCase))
                    {
                        columnNames.Add(columnName);
                    }
                }
            }

            return columnNames;
        }

        /// <summary>
        /// Creates a GeoPackage binary blob from WKB data
        /// </summary>
        /// <param name="wkb">Well-Known Binary geometry data</param>
        /// <param name="srid">Spatial Reference System Identifier</param>
        /// <returns>GeoPackage binary blob</returns>
        private static byte[] CreateGpkgBlob(byte[] wkb, int srid)
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
    }
}

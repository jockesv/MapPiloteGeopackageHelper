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
using System.Runtime.CompilerServices;
using System.Globalization;

namespace MapPiloteGeopackageHelper
{
    /// <summary>
    /// Modern fluent API for GeoPackage operations
    /// </summary>
    public sealed class GeoPackage : IDisposable, IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _path;

        private GeoPackage(string path, SqliteConnection connection)
        {
            _path = path;
            _connection = connection;
        }

        /// <summary>
        /// Opens or creates a GeoPackage file
        /// </summary>
        public static async Task<GeoPackage> OpenAsync(string path, int defaultSrid = 3006, CancellationToken ct = default, Action<string>? onStatus = null)
        {
            var exists = File.Exists(path);
            
            var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync(ct);

            var geoPackage = new GeoPackage(path, connection);

            if (!exists)
            {
                await geoPackage.InitializeAsync(defaultSrid, ct, onStatus);
            }

            return geoPackage;
        }

        /// <summary>
        /// Ensures a layer exists with the given schema, creating it if needed
        /// </summary>
        public async Task<GeoPackageLayer> EnsureLayerAsync(
            string layerName,
            Dictionary<string, string> attributeColumns,
            int srid = 3006,
            string geometryType = "POINT",
            string geometryColumn = "geom",
            CancellationToken ct = default)
        {
            // Check if layer exists
            var exists = await LayerExistsAsync(layerName, ct);
            
            if (!exists)
            {
                await CreateLayerAsync(layerName, attributeColumns, srid, geometryType, geometryColumn, ct);
            }

            return new GeoPackageLayer(this, layerName, geometryColumn);
        }

        /// <summary>
        /// Get comprehensive metadata about this GeoPackage
        /// </summary>
        public async Task<CMPGeopackageReadDataHelper.GeopackageInfo> GetInfoAsync(CancellationToken ct = default)
        {
            // Use existing logic but make async
            return await Task.Run(() => CMPGeopackageReadDataHelper.GetGeopackageInfo(_path), ct);
        }

        internal SqliteConnection Connection => _connection;
        internal string Path => _path;

        private async Task InitializeAsync(int srid, CancellationToken ct, Action<string>? onStatus = null)
        {
            // Use existing creation logic with callback support
            await Task.Run(() =>
            {
                CMPGeopackageUtils.CreateGeoPackageMetadataTables(_connection);
                CMPGeopackageUtils.SetupSpatialReferenceSystem(_connection, srid);
                onStatus?.Invoke($"Successfully initialized GeoPackage: {_path}");
            }, ct);
        }

        private async Task<bool> LayerExistsAsync(string layerName, CancellationToken ct)
        {
            const string sql = "SELECT COUNT(*) FROM gpkg_contents WHERE table_name = @name";
            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@name", layerName);
            
            var count = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            return count > 0;
        }

        private async Task CreateLayerAsync(
            string layerName,
            Dictionary<string, string> attributeColumns,
            int srid,
            string geometryType,
            string geometryColumn,
            CancellationToken ct)
        {
            // Use existing layer creation logic with callback support
            await Task.Run(() =>
            {
                GeopackageLayerCreateHelper.CreateGeopackageLayer(
                    _path, layerName, attributeColumns, geometryType, srid,
                    onStatus: _ => { },  // Discard status messages
                    onError: _ => { });   // Discard error messages (let exceptions bubble up)
            }, ct);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Represents a layer in a GeoPackage for fluent operations
    /// </summary>
    public sealed class GeoPackageLayer
    {
        private readonly GeoPackage _geoPackage;
        private readonly string _layerName;
        private readonly string _geometryColumn;

        internal GeoPackageLayer(GeoPackage geoPackage, string layerName, string geometryColumn)
        {
            _geoPackage = geoPackage;
            _layerName = layerName;
            _geometryColumn = geometryColumn;
        }

        /// <summary>
        /// Bulk insert features with progress reporting
        /// </summary>
        public async Task BulkInsertAsync(
            IEnumerable<FeatureRecord> features,
            BulkInsertOptions? options = null,
            IProgress<BulkProgress>? progress = null,
            CancellationToken ct = default)
        {
            options ??= new BulkInsertOptions();
            
            var featureList = features.ToList();
            var total = featureList.Count;
            var processed = 0;

            // Get column info using existing logic
            var columnInfo = await GetColumnInfoAsync(ct);
            
            var insertSql = GetInsertSql(options.ConflictPolicy);
            using var command = new SqliteCommand(insertSql, _geoPackage.Connection);
            
            // Create parameters
            foreach (var col in columnInfo)
                command.Parameters.AddWithValue($"@{col.Name}", DBNull.Value);
            command.Parameters.AddWithValue("@geom", DBNull.Value);

            var wkbWriter = new WKBWriter();
            SqliteTransaction? transaction = null;

            try
            {
                transaction = _geoPackage.Connection.BeginTransaction();
                command.Transaction = transaction;

                foreach (var feature in featureList)
                {
                    ct.ThrowIfCancellationRequested();

                    // Use existing validation and conversion
                    await BindFeatureAsync(command, feature, columnInfo, wkbWriter, options.Srid);
                    await command.ExecuteNonQueryAsync(ct);

                    processed++;
                    
                    if (processed % options.BatchSize == 0)
                    {
                        transaction.Commit();
                        transaction.Dispose();
                        transaction = _geoPackage.Connection.BeginTransaction();
                        command.Transaction = transaction;
                        
                        progress?.Report(new BulkProgress(processed, total));
                    }
                }

                transaction.Commit();
                progress?.Report(new BulkProgress(total, total));
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        /// <summary>
        /// Read features as async enumerable with options
        /// </summary>
        public async IAsyncEnumerable<FeatureRecord> ReadFeaturesAsync(
            ReadOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            options ??= new ReadOptions();

            // Build query with WHERE/LIMIT/OFFSET support
            var sql = BuildSelectSql(options);
            using var command = new SqliteCommand(sql, _geoPackage.Connection);
            using var reader = await command.ExecuteReaderAsync(ct);
            
            // Get column info for attribute mapping
            var attributeColumns = await GetAttributeColumnNamesAsync(ct);
            var wkbReader = options.IncludeGeometry ? new WKBReader() : null;
            var geomOrdinal = -1;
            
            if (options.IncludeGeometry)
            {
                try { geomOrdinal = reader.GetOrdinal(_geometryColumn); } 
                catch { geomOrdinal = -1; }
            }
            
            while (await reader.ReadAsync(ct))
            {
                ct.ThrowIfCancellationRequested();
                
                // Build attributes dictionary
                var attrs = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var colName in attributeColumns)
                {
                    try
                    {
                        var ord = reader.GetOrdinal(colName);
                        attrs[colName] = reader.IsDBNull(ord) ? null : 
                            Convert.ToString(reader.GetValue(ord), CultureInfo.InvariantCulture);
                    }
                    catch { /* column might not exist */ }
                }

                // Parse geometry if requested
                Geometry? geometry = null;
                if (options.IncludeGeometry && geomOrdinal >= 0 && !reader.IsDBNull(geomOrdinal))
                {
                    try
                    {
                        var gpkgBlob = (byte[])reader.GetValue(geomOrdinal);
                        var wkb = CMPGeopackageReadDataHelper.StripGpkgHeader(gpkgBlob);
                        geometry = wkbReader!.Read(wkb);
                    }
                    catch { /* ignore geometry parse errors */ }
                }

                yield return new FeatureRecord(geometry, attrs);
            }
        }

        /// <summary>
        /// Delete features matching a condition
        /// </summary>
        public async Task<int> DeleteAsync(string? whereClause = null, CancellationToken ct = default)
        {
            var sql = string.IsNullOrEmpty(whereClause) 
                ? $"DELETE FROM {_layerName}"
                : $"DELETE FROM {_layerName} WHERE {whereClause}";
                
            using var command = new SqliteCommand(sql, _geoPackage.Connection);
            return await command.ExecuteNonQueryAsync(ct);
        }

        /// <summary>
        /// Get feature count
        /// </summary>
        public async Task<long> CountAsync(string? whereClause = null, CancellationToken ct = default)
        {
            var sql = string.IsNullOrEmpty(whereClause)
                ? $"SELECT COUNT(*) FROM {_layerName}"
                : $"SELECT COUNT(*) FROM {_layerName} WHERE {whereClause}";
                
            using var command = new SqliteCommand(sql, _geoPackage.Connection);
            return (long)(await command.ExecuteScalarAsync(ct) ?? 0);
        }

        /// <summary>
        /// Create spatial index on geometry column (if supported)
        /// </summary>
        public async Task CreateSpatialIndexAsync(CancellationToken ct = default)
        {
            // SQLite spatial index requires R*Tree extension
            var sql = $"CREATE INDEX IF NOT EXISTS idx_{_layerName}_{_geometryColumn} ON {_layerName}({_geometryColumn})";
            using var command = new SqliteCommand(sql, _geoPackage.Connection);
            await command.ExecuteNonQueryAsync(ct);
        }

        private string GetInsertSql(ConflictPolicy policy)
        {
            var verb = policy switch
            {
                ConflictPolicy.Ignore => "INSERT OR IGNORE",
                ConflictPolicy.Replace => "INSERT OR REPLACE", 
                _ => "INSERT"
            };

            // Get column info synchronously for SQL building
            var columnInfo = GetColumnInfoSync();
            var columnNames = columnInfo.Select(c => c.Name).ToList();
            var columnList = string.Join(", ", columnNames.Concat(new[] { _geometryColumn }));
            var parameterList = string.Join(", ", columnNames.Select(c => $"@{c}").Concat(new[] { "@geom" }));
            
            return $"{verb} INTO {_layerName} ({columnList}) VALUES ({parameterList})";
        }

        private string BuildSelectSql(ReadOptions options)
        {
            var sql = $"SELECT * FROM {_layerName}";
            
            if (!string.IsNullOrEmpty(options.WhereClause))
                sql += $" WHERE {options.WhereClause}";
            
            if (!string.IsNullOrEmpty(options.OrderBy))
                sql += $" ORDER BY {options.OrderBy}";
                
            if (options.Limit.HasValue)
                sql += $" LIMIT {options.Limit}";
                
            if (options.Offset.HasValue)
                sql += $" OFFSET {options.Offset}";
                
            return sql;
        }

        private async Task<List<CGeopackageAddDataHelper.ColumnInfo>> GetColumnInfoAsync(CancellationToken ct)
        {
            return await Task.Run(() => GetColumnInfoSync(), ct);
        }

        private List<CGeopackageAddDataHelper.ColumnInfo> GetColumnInfoSync()
        {
            // Use existing synchronous logic
            var columnInfo = new List<CGeopackageAddDataHelper.ColumnInfo>();
            
            string query = $"PRAGMA table_info({_layerName})";
            using var command = new SqliteCommand(query, _geoPackage.Connection);
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                string columnName = reader.GetString(1);
                string columnType = reader.GetString(2);
                
                if (!columnName.Equals("id", StringComparison.OrdinalIgnoreCase) && 
                    !columnName.Equals(_geometryColumn, StringComparison.OrdinalIgnoreCase))
                {
                    columnInfo.Add(new CGeopackageAddDataHelper.ColumnInfo(columnName, columnType));
                }
            }
            
            return columnInfo;
        }

        private async Task<List<string>> GetAttributeColumnNamesAsync(CancellationToken ct)
        {
            var columnInfo = await GetColumnInfoAsync(ct);
            return columnInfo.Select(c => c.Name).ToList();
        }

        private Task BindFeatureAsync(
            SqliteCommand command, 
            FeatureRecord feature, 
            List<CGeopackageAddDataHelper.ColumnInfo> columnInfo, 
            WKBWriter wkbWriter, 
            int srid)
        {
            // Use existing validation and conversion from CGeopackageAddDataHelper
            for (int idx = 0; idx < columnInfo.Count; idx++)
            {
                var col = columnInfo[idx];
                feature.Attributes.TryGetValue(col.Name, out var raw);
                var valForValidation = raw ?? string.Empty;
                
                // Use existing validation - no warning callback in fluent API (silent operation)
                CGeopackageAddDataHelper.ValidateDataTypeCompatibility(col, valForValidation, idx);

                // Use existing conversion logic
                var converted = ConvertValueToSqliteType(col, valForValidation);
                command.Parameters[$"@{col.Name}"].Value = converted ?? DBNull.Value;
            }

            // Handle geometry using existing logic
            if (feature.Geometry == null)
            {
                command.Parameters["@geom"].Value = DBNull.Value;
            }
            else
            {
                var wkb = wkbWriter.Write(feature.Geometry);
                var gpkgBlob = CreateGpkgBlob(wkb, srid);
                command.Parameters["@geom"].Value = gpkgBlob;
            }

            return Task.CompletedTask;
        }

        // Copy existing conversion logic from CGeopackageAddDataHelper
        private static object ConvertValueToSqliteType(CGeopackageAddDataHelper.ColumnInfo columnInfo, string value)
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

        // Copy existing GPKG blob creation logic
        private static byte[] CreateGpkgBlob(byte[] wkb, int srid)
        {
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
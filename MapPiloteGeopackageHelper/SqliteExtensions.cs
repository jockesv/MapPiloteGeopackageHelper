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

namespace MapPiloteGeopackageHelper
{
    /// <summary>
    /// Extension methods to add convenient async support to SqliteConnection.
    /// These methods reduce boilerplate code by encapsulating SqliteCommand creation 
    /// and disposal within the extension method itself.
    /// </summary>
    /// <remarks>
    /// Internal class - only accessible within this assembly. 
    /// These extensions support the fluent API implementation by providing
    /// cleaner async database operations without exposing implementation details.
    /// </remarks>
    internal static class SqliteExtensions
    {
        /// <summary>
        /// Executes a SQL statement that doesn't return data (INSERT, UPDATE, DELETE, CREATE TABLE, etc.)
        /// and returns the number of rows affected.
        /// </summary>
        /// <param name="connection">The SQLite database connection to execute against</param>
        /// <param name="sql">The SQL statement to execute (e.g., "CREATE TABLE users (id INTEGER)")</param>
        /// <param name="ct">Cancellation token to allow operation cancellation</param>
        /// <returns>
        /// Task&lt;int&gt; representing the number of rows affected by the operation.
        /// For CREATE/DROP statements, this is typically 0.
        /// For INSERT/UPDATE/DELETE, this is the count of affected rows.
        /// </returns>
        /// <example>
        /// // Instead of writing:
        /// using var cmd = new SqliteCommand("DELETE FROM users WHERE inactive = 1", connection);
        /// int deletedCount = await cmd.ExecuteNonQueryAsync(ct);
        /// 
        /// // You can write:
        /// int deletedCount = await connection.ExecuteNonQueryAsync("DELETE FROM users WHERE inactive = 1", ct);
        /// </example>
        public static async Task<int> ExecuteNonQueryAsync(this SqliteConnection connection, string sql, CancellationToken ct = default)
        {
            // Create a new command with the provided SQL and connection
            using var command = new SqliteCommand(sql, connection);
            
            // Execute the command asynchronously and return the result
            // The 'using' statement ensures the SqliteCommand is properly disposed
            // even if an exception occurs during execution
            return await command.ExecuteNonQueryAsync(ct);
        }

        /// <summary>
        /// Executes a SQL query that returns a single value (first column of first row).
        /// Commonly used for aggregate functions like COUNT(), MAX(), SUM(), or single value lookups.
        /// </summary>
        /// <param name="connection">The SQLite database connection to execute against</param>
        /// <param name="sql">The SQL query to execute (e.g., "SELECT COUNT(*) FROM users")</param>
        /// <param name="ct">Cancellation token to allow operation cancellation</param>
        /// <returns>
        /// Task&lt;object?&gt; representing the scalar value returned by the query.
        /// Returns null if the query returns no rows or the first column is NULL.
        /// The actual type depends on the SQL query (long for COUNT, string for text fields, etc.)
        /// </returns>
        /// <example>
        /// // Get total count of records
        /// var count = (long)await connection.ExecuteScalarAsync("SELECT COUNT(*) FROM cities");
        /// 
        /// // Get maximum ID value
        /// var maxId = (long?)await connection.ExecuteScalarAsync("SELECT MAX(id) FROM users");
        /// 
        /// // Get a single user name
        /// var name = (string?)await connection.ExecuteScalarAsync("SELECT name FROM users WHERE id = 1");
        /// </example>
        /// <remarks>
        /// Important: You need to cast the result to the expected type since it returns object?.
        /// If the query might return NULL, use nullable types (e.g., long? instead of long).
        /// Only the first column of the first row is returned - additional columns/rows are ignored.
        /// </remarks>
        public static async Task<object?> ExecuteScalarAsync(this SqliteConnection connection, string sql, CancellationToken ct = default)
        {
            // Create a new command with the provided SQL and connection
            using var command = new SqliteCommand(sql, connection);
            
            // Execute the scalar query asynchronously and return the result
            // The 'using' statement ensures the SqliteCommand is properly disposed
            // Returns null if no rows are found or if the first column value is NULL
            return await command.ExecuteScalarAsync(ct);
        }
    }
}
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
namespace MapPiloteGeopackageHelper
{
    /// <summary>
    /// Options for bulk insert operations
    /// </summary>
    public sealed record BulkInsertOptions(
        int BatchSize = 1000,
        int Srid = 3006,
        bool CreateSpatialIndex = false,
        ConflictPolicy ConflictPolicy = ConflictPolicy.Abort
    );

    /// <summary>
    /// Progress information for bulk operations
    /// </summary>
    public sealed record BulkProgress(
        int Processed,
        int Total
    )
    {
        public double PercentComplete => Total > 0 ? (double)Processed / Total * 100.0 : 0.0;
        public bool IsComplete => Processed >= Total;
        public int Remaining => Math.Max(0, Total - Processed);
    }

    /// <summary>
    /// How to handle conflicts during insert
    /// </summary>
    public enum ConflictPolicy
    {
        /// <summary>Abort on conflict (default)</summary>
        Abort,
        /// <summary>Ignore conflicting rows</summary>
        Ignore,
        /// <summary>Replace existing rows</summary>
        Replace
    }

    /// <summary>
    /// Options for reading features
    /// </summary>
    public sealed record ReadOptions(
        bool IncludeGeometry = true,
        string? WhereClause = null,
        int? Limit = null,
        int? Offset = null,
        string? OrderBy = null
    );

    /// <summary>
    /// Options for layer creation
    /// </summary>
    public sealed record LayerCreateOptions(
        int Srid = 3006,
        string GeometryType = "POINT",
        string GeometryColumn = "geom",
        bool CreateSpatialIndex = false,
        Dictionary<string, string>? Constraints = null
    );

    /// <summary>
    /// Schema validation result
    /// </summary>
    public sealed record ValidationResult(
        bool IsValid,
        List<string> Errors
    )
    {
        public static ValidationResult Success() => new(true, new List<string>());
        public static ValidationResult Failure(params string[] errors) => new(false, errors.ToList());
    }
}
using MapPiloteGeopackageHelper;
using System.Globalization;

// Simple console schema-and-sample browser for GeoPackage files.
// - Prints layers, columns, SRIDs, extents.
// - Emits a C# record model per layer and sample reader code the user can copy.

string gpkg = @"Z:\Projekt 2025\Utredning_serviceplatser\BatchMunicipalities\Municipality.gpkg";

Console.WriteLine(gpkg);


if (!File.Exists(gpkg))
{
    Console.WriteLine($"File not found: {gpkg}");
    return;
}

Console.WriteLine($"Inspecting GeoPackage: {gpkg}\n");

var info = CMPGeopackageReadDataHelper.GetGeopackageInfo(gpkg);

foreach (var layer in info.Layers)
{
    Console.WriteLine($"Layer: {layer.TableName}");
    Console.WriteLine($"  Type: {layer.DataType}");
    Console.WriteLine($"  SRID: {layer.Srid?.ToString() ?? "<null>"}");
    Console.WriteLine($"  Geometry: {layer.GeometryColumn ?? "<none>"} ({layer.GeometryType ?? "<unknown>"})");
    Console.WriteLine($"  Extent: [{layer.MinX?.ToString("G", CultureInfo.InvariantCulture) ?? ""}, {layer.MinY?.ToString("G", CultureInfo.InvariantCulture) ?? ""}] -> [{layer.MaxX?.ToString("G", CultureInfo.InvariantCulture) ?? ""}, {layer.MaxY?.ToString("G", CultureInfo.InvariantCulture) ?? ""}]");

    Console.WriteLine("  Columns:");
    foreach (var c in layer.Columns)
    {
        var pk = c.IsPrimaryKey ? " PK" : string.Empty;
        var notnull = c.NotNull ? " NOT NULL" : string.Empty;
        Console.WriteLine($"    - {c.Name} : {c.Type}{pk}{notnull}");
    }

    // Emit a simple, copy/paste C# record for attributes (ignores PK and geometry)
    var attrCols = layer.AttributeColumns;
    Console.WriteLine("\n  Suggested C# attribute record you can use:");
    Console.WriteLine($"  public sealed record {Pascal(layer.TableName)}Attributes(");
    for (int i = 0; i < attrCols.Count; i++)
    {
        var ac = attrCols[i];
        var clrType = MapSqlTypeToClr(ac.Type, ac.NotNull);
        var comma = i == attrCols.Count - 1 ? string.Empty : ",";
        Console.WriteLine($"      {clrType} {Pascal(ac.Name)}{comma}");
    }
    Console.WriteLine("  );\n");

    Console.WriteLine("  Example code to read as FeatureRecord and map to your type:");
    Console.WriteLine("  // using MapPiloteGeopackageHelper; using NetTopologySuite.Geometries;\n" +
                      $"  foreach (var f in CMPGeopackageReadDataHelper.ReadFeatures(\"{gpkg}\", \"{layer.TableName}\"))\n" +
                      "  {\n" +
                      "      var attrs = f.Attributes;\n" +
                      "      // Access values by column name, e.g.:\n" +
                      "      var name = attrs.GetValueOrDefault(\"name\");\n" +
                      "      // Convert to your target types as needed.\n" +
                      "  }\n");

    // Print up to 3 sample features with attributes
    Console.WriteLine("  Sample rows (up to 3):");
    var includeGeometry = !string.IsNullOrEmpty(layer.GeometryColumn);
    var geometryColumn = layer.GeometryColumn ?? "geom";
    int printed = 0;
    foreach (var f in CMPGeopackageReadDataHelper.ReadFeatures(gpkg, layer.TableName, geometryColumn: geometryColumn, includeGeometry: includeGeometry))
    {
        var geomSummary = f.Geometry == null
            ? "<no geom>"
            : f.Geometry is NetTopologySuite.Geometries.Point pt
                ? $"POINT({pt.X.ToString("G", CultureInfo.InvariantCulture)},{pt.Y.ToString("G", CultureInfo.InvariantCulture)})"
                : f.Geometry.GeometryType;

        var attrs = string.Join(
            ", ",
            f.Attributes.Select(kvp => $"{kvp.Key}={(kvp.Value ?? "<null>")}")
        );

        Console.WriteLine($"    - {geomSummary} | {attrs}");
        printed++;
        if (printed >= 3) break;
    }

    Console.WriteLine(new string('-', 80));
}

static string Pascal(string input)
{
    if (string.IsNullOrEmpty(input)) return input;
    var parts = input.Split(new[] { '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
    return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
}

static string MapSqlTypeToClr(string sqlType, bool notNull)
{
    var t = (sqlType ?? string.Empty).Trim().ToUpperInvariant();
    string core = t switch
    {
        "INTEGER" or "INT" => "long",
        "REAL" or "FLOAT" or "DOUBLE" => "double",
        "TEXT" or "VARCHAR" or "CHAR" => "string",
        "BLOB" => "byte[]",
        _ => "string"
    };

    // Make reference types nullable if column can be null
    if (core is "string" or "byte[]")
        return notNull ? core : core + "?";

    // Value types nullable only when column nullable
    return notNull ? core : core + "?";
}

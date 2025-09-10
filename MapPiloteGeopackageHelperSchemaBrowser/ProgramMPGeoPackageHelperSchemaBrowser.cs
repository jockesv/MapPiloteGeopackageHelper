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
using MapPiloteGeopackageHelper;
using System.Globalization;

// =============================================================
// GeoPackage Schema Browser for MapPiloteGeopackageHelper
// -------------------------------------------------------------
// This example demonstrates how to:
//  1) Open existing GeoPackage files
//  2) Inspect available layers and their schemas
//  3) Browse spatial reference systems (SRS)
//  4) Examine column definitions and constraints
//  5) Display comprehensive metadata information
// Perfect for exploring unknown GeoPackage files!
// =============================================================

string workDir = Environment.CurrentDirectory;
string gpkg = Path.Combine(workDir, "Data", "AdmBordersSweden.gpkg");

Console.WriteLine("=== MapPilote GeoPackage Schema Browser ===");

// Show reference mode at runtime - check the LIBRARY we're testing, not this project
#if DEBUG
var helperAssembly = typeof(CMPGeopackageCreateHelper).Assembly;
var assemblyLocation = helperAssembly.Location;
var assemblyDirectory = Path.GetDirectoryName(assemblyLocation) ?? "";

Console.WriteLine($"Checking MapPiloteGeopackageHelper library location...");
Console.WriteLine($"   Assembly: {helperAssembly.FullName}");
Console.WriteLine($"   Location: {assemblyLocation}");

// Check if we're in a local development environment but using copied assemblies
var isInTestProjectBin = assemblyLocation.Contains("\\bin\\Debug\\") || 
                         assemblyLocation.Contains("/bin/Debug/") ||
                         assemblyLocation.Contains("\\bin\\Release\\") || 
                         assemblyLocation.Contains("/bin/Release/");

// Read the MSBuild UseLocalProjects setting from the project
var projectDir = Path.GetDirectoryName(Environment.CurrentDirectory) ?? "";
var useLocalProjectsFromBuild = Environment.GetEnvironmentVariable("UseLocalProjects");
var assemblySize = new FileInfo(assemblyLocation).Length;

// Better detection logic - check multiple indicators
var isFromNuGet = false;
var nuGetIndicators = new List<string>();

// Check 1: Direct NuGet cache path
if (assemblyLocation.Contains("\\.nuget\\packages\\") || 
    assemblyLocation.Contains("/.nuget/packages/") ||
    assemblyLocation.Contains("\\packages\\mappilotegeopackagehelper\\") ||
    assemblyLocation.Contains("/packages/mappilotegeopackagehelper/"))
{
    isFromNuGet = true;
    nuGetIndicators.Add("Direct NuGet cache path");
}

// Check 2: Look for NuGet restore files in obj folder
var objFolder = Path.Combine(Environment.CurrentDirectory, "obj");
if (Directory.Exists(objFolder))
{
    var nugetRestoreFiles = Directory.GetFiles(objFolder, "*.nuget.*", SearchOption.AllDirectories);
    if (nugetRestoreFiles.Length > 0)
    {
        nuGetIndicators.Add($"NuGet restore files in obj ({nugetRestoreFiles.Length} files)");
        
        // Check if any restore file mentions our package
        foreach (var file in nugetRestoreFiles.Take(5)) // Check first 5 files
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains("MapPiloteGeopackageHelper", StringComparison.OrdinalIgnoreCase))
                {
                    isFromNuGet = true;
                    nuGetIndicators.Add($"Package found in {Path.GetFileName(file)}");
                    break;
                }
            }
            catch { /* ignore file read errors */ }
        }
    }
}

// Check 3: Look for .deps.json file which indicates NuGet resolution
var depsJsonPath = Path.Combine(Path.GetDirectoryName(assemblyLocation) ?? "", 
                                Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "") + ".deps.json");
if (File.Exists(depsJsonPath))
{
    try
    {
        var depsContent = File.ReadAllText(depsJsonPath);
        if (depsContent.Contains("MapPiloteGeopackageHelper", StringComparison.OrdinalIgnoreCase))
        {
            nuGetIndicators.Add("Found in .deps.json (NuGet dependency)");
            if (depsContent.Contains("\"type\": \"package\""))
            {
                isFromNuGet = true;
                nuGetIndicators.Add("Confirmed as package dependency in .deps.json");
            }
        }
    }
    catch { /* ignore file read errors */ }
}

Console.WriteLine($"   MSBuild UseLocalProjects: {useLocalProjectsFromBuild ?? "not set in environment"}");
Console.WriteLine($"   NuGet Detection Indicators: {string.Join(", ", nuGetIndicators)}");

if (isFromNuGet)
{
    Console.WriteLine("LIBRARY MODE: Using NUGET MapPiloteGeopackageHelper package");  
    Console.WriteLine("   Testing against published NuGet package from nuget.org");
}
else if (isInTestProjectBin)
{
    // When in test project bin, we need to check the build configuration
    // If UseLocalProjects=false in Directory.Build.props, it's likely NuGet even if copied
    var directoryBuildProps = Path.Combine(projectDir, "Directory.Build.props");
    bool useLocalFromProps = true; // default assumption
    
    if (File.Exists(directoryBuildProps))
    {
        var propsContent = File.ReadAllText(directoryBuildProps);
        if (propsContent.Contains("<UseLocalProjects") && propsContent.Contains(">false<"))
        {
            useLocalFromProps = false;
        }
    }
    
    if (!useLocalFromProps)
    {
        Console.WriteLine("LIBRARY MODE: Using NUGET MapPiloteGeopackageHelper package");  
        Console.WriteLine("   Testing against published NuGet package (copied to bin folder)");
        Console.WriteLine("   Assembly copied from NuGet cache to local bin during build");
    }
    else
    {
        Console.WriteLine("LIBRARY MODE: Using LOCAL MapPiloteGeopackageHelper");
        Console.WriteLine("   Testing against your development code");
    }
}
else
{
    Console.WriteLine("LIBRARY MODE: UNKNOWN - Please check manually");
    Console.WriteLine($"   Location: {assemblyLocation}");
}

// Show additional diagnostic info
Console.WriteLine($"   Diagnostic info:");
Console.WriteLine($"   - Is in NuGet cache: {isFromNuGet}");
Console.WriteLine($"   - Is in bin folder: {isInTestProjectBin}");
Console.WriteLine($"   - Assembly file size: {assemblySize / 1024.0:F1} KB");
Console.WriteLine();
#else
Console.WriteLine("LIBRARY MODE: Using NUGET MapPiloteGeopackageHelper package (Release build)");
Console.WriteLine();
#endif

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

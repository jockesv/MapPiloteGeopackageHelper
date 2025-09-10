# MapPiloteGeopackageHelper

Modern .NET library for creating, reading, and bulk-loading GeoPackage (GPKG) data using SQLite and NetTopologySuite.

## Quick Start - Modern Fluent API

```csharp
// Create/open GeoPackage with fluent API
using var geoPackage = await GeoPackage.OpenAsync("data.gpkg", srid: 3006);

// Create layer with schema
var layer = await geoPackage.EnsureLayerAsync("cities", new Dictionary<string, string>
{
    ["name"] = "TEXT",
    ["population"] = "INTEGER",
    ["area_km2"] = "REAL"
});

// Bulk insert with progress
var progress = new Progress<BulkProgress>(p => 
    Console.WriteLine($"Progress: {p.PercentComplete:F1}%"));

await layer.BulkInsertAsync(features, 
    new BulkInsertOptions(BatchSize: 1000, CreateSpatialIndex: true),
    progress);

// Query with async streaming
await foreach (var city in layer.ReadFeaturesAsync(
    new ReadOptions(WhereClause: "population > 100000", Limit: 10)))
{
    Console.WriteLine($"City: {city.Attributes["name"]} - {city.Attributes["population"]} people");
}

// Count and delete operations
var count = await layer.CountAsync("population < 50000");
var deleted = await layer.DeleteAsync("population < 10000");
```

## Modern Features

| Feature | Description | Example |
|---------|-------------|---------|
| **Async/Await** | Proper async support with CancellationToken | `await layer.BulkInsertAsync(...)` |
| **Fluent API** | Chain operations naturally | `GeoPackage.OpenAsync().EnsureLayerAsync()` |
| **Progress Reporting** | Track long-running operations | `IProgress<BulkProgress>` |
| **Options Objects** | Clean configuration, no parameter soup | `BulkInsertOptions(BatchSize: 1000)` |
| **Streaming** | `IAsyncEnumerable` for large datasets | `await foreach (var item in ...)` |
| **Rich Queries** | WHERE, LIMIT, ORDER BY support | `ReadOptions(WhereClause: "pop > 1000")` |
| **Conflict Handling** | Insert policies (Abort/Ignore/Replace) | `ConflictPolicy.Ignore` |
| **CRUD Operations** | Count, Delete with conditions | `await layer.DeleteAsync("status = 'old'")` |

## API Comparison

### Modern API (Recommended)
```csharp
// One-liner with progress and options
using var gp = await GeoPackage.OpenAsync("data.gpkg");
var layer = await gp.EnsureLayerAsync("places", schema);
await layer.BulkInsertAsync(features, options, progress);
```

### Traditional API (Still Supported)
```csharp
// Multi-step process
CMPGeopackageCreateHelper.CreateGeoPackage(path, srid);
GeopackageLayerCreateHelper.CreateGeopackageLayer(path, name, schema);
CGeopackageAddDataHelper.BulkInsertFeatures(path, name, features);
```

## Sample Projects

| Project | Purpose | API Style |
|---------|---------|-----------|
| **FluentApiExample** | Comprehensive modern API demo | Modern |
| **MapPiloteGeopackageHelperHelloWorld** | Step-by-step tutorial | Traditional |
| **MapPiloteGeopackageHelperSchemaBrowser** | Inspect unknown GeoPackages | Analysis |
| **BulkLoadPerformaceTester** | Performance comparison | Benchmarks |

## Reference Links (GeoPackage Specification)

- **GeoPackage Encoding Standard** - https://www.geopackage.org/spec/
- **OGC Standard page** - https://www.ogc.org/standard/geopackage/
- **Core tables (spec sections)**
  - gpkg_contents: https://www.geopackage.org/spec/#_contents
  - gpkg_spatial_ref_sys: https://www.geopackage.org/spec/#_spatial_ref_sys
  - gpkg_geometry_columns: https://www.geopackage.org/spec/#_geometry_columns
- **Binary geometry format** - https://www.geopackage.org/spec/#gpb_format

## What This Library Does

* Creates GeoPackages with required core tables  
* Creates layers (tables) with geometry + custom attribute columns  
* Bulk writes features with validation and progress tracking  
* Streams features back with filtering and paging  
* Modern async patterns with cancellation support  
* Schema inspection and validation  

## Getting Started

1. **Install**: `dotnet add package MapPiloteGeopackageHelper`
2. **Explore**: Check out `FluentApiExample` project 
3. **Inspect**: Use `MapPiloteGeopackageHelperSchemaBrowser` for unknown files
4. **Learn**: Traditional patterns in `MapPiloteGeopackageHelperHelloWorld`

Open the generated `.gpkg` files in QGIS, ArcGIS, or any GIS software!


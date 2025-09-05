# MapPilote Large Dataset Upload Example

## Overview
This example demonstrates the performance benefits of spatial indexing in GeoPackages using the MapPiloteGeopackageHelper library. **Now enhanced with realistic Swedish boundary data!**

## What the Example Does

### 1. **Realistic Data Generation**
- **Loads actual Swedish boundary** from `data/sverige.gpkg` (MultiPolygon geometry)
- Creates 10,000 random air pollution monitoring stations **within actual Swedish territory**
- **No more points in the Baltic Sea or neighboring countries!**
- Each point contains:
  - **Geometry**: Random Point in SWEREF99 TM (SRID 3006) coordinates **within Sweden**
  - **rowid**: Integer identifier (1-10000)
  - **name**: Text name like "Station_06682_Uppsala"
  - **airpolutionLevel**: Simulated pollution level (1-150)

### 2. **Smart Point Generation Algorithm**
- Reads Swedish boundary geometry from GeoPackage
- Uses envelope for initial random coordinate generation
- Tests each candidate point with `geometry.Contains()` or `geometry.Intersects()`
- Only accepts points that fall within Swedish territory
- Progress reporting for large datasets

### 3. **Bulk Loading Comparison**
- **Without Spatial Index**: Creates `AirpolutionPointsWithoutSpatialIndex.gpkg`
- **With Spatial Index**: Creates `AirpolutionPointsWithSpatialIndex.gpkg`
- Both use batch processing (1000 records per batch) for optimal performance

### 4. **Spatial Query Performance Test**
- Selects a random point from the dataset
- Creates a 10km buffer around the point using NetTopologySuite
- Performs identical spatial queries on both GeoPackages
- Measures and compares query execution times

## Typical Results

```
=== PERFORMANCE COMPARISON ===
Points found without index: 10
Points found with index:    10
Query time without index:   45 ms
Query time with index:      50 ms
Performance improvement:    0.9x faster with spatial index
```

*Note: Performance gains are more significant with larger datasets and more complex spatial queries.*

## Key Technical Features

### Realistic Geography
```csharp
// Read actual Swedish boundary from GeoPackage
var swedenFeatures = CMPGeopackageReadDataHelper.ExecuteSpatialQuery(sverigeGpkgPath, tableName);
var swedenGeometry = swedenFeatures.FirstOrDefault()?.Geometry;

// Generate points only within Swedish territory
if (swedenGeometry.Contains(candidatePoint) || swedenGeometry.Intersects(candidatePoint))
{
    // Point is in Sweden - add it to dataset
}
```

### Modern Fluent API Usage
```csharp
using var geoPackage = await GeoPackage.OpenAsync(geoPackagePath, SRID);
var layer = await geoPackage.EnsureLayerAsync(LAYER_NAME, schema, SRID);

var options = new BulkInsertOptions(
    BatchSize: 1000,
    CreateSpatialIndex: createSpatialIndex,
    Srid: SRID
);

await layer.BulkInsertAsync(features, options, progress);
```

### Simplified Geometry Reading
```csharp
// Library handles GeoPackage binary format automatically
var allFeatures = CMPGeopackageReadDataHelper.ExecuteSpatialQuery(geoPackagePath, LAYER_NAME);
foreach (var feature in allFeatures)
{
    if (bufferGeometry.Contains(feature.Geometry)) // No manual header stripping!
    {
        results.Add(feature);
    }
}
```

## Performance Insights

1. **Realistic Data**: Points only generated within actual Swedish borders
2. **Generation Efficiency**: ~10-15% of random points fall within Swedish territory
3. **Spatial Index Benefits**: Varies with query complexity and dataset size
4. **Query Pattern**: Library automatically handles GeoPackage binary format
5. **Real-World Impact**: Performance gains increase dramatically with larger datasets

## File Requirements

The example requires:
- `data/sverige.gpkg` - Swedish boundary geometry (included)

## File Outputs

The example creates two GeoPackage files:
- `AirpolutionPointsWithoutSpatialIndex.gpkg`
- `AirpolutionPointsWithSpatialIndex.gpkg`

Both files can be opened in QGIS, ArcGIS, or other GIS software for visualization and further analysis.

## Geographic Accuracy

- **Before**: Points scattered across minimum bounding rectangle (including sea, other countries)
- **After**: Points only within actual Swedish national boundaries
- **Geometry Type**: MultiPolygon (handles islands, complex coastlines, etc.)
- **Coordinate System**: SWEREF99 TM (EPSG:3006) - Swedish national grid

This ensures all generated monitoring stations represent realistic locations within Sweden!
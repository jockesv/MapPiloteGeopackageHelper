using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace MapPiloteGeopackageHelper
{
    // Standard DTO used by apps: attributes by name + optional geometry
    public sealed record FeatureRecord(
        Geometry? Geometry,
        IReadOnlyDictionary<string, string?> Attributes);
}

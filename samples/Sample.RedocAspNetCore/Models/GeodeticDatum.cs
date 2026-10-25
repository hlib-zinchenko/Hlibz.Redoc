using System.ComponentModel;

namespace Sample.RedocAspNetCore.Models;

public sealed record GeodeticDatum(
    [property: Description("Name of the geodetic datum, e.g. \"World Geodetic System 1984\".")] string Name,
    [property: Description("EPSG code identifying the datum.")] string EpsgCode);

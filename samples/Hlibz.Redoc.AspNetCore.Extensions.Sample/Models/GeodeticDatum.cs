using System.ComponentModel;

namespace Hlibz.Redoc.AspNetCore.Extensions.Sample.Models;

public sealed record GeodeticDatum(
    [property: Description("Name of the geodetic datum, e.g. \"World Geodetic System 1984\".")] string Name,
    [property: Description("EPSG code identifying the datum.")] string EpsgCode);

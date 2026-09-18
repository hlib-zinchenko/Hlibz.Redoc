using System.ComponentModel;

namespace Hlibz.Redoc.AspNetCore.Extensions.Sample.Models;

public sealed record CoordinatePrecision(
    [property: Description("Horizontal accuracy in meters.")] double HorizontalMeters,
    [property: Description("Vertical accuracy in meters.")] double VerticalMeters,
    [property: Description("The geodetic datum the coordinates are expressed in.")] GeodeticDatum Datum);

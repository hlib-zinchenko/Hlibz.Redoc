using System.ComponentModel;

namespace Hlibz.Redoc.AspNetCore.Extensions.Sample.Models;

public sealed record GeoCoordinates(
    [property: Description("Latitude in decimal degrees.")] double Latitude,
    [property: Description("Longitude in decimal degrees.")] double Longitude,
    [property: Description("How precisely these coordinates are known.")] CoordinatePrecision Precision);

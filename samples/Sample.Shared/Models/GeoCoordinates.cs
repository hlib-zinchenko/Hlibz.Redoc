using System.ComponentModel;

namespace Sample.Shared.Models;

public sealed record GeoCoordinates(
    [property: Description("Latitude in decimal degrees.")] double Latitude,
    [property: Description("Longitude in decimal degrees.")] double Longitude,
    [property: Description("How precisely these coordinates are known.")] CoordinatePrecision Precision);

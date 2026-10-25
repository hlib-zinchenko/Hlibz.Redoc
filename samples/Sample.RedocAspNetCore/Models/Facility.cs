using System.ComponentModel;

namespace Sample.RedocAspNetCore.Models;

public sealed record Facility(
    [property: Description("Name identifying this facility, e.g. a plant or plant campus.")] string Name,
    [property: Description("Physical address of the facility.")] Address Address,
    [property: Description("Geographic coordinates of the facility.")] GeoCoordinates Coordinates);

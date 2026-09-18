using System.ComponentModel;

namespace Hlibz.Redoc.AspNetCore.Extensions.Sample.Models;

public sealed record Facility(
    [property: Description("Name identifying this facility, e.g. a plant or plant campus.")] string Name,
    [property: Description("Physical address of the facility.")] Address Address,
    [property: Description("Geographic coordinates of the facility.")] GeoCoordinates Coordinates);

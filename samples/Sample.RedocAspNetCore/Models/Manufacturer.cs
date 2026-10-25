using System.ComponentModel;

namespace Sample.RedocAspNetCore.Models;

public sealed record Manufacturer(
    [property: Description("Legal name of the manufacturer.")] string Name,
    [property: Description("Country where the manufacturer is registered.")] string Country,
    [property: Description("The manufacturer's primary production site.")] Facility Headquarters);

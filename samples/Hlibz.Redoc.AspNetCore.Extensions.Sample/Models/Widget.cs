using System.ComponentModel;

namespace Hlibz.Redoc.AspNetCore.Extensions.Sample.Models;

public sealed record Widget(
    [property: Description("Unique identifier for the widget.")] Guid Id,
    [property: Description("Display name of the widget.")] string Name,
    [property: Description("Unit price in USD.")] decimal Price,
    [property: Description("Current lifecycle status of the widget.")] WidgetStatus Status,
    [property: Description("Warehouse the widget currently ships from, if it has been assigned one.")]
        Address? Warehouse,
    [property: Description("Free-form labels used for search and filtering.")]
        IReadOnlyCollection<string> Tags,
    [property: Description("The company that manufactures this widget.")]
        Manufacturer Manufacturer)
{
    public static IReadOnlyCollection<Widget> Sample { get; } =
    [
        new(
            Guid.Parse("f47f8f2e-6b6a-4b8a-9c3e-1a2b3c4d5e6f"),
            "Mechanical Keyboard",
            129.99m,
            WidgetStatus.Active,
            new Address("221B Baker Street", "London", "NW1 6XE", "UK"),
            ["electronics", "peripherals"],
            new Manufacturer(
                "Keytronic Foundry",
                "Taiwan",
                new Facility(
                    "Taipei Assembly Plant",
                    new Address("88 Xinyi Road", "Taipei", "110", "Taiwan"),
                    new GeoCoordinates(
                        25.0330,
                        121.5654,
                        new CoordinatePrecision(
                            1.5,
                            2.0,
                            new GeodeticDatum("World Geodetic System 1984", "EPSG:4326")))))),
        new(
            Guid.Parse("a1b2c3d4-e5f6-4789-9abc-def012345678"),
            "Standing Desk",
            349.00m,
            WidgetStatus.Draft,
            null,
            ["furniture"],
            new Manufacturer(
                "Uplift Frames",
                "USA",
                new Facility(
                    "Austin Fabrication Plant",
                    new Address("400 Congress Ave", "Austin", "78701", "USA"),
                    new GeoCoordinates(
                        30.2672,
                        -97.7431,
                        new CoordinatePrecision(
                            2.0,
                            3.0,
                            new GeodeticDatum("World Geodetic System 1984", "EPSG:4326")))))),
    ];
}

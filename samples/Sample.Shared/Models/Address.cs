using System.ComponentModel;

namespace Sample.Shared.Models;

public sealed record Address(
    [property: Description("Street name and number.")] string Street,
    [property: Description("City name.")] string City,
    [property: Description("Postal or ZIP code.")] string PostalCode,
    [property: Description("Country name.")] string Country);

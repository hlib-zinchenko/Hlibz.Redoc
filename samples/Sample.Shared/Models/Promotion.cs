using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sample.Shared.Models;

/// <summary>
/// A price promotion on a widget, in one of three kinds told apart by <c>kind</c>. Polymorphic on
/// purpose: ASP.NET Core describes it as an <c>anyOf</c> with a discriminator, which is what the
/// try-it console's variant picker is for.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PercentOffPromotion), "percentOff")]
[JsonDerivedType(typeof(FixedPricePromotion), "fixedPrice")]
[JsonDerivedType(typeof(BundlePromotion), "bundle")]
public abstract record Promotion(
    [property: Description("What shoppers see on the widget's page.")] string Title,
    [property: Description("The last day the promotion runs, if it ends.")] DateOnly? EndsOn);

/// <summary>A percentage off the list price.</summary>
public sealed record PercentOffPromotion(
    string Title,
    DateOnly? EndsOn,
    [property: Description("Percent off, from 1 to 99."), Range(1, 99)] int Percent)
    : Promotion(Title, EndsOn);

/// <summary>A fixed price for the duration.</summary>
public sealed record FixedPricePromotion(
    string Title,
    DateOnly? EndsOn,
    [property: Description("The promotional price in USD.")] decimal Price)
    : Promotion(Title, EndsOn);

/// <summary>A discount when bought together with other widgets.</summary>
public sealed record BundlePromotion(
    string Title,
    DateOnly? EndsOn,
    [property: Description("The other widgets in the bundle.")] IReadOnlyCollection<Guid> WithWidgets,
    [property: Description("Discount on the whole bundle, in USD.")] decimal Discount)
    : Promotion(Title, EndsOn);

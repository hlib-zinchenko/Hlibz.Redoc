using System.ComponentModel;

namespace Sample.Shared.Models;

/// <summary>How many units one region, or all of them, can ship.</summary>
public sealed record Availability(
    [property: Description("The region that answered, or \"all\" for every region added up.")] string Region,
    [property: Description("Units ready to ship.")] int Units);

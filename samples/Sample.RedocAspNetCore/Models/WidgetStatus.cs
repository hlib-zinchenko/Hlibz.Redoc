using System.ComponentModel;

namespace Sample.RedocAspNetCore.Models;

[Description("Lifecycle status of a widget.")]
public enum WidgetStatus
{
    Draft,
    Active,
    Discontinued,
}

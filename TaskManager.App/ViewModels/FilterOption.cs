namespace TaskManager.App.ViewModels;

/// <summary>
/// An item of the filter drop-down list: a label and a value ("All statuses" has an empty value).
/// <see cref="ToString"/> returns the label: it is what the Picker shows.
/// </summary>
public sealed record FilterOption<T>(string Title, T Value)
{
    public override string ToString() => Title;
}

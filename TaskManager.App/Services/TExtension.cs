namespace TaskManager.App.Services;

/// <summary>
/// Translation of a label right in XAML: <c>Text="{loc:T 'Новая задача'}"</c>. The key is the Russian text. The value is read when
/// the page is created; on a language change the window is rebuilt and the pages are created again in the new language.
/// </summary>
[ContentProperty(nameof(Key))]
public sealed class Extension : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

namespace TaskManager.App.Services;

/// <summary>
/// Перевод подписи прямо в XAML: <c>Text="{loc:T 'Новая задача'}"</c>. Ключ — русский текст. Значение читается при
/// создании страницы; при смене языка окно пересобирается, и страницы создаются заново на новом языке.
/// </summary>
[ContentProperty(nameof(Key))]
public sealed class TExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

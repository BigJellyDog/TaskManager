namespace TaskManager.App.ViewModels;

/// <summary>
/// Пункт выпадающего списка фильтра: подпись и значение ("Все статусы" имеет пустое значение).
/// <see cref="ToString"/> возвращает подпись: именно её показывает Picker.
/// </summary>
public sealed record FilterOption<T>(string Title, T Value)
{
    public override string ToString() => Title;
}

using System.Collections;

namespace TaskManager.App.Controls;

/// <summary>Колонка доски задач (один статус): заголовок, счётчик и карточки.</summary>
public partial class BoardColumn : ContentView
{
    public static readonly BindableProperty ColumnTitleProperty =
        BindableProperty.Create(nameof(ColumnTitle), typeof(string), typeof(BoardColumn), string.Empty);

    public static readonly BindableProperty ColumnIconProperty =
        BindableProperty.Create(nameof(ColumnIcon), typeof(string), typeof(BoardColumn), string.Empty);

    public static readonly BindableProperty CountProperty =
        BindableProperty.Create(nameof(Count), typeof(int), typeof(BoardColumn), 0);

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(BoardColumn), null);

    public BoardColumn()
    {
        InitializeComponent();
    }

    public string ColumnTitle
    {
        get => (string)GetValue(ColumnTitleProperty);
        set => SetValue(ColumnTitleProperty, value);
    }

    public string ColumnIcon
    {
        get => (string)GetValue(ColumnIconProperty);
        set => SetValue(ColumnIconProperty, value);
    }

    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
}

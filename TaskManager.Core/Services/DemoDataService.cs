using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Querying;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Services;

/// <summary>
/// Демо-данные для знакомства с программой и для скриншотов. Всё создаётся обычными сервисами (поэтому история,
/// автоблокировки и повторения настоящие), а время сдвигается подставными часами: "20 дней назад" создаётся задача,
/// "15 дней назад" она завершается. Все демо-задачи отмечены тегом <see cref="DemoTag"/>.
/// Тексты написаны по-русски; чтобы создать набор на другом языке, в <see cref="SeedAsync"/> передаётся функция перевода.
/// </summary>
public class DemoDataService
{
    public const string DemoTag = "демо";

    /// <summary>Названия демо-тега на всех языках приложения: набор, созданный на одном языке, находится и удаляется на другом.</summary>
    public static readonly string[] KnownDemoTags = [DemoTag, "demo"];

    private readonly ITaskRepository _tasks;
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public DemoDataService(ITaskRepository tasks, ITagRepository tags, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _tasks = tasks;
        _tags = tags;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Есть ли уже (не удалённые) демо-задачи.</summary>
    public async Task<bool> HasDemoDataAsync(CancellationToken cancellationToken = default)
    {
        foreach (var tag in KnownDemoTags)
        {
            var page = await RealService().SearchAsync(new TaskFilter(Tag: tag), pageSize: 1, cancellationToken: cancellationToken);
            if (page.TotalCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Удаляет (в корзину) все демо-задачи. Возвращает, сколько задач удалено.</summary>
    public async Task<int> RemoveDemoDataAsync(CancellationToken cancellationToken = default)
    {
        var service = RealService();
        var found = new List<TaskItem>();
        foreach (var tag in KnownDemoTags)
        {
            var demo = await service.SearchAsync(
                new TaskFilter(Tag: tag),
                pageSize: TaskQuery.MaxPageSize,
                cancellationToken: cancellationToken);
            found.AddRange(demo.Items);
        }

        var removed = 0;
        foreach (var task in found.DistinctBy(t => t.Id).OrderBy(t => t.Id))
        {
            try
            {
                // Удаление родителя удаляет и его подзадачи: к тому моменту, когда очередь дойдёт до них, они уже удалены.
                await service.DeleteAsync(task.Id, cancellationToken: cancellationToken);
                removed++;
            }
            catch (TaskNotFoundException)
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Создаёт демо-набор. <paramref name="translate"/> переводит русский текст (название, описание, тег) на язык
    /// интерфейса; без неё набор получается русским. Возвращает, сколько задач создано (повторы и копии не считаются).
    /// </summary>
    public async Task<int> SeedAsync(Func<string, string>? translate = null, CancellationToken cancellationToken = default)
    {
        string T(string text) => translate?.Invoke(text) ?? text;

        var now = _clock.GetUtcNow();
        var zone = _clock.LocalTimeZone;
        var clock = new SimulatedClock(_clock);
        var service = new TaskService(_tasks, _tags, _unitOfWork, clock);

        var ids = new Dictionary<string, int>();
        var steps = new List<(DateTimeOffset At, int Order, Func<Task> Run)>();

        // Момент "через days дней в hour:minute по местному времени" (отрицательные дни означают прошлое).
        DateTimeOffset At(int days, int hour, int minute = 0)
        {
            var local = TimeZoneInfo.ConvertTime(now, zone).Date.AddDays(days).AddHours(hour).AddMinutes(minute);
            if (zone.IsInvalidTime(local))
            {
                local = local.AddHours(1);
            }

            return new DateTimeOffset(local, zone.GetUtcOffset(local));
        }

        void Step(DateTimeOffset at, Func<Task> run) => steps.Add((at, steps.Count, run));

        void Create(
            DateTimeOffset at,
            string key,
            string title,
            Priority priority,
            DateTimeOffset? deadline,
            string[] tags,
            string? description = null,
            string? parent = null,
            RecurrenceRule? rule = null) =>
            Step(at, async () =>
            {
                var request = new CreateTaskRequest(
                    T(title),
                    priority,
                    description is null ? null : T(description),
                    deadline,
                    [T(DemoTag), .. tags.Select(T)],
                    rule,
                    parent is null ? null : ids[parent]);
                ids[key] = (await service.CreateAsync(request, cancellationToken)).Id;
            });

        void Move(DateTimeOffset at, string key, TaskStatus status) =>
            Step(at, async () => await service.ChangeStatusAsync(ids[key], status, cancellationToken: cancellationToken));

        void Depend(DateTimeOffset at, string task, string on) =>
            Step(at, async () => await service.AddDependencyAsync(ids[task], ids[on], cancellationToken));

        // ---- Прошлое: выполненные, отменённая и просроченные задачи, чтобы статистика и история были живыми ----
        Create(At(-20, 9), "ci", "Настроить CI", Priority.Medium, At(-14, 18), ["работа", "разработка"]);
        Move(At(-19, 10), "ci", TaskStatus.InProgress);
        Move(At(-15, 16), "ci", TaskStatus.Done);

        Create(At(-12, 9), "resume", "Обновить резюме", Priority.Low, At(-8, 18), ["карьера"]);
        Move(At(-10, 11), "resume", TaskStatus.InProgress);
        Move(At(-9, 20), "resume", TaskStatus.Done);

        Create(At(-9, 9), "mail", "Разобрать почту", Priority.Low, At(-6, 18), []);
        Move(At(-5, 15), "mail", TaskStatus.InProgress);
        Move(At(-4, 10), "mail", TaskStatus.Done);

        Create(At(-7, 9), "slides", "Подготовить презентацию", Priority.High, At(-3, 12), ["работа"]);
        Move(At(-4, 14), "slides", TaskStatus.InProgress);
        Move(At(-3, 9), "slides", TaskStatus.Done);

        Create(At(-6, 9), "gym", "Записаться в спортзал", Priority.Low, null, ["здоровье"]);
        Move(At(-5, 19), "gym", TaskStatus.InProgress);
        Move(At(-5, 20), "gym", TaskStatus.Done);

        // Повторяющаяся: завершили вчера, и сервис сам создал следующую копию на сегодняшний вечер.
        Create(At(-3, 9), "flowers", "Полить цветы", Priority.Low, At(-1, 20), ["дом"], rule: new RecurrenceRule(RecurrenceFrequency.Daily, 1, RecurrenceMode.AfterCompletion));
        Move(At(-1, 19), "flowers", TaskStatus.InProgress);
        Move(At(-1, 19, 30), "flowers", TaskStatus.Done);

        Create(At(-2, 9), "report", "Сдать отчёт за октябрь", Priority.High, At(-1, 18), ["работа", "финансы"], "Свести расходы и отправить бухгалтеру");
        Move(At(-1, 10), "report", TaskStatus.InProgress);

        Create(At(-3, 12), "bank", "Позвонить в банк", Priority.Medium, At(-2, 17), ["деньги"]);

        Create(At(-2, 12), "idea", "Идея: приложение для рецептов", Priority.Low, null, ["идеи"]);
        Move(At(-1, 9), "idea", TaskStatus.Cancelled);

        // ---- Сейчас: проект с подзадачами трёх уровней и зависимостями, ближайшие дедлайны, повторения ----
        Create(now, "release", "Подготовить релиз 1.0", Priority.Critical, At(2, 18), ["работа", "релиз"], "Финальная сборка, проверка и публикация");
        Create(now, "tests", "Написать тесты", Priority.High, At(1, 12), ["работа", "разработка"], parent: "release");
        Create(now, "tests-services", "Тесты сервисов", Priority.Medium, null, ["разработка"], parent: "tests");
        Create(now, "tests-repos", "Тесты репозиториев", Priority.Medium, At(1, 11), ["разработка"], parent: "tests");
        Create(now, "docs", "Обновить документацию", Priority.Medium, At(3, 17), ["работа", "документация"], parent: "release");
        Create(now, "installer", "Собрать установщик", Priority.High, At(2, 12), ["работа", "релиз"], parent: "release");
        Create(now, "publish", "Выложить релиз на GitHub", Priority.Medium, At(2, 17), ["работа", "релиз"], parent: "release");
        Depend(now, "installer", "tests");
        Depend(now, "publish", "installer");
        Move(now, "tests-services", TaskStatus.InProgress);
        Move(now, "tests-services", TaskStatus.Done);
        Move(now, "tests", TaskStatus.InProgress);
        Move(now, "release", TaskStatus.InProgress);

        Create(now, "rent", "Оплатить аренду", Priority.High, now.AddHours(6), ["дом", "деньги"], rule: new RecurrenceRule(RecurrenceFrequency.Monthly, 1, RecurrenceMode.AfterCompletion));
        Create(now, "groceries", "Купить продукты", Priority.Low, now.AddHours(3), ["дом", "покупки"], "Молоко, хлеб, яйца, овощи");
        Create(now, "doctor", "Записаться к врачу", Priority.Medium, now.AddHours(20), ["здоровье"]);

        // Планёрка: следующий понедельник 10:00, повтор по календарю каждую неделю.
        var daysToMonday = (((int)DayOfWeek.Monday - (int)TimeZoneInfo.ConvertTime(now, zone).DayOfWeek) + 7) % 7;
        daysToMonday = daysToMonday == 0 ? 7 : daysToMonday;
        Create(now, "standup", "Планёрка команды", Priority.Medium, At(daysToMonday, 10), ["работа"], rule: new RecurrenceRule(RecurrenceFrequency.Weekly, 1, RecurrenceMode.ByCalendar, WeekDays.Monday));

        Create(now, "tax", "Подготовить налоговую декларацию", Priority.High, At(9, 18), ["финансы"]);
        Create(now, "tap", "Починить кран", Priority.Medium, At(4, 18), ["дом"]);
        Create(now, "maui", "Изучить MAUI", Priority.Medium, At(14, 18), ["обучение", "разработка"]);
        Create(now, "vacation", "Запланировать отпуск", Priority.Low, At(30, 12), ["отпуск"]);
        Create(now, "book", "Прочитать «Чистый код»", Priority.Low, null, ["обучение"]);
        Move(now, "book", TaskStatus.InProgress);

        foreach (var step in steps.OrderBy(s => s.At).ThenBy(s => s.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            clock.Set(step.At);
            await step.Run();
        }

        clock.Set(now);
        return ids.Count;
    }

    private TaskService RealService() => new(_tasks, _tags, _unitOfWork, _clock);

    /// <summary>Часы, которые можно переставлять: нужны, чтобы создавать задачи "в прошлом" через обычный сервис.</summary>
    private sealed class SimulatedClock : TimeProvider
    {
        private readonly TimeProvider _real;
        private DateTimeOffset _now;

        public SimulatedClock(TimeProvider real)
        {
            _real = real;
            _now = real.GetUtcNow();
        }

        public override TimeZoneInfo LocalTimeZone => _real.LocalTimeZone;

        public void Set(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}

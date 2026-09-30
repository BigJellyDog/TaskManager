using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Tests;

public class TaskItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);

    private TaskItem NewTask() => TaskItem.Create("Написать тесты", Priority.Medium, _clock);

    // ---- Creation ----

    [Fact]
    public void Create_ValidData_SetsFields()
    {
        var deadline = Now.AddDays(2);

        var task = TaskItem.Create("  Задеплоить  ", Priority.High, _clock, "  описание ", deadline);

        Assert.Equal("Задеплоить", task.Title);
        Assert.Equal("описание", task.Description);
        Assert.Equal(Priority.High, task.Priority);
        Assert.Equal(TaskStatus.Todo, task.Status);
        Assert.Equal(deadline, task.Deadline);
        Assert.Equal(Now, task.CreatedAt);
        Assert.Null(task.CompletedAt);
        Assert.False(task.IsDeleted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyTitle_Throws(string? title)
    {
        Assert.Throws<DomainValidationException>(() => TaskItem.Create(title!, Priority.Low, _clock));
    }

    [Fact]
    public void Create_TitleOf200Chars_IsAllowed()
    {
        var task = TaskItem.Create(new string('a', 200), Priority.Low, _clock);

        Assert.Equal(200, task.Title.Length);
    }

    [Fact]
    public void Create_TitleOf201Chars_Throws()
    {
        Assert.Throws<DomainValidationException>(() => TaskItem.Create(new string('a', 201), Priority.Low, _clock));
    }

    [Fact]
    public void Create_DeadlineInPast_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => TaskItem.Create("t", Priority.Low, _clock, deadline: Now.AddMinutes(-1)));
    }

    [Fact]
    public void Create_DeadlineEqualToNow_IsAllowed()
    {
        var task = TaskItem.Create("t", Priority.Low, _clock, deadline: Now);

        Assert.Equal(Now, task.Deadline);
    }

    [Fact]
    public void Create_UnknownPriority_Throws()
    {
        Assert.Throws<DomainValidationException>(() => TaskItem.Create("t", (Priority)99, _clock));
    }

    [Fact]
    public void Create_BlankDescription_BecomesNull()
    {
        var task = TaskItem.Create("t", Priority.Low, _clock, "   ");

        Assert.Null(task.Description);
    }

    // ---- Editing ----

    [Fact]
    public void SetTitle_Empty_Throws()
    {
        var task = NewTask();

        Assert.Throws<DomainValidationException>(() => task.SetTitle(" "));
        Assert.Equal("Написать тесты", task.Title);
    }

    [Fact]
    public void SetDeadline_PastDate_IsAllowedOnExistingTask()
    {
        var task = NewTask();

        task.SetDeadline(Now.AddDays(-1));

        Assert.Equal(Now.AddDays(-1), task.Deadline);
    }

    [Fact]
    public void Edit_ChangesFields()
    {
        var task = NewTask();

        task.SetTitle("Новое название");
        task.SetDescription("текст");
        task.SetPriority(Priority.Critical);

        Assert.Equal("Новое название", task.Title);
        Assert.Equal("текст", task.Description);
        Assert.Equal(Priority.Critical, task.Priority);
    }

    // ---- Statuses ----

    [Fact]
    public void ChangeStatus_ToDone_SetsCompletedAt()
    {
        var task = NewTask();
        task.ChangeStatus(TaskStatus.InProgress, _clock);
        _clock.Advance(TimeSpan.FromHours(3));

        task.ChangeStatus(TaskStatus.Done, _clock);

        Assert.Equal(TaskStatus.Done, task.Status);
        Assert.Equal(Now.AddHours(3), task.CompletedAt);
    }

    [Fact]
    public void ChangeStatus_ForbiddenTransition_ThrowsAndKeepsStatus()
    {
        var task = NewTask();

        Assert.Throws<InvalidStatusTransitionException>(() => task.ChangeStatus(TaskStatus.Done, _clock));
        Assert.Equal(TaskStatus.Todo, task.Status);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Reopen_DoneTask_BecomesInProgressAndClearsCompletedAt()
    {
        var task = NewTask();
        task.ChangeStatus(TaskStatus.InProgress, _clock);
        task.ChangeStatus(TaskStatus.Done, _clock);

        task.Reopen();

        Assert.Equal(TaskStatus.InProgress, task.Status);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Reopen_CancelledTask_BecomesTodo()
    {
        var task = NewTask();
        task.ChangeStatus(TaskStatus.Cancelled, _clock);

        task.Reopen();

        Assert.Equal(TaskStatus.Todo, task.Status);
    }

    [Fact]
    public void Reopen_OpenTask_Throws()
    {
        Assert.Throws<InvalidStatusTransitionException>(() => NewTask().Reopen());
    }

    // ---- Deletion ----

    [Fact]
    public void Delete_ThenRestore_TogglesFlag()
    {
        var task = NewTask();

        task.Delete();
        Assert.True(task.IsDeleted);

        task.Restore();
        Assert.False(task.IsDeleted);
    }

    [Fact]
    public void Delete_AlreadyDeleted_Throws()
    {
        var task = NewTask();
        task.Delete();

        Assert.Throws<DomainValidationException>(task.Delete);
    }

    [Fact]
    public void Restore_NotDeleted_Throws()
    {
        Assert.Throws<DomainValidationException>(NewTask().Restore);
    }

    [Fact]
    public void DeletedTask_CannotBeModified()
    {
        var task = NewTask();
        task.Delete();

        Assert.Throws<DomainValidationException>(() => task.SetTitle("x"));
        Assert.Throws<DomainValidationException>(() => task.ChangeStatus(TaskStatus.InProgress, _clock));
        Assert.Throws<DomainValidationException>(task.Reopen);
    }

    // ---- Tags ----

    [Fact]
    public void AddTag_SameNameTwice_AddsOnce()
    {
        var task = NewTask();

        task.AddTag(new Tag("работа"));
        task.AddTag(new Tag("РАБОТА"));

        Assert.Single(task.Tags);
    }

    [Fact]
    public void RemoveTag_RemovesIt()
    {
        var task = NewTask();
        var tag = new Tag("работа");
        task.AddTag(tag);

        task.RemoveTag(tag);

        Assert.Empty(task.Tags);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Tag_EmptyName_Throws(string name)
    {
        Assert.Throws<DomainValidationException>(() => new Tag(name));
    }

    [Fact]
    public void Tag_TooLongName_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Tag(new string('a', 51)));
    }

    [Fact]
    public void Tag_Rename_TrimsAndValidates()
    {
        var tag = new Tag("work");

        tag.Rename("  job ");

        Assert.Equal("job", tag.Name);
        Assert.Throws<DomainValidationException>(() => tag.Rename(""));
    }

    // ---- The recurrence rule ----

    [Fact]
    public void RecurrenceRule_IntervalBelowOne_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => new RecurrenceRule(RecurrenceFrequency.Daily, 0, RecurrenceMode.AfterCompletion));
    }

    [Fact]
    public void RecurrenceRule_WeeklyWithoutDays_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => new RecurrenceRule(RecurrenceFrequency.Weekly, 1, RecurrenceMode.ByCalendar));
    }

    [Fact]
    public void RecurrenceRule_DaysForNonWeekly_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => new RecurrenceRule(RecurrenceFrequency.Monthly, 1, RecurrenceMode.ByCalendar, WeekDays.Monday));
    }

    [Fact]
    public void RecurrenceRule_WeeklyWithDays_IsValid()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Weekly, 1, RecurrenceMode.ByCalendar, WeekDays.Monday | WeekDays.Friday);

        Assert.Equal(WeekDays.Monday | WeekDays.Friday, rule.WeekDays);
    }
}

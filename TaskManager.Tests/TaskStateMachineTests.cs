using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Tests;

public class TaskStateMachineTests
{
    private static readonly (TaskStatus From, TaskStatus To)[] Allowed =
    [
        (TaskStatus.Todo, TaskStatus.InProgress),
        (TaskStatus.Todo, TaskStatus.Blocked),
        (TaskStatus.Todo, TaskStatus.Cancelled),
        (TaskStatus.InProgress, TaskStatus.Todo),
        (TaskStatus.InProgress, TaskStatus.Blocked),
        (TaskStatus.InProgress, TaskStatus.Done),
        (TaskStatus.InProgress, TaskStatus.Cancelled),
        (TaskStatus.Blocked, TaskStatus.Todo),
        (TaskStatus.Blocked, TaskStatus.Cancelled),
    ];

    public static TheoryData<TaskStatus, TaskStatus> AllowedTransitions
    {
        get
        {
            var data = new TheoryData<TaskStatus, TaskStatus>();
            foreach (var (from, to) in Allowed)
            {
                data.Add(from, to);
            }

            return data;
        }
    }

    // All the other pairs out of 25, including the transition to the same status.
    public static TheoryData<TaskStatus, TaskStatus> ForbiddenTransitions
    {
        get
        {
            var data = new TheoryData<TaskStatus, TaskStatus>();
            foreach (var from in Enum.GetValues<TaskStatus>())
            {
                foreach (var to in Enum.GetValues<TaskStatus>())
                {
                    if (!Allowed.Contains((from, to)))
                    {
                        data.Add(from, to);
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void EnsureTransition_AllowedPair_DoesNotThrow(TaskStatus from, TaskStatus to)
    {
        Assert.True(TaskStateMachine.CanTransition(from, to));
        TaskStateMachine.EnsureTransition(from, to);
    }

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void EnsureTransition_ForbiddenPair_Throws(TaskStatus from, TaskStatus to)
    {
        Assert.False(TaskStateMachine.CanTransition(from, to));

        var ex = Assert.Throws<InvalidStatusTransitionException>(() => TaskStateMachine.EnsureTransition(from, to));
        Assert.Equal(from, ex.From);
        Assert.Equal(to, ex.To);
    }

    [Fact]
    public void EnsureTransition_CancelledToDone_Throws()
    {
        Assert.Throws<InvalidStatusTransitionException>(
            () => TaskStateMachine.EnsureTransition(TaskStatus.Cancelled, TaskStatus.Done));
    }

    [Fact]
    public void EnsureTransition_DoneToInProgress_SuggestsReopen()
    {
        var ex = Assert.Throws<InvalidStatusTransitionException>(
            () => TaskStateMachine.EnsureTransition(TaskStatus.Done, TaskStatus.InProgress));

        Assert.Contains("переоткрыть", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TaskStatus.Done, TaskStatus.InProgress)]
    [InlineData(TaskStatus.Cancelled, TaskStatus.Todo)]
    public void EnsureReopen_DoneOrCancelled_ReturnsTarget(TaskStatus from, TaskStatus expected)
    {
        Assert.Equal(expected, TaskStateMachine.EnsureReopen(from));
    }

    [Theory]
    [InlineData(TaskStatus.Todo)]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Blocked)]
    public void EnsureReopen_OpenTask_Throws(TaskStatus from)
    {
        Assert.Throws<InvalidStatusTransitionException>(() => TaskStateMachine.EnsureReopen(from));
    }

    [Fact]
    public void GetAllowedTransitions_MatchesTable()
    {
        foreach (var status in Enum.GetValues<TaskStatus>())
        {
            var expected = Allowed.Where(p => p.From == status).Select(p => p.To).Order();
            Assert.Equal(expected, TaskStateMachine.GetAllowedTransitions(status).Order());
        }
    }
}

namespace TaskManager.Core.Domain;

/// <summary>The task priority. The numbers grow with importance, so sorting by value works as it should.</summary>
public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

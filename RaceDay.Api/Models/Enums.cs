namespace RaceDay.Api.Models;

/// <summary>The two roles a RaceDay user can hold (Users.Role).</summary>
public enum UserRole
{
    Organiser = 1,
    Participant = 2
}

/// <summary>The kind of event (Events.EventType, a Part 2 addition to the Part 1 schema).</summary>
public enum EventType
{
    Run = 1,
    Walk = 2,
    Cycle = 3
}

/// <summary>Enrolments.Status.</summary>
public enum EnrolmentStatus
{
    Confirmed = 1,
    Cancelled = 2,
    Pending = 3
}

/// <summary>Results.Status.</summary>
public enum ResultStatus
{
    Finished = 1,
    DNF = 2,
    DSQ = 3
}

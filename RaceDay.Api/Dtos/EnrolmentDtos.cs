using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string ParticipantEmail { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string? BibNumber { get; set; }
    public EnrolmentStatus Status { get; set; }

    public static readonly Expression<Func<Enrolment, EnrolmentResponse>> Projection = e => new EnrolmentResponse
    {
        EnrolmentId = e.EnrolmentId,
        ParticipantId = e.ParticipantId,
        ParticipantName = e.Participant.FirstName + " " + e.Participant.LastName,
        ParticipantEmail = e.Participant.Email,
        CategoryId = e.CategoryId,
        CategoryName = e.Category.CategoryName,
        EventId = e.Category.EventId,
        EventName = e.Category.Event.EventName,
        EventDate = e.Category.Event.EventDate,
        EnrolmentDate = e.EnrolmentDate,
        BibNumber = e.BibNumber,
        Status = e.Status
    };
}

/// <summary>Result details (Part 1 plan: finishTime, position, status).</summary>
public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss (greater than zero, under 24 hours). Required when status is Finished.</summary>
    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position within the category (1 = first). Required when status is Finished.</summary>
    [Range(1, 1000000)]
    public int? Position { get; set; }

    /// <summary>Finished, DNF or DSQ. Defaults to Finished.</summary>
    public ResultStatus? Status { get; set; }
}

public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string? BibNumber { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public ResultStatus Status { get; set; }

    public static readonly Expression<Func<EventResult, ResultResponse>> Projection = r => new ResultResponse
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        ParticipantId = r.Enrolment.ParticipantId,
        ParticipantName = r.Enrolment.Participant.FirstName + " " + r.Enrolment.Participant.LastName,
        BibNumber = r.Enrolment.BibNumber,
        EventId = r.Enrolment.Category.EventId,
        EventName = r.Enrolment.Category.Event.EventName,
        EventDate = r.Enrolment.Category.Event.EventDate,
        CategoryId = r.Enrolment.CategoryId,
        CategoryName = r.Enrolment.Category.CategoryName,
        FinishTime = r.FinishTime,
        Position = r.Position,
        Status = r.Status
    };
}

namespace Buckeye.Lending.Api.Dtos;

public record AddToQueueRequest
{
    public int LoanApplicationId { get; init; }
    public int Priority { get; init; } = 3;
}

public record UpdateItemRequest
{
    public int Priority { get; init; }
    public string? Notes { get; init; }
}

public record ReviewQueueDto
{
    public int Id { get; init; }
    public string OfficerId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<ReviewItemDto> Items { get; init; } = [];
}

public record ReviewItemDto
{
    public int Id { get; init; }
    public int QueueId { get; init; }
    public int LoanApplicationId { get; init; }
    public int Priority { get; init; }
    public string? Notes { get; init; }
    public LoanApplicationSummaryDto? LoanApplication { get; init; }
}

public record LoanApplicationSummaryDto
{
    public int Id { get; init; }
    public string ApplicantName { get; init; } = string.Empty;
    public decimal LoanAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public int RiskRating { get; init; }
}

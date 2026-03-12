using Buckeye.Lending.Api.Data;
using Buckeye.Lending.Api.Dtos;
using Buckeye.Lending.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Buckeye.Lending.Api.Controllers;

[ApiController]
[Route("api/review-queue")]
public class ReviewQueueController : ControllerBase
{
    private readonly LendingContext _context;
    private const string CurrentOfficerId = "default-officer";

    public ReviewQueueController(LendingContext context)
    {
        _context = context;
    }

    /// <summary>Gets the current officer's review queue.</summary>
    [HttpGet]
    public async Task<ActionResult<ReviewQueueDto>> GetQueue()
    {
        var getQueueByOfficerQuery =
            from reviewQueueEntity in _context.ReviewQueues
            where reviewQueueEntity.OfficerId == CurrentOfficerId
            select reviewQueueEntity;

        var queue = await getQueueByOfficerQuery
            .Include(q => q.Items)
            .ThenInclude(i => i.LoanApplication)
            .FirstOrDefaultAsync();

        if (queue == null)
            return NotFound();

        return Ok(MapQueue(queue));
    }

    /// <summary>Adds a loan application to the queue using upsert semantics.</summary>
    [HttpPost]
    public async Task<ActionResult<ReviewItemDto>> AddToQueue(AddToQueueRequest request)
    {
        if (request.Priority < 1 || request.Priority > 5)
            return BadRequest("Priority must be between 1 and 5.");

        var loanApplicationByIdQuery =
            from loanApplicationEntity in _context.LoanApplications
            where loanApplicationEntity.Id == request.LoanApplicationId
            select loanApplicationEntity;

        var loanApplication = await loanApplicationByIdQuery.FirstOrDefaultAsync();
        if (loanApplication == null)
            return BadRequest($"Loan application {request.LoanApplicationId} not found.");

        var addQueueByOfficerQuery =
            from reviewQueueForAddEntity in _context.ReviewQueues
            where reviewQueueForAddEntity.OfficerId == CurrentOfficerId
            select reviewQueueForAddEntity;

        var queue = await addQueueByOfficerQuery
            .Include(q => q.Items)
            .FirstOrDefaultAsync();

        if (queue == null)
        {
            queue = new ReviewQueue
            {
                OfficerId = CurrentOfficerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ReviewQueues.Add(queue);
        }

        var existingItem = (
            from queuedItemEntity in queue.Items
            where queuedItemEntity.LoanApplicationId == request.LoanApplicationId
            select queuedItemEntity
        ).FirstOrDefault();

        if (existingItem != null)
        {
            existingItem.Priority = request.Priority;
            queue.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            queue.Items.Add(new ReviewItem
            {
                LoanApplicationId = request.LoanApplicationId,
                Priority = request.Priority
            });
            queue.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var savedItemByQueueAndLoanAppQuery =
            from savedReviewItemEntity in _context.ReviewItems
            where savedReviewItemEntity.QueueId == queue.Id && savedReviewItemEntity.LoanApplicationId == request.LoanApplicationId
            select savedReviewItemEntity;

        var savedItem = await savedItemByQueueAndLoanAppQuery
            .Include(i => i.LoanApplication)
            .FirstAsync();

        return CreatedAtAction(nameof(GetQueue), MapItem(savedItem));
    }

    /// <summary>Updates one queue item by ID.</summary>
    [HttpPut("{itemId:int}")]
    public async Task<ActionResult<ReviewItemDto>> UpdateItem(int itemId, UpdateItemRequest request)
    {
        if (request.Priority.HasValue && (request.Priority.Value < 1 || request.Priority.Value > 5))
            return BadRequest("Priority must be between 1 and 5.");

        var updateItemByIdForOfficerQuery =
            from reviewItemToUpdateEntity in _context.ReviewItems
            join reviewQueueForUpdateEntity in _context.ReviewQueues on reviewItemToUpdateEntity.QueueId equals reviewQueueForUpdateEntity.Id
            where reviewItemToUpdateEntity.Id == itemId && reviewQueueForUpdateEntity.OfficerId == CurrentOfficerId
            select reviewItemToUpdateEntity;

        var item = await updateItemByIdForOfficerQuery
            .Include(i => i.LoanApplication)
            .FirstOrDefaultAsync();

        if (item == null)
            return NotFound();

        if (request.Priority.HasValue)
            item.Priority = request.Priority.Value;

        if (request.Notes is not null)
            item.Notes = request.Notes;

        var updateQueueTimestampByIdQuery =
            from queueForTimestampUpdateEntity in _context.ReviewQueues
            where queueForTimestampUpdateEntity.Id == item.QueueId
            select queueForTimestampUpdateEntity;

        var queue = await updateQueueTimestampByIdQuery.FirstAsync();
        queue.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(MapItem(item));
    }

    /// <summary>Removes one queue item by ID.</summary>
    [HttpDelete("{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int itemId)
    {
        var removeItemByIdForOfficerQuery =
            from reviewItemToRemoveEntity in _context.ReviewItems
            join reviewQueueForRemoveEntity in _context.ReviewQueues on reviewItemToRemoveEntity.QueueId equals reviewQueueForRemoveEntity.Id
            where reviewItemToRemoveEntity.Id == itemId && reviewQueueForRemoveEntity.OfficerId == CurrentOfficerId
            select reviewItemToRemoveEntity;

        var item = await removeItemByIdForOfficerQuery.FirstOrDefaultAsync();
        if (item == null)
            return NotFound();

        var removeQueueByIdQuery =
            from queueForRemoveUpdateEntity in _context.ReviewQueues
            where queueForRemoveUpdateEntity.Id == item.QueueId
            select queueForRemoveUpdateEntity;

        var queueToUpdate = await removeQueueByIdQuery.FirstAsync();
        queueToUpdate.UpdatedAt = DateTime.UtcNow;

        _context.ReviewItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Clears all items from the current officer's queue.</summary>
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearQueue()
    {
        var clearQueueByOfficerQuery =
            from queueForClearEntity in _context.ReviewQueues
            where queueForClearEntity.OfficerId == CurrentOfficerId
            select queueForClearEntity;

        var queue = await clearQueueByOfficerQuery
            .Include(q => q.Items)
            .FirstOrDefaultAsync();

        if (queue == null)
            return NotFound();

        if (queue.Items.Count > 0)
            _context.ReviewItems.RemoveRange(queue.Items);

        queue.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static ReviewQueueDto MapQueue(ReviewQueue queue)
    {
        return new ReviewQueueDto
        {
            Id = queue.Id,
            OfficerId = queue.OfficerId,
            CreatedAt = queue.CreatedAt,
            UpdatedAt = queue.UpdatedAt,
            Items = (
                from queueItemForMapEntity in queue.Items
                orderby queueItemForMapEntity.Priority descending, queueItemForMapEntity.Id
                select MapItem(queueItemForMapEntity)
            ).ToList()
        };
    }

    private static ReviewItemDto MapItem(ReviewItem item)
    {
        return new ReviewItemDto
        {
            Id = item.Id,
            QueueId = item.QueueId,
            LoanApplicationId = item.LoanApplicationId,
            Priority = item.Priority,
            Notes = item.Notes,
            LoanApplication = item.LoanApplication == null
                ? null
                : new LoanApplicationSummaryDto
                {
                    Id = item.LoanApplication.Id,
                    ApplicantName = item.LoanApplication.ApplicantName,
                    LoanAmount = item.LoanApplication.LoanAmount,
                    Status = item.LoanApplication.Status,
                    RiskRating = item.LoanApplication.RiskRating
                }
        };
    }
}

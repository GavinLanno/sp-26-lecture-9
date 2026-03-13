using Microsoft.AspNetCore.Mvc;
using Buckeye.Lending.Api.Models;
using Buckeye.Lending.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Buckeye.Lending.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // sets the base URI to `/api/loanapplications`
public class LoanApplicationsController : ControllerBase
{
    // In-memory data — in real app, this is a database
    private readonly LendingContext _context;

    public LoanApplicationsController(LendingContext context)
    {
        _context = context;
    }

    // GET: api/LoanApplications?loanTypeId=1&minAmount=100000
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LoanApplicationDto>>> GetAll(
        [FromQuery] int? loanTypeId,
        [FromQuery] decimal? minAmount,
        [FromQuery] decimal? maxAmount,
        [FromQuery] string? search)
    {
        var query = _context.LoanApplications
            .Include(l => l.Applicant)
            .Include(l => l.LoanType)
            .AsQueryable();

        if (loanTypeId.HasValue)
            query = query.Where(l => l.LoanTypeId == loanTypeId.Value);

        if (minAmount.HasValue)
            query = query.Where(l => l.LoanAmount >= minAmount.Value);

        if (maxAmount.HasValue)
            query = query.Where(l => l.LoanAmount <= maxAmount.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l => l.ApplicantName.Contains(search, StringComparison.OrdinalIgnoreCase));

        return Ok(await query.ToListAsync());
    }

    // GET: api/LoanApplications/2
    [HttpGet("{id}")]
    public async Task<ActionResult<LoanApplicationDto>> GetById(int id)
    {
        var app = await _context.LoanApplications
            .Include(l => l.Applicant)
            .Include(l => l.LoanType)
            .Include(l => l.Payments)
            .Include(l => l.LoanNotes)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (app == null)
            throw new KeyNotFoundException($"Loan application with ID {id} not found");
        return Ok(app);
    }

    // POST: api/LoanApplicationDtos
    [HttpPost]
    public async Task<ActionResult<LoanApplicationDto>> Create(LoanApplicationDto application)
    {
        var applicant = await _context.Applicants.FindAsync(application.ApplicantId);
        if (applicant == null)
            throw new ArgumentException($"Applicant {application.ApplicantId} not found.", nameof(application.ApplicantId));

        var loanType = await _context.LoanTypes.FindAsync(application.LoanTypeId);
        if (loanType == null)
            throw new ArgumentException($"Loan type {application.LoanTypeId} not found.", nameof(application.LoanTypeId));

        // Set server-controlled fields
        application.ApplicantName = applicant.Name;
        application.RiskRating = CalculateRiskRating(application.LoanAmount, application.AnnualIncome);
        application.Status = "Pending Review";
        application.SubmittedDate = DateTime.UtcNow;
        application.Applicant = null;
        application.LoanType = null;

        _context.LoanApplications.Add(application);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = application.Id },
            application
        );
    }

    // PUT: api/LoanApplicationDtos/2
    [HttpPut("{id}")]
    public async Task<ActionResult<LoanApplicationDto>> Update(int id, LoanApplicationDto updated)
    {
        var existing = await _context.LoanApplications.FindAsync(id);
        if (existing == null)
            throw new KeyNotFoundException($"Loan application with ID {id} not found");

        var applicant = await _context.Applicants.FindAsync(updated.ApplicantId);
        if (applicant == null)
            throw new ArgumentException($"Applicant {updated.ApplicantId} not found.", nameof(updated.ApplicantId));

        var loanType = await _context.LoanTypes.FindAsync(updated.LoanTypeId);
        if (loanType == null)
            throw new ArgumentException($"Loan type {updated.LoanTypeId} not found.", nameof(updated.LoanTypeId));

        // Update allowed fields
        existing.ApplicantName = applicant.Name;
        existing.LoanAmount = updated.LoanAmount;
        existing.RiskRating = CalculateRiskRating(updated.LoanAmount, updated.AnnualIncome);
        existing.AnnualIncome = updated.AnnualIncome;
        existing.ApplicantId = updated.ApplicantId;
        existing.LoanTypeId = updated.LoanTypeId;
        // Don't update: Id, Status, SubmittedDate (server-controlled)

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    // DELETE: api/LoanApplicationDtos/3
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var app = await _context.LoanApplications.FindAsync(id);
        if (app == null)
            throw new KeyNotFoundException($"Loan application with ID {id} not found");

        var isReferencedInQueue = await _context.ReviewItems.AnyAsync(reviewItemEntity => reviewItemEntity.LoanApplicationId == id);
        if (isReferencedInQueue)
            throw new InvalidOperationException($"Loan application {id} cannot be deleted while it is referenced in a review queue.");

        _context.LoanApplications.Remove(app);
        await _context.SaveChangesAsync();
        return NoContent();  // 204
    }

    private static int CalculateRiskRating(decimal loanAmount, decimal annualIncome)
    {
        if (annualIncome <= 0)
            return 5;

        var debtToIncomeRatio = loanAmount / annualIncome;

        if (debtToIncomeRatio <= 0.75m)
            return 1;
        if (debtToIncomeRatio <= 1.50m)
            return 2;
        if (debtToIncomeRatio <= 2.50m)
            return 3;
        if (debtToIncomeRatio <= 3.50m)
            return 4;

        return 5;
    }
}

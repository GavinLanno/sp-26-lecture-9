using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Buckeye.Lending.Api.Models;

public class LoanApplicationDto
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string ApplicantName { get; set; } = string.Empty;

    [Required, Column(TypeName = "decimal(12,2)")]
    [Range(typeof(decimal), "1", "79228162514264337593543950335")]
    public decimal LoanAmount { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal AnnualIncome { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Pending";

    // Server-controlled: calculated in the controller from amount/income.
    public int RiskRating { get; set; }

    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    // Foreign key — which applicant filed this application
    [Range(1, int.MaxValue)]
    public int ApplicantId { get; set; }
    public Applicant? Applicant { get; set; }

    // Foreign key — what type of loan (replaces the old string LoanType)
    [Range(1, int.MaxValue)]
    public int LoanTypeId { get; set; }
    public LoanType? LoanType { get; set; }

    // Navigation — one application can have many payments and notes
    public List<LoanPayment> Payments { get; set; } = [];
    public List<LoanNote> LoanNotes { get; set; } = [];
}

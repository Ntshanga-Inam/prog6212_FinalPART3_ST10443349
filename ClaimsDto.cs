using System.ComponentModel.DataAnnotations;

namespace CMCS.Models
{
    public class ClaimDto
    {
        public int ClaimId { get; set; }
        public int LecturerId { get; set; }
        public DateTime ClaimMonth { get; set; }
        public decimal TotalHours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal Amount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft";
        public DateTime SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public int? ApprovedBy { get; set; }
        public string LecturerName { get; set; } = string.Empty;
        public List<ClaimItemDto> ClaimItems { get; set; } = new List<ClaimItemDto>();

        // No navigation properties that cause cycles
    }

    public class ClaimItemDto
    {
        public int ClaimItemId { get; set; }
        public int ClaimId { get; set; }
        public DateTime Date { get; set; }
        public decimal HoursWorked { get; set; }
        public string Module { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        // No Claim navigation property
    }
}
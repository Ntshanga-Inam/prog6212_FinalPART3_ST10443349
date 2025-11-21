using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class Claim
    {
        [Key]
        public int ClaimId { get; set; }

        [Required]
        public int LecturerId { get; set; } // This stores the integer ID from the database

        [Required]
        [Display(Name = "Claim Month")]
        public DateTime ClaimMonth { get; set; }

        [Required]
        [Range(0.5, 200, ErrorMessage = "Total hours must be between 0.5 and 200")]
        [Display(Name = "Total Hours")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalHours { get; set; }

        [Required]
        [Range(100, 1000, ErrorMessage = "Hourly rate must be between 100 and 1000")]
        [Display(Name = "Hourly Rate")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; }

        [Required]
        [Display(Name = "Total Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string Notes { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Draft";

        public DateTime SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public int? ApprovedBy { get; set; }

        // Navigation properties
        public virtual User? Lecturer { get; set; }
        public virtual User? ApprovedByUser { get; set; }
        public virtual ICollection<ClaimItem> ClaimItems { get; set; } = new List<ClaimItem>();
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
        public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();

        [NotMapped]
        public string LecturerName { get; set; } = string.Empty;

        [NotMapped]
        public string FormattedLecturerId => LecturerId.ToString(); // Helper property for string representation

        public void CalculateAmount()
        {
            Amount = TotalHours * HourlyRate;
        }
    }
}
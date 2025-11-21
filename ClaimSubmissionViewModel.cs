// In ClaimSubmissionViewModel.cs - Make sure the model is properly structured:
using System.ComponentModel.DataAnnotations;

namespace CMCS.Models
{
    public class ClaimSubmissionViewModel
    {
        [Required(ErrorMessage = "Claim month is required")]
        [Display(Name = "Claim Month")]
        [DataType(DataType.Date)]
        public DateTime ClaimMonth { get; set; } = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        [Required(ErrorMessage = "Total hours is required")]
        [Range(0.5, 200, ErrorMessage = "Total hours must be between 0.5 and 200")]
        [Display(Name = "Total Hours")]
        public decimal TotalHours { get; set; }

        [Required(ErrorMessage = "Hourly rate is required")]
        [Range(100, 1000, ErrorMessage = "Hourly rate must be between R100 and R1000")]
        [Display(Name = "Hourly Rate (R)")]
        public decimal HourlyRate { get; set; } = 250;

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string Notes { get; set; } = string.Empty;

        [Display(Name = "Supporting Documents")]
        public List<DocumentUpload> Documents { get; set; } = new List<DocumentUpload>();

        // Claim Items for detailed hour tracking
        public List<ClaimItemViewModel> ClaimItems { get; set; } = new List<ClaimItemViewModel>();

        // Calculated property for display
        [Display(Name = "Total Amount (R)")]
        public decimal TotalAmount => TotalHours * HourlyRate;
    }

    public class ClaimItemViewModel
    {
        [Required(ErrorMessage = "Date is required")]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Hours worked is required")]
        [Range(0.5, 24, ErrorMessage = "Hours worked must be between 0.5 and 24")]
        [Display(Name = "Hours Worked")]
        public decimal HoursWorked { get; set; }

        [Required(ErrorMessage = "Module is required")]
        [StringLength(100, ErrorMessage = "Module cannot exceed 100 characters")]
        [Display(Name = "Module")]
        public string Module { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;
    }

    public class DocumentUpload
    {
        [Display(Name = "Select File")]
        public IFormFile? File { get; set; }

        [StringLength(100)]
        public string Description { get; set; } = string.Empty;
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class ClaimItem
    {
        [Key]
        public int ClaimItemId { get; set; }

        [Required]
        public int ClaimId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Range(0.5, 24, ErrorMessage = "Hours worked must be between 0.5 and 24")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal HoursWorked { get; set; }

        [Required]
        [StringLength(100)]
        public string Module { get; set; } = string.Empty; // Initialize with default value

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty; // Initialize with default value

        // Navigation property - make nullable since it might not be loaded
        public virtual Claim? Claim { get; set; }
    }
}
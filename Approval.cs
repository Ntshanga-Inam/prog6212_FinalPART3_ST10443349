using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class Approval
    {
        [Key]
        public int ApprovalId { get; set; }

        [Required]
        public int ClaimId { get; set; }

        [Required]
        public int ApproverId { get; set; }

        [Required]
        [StringLength(50)]
        public string ApprovedByRole { get; set; } = string.Empty; // Initialize with default value

        public DateTime ApprovalDate { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; } = string.Empty; // Initialize with default value

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty; // Initialize with default value


        // Navigation properties - make nullable since they might not be loaded
        public virtual Claim? Claim { get; set; }
        public virtual User? Approver { get; set; }
    }
}
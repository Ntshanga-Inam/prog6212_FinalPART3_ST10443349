using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class Document
    {
        [Key]
        public int DocumentId { get; set; }

        [Required]
        public int ClaimId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty; // Initialize with default value

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty; // Initialize with default value

        public long FileSize { get; set; }

        public DateTime UploadedDate { get; set; }

        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty; // Initialize with default value

        // Navigation property - make nullable since it might not be loaded
        public virtual Claim? Claim { get; set; }
    }
}
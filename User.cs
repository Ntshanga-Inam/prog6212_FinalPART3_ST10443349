using System.ComponentModel.DataAnnotations;

namespace CMCS.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string FirstName { get; set; } = string.Empty; // Initialize with default value

        [Required]
        public string LastName { get; set; } = string.Empty; // Initialize with default value

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty; // Initialize with default value

        [Required]
        public string Password { get; set; } = string.Empty; // Initialize with default value

        [Required]
        public string Role { get; set; } = string.Empty; // Initialize with default value

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
        public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();
    }
}
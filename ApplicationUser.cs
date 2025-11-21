// [file name]: ApplicationUser.cs
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CMCS.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Lecturer";


        // public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
        // public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();
    }
}
using Microsoft.EntityFrameworkCore;
using CMCS.Models;
using System.Linq.Expressions;

namespace CMCS.Services
{
    public class EfDataService : IDataService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public EfDataService(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // In EfDataService.cs - Update the GetClaims method
        public List<Claim> GetClaims()
        {
            try
            {
                var claims = _context.Claims
                    .Include(c => c.ClaimItems)
                    .Include(c => c.Documents)
                    .AsNoTracking()
                    .ToList();

                Console.WriteLine($"Retrieved {claims.Count} claims from database");

                // Debug: Log each claim
                foreach (var claim in claims)
                {
                    Console.WriteLine($"Claim ID: {claim.ClaimId}, Lecturer ID: {claim.LecturerId}, Status: {claim.Status}");
                }

                return claims;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claims: {ex.Message}");
                return new List<Claim>();
            }
        }

        public Claim? GetClaim(int id)
        {
            try
            {
                return _context.Claims
                    .Include(c => c.ClaimItems)
                    .Include(c => c.Documents)
                    .AsNoTracking()
                    .FirstOrDefault(c => c.ClaimId == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claim {id}: {ex.Message}");
                return null;
            }
        }

        public async void SaveClaim(Claim claim)
        {
            try
            {
                var existingClaim = await _context.Claims
                    .Include(c => c.ClaimItems)
                    .Include(c => c.Documents)
                    .FirstOrDefaultAsync(c => c.ClaimId == claim.ClaimId);

                if (existingClaim != null)
                {
                    // Update existing claim
                    _context.Entry(existingClaim).CurrentValues.SetValues(claim);

                    // Update claim items
                    foreach (var item in claim.ClaimItems)
                    {
                        var existingItem = existingClaim.ClaimItems.FirstOrDefault(ci => ci.ClaimItemId == item.ClaimItemId);
                        if (existingItem != null)
                        {
                            _context.Entry(existingItem).CurrentValues.SetValues(item);
                        }
                        else
                        {
                            existingClaim.ClaimItems.Add(item);
                        }
                    }

                    // Remove deleted claim items
                    foreach (var existingItem in existingClaim.ClaimItems.ToList())
                    {
                        if (!claim.ClaimItems.Any(ci => ci.ClaimItemId == existingItem.ClaimItemId))
                        {
                            _context.ClaimItems.Remove(existingItem);
                        }
                    }
                }
                else
                {
                    // Add new claim
                    _context.Claims.Add(claim);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving claim: {ex.Message}");
            }
        }

        public async void DeleteClaim(int id)
        {
            try
            {
                var claim = await _context.Claims.FindAsync(id);
                if (claim != null)
                {
                    _context.Claims.Remove(claim);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting claim {id}: {ex.Message}");
            }
        }

        public List<User> GetUsers()
        {
            try
            {
                return _context.Users
                    .Select(u => new User
                    {
                        UserId = u.UserId,
                        FirstName = u.FirstName ?? string.Empty,
                        LastName = u.LastName ?? string.Empty,
                        Email = u.Email ?? string.Empty,
                        Role = u.Role ?? string.Empty
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting users: {ex.Message}");
                return new List<User>();
            }
        }

        public async void SaveDocument(Document document)
        {
            try
            {
                if (document.DocumentId == 0)
                {
                    _context.Documents.Add(document);
                }
                else
                {
                    _context.Documents.Update(document);
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving document: {ex.Message}");
            }
        }

        public List<Document> GetDocumentsByClaimId(int claimId)
        {
            try
            {
                return _context.Documents
                    .Where(d => d.ClaimId == claimId)
                    .AsNoTracking()
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting documents for claim {claimId}: {ex.Message}");
                return new List<Document>();
            }
        }

        public async void SaveApproval(Approval approval)
        {
            try
            {
                if (approval.ApprovalId == 0)
                {
                    _context.Approvals.Add(approval);
                }
                else
                {
                    _context.Approvals.Update(approval);
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving approval: {ex.Message}");
            }
        }

        public List<Approval> GetApprovalsByClaimId(int claimId)
        {
            try
            {
                return _context.Approvals
                    .Where(a => a.ClaimId == claimId)
                    .AsNoTracking()
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting approvals for claim {claimId}: {ex.Message}");
                return new List<Approval>();
            }
        }

        public List<Approval> GetApprovalsByApproverId(int approverId)
        {
            try
            {
                return _context.Approvals
                    .Where(a => a.ApproverId == approverId)
                    .AsNoTracking()
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting approvals for approver {approverId}: {ex.Message}");
                return new List<Approval>();
            }
        }

        public async Task<List<Claim>> GetClaimsAsync(Expression<Func<Claim, bool>>? predicate = null)
        {
            try
            {
                var query = _context.Claims
                    .Include(c => c.ClaimItems)
                    .Include(c => c.Documents)
                    .AsQueryable();

                if (predicate != null)
                {
                    query = query.Where(predicate);
                }

                return await query.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claims async: {ex.Message}");
                return new List<Claim>();
            }
        }

        public async Task<List<Claim>> GetClaimsAsync()
        {
            try
            {
                return await _context.Claims
                    .Include(c => c.ClaimItems)
                    .Include(c => c.Documents)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claims async: {ex.Message}");
                return new List<Claim>();
            }
        }

        public async Task<Claim?> GetClaimAsync(int id)
        {
            try
            {
                return await _context.Claims
                    .Include(c => c.ClaimItems)
                    .Include(c => c.Documents)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ClaimId == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claim {id} async: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SaveClaimAsync(Claim claim)
        {
            try
            {
                if (claim.ClaimId == 0)
                {
                    _context.Claims.Add(claim);
                }
                else
                {
                    _context.Claims.Update(claim);
                }
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving claim async: {ex.Message}");
                return false;
            }
        }

        public User GetUserByEmail(string email)
        {
            try
            {
                var user = _context.Users
                    .AsNoTracking()
                    .FirstOrDefault(u => u.Email == email);

                if (user == null) return new User();

                return new User
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Password = user.Password ?? string.Empty,
                    Role = user.Role ?? string.Empty,
                    IsActive = user.IsActive,
                    CreatedDate = user.CreatedDate
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user by email {email}: {ex.Message}");
                return new User();
            }
        }

        public void SaveUser(User user)
        {
            try
            {
                var dbUser = new User
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Password = user.Password ?? string.Empty,
                    Role = user.Role ?? string.Empty,
                    IsActive = user.IsActive,
                    CreatedDate = user.CreatedDate
                };

                if (user.UserId == 0)
                {
                    _context.Users.Add(dbUser);
                }
                else
                {
                    _context.Users.Update(dbUser);
                }
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving user: {ex.Message}");
            }
        }

        public bool ValidateUser(string email, string password)
        {
            try
            {
                var user = _context.Users
                    .AsNoTracking()
                    .FirstOrDefault(u => u.Email == email && u.IsActive);

                if (user == null) return false;

                // Simple password verification (in real app, use hashing)
                return user.Password == password;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating user {email}: {ex.Message}");
                return false;
            }
        }

        public List<User> GetUsersByRole(string role)
        {
            try
            {
                return _context.Users
                    .Where(u => u.Role == role && u.IsActive)
                    .AsNoTracking()
                    .Select(u => new User
                    {
                        UserId = u.UserId,
                        FirstName = u.FirstName ?? string.Empty,
                        LastName = u.LastName ?? string.Empty,
                        Email = u.Email ?? string.Empty,
                        Role = u.Role ?? string.Empty
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting users by role {role}: {ex.Message}");
                return new List<User>();
            }
        }

        // Additional helper methods for better data access

        public List<Claim> GetClaimsByLecturerId(int lecturerId)
        {
            try
            {
                return _context.Claims
                    .Where(c => c.LecturerId == lecturerId)
                    .Include(c => c.ClaimItems)
                    .Include(c => c.Documents)
                    .AsNoTracking()
                    .OrderByDescending(c => c.SubmittedDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claims for lecturer {lecturerId}: {ex.Message}");
                return new List<Claim>();
            }
        }

        public List<Claim> GetClaimsByStatus(string status)
        {
            try
            {
                return _context.Claims
                    .Where(c => c.Status == status)
                    .Include(c => c.ClaimItems)
                    .Include(c => c.Documents)
                    .AsNoTracking()
                    .OrderByDescending(c => c.SubmittedDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claims by status {status}: {ex.Message}");
                return new List<Claim>();
            }
        }

        public async Task<int> GetNextClaimIdAsync()
        {
            try
            {
                var maxId = await _context.Claims
                    .AsNoTracking()
                    .MaxAsync(c => (int?)c.ClaimId) ?? 0;
                return maxId + 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting next claim ID: {ex.Message}");
                return 1; // Default starting ID
            }
        }

        public async Task<int> GetNextDocumentIdAsync()
        {
            try
            {
                var maxId = await _context.Documents
                    .AsNoTracking()
                    .MaxAsync(d => (int?)d.DocumentId) ?? 0;
                return maxId + 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting next document ID: {ex.Message}");
                return 1; // Default starting ID
            }
        }

        public async Task<int> GetNextApprovalIdAsync()
        {
            try
            {
                var maxId = await _context.Approvals
                    .AsNoTracking()
                    .MaxAsync(a => (int?)a.ApprovalId) ?? 0;
                return maxId + 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting next approval ID: {ex.Message}");
                return 1; // Default starting ID
            }
        }

        public User? GetUserById(int userId)
        {
            try
            {
                var user = _context.Users
                    .AsNoTracking()
                    .FirstOrDefault(u => u.UserId == userId);

                if (user == null) return null;

                return new User
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Password = user.Password ?? string.Empty,
                    Role = user.Role ?? string.Empty,
                    IsActive = user.IsActive,
                    CreatedDate = user.CreatedDate
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user by ID {userId}: {ex.Message}");
                return null;
            }
        }

        public List<Claim> GetClaimsByDateRange(DateTime startDate, DateTime endDate)
        {
            try
            {
                return _context.Claims
                    .Where(c => c.SubmittedDate >= startDate && c.SubmittedDate <= endDate)
                    .Include(c => c.ClaimItems)
                    .Include(c => c.Documents)
                    .AsNoTracking()
                    .OrderByDescending(c => c.SubmittedDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claims by date range {startDate} to {endDate}: {ex.Message}");
                return new List<Claim>();
            }
        }

        public decimal GetTotalApprovedAmount()
        {
            try
            {
                return _context.Claims
                    .Where(c => c.Status == "Approved")
                    .Sum(c => c.Amount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting total approved amount: {ex.Message}");
                return 0;
            }
        }

        public Dictionary<string, int> GetClaimsCountByStatus()
        {
            try
            {
                return _context.Claims
                    .GroupBy(c => c.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionary(x => x.Status ?? "Unknown", x => x.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claims count by status: {ex.Message}");
                return new Dictionary<string, int>();
            }
        }

        public async Task<bool> UpdateClaimStatusAsync(int claimId, string status, int? approvedBy = null)
        {
            try
            {
                var claim = await _context.Claims.FindAsync(claimId);
                if (claim != null)
                {
                    claim.Status = status;
                    if (approvedBy.HasValue)
                    {
                        claim.ApprovedBy = approvedBy.Value;
                        if (status == "Approved")
                        {
                            claim.ApprovedDate = DateTime.Now;
                        }
                    }
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating claim {claimId} status to {status}: {ex.Message}");
                return false;
            }
        }
    }
}
using CMCS.Models;
using System.Linq.Expressions;

namespace CMCS.Services
{
    public interface IDataService
    {
        List<Claim> GetClaims();
        Claim? GetClaim(int id);
        void SaveClaim(Claim claim);
        void DeleteClaim(int id);
        List<User> GetUsers();
        void SaveDocument(Document document);
        List<Document> GetDocumentsByClaimId(int claimId);

        // New methods for approvals
        void SaveApproval(Approval approval);
        List<Approval> GetApprovalsByClaimId(int claimId);
        List<Approval> GetApprovalsByApproverId(int approverId);

        // Additional methods for Entity Framework
        Task<List<Claim>> GetClaimsAsync();
        Task<List<Claim>> GetClaimsAsync(Expression<Func<Claim, bool>> predicate);
        Task<Claim?> GetClaimAsync(int id);
        Task<bool> SaveClaimAsync(Claim claim);
        // Services/IDataService.cs
    
        User GetUserByEmail(string email);
        void SaveUser(User user);
        bool ValidateUser(string email, string password);
        List<User> GetUsersByRole(string role);
    
    }
}
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace CMCS.Hubs
{
    public class ClaimHub : Hub
    {
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task NotifyClaimSubmitted(int claimId, string lecturerId)
        {
            await Clients.Group("Coordinators").SendAsync("ReceiveNewClaim", claimId, lecturerId);
        }

        public async Task NotifyClaimStatusUpdate(int claimId, string newStatus, string userRole)
        {
            await Clients.All.SendAsync("ReceiveClaimStatusUpdate", claimId, newStatus, userRole);
        }

        public async Task NotifyCoordinatorApproval(int claimId)
        {
            await Clients.Group("Managers").SendAsync("ReceiveCoordinatorApproval", claimId);
        }

        public async Task NotifyManagerApproval(int claimId)
        {
            await Clients.Group("HR").SendAsync("ReceiveManagerApproval", claimId);
        }
    }
}
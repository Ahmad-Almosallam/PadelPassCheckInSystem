using PadelPassCheckInSystem.Integration.Rekaz.Models;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;

namespace PadelPassCheckInSystem.Services;

public interface IEndUserService
{
    Task<EndUsersPaginatedViewModel> GetEndUsersAsync(string searchPhoneNumber,  string status, int page = 1, int pageSize = 10);
    Task<(bool Success, string Message, EndUser? EndUser)> CreateEndUserAsync(EndUserViewModel model);
    Task<(bool Success, string Message)> UpdateEndUserAsync(int id, EndUserViewModel model);
    Task<(bool Success, string Message)> DeleteEndUserAsync(int id);
    Task<EndUser?> GetEndUserByIdAsync(int id);
    Task<(bool Success, string Message, string? DownloadUrl)> GenerateQRCodeAsync(int endUserId, bool forceRegenerate = false, string baseUrl = "");
    Task<(bool Success, string Message)> StopSubscriptionAsync(int endUserId, string stopReason);
    Task<(bool Success, string Message)> ReactivateSubscriptionAsync(int endUserId);
    Task<(bool Success, string Message, int count)> SyncRekazAsync(List<RekazCustomer> customers);
}

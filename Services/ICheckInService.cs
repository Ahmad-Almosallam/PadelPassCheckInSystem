using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Services
{
    public interface ICheckInService
    {
        Task<(bool Success, string Message, int? CheckInId)> CheckInAsync(string identifier, int branchId);
        Task<(bool Success, string Message)> AssignCourtAsync(int checkInId, string courtName, int playDurationMinutes, DateTime? playStartTime, string notes);
        Task<(bool Success, string Message)> DeleteCheckInAsync(int checkInId, int branchId);
        Task<bool> HasCheckedInTodayAsync(int endUserId);
        Task<List<CheckIn>> GetPendingCourtAssignmentsAsync(int branchId);
        Task<List<CheckIn>> GetTodayCheckInsWithCourtInfoAsync(int branchId);

        Task<(bool IsValid, string Message, EndUser User)> ValidateCheckInAsync(
            string identifier,
            int branchId);
    }
}
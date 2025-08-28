using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Services
{
    public interface ICheckInService
    {
        Task<(bool Success, string Message, int? CheckInId)> CheckInAsync(string identifier,
            int branchId, DateTime requestCheckInDate);

        Task<(bool Success, string Message)> AssignCourtAsync(
            int checkInId,
            int branchCourtId,
            int playDurationMinutes,
            DateTime? playStartTime,
            string notes,
            bool playerAttended = true);

        Task<(bool Success, string Message)> DeleteCheckInAsync(
            int checkInId,
            int? branchId = null);

        Task<bool> HasCheckedInTodayAsync(
            int endUserId);

        Task<List<CheckIn>> GetPendingCourtAssignmentsAsync(
            int branchId);

        Task<List<CheckIn>> GetTodayCheckInsWithCourtInfoAsync(
            int branchId);

        Task<(bool IsValid, string Message, EndUser User)> ValidateCheckInAsync(
            string identifier,
            int branchId, DateTime checkInDate);

        Task<(bool Success, string Message)> EditCheckInAsync(
            int checkInId,
            string courtName,
            int playDurationMinutes,
            DateTime? playStartTime,
            string notes);

        Task<(bool Success, string Message, int? CheckInId)> AdminManualCheckInAsync(
            string phoneNumber,
            int branchId,
            DateTime checkInDateTime,
            int branchCourtId ,
            int? playDurationMinutes = null,
            DateTime? playStartTime = null,
            string notes = null,
            bool playerAttended = true);

        Task<(bool IsValid, string Message, EndUser User)> ValidateEndUserForManualCheckInAsync(
            string phoneNumber,
            DateTime checkInDate);
    }
}
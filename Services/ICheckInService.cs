public interface ICheckInService
{
    Task<(bool Success, string Message)> CheckInAsync(string identifier, int branchId);
    Task<bool> HasCheckedInTodayAsync(int endUserId);
}
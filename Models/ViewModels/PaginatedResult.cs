using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public int StartIndex => (CurrentPage - 1) * PageSize + 1;
        public int EndIndex => Math.Min(CurrentPage * PageSize, TotalItems);
    }

    public class CheckInsPaginatedViewModel
    {
        public PaginatedResult<CheckIn> CheckIns { get; set; } = new PaginatedResult<CheckIn>();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? BranchId { get; set; }
        public string PhoneNumber { get; set; }
        public List<Branch> Branches { get; set; } = new List<Branch>();
    }

    public class EndUsersPaginatedViewModel
    {
        public PaginatedResult<EndUser> EndUsers { get; set; } = new PaginatedResult<EndUser>();
        public string SearchPhoneNumber { get; set; }
        // Precomputed statistics (based on the filtered set, before pagination)
        public int ActiveSubscriptions { get; set; }
        public int CurrentlyPaused { get; set; }
        public int StoppedCount { get; set; }
        public int ExpiredCount { get; set; }
        public int NotSetPlaytomicUserIdsCount { get; set; }
        public int StoppedByWarningsCount { get; set; }
    }
}

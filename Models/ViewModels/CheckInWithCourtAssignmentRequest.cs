using System;

namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class CheckInWithCourtAssignmentRequest
    {
        public string Identifier { get; set; }  // This can be either barcode or phone number
        public string CourtName { get; set; }
        public int PlayDurationMinutes { get; set; }
        public DateTime PlayStartTime { get; set; }
        public string Notes { get; set; }
    }
}

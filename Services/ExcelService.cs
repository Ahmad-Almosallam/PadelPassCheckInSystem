using ClosedXML.Excel;
using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Services
{
    public class ExcelService : IExcelService
    {
        public byte[] ExportCheckInsToExcel(List<CheckIn> checkIns)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Check-Ins");

            // Headers
            worksheet.Cell(1, 1).Value = "End User Name";
            worksheet.Cell(1, 2).Value = "Phone Number";
            worksheet.Cell(1, 3).Value = "Branch Name";
            worksheet.Cell(1, 4).Value = "Check-In Date";
            worksheet.Cell(1, 5).Value = "Check-In Time";
            worksheet.Cell(1, 6).Value = "Court";
            worksheet.Cell(1, 7).Value = "Play Duration";
            worksheet.Cell(1, 8).Value = "Play Start Time";
            worksheet.Cell(1, 9).Value = "Attended";

            // Style headers
            var headerRange = worksheet.Range(1, 1, 1, 5);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Data
            int row = 2;
            foreach (var checkIn in checkIns)
            {
                worksheet.Cell(row, 1).Value = checkIn.EndUser.Name;
                worksheet.Cell(row, 2).Value = checkIn.EndUser.PhoneNumber;
                worksheet.Cell(row, 3).Value = checkIn.Branch.Name;
                worksheet.Cell(row, 4).Value = checkIn.CheckInDateTime.ToLocalTime().ToString("yyyy-MM-dd");
                worksheet.Cell(row, 5).Value = checkIn.CheckInDateTime.ToLocalTime().ToString("HH:mm:ss");
                worksheet.Cell(row, 6).Value = checkIn.BranchCourtId.HasValue ? checkIn.BranchCourt.CourtName : checkIn.CourtName;
                worksheet.Cell(row, 7).Value = checkIn.PlayDuration;
                worksheet.Cell(row, 8).Value = checkIn.PlayStartTime;
                worksheet.Cell(row, 9).Value = checkIn.PlayerAttended;
                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}


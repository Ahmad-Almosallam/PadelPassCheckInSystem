using ClosedXML.Excel;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Shared;
using PadelPassCheckInSystem.Extensions;

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
            worksheet.Cell(1, 3).Value = "Subscription Code";
            worksheet.Cell(1, 4).Value = "Branch Name";
            worksheet.Cell(1, 5).Value = "Check-In Date";
            worksheet.Cell(1, 6).Value = "Check-In Time";
            worksheet.Cell(1, 7).Value = "Court";
            worksheet.Cell(1, 8).Value = "Play Duration";
            worksheet.Cell(1, 9).Value = "Play Start Time";
            worksheet.Cell(1, 10).Value = "Attended";
            
            // Data
            var row = 2;
            var numberOfColumns = 10;
            foreach (var checkIn in checkIns)
            {
                worksheet.Cell(row, 1).Value = checkIn.EndUser.Name;
                worksheet.Cell(row, 2).Value = checkIn.EndUser.PhoneNumber;
                worksheet.Cell(row, 3).Value = checkIn.EndUserSubscription.Code;
                worksheet.Cell(row, 4).Value = checkIn.Branch.Name;
                worksheet.Cell(row, 5).Value = checkIn.CheckInDateTime.ToLocalTime(AppConstant.KsaTimeZoneId).ToString("yyyy-MM-dd");
                worksheet.Cell(row, 6).Value = checkIn.CreatedAt.ToLocalTime(AppConstant.KsaTimeZoneId).ToString("HH:mm:ss");
                worksheet.Cell(row, 7).Value = checkIn.BranchCourtId.HasValue ? checkIn.BranchCourt.CourtName : checkIn.CourtName;
                worksheet.Cell(row, 8).Value = checkIn.PlayDuration;
                worksheet.Cell(row, 9).Value = checkIn.PlayStartTime!.Value.ToLocalTime(AppConstant.KsaTimeZoneId).ToString("HH:mm:ss");
                worksheet.Cell(row, 10).Value = checkIn.PlayerAttended;
                row++;
            }
            
            // Create Excel table (this makes it filterable)
            var dataRange = worksheet.Range(1, 1, row - 1, numberOfColumns);
            var table = dataRange.CreateTable("CheckInsTable");
            
            // Style the table (optional - you can choose different table styles)
            table.Theme = XLTableTheme.TableStyleMedium2;
            
            // Alternative: If you prefer custom styling instead of table theme
            // Style headers manually
            var headerRange = worksheet.Range(1, 1, 1, numberOfColumns);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
            headerRange.Style.Font.FontColor = XLColor.White;
            
            // Auto-fit columns
            worksheet.Columns().AdjustToContents();
            
            // Optional: Set minimum column widths or add padding
            // Method 1: Set specific widths for each column
            // worksheet.Column(1).Width = Math.Max(worksheet.Column(1).Width, 15); // End User Name
            // worksheet.Column(2).Width = Math.Max(worksheet.Column(2).Width, 12); // Phone Number
            // worksheet.Column(3).Width = Math.Max(worksheet.Column(3).Width, 15); // Branch Name
            // worksheet.Column(4).Width = Math.Max(worksheet.Column(4).Width, 12); // Check-In Date
            // worksheet.Column(5).Width = Math.Max(worksheet.Column(5).Width, 12); // Check-In Time
            // worksheet.Column(6).Width = Math.Max(worksheet.Column(6).Width, 10); // Court
            // worksheet.Column(7).Width = Math.Max(worksheet.Column(7).Width, 12); // Play Duration
            // worksheet.Column(8).Width = Math.Max(worksheet.Column(8).Width, 15); // Play Start Time
            // worksheet.Column(9).Width = Math.Max(worksheet.Column(9).Width, 10); // Attended
            // worksheet.Column(10).Width = Math.Max(worksheet.Column(10).Width, 10); // Attended
            
            // Alternative Method 2: Just use AdjustToContents() - simpler approach
            worksheet.Columns().AdjustToContents();
            
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
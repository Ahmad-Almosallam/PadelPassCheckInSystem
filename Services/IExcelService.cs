using PadelPassCheckInSystem.Models.Entities;

public interface IExcelService
{
    byte[] ExportCheckInsToExcel(List<CheckIn> checkIns);
}
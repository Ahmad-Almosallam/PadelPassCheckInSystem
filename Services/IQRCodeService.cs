public interface IQRCodeService
{
    byte[] GenerateQRCode(string text);
    string GenerateQRCodeBase64(string text);
}
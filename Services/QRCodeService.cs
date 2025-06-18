using QRCoder;

public class QRCodeService : IQRCodeService
{
    public byte[] GenerateQRCode(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(10);
    }

    public string GenerateQRCodeBase64(string text)
    {
        var qrCodeBytes = GenerateQRCode(text);
        return Convert.ToBase64String(qrCodeBytes);
    }
}
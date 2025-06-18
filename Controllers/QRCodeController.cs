using Microsoft.AspNetCore.Mvc;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace PadelPassCheckInSystem.Controllers
{
    public class QRCodeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IQRCodeService _qrCodeService;

        public QRCodeController(ApplicationDbContext context, IQRCodeService qrCodeService)
        {
            _context = context;
            _qrCodeService = qrCodeService;
        }

        [HttpGet("qr/{token}")]
        public IActionResult Download(string token)
        {
            var endUser = _context.EndUsers.FirstOrDefault(u => u.QRCodeDownloadToken == token);
            
            if (endUser == null || endUser.HasDownloadedQR)
            {
                return NotFound();
            }

            ViewBag.QRCodeData = _qrCodeService.GenerateQRCodeBase64(endUser.UniqueIdentifier);
            ViewBag.Token = token;
            return View();
        }

        [HttpPost("qr/{token}/confirm")]
        public async Task<IActionResult> ConfirmDownload(string token)
        {
            var endUser = await _context.EndUsers.FirstOrDefaultAsync(u => u.QRCodeDownloadToken == token && !u.HasDownloadedQR);
            
            if (endUser == null)
            {
                return NotFound();
            }

            endUser.HasDownloadedQR = true;
            endUser.QRCodeDownloadToken = null;
            await _context.SaveChangesAsync();

            var qrCodeBase64 = _qrCodeService.GenerateQRCodeBase64(endUser.UniqueIdentifier);
            return Json(new { success = true, qrCode = qrCodeBase64 });
        }
    }
}

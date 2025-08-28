// Services/IBranchCourtService.cs

using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Models.ViewModels.BranchCourts;

namespace PadelPassCheckInSystem.Services;

public interface IBranchCourtService
{
    Task<List<BranchCourtViewModel>> GetAllBranchCourtsAsync();

    Task<List<BranchCourtViewModel>> GetBranchCourtsAsync(
        int branchId);

    Task<BranchCourtViewModel> GetBranchCourtByIdAsync(
        int id);

    Task<(bool Success, string Message, BranchCourt? Court)> CreateBranchCourtAsync(
        CreateBranchCourtViewModel model);

    Task<(bool Success, string Message)> UpdateBranchCourtAsync(
        int id,
        UpdateBranchCourtViewModel model);

    Task<(bool Success, string Message)> DeleteBranchCourtAsync(
        int id);

    Task<(bool Success, string Message)> ToggleCourtStatusAsync(
        int id);

    Task<bool> CourtNameExistsInBranchAsync(
        string courtName,
        int branchId,
        int? excludeCourtId = null);

    Task<bool> CanDeleteCourtAsync(
        int id);

    Task<List<BranchCourt>> GetActiveCourtsByBranchAsync(
        int branchId);
}

public class BranchCourtService : IBranchCourtService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BranchCourtService> _logger;

    public BranchCourtService(
        ApplicationDbContext context,
        ILogger<BranchCourtService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<BranchCourtViewModel>> GetAllBranchCourtsAsync()
    {
        return await _context.BranchCourts
            .Include(bc => bc.Branch)
            .OrderBy(bc => bc.Branch.Name)
            .ThenBy(bc => bc.CourtName)
            .Select(bc => new BranchCourtViewModel
            {
                Id = bc.Id,
                CourtName = bc.CourtName,
                BranchId = bc.BranchId,
                BranchName = bc.Branch.Name,
                IsActive = bc.IsActive,
                CreatedAt = bc.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<BranchCourtViewModel>> GetBranchCourtsAsync(
        int branchId)
    {
        return await _context.BranchCourts
            .Include(bc => bc.Branch)
            .Where(bc => bc.BranchId == branchId)
            .OrderBy(bc => bc.CourtName)
            .Select(bc => new BranchCourtViewModel
            {
                Id = bc.Id,
                CourtName = bc.CourtName,
                BranchId = bc.BranchId,
                BranchName = bc.Branch.Name,
                IsActive = bc.IsActive,
                CreatedAt = bc.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<BranchCourtViewModel?> GetBranchCourtByIdAsync(
        int id)
    {
        return await _context.BranchCourts
            .Include(bc => bc.Branch)
            .Where(bc => bc.Id == id)
            .Select(bc => new BranchCourtViewModel
            {
                Id = bc.Id,
                CourtName = bc.CourtName,
                BranchId = bc.BranchId,
                BranchName = bc.Branch.Name,
                IsActive = bc.IsActive,
                CreatedAt = bc.CreatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Success, string Message, BranchCourt? Court)> CreateBranchCourtAsync(
        CreateBranchCourtViewModel model)
    {
        try
        {
            // Check if branch exists
            var branchExists = await _context.Branches.AnyAsync(b => b.Id == model.BranchId && b.IsActive);
            if (!branchExists)
            {
                return (false, "Selected branch does not exist or is inactive.", null);
            }

            // Check if court name already exists in this branch
            if (await CourtNameExistsInBranchAsync(model.CourtName.Trim(), model.BranchId))
            {
                return (false, "A court with this name already exists in the selected branch.", null);
            }

            var branchCourt = new BranchCourt
            {
                CourtName = model.CourtName.Trim(),
                BranchId = model.BranchId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.BranchCourts.Add(branchCourt);
            await _context.SaveChangesAsync();

            return (true, $"Court '{branchCourt.CourtName}' created successfully.", branchCourt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating branch court: {@Model}", model);
            return (false, "An error occurred while creating the court.", null);
        }
    }

    public async Task<(bool Success, string Message)> UpdateBranchCourtAsync(
        int id,
        UpdateBranchCourtViewModel model)
    {
        try
        {
            var branchCourt = await _context.BranchCourts.FindAsync(id);
            if (branchCourt == null)
            {
                return (false, "Court not found.");
            }

            // Check if new court name already exists in the branch (excluding current court)
            if (await CourtNameExistsInBranchAsync(model.CourtName.Trim(), branchCourt.BranchId, id))
            {
                return (false, "A court with this name already exists in this branch.");
            }

            branchCourt.CourtName = model.CourtName.Trim();
            branchCourt.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            return (true, $"Court '{branchCourt.CourtName}' updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating branch court {Id}: {@Model}", id, model);
            return (false, "An error occurred while updating the court.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteBranchCourtAsync(
        int id)
    {
        try
        {
            var branchCourt = await _context.BranchCourts
                .Include(bc => bc.CheckIns)
                .FirstOrDefaultAsync(bc => bc.Id == id);

            if (branchCourt == null)
            {
                return (false, "Court not found.");
            }

            // Check if court has any check-ins
            if (branchCourt.CheckIns.Any())
            {
                return (false,
                    $"Cannot delete court '{branchCourt.CourtName}' because it has associated check-ins. You can deactivate it instead.");
            }

            _context.BranchCourts.Remove(branchCourt);
            await _context.SaveChangesAsync();

            return (true, $"Court '{branchCourt.CourtName}' deleted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting branch court {Id}", id);
            return (false, "An error occurred while deleting the court.");
        }
    }

    public async Task<(bool Success, string Message)> ToggleCourtStatusAsync(
        int id)
    {
        try
        {
            var branchCourt = await _context.BranchCourts.FindAsync(id);
            if (branchCourt == null)
            {
                return (false, "Court not found.");
            }

            branchCourt.IsActive = !branchCourt.IsActive;
            await _context.SaveChangesAsync();

            var statusText = branchCourt.IsActive ? "activated" : "deactivated";
            return (true, $"Court '{branchCourt.CourtName}' has been {statusText}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling court status {Id}", id);
            return (false, "An error occurred while updating the court status.");
        }
    }

    public async Task<bool> CourtNameExistsInBranchAsync(
        string courtName,
        int branchId,
        int? excludeCourtId = null)
    {
        var query = _context.BranchCourts
            .Where(bc => bc.BranchId == branchId &&
                         bc.CourtName.ToLower() == courtName.ToLower());

        if (excludeCourtId.HasValue)
        {
            query = query.Where(bc => bc.Id != excludeCourtId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<bool> CanDeleteCourtAsync(
        int id)
    {
        return !await _context.CheckIns.AnyAsync(ci => ci.BranchCourtId == id);
    }

    public async Task<List<BranchCourt>> GetActiveCourtsByBranchAsync(
        int branchId)
    {
        return await _context.BranchCourts
            .Where(bc => bc.BranchId == branchId && bc.IsActive)
            .OrderBy(bc => bc.CourtName)
            .ToListAsync();
    }
}
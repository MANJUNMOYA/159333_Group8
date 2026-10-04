using System.Security.Claims;
using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Controllers;

public sealed class CateringRequestsController(
    ApplicationDbContext context,
    UserManager<IdentityUser> userManager) : Controller
{
    [HttpGet]
    [Authorize(Roles = PlatformRoles.Customer)]
    public async Task<IActionResult> Create()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var model = new CreateCateringRequestViewModel();
        PopulateCustomer(model, user);
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = PlatformRoles.Customer)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCateringRequestViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        PopulateCustomer(model, user);
        if (string.IsNullOrWhiteSpace(model.CustomerEmail))
        {
            ModelState.AddModelError(string.Empty, "Your account needs an email address to submit a catering request.");
        }
        if (model.Budget.HasValue && decimal.Round(model.Budget.Value, 2) != model.Budget.Value)
        {
            ModelState.AddModelError(nameof(model.Budget), "Enter a budget with no more than two decimal places.");
        }
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var now = DateTime.UtcNow;
        var request = new CateringRequest
        {
            CustomerUserId = user.Id,
            CustomerDisplayName = model.CustomerDisplayName,
            CustomerEmail = model.CustomerEmail,
            EventName = model.EventName.Trim(),
            EventDate = model.EventDate!.Value,
            EventTime = model.EventTime!.Value,
            Location = model.Location.Trim(),
            NumberOfGuests = model.NumberOfGuests,
            Budget = model.Budget,
            DietaryRequirements = model.DietaryRequirements?.Trim() ?? string.Empty,
            AdditionalRequirements = model.AdditionalRequirements?.Trim() ?? string.Empty,
            Status = CateringRequestStatuses.Pending,
            SubmittedAtUtc = now,
            UpdatedAtUtc = now
        };
        context.CateringRequests.Add(request);
        await context.SaveChangesAsync();

        TempData["CateringMessage"] = $"Your catering request #{request.Id} has been submitted. Its status is Pending.";
        return RedirectToAction(nameof(Create));
    }

    [HttpPost("/api/catering-requests/{id:int}/status")]
    [Authorize(Roles = PlatformRoles.Merchant)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] CateringStatusUpdateRequest? input)
    {
        if (input is null || !ModelState.IsValid)
        {
            return BadRequest(new { message = "Choose a valid catering request status." });
        }

        var request = await context.CateringRequests.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (request is null)
        {
            return NotFound(new { message = "Catering request was not found." });
        }
        if (request.Status != input.CurrentStatus)
        {
            return Conflict(new { message = "This request has changed. Refresh the dashboard before updating it." });
        }
        if (!CateringRequestStatuses.NextStatuses(request.Status).Contains(input.Status))
        {
            return BadRequest(new { message = "This status change is not available for the catering request." });
        }

        // Compare the current status in the UPDATE so concurrent merchants cannot overwrite a decision.
        var updated = await context.CateringRequests
            .Where(item => item.Id == id && item.Status == input.CurrentStatus)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, input.Status)
                .SetProperty(item => item.UpdatedAtUtc, DateTime.UtcNow));
        if (updated != 1)
        {
            return Conflict(new { message = "This request has changed. Refresh the dashboard before updating it." });
        }

        return Ok(new
        {
            status = input.Status,
            nextStatuses = CateringRequestStatuses.NextStatuses(input.Status),
            message = $"Catering request #{id} is now {input.Status.ToLowerInvariant()}."
        });
    }

    private void PopulateCustomer(CreateCateringRequestViewModel model, IdentityUser user)
    {
        var displayName = User.FindFirstValue("display_name") ?? user.Email ?? "Customer";
        model.CustomerDisplayName = displayName.Length > 160 ? displayName[..160] : displayName;
        model.CustomerEmail = user.Email ?? string.Empty;
    }
}

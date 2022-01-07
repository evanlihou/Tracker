using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tracker.Data;
using Tracker.Models;

namespace Tracker.Controllers;

[Authorize]
[ApiController]
[Route("reminder-type")]
public class ReminderTypeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReminderTypeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpPost]
    public ActionResult<Reminder> CreateReminderType([FromBody] ReminderTypeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }
        
        var existingType =
            _db.ReminderTypes.Any(x => x.UserId == _userManager.GetUserId(User) && x.Name == model.Name);

        if (existingType)
        {
            return BadRequest(new
            {
                Message = "Reminder type with this name already exists"
            });
        }

        var newType = new ReminderType
        {
            Name = model.Name,
            UserId = _userManager.GetUserId(User)
        };

        _db.ReminderTypes.Add(newType);
        _db.SaveChanges();

        return Ok(newType);
    }

    public class ReminderTypeViewModel
    {
        [MaxLength(100)]
        [Required]
        public string? Name { get; set; }
    }
}
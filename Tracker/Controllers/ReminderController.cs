using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Tracker.Models;

namespace Tracker.Controllers;

[Authorize]
[Route("reminder")]
public class ReminderController : BaseController
{

    [HttpGet]
    public async Task<ActionResult> List()
    {
        var reminders = await Db.Reminders.Include(x => x.ReminderType)
            .Where(x => x.UserId == UserId).ToListAsync();

        return View(new ReminderListViewModel
        {
            Reminders = reminders,
            UserTimeZone = await GetUserTimeZone()
        });
    }

    public class ReminderListViewModel
    {
        public List<Reminder> Reminders = null!;
        public TimeZoneInfo UserTimeZone = null!;
    }

    [HttpGet("{reminderId:int}")]
    public async Task<ActionResult> Edit(int reminderId)
    {
        var reminder = await Db.Reminders.Include(x => x.ReminderType).SingleOrDefaultAsync(x => x.Id == reminderId);

        if (reminder == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
        }

        return View(reminder);
    }

    [HttpPost("{reminderId:int}")]
    public async Task<ActionResult> Edit(int reminderId, [FromForm] Reminder model)
    {
        ModelState.Remove("ReminderType");
        ModelState.Remove("UserId");
        if (!ModelState.IsValid) return View(model);

        var dbReminder = await Db.Reminders.FindAsync(reminderId);

        if (dbReminder == null) return View(null);
        
        dbReminder.Name = model.Name;
        dbReminder.CronLocal = model.CronLocal;
        dbReminder.ReminderTypeId = model.ReminderTypeId;

        await Db.SaveChangesAsync();

        return RedirectToAction("List");
    }

    [HttpPost]
    public async Task<ActionResult<Reminder>> CreateReminder([FromBody] ReminderViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var existing = await Db.Reminders.AnyAsync(x =>
                x.UserId == UserId && x.Name == model.Name &&
                x.ReminderTypeId == model.ReminderTypeId);

        if (existing)
        {
            return BadRequest(new
            {
                Message = "Reminder with this name already exists"
            });
        }

        if (await Db.ReminderTypes.SingleOrDefaultAsync(x =>
                x.Id == model.ReminderTypeId && x.UserId == UserId) == null)
        {
            return BadRequest(new
            {
                Message = "Reminder type not found"
            });
        }

        var userTimeZone = (await UserManager.GetUserAsync(User)).TimeZone;

        var schedule = CronScheduleBuilder.DailyAtHourAndMinute(1, 0).InTimeZone(userTimeZone);
        var trigger = (ICronTrigger)TriggerBuilder
            .Create()
            .WithSchedule(schedule)
            .Build();
        
        var newReminder = new Reminder
        {
            Name = model.Name,
            ReminderTypeId = model.ReminderTypeId,
            UserId = UserId,
            CronLocal = trigger.CronExpressionString
        };

        Db.Reminders.Add(newReminder);
        Db.SaveChanges();

        return Ok(newReminder);
    }

    public class ReminderViewModel
    {
        [MaxLength(100)]
        [Required]
        public string Name { get; set; }
        
        [Required]
        public int ReminderTypeId { get; set; }
        
        [MaxLength(100)]
        public string? CronLocal { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Tracker.Models;

namespace Tracker.Controllers;

[Authorize]
[ApiController]
[Route("reminder")]
public class ReminderController : BaseController
{

    [HttpGet]
    public ActionResult<List<Reminder>> GetReminders()
    {
        var reminders = Db.Reminders.Include(x => x.ReminderType)
            .Where(x => x.UserId == UserId).ToList();
        
        if (!reminders.Any()) return NoContent();
        return Ok(reminders);
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
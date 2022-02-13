using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Tracker.Data.Migrations;
using Tracker.Models;
using Tracker.Services;

namespace Tracker.Controllers;

[Authorize]
[Route("reminder")]
public class ReminderController : BaseController
{
    private readonly ReminderService _reminderService;

    public ReminderController(ReminderService reminderService)
    {
        _reminderService = reminderService;
    }

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

    [HttpGet("create")]
    public ActionResult Create()
    {
        return View();
    }

    [HttpPost("create")]

    public async Task<ActionResult> Create([FromForm] Reminder model)
    {
        ModelState.Remove("ReminderType");
        ModelState.Remove("UserId");
        if (!ModelState.IsValid) return View(model);
        
        var userTimeZone = await GetUserTimeZone();
        
        var dbReminder = new Reminder
        {
            UserId = UserId,
            Name = model.Name,
            CronLocal = model.CronLocal
        };

        if (!await Db.ReminderTypes.AnyAsync(x => x.Id == model.ReminderTypeId && x.UserId == UserId))
        {
            return BadRequest();
        }
        
        dbReminder.ReminderTypeId = model.ReminderTypeId;
        
        if (model.StartDate == null) dbReminder.StartDate = null;
        else dbReminder.StartDate = TimeZoneInfo.ConvertTimeToUtc((DateTime)model.StartDate, userTimeZone);
        if (model.EndDate == null) dbReminder.EndDate = null;
        else dbReminder.EndDate = TimeZoneInfo.ConvertTimeToUtc((DateTime)model.EndDate, userTimeZone);
        dbReminder.ReminderMinutes = model.ReminderMinutes;

        dbReminder.NextRun = await _reminderService.CalculateNextRunTime(dbReminder);

        await Db.Reminders.AddAsync(dbReminder);

        await Db.SaveChangesAsync();

        return RedirectToAction("List");
    }
    
    [HttpGet("{reminderId:int}")]
    public async Task<ActionResult> Edit(int reminderId)
    {
        var userTimeZone = await GetUserTimeZone();
        var reminder = await Db.Reminders.Include(x => x.ReminderType).SingleOrDefaultAsync(x => x.Id == reminderId);

        if (reminder == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View(reminder);
        }
        
        reminder.StartDate = reminder.StartDate != null
            ? TimeZoneInfo.ConvertTimeFromUtc((DateTime)reminder.StartDate, userTimeZone)
            : null;
        reminder.EndDate = reminder.EndDate != null
            ? TimeZoneInfo.ConvertTimeFromUtc((DateTime)reminder.EndDate, userTimeZone)
            : null;

        return View(reminder);
    }

    [HttpPost("{reminderId:int}")]
    public async Task<ActionResult> Edit(int reminderId, [FromForm] Reminder model)
    {
        ModelState.Remove("ReminderType");
        ModelState.Remove("UserId");
        if (!ModelState.IsValid) return View(model);

        var dbReminder = await Db.Reminders.SingleOrDefaultAsync(x => x.Id == reminderId && x.UserId == UserId);
        if (dbReminder == null) return BadRequest();
        

        var userTimeZone = await GetUserTimeZone();
        
        dbReminder.Name = model.Name;
        dbReminder.CronLocal = model.CronLocal;

        if (!await Db.ReminderTypes.AnyAsync(x => x.Id == model.ReminderTypeId && x.UserId == UserId))
        {
            return BadRequest();
        }
        dbReminder.ReminderTypeId = model.ReminderTypeId;
        
        if (model.StartDate == null) dbReminder.StartDate = null;
        else dbReminder.StartDate = TimeZoneInfo.ConvertTimeToUtc((DateTime)model.StartDate, userTimeZone);
        if (model.EndDate == null) dbReminder.EndDate = null;
        else dbReminder.EndDate = TimeZoneInfo.ConvertTimeToUtc((DateTime)model.EndDate, userTimeZone);
        dbReminder.ReminderMinutes = model.ReminderMinutes;

        dbReminder.NextRun = await _reminderService.CalculateNextRunTime(dbReminder);

        await Db.SaveChangesAsync();

        return RedirectToAction("List");
    }

    [HttpGet("completions/{reminderId:int}")]
    public async Task<ActionResult> Completions(int reminderId)
    {
        var completions = await Db.ReminderCompletions.Where(x => x.ReminderId == reminderId).OrderByDescending(x => x.CompletionTime).Take(20).ToListAsync();

        if (completions.Count == 0)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
        }

        return View(completions);
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Models;
using Tracker.Services;

namespace Tracker.Controllers;

[Authorize]
[Route("completion-link")]
public class CompletionLinkController : BaseController
{
    private async Task PopulateReminders()
    {
        ViewBag.Reminders = await Db.Reminders.Where(x => x.UserId == UserId).ToListAsync();
    }
    
    public async Task<ActionResult> List()
    {
        var links = await Db.CompletionLinks.Where(x => x.UserId == UserId).ToListAsync();

        return View(links);
    }
    
    [HttpGet("create")]
    public async Task<ActionResult> Create(int id)
    {
        await PopulateReminders();
        return View();
    }

    [HttpPost("create")]
    public async Task<ActionResult> Create([FromForm] CompletionLink model)
    {
        await PopulateReminders();
        ModelState.Remove("UserId");
        if (!ModelState.IsValid) return View(model);
        
        var dbType = new CompletionLink()
        {
            UserId = UserId,
            Name = model.Name,
            Reminders = model.Reminders,
            Guid = Guid.NewGuid()
        };
        
        Db.CompletionLinks.Add(dbType);

        await Db.SaveChangesAsync();
        
        return RedirectToAction("List");
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult> Edit(int id)
    {
        await PopulateReminders();
        var type = await Db.CompletionLinks.Include(x => x.Reminders)
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);

        return View(type);
    }
    
    [HttpPost("{id:int}")]
    public async Task<ActionResult> Edit(int id, [FromForm] Dictionary<int, object> selectedReminders, [FromForm] CompletionLink model)
    {
        await PopulateReminders();
        ModelState.Remove("UserId");
        foreach (var (key, value) in ModelState.FindKeysWithPrefix("Reminders"))
        {
            ModelState.Remove(key);
        }
        if (!ModelState.IsValid) return View(model);

        var dbType = await Db.CompletionLinks.Include(x => x.Reminders).SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (dbType == null) return NotFound();

        dbType.Name = model.Name;

        dbType.Reminders.RemoveAll(x => true);
        foreach (var reminderId in selectedReminders.Keys)
        {
            dbType.Reminders.Add(await Db.Reminders.FindAsync(reminderId)!);
        }
        //dbType.Reminders = selectedReminders.Select(selected => Db.Reminders.Find(selected.Key)).ToList();

        await Db.SaveChangesAsync();

        return RedirectToAction("List");
    }
    
    [AllowAnonymous]
    [HttpGet("complete/{linkGuid:guid}")]
    public async Task<ActionResult> Complete([FromRoute] Guid linkGuid, [FromServices] ReminderService reminderService)
    {
        // TODO: This is all very naive and may require more in-depth security thinking
        var completionLink = await Db.CompletionLinks.Include(x => x.Reminders).SingleOrDefaultAsync(x => x.Guid == linkGuid);
        if (completionLink is null) return NotFound();
        var numCompletions = 0;
        foreach (var reminder in completionLink.Reminders!)
        {
            if (!reminder.IsPendingCompletion) continue;

            await reminderService.MarkCompleted(reminder.Id, null, false);
            numCompletions++;
        }
        return Ok($"Completed {numCompletions} reminders.");
    }
}
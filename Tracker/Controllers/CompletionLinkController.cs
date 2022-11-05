using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Models;

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
            Reminders = model.Reminders
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
    public async Task<ActionResult> Edit(int id, [FromForm] CompletionLink model)
    {
        await PopulateReminders();
        ModelState.Remove("UserId");
        if (!ModelState.IsValid) return View(model);

        var dbType = await Db.CompletionLinks.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (dbType == null) return NotFound();

        dbType.Name = model.Name;
        dbType.Reminders = model.Reminders;

        await Db.SaveChangesAsync();

        return RedirectToAction("List");
    }
}
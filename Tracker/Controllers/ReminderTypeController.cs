using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Models;
using Tracker.Models.ViewModels;

namespace Tracker.Controllers;

[Authorize]
[Route("reminder-type")]
public class ReminderTypeController : BaseController
{
    [HttpGet("")]
    public async Task<ActionResult> List()
    {
        var types = await Db.ReminderTypes.Where(x => x.UserId == UserId).ToListAsync();

        return View(types);
    }

    [HttpGet("create")]
    public ActionResult Create(int id)
    {
        return View();
    }

    [HttpPost("create")]
    public async Task<ActionResult> Create([FromForm] ReminderType model)
    {
        ModelState.Remove("UserId");
        if (!ModelState.IsValid) return View(model);
        
        var dbType = new ReminderType()
        {
            UserId = UserId,
            Name = model.Name
        };
        
        Db.ReminderTypes.Add(dbType);

        await Db.SaveChangesAsync();
        
        return RedirectToAction("List");
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult> Edit(int id)
    {
        var type = await Db.ReminderTypes.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);

        return View(type);
    }
    
    [HttpPost("{id:int}")]
    public async Task<ActionResult> Edit(int id, [FromForm] ReminderType model)
    {
        ModelState.Remove("UserId");
        if (!ModelState.IsValid) return View(model);
        
        var dbType = await Db.ReminderTypes.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (dbType == null) return NotFound();

        dbType.Name = model.Name;

        await Db.SaveChangesAsync();

        return RedirectToAction("List");
    }
}
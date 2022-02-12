using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Models;

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

    [HttpGet("{id:int}")]
    public async Task<ActionResult> CreateOrEdit(int id)
    {
        if (id == 0) return View(new ReminderType());

        var type = await Db.ReminderTypes.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);

        return type == null ? View() : View(type);
    }

    [HttpPost("{id:int}")]
    public async Task<ActionResult> CreateOrEdit(int id, [FromForm] ReminderType model)
    {
        ReminderType dbType;
        var isCreate = id == 0;
        if (isCreate) dbType = new ReminderType();
        else
        {
            var dbResult = await Db.ReminderTypes.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
            if (dbResult == null) return View();

            dbType = dbResult;
        }

        dbType.Name = model.Name;

        if (isCreate)
        {
            dbType.UserId = UserId;
            Db.ReminderTypes.Add(dbType);
        }

        await Db.SaveChangesAsync();
        
        return RedirectToAction("List");
    }

    public class ReminderTypeViewModel
    {
        [MaxLength(100)]
        [Required]
        public string? Name { get; set; }
    }
}
using System.Net.Cache;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tracker.Data;
using Tracker.Models;

namespace Tracker.Controllers;

public class BaseController : ControllerBase
{
    private UserManager<ApplicationUser>? _userManager;
    private ApplicationDbContext? _db;
    protected UserManager<ApplicationUser> UserManager =>
        _userManager ??= HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();

    protected ApplicationDbContext Db => _db ??= HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

    protected string UserId => UserManager.GetUserId(User);
}
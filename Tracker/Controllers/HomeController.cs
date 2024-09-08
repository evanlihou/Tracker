using Microsoft.AspNetCore.Mvc;

namespace Tracker.Controllers;

public class HomeController : BaseController
{
    [HttpGet]
    public ActionResult Index()
    {
        if (!string.IsNullOrEmpty(UserId))
        {
            return RedirectToAction("List", "Reminder");
        }

        return View();
    }
}
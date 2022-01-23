using Microsoft.AspNetCore.Mvc;

namespace Tracker.Controllers;

public class HomeController : BaseController
{
    [HttpGet]
    public ActionResult Index()
    {
        return View();
    }
}
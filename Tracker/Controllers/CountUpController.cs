using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tracker.Models;
using Tracker.Services;

namespace Tracker.Controllers;

[Authorize]
[Route("countup")]
public class CountUpController(CountUpService service) : BaseController
{
    [HttpGet("")]
    public async Task<ActionResult> List()
    {
        var countUps = await service.GetListForUser(UserId ?? throw new ApplicationException("Could not get user id"));

        return View(countUps);
    }

    [HttpGet("create")]
    public ActionResult Create()
    {
        return View();
    }

    [HttpPost("create")]
    public async Task<ActionResult> Create([FromForm] CountUp countUp)
    {
        ModelState.Remove(nameof(CountUp.UserId));
        ModelState.Remove(nameof(CountUp.Id));
        if (!ModelState.IsValid) return View(countUp);
        countUp.Id = 0;
        var newCountUp = await service.CreateOrEdit(countUp, UserId!);
        if (newCountUp.IsFailed)
        {
            throw new ApplicationException(newCountUp.ToString());
        }

        return RedirectToAction("Edit", new { countUpId = newCountUp.Value.Id });
    }
    
    [HttpGet("{countUpId:int:required}")]
    public async Task<ActionResult> Edit([FromRoute] int countUpId, [FromQuery] bool? success)
    {
        var countUp = await service.Get(countUpId, UserId ?? throw new ApplicationException("Could not get user id"));

        ViewData["Success"] = success;
        
        return View(countUp.Value);
    }
    
    [HttpPost("{countUpId:int:required}")]
    public async Task<ActionResult> Edit([FromRoute] int countUpId, [FromForm] CountUp countUp)
    {
        ModelState.Remove(nameof(CountUp.UserId));
        ModelState.Remove(nameof(CountUp.Id));
        if (!ModelState.IsValid) return View(countUp);
        countUp.Id = countUpId;
        var newCountUp = await service.CreateOrEdit(countUp, UserId!);
        if (newCountUp.IsFailed)
        {
            throw new ApplicationException(newCountUp.ToString());
        }

        return RedirectToAction("Edit", new { countUpId = newCountUp.Value.Id, success = true });
    }

    [HttpGet("{countUpId:int:required}/history")]
    public async Task<ActionResult> History([FromRoute] int countUpId)
    {
        var histories = await service.GetHistory(countUpId, UserId!);
        if (histories.IsFailed)
        {
            throw new ApplicationException(histories.ToString());
        }

        return View(histories.Value);
    }
    
    [HttpGet("{countUpId:int:required}/startStop/{s}")]
    public ActionResult StartStop([FromRoute] int countUpId, [FromRoute] StartStopAction s = StartStopAction.Start)
    {
        return View(new StartStopViewModel
        {
            CountUpId = countUpId,
            Action = s,
            LocalTime = null
        });
    }

    [HttpPost("{countUpId:int:required}/startStop")]
    public async Task<ActionResult> StartStopPost([FromRoute] int countUpId, [FromForm] StartStopViewModel viewModel)
    {
        if (!ModelState.IsValid) return View(viewModel);

        _ = viewModel.Action switch
        {
            StartStopAction.Start => await service.Start(countUpId, UserId!, viewModel.LocalTime,
                viewModel.LocalTime is not null ? await GetUserTimeZone() : null),
            StartStopAction.Stop => await service.Stop(countUpId, UserId!, viewModel.LocalTime,
                viewModel.LocalTime is not null ? await GetUserTimeZone() : null),
            _ => throw new ArgumentOutOfRangeException()
        };

        return RedirectToAction("Edit", new { countUpId, success = true });
    }

    public class StartStopViewModel
    {
        public int CountUpId { get; set; }
        public StartStopAction Action { get; set; }
        public DateTime? LocalTime { get; set; }
    }

    public enum StartStopAction
    {
        Start,
        Stop
    }
}
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Models;
using Tracker.Models.Errors;

namespace Tracker.Services;

public class CountUpService(ApplicationDbContext dbContext)
{
    public async Task<IEnumerable<CountUp>> GetListForUser(string userId)
    {
        return await dbContext.CountUps.AsNoTracking().Where(c => c.UserId == userId).ToListAsync();
    }

    public async Task<Result<CountUp>> Get(int id, string userId)
    {
        var countUp = await dbContext.CountUps.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == id);

        if (countUp is null) return Result.Fail(new NotFoundError());

        return countUp;
    }

    public async Task<Result<IEnumerable<CountUpHistory>>> GetHistory(int id, string userId)
    {
        var countUp = await dbContext.CountUps.Include(c => c.Histories!.OrderByDescending(h => h.Id))
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == id);

        if (countUp is null) return Result.Fail(new NotFoundError());

        return Result.Ok(countUp.Histories!.AsEnumerable());
    }

    public async Task<Result<CountUp>> CreateOrEdit(CountUp updatedRecord, string userId)
    {
        var dbRecord = updatedRecord.Id != 0
            ? await dbContext.CountUps.FirstOrDefaultAsync(c => c.Id == updatedRecord.Id && c.UserId == userId)
            : null;

        if (dbRecord is null)
        {
            dbRecord = new CountUp
            {
                UserId = userId,
                Name = updatedRecord.Name
            };
            await dbContext.CountUps.AddAsync(dbRecord);
        }
        else
        {
            dbRecord.Name = updatedRecord.Name;
            dbContext.CountUps.Update(dbRecord);
        }

        await dbContext.SaveChangesAsync();

        return Result.Ok(dbRecord);
    }

    public async Task<Result> Start(int id, string userId, DateTime? startTimeLocal = null, TimeZoneInfo? timeZone = null)
    {
        var countUp = await dbContext.CountUps.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == id);
        if (countUp is null) return Result.Fail(new NotFoundError());

        DateTime startTime;
        if (startTimeLocal is null || timeZone is null)
        {
            startTime = DateTime.UtcNow;
        }
        else
        {
            startTime = TimeZoneInfo.ConvertTimeToUtc(startTimeLocal.Value, timeZone);
        }

        countUp.CountingFromUtc = startTime;

        var runningHistories = await dbContext.CountUpHistories
            .Where(h => h.CountUpId == countUp.Id && h.EndTimeUtc == null).ToListAsync();

        foreach (var history in runningHistories)
        {
            history.EndTimeUtc = startTime;
        }

        await dbContext.CountUpHistories.AddAsync(new CountUpHistory
        {
            CountUpId = countUp.Id,
            StartTimeUtc = startTime
        });

        await dbContext.SaveChangesAsync();
        
        return Result.Ok();
    }

    public async Task<Result> Stop(int id, string userId, DateTime? startTimeLocal = null, TimeZoneInfo? timeZone = null)
    {
        var countUp = await dbContext.CountUps.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == id);
        if (countUp is null) return Result.Fail(new NotFoundError());
        
        DateTime endTime;
        if (startTimeLocal is null || timeZone is null)
        {
            endTime = DateTime.UtcNow;
        }
        else
        {
            endTime = TimeZoneInfo.ConvertTimeToUtc(startTimeLocal.Value, timeZone);
        }

        countUp.CountingFromUtc = null;
        
        var runningHistories = await dbContext.CountUpHistories
            .Where(h => h.CountUpId == countUp.Id && h.EndTimeUtc == null).ToListAsync();

        foreach (var history in runningHistories)
        {
            history.EndTimeUtc = endTime;
        }

        await dbContext.SaveChangesAsync();
        
        return Result.Ok();
    }
}
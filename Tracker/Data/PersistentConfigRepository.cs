using Microsoft.EntityFrameworkCore;
using Tracker.Models;

namespace Tracker.Data
{
    public class PersistentConfigRepository(ApplicationDbContext dbContext)
    {
        public async Task<PersistentConfig?> GetByCodeOrNull(string configCode, CancellationToken cancellationToken = default)
        {
            return await dbContext.PersistentConfigs.SingleOrDefaultAsync(x => x.ConfigCode == configCode,
                cancellationToken);
        }

        public async Task<PersistentConfig> UpdateByCode(string configCode, string value, CancellationToken cancellationToken = default)
        {
            var config = await GetByCodeOrNull(configCode, cancellationToken);
            if (config == null)
            {
                config = new PersistentConfig
                {
                    ConfigCode = configCode,
                    Value = value
                };
                dbContext.PersistentConfigs.Add(config);
            }
            else
            {
                config.Value = value;
                dbContext.PersistentConfigs.Update(config);   
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            return config;
        }

    }
}
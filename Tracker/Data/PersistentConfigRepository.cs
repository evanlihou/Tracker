using Microsoft.EntityFrameworkCore;
using Tracker.Models;

namespace Tracker.Data
{
    public class PersistentConfigRepository
    {
        private readonly ApplicationDbContext _db;

        public PersistentConfigRepository(ApplicationDbContext dbContext)
        {
            _db = dbContext;
        }
        
        public async Task<PersistentConfig?> GetByCodeOrNull(string configCode, CancellationToken cancellationToken = default)
        {
            return await _db.PersistentConfigs.SingleOrDefaultAsync(x => x.ConfigCode == configCode, cancellationToken);
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
                _db.PersistentConfigs.Add(config);
            }
            else
            {
                config.Value = value;
                _db.PersistentConfigs.Update(config);   
            }
            await _db.SaveChangesAsync(cancellationToken);

            return config;
        }

    }
}
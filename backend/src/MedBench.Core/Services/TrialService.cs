using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;

namespace MedBench.Core.Services
{
    public class TrialService : ITrialService
    {
        private readonly IMongoCollection<Trial> _trials;

        public TrialService(IMongoDatabase database)
        {
            _trials = database.GetCollection<Trial>("Trials");
        }

        public async Task<IEnumerable<Trial>> GetTrialsByExperimentIdAsync(string experimentId)
        {
            return await _trials.Find(t => t.ExperimentId == experimentId)
                              .ToListAsync();
        }
    }
} 
using System.Collections.Generic;
using System.Threading.Tasks;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;

namespace MedBench.Core.Services
{
    public class TrialService : ITrialService
    {
        private readonly ITrialRepository _trialRepository;

        public TrialService(ITrialRepository trialRepository)
        {
            _trialRepository = trialRepository;
        }

        public async Task<IEnumerable<Trial>> GetTrialsByExperimentIdAsync(string experimentId)
        {
            return await _trialRepository.GetByExperimentIdAsync(experimentId);
        }
    }
} 
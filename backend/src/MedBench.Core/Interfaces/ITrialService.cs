using System.Collections.Generic;
using System.Threading.Tasks;

public interface ITrialService
{
    // ... existing methods
    Task<IEnumerable<Trial>> GetTrialsByExperimentIdAsync(string experimentId);
} 
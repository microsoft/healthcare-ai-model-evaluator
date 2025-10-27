//In the frontend this is called Experiments, but in the backend it's called TestScenarios
namespace MedBench.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TestScenariosController : ControllerBase
{
    private readonly ITestScenarioRepository _testScenarioRepository;
    private readonly IClinicalTaskRepository _clinicalTaskRepository;
    private readonly ILogger<TestScenariosController> _logger;

    public TestScenariosController(ITestScenarioRepository testScenarioRepository, IClinicalTaskRepository clinicalTaskRepository, ILogger<TestScenariosController> logger)
    {
        _testScenarioRepository = testScenarioRepository;
        _clinicalTaskRepository = clinicalTaskRepository;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<TestScenario>>> GetAll()
    {
        var scenarios = await _testScenarioRepository.GetAllAsync();
        return Ok(scenarios);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<TestScenario>> Get(string id)
    {
        try
        {
            var scenario = await _testScenarioRepository.GetByIdAsync(id);
            return Ok(scenario);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<TestScenario>> Create(TestScenario scenario)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Ensure we have an ID before saving
            if (string.IsNullOrEmpty(scenario.Id))
            {
                scenario.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            }

            scenario.OwnerId = userId;
            Console.WriteLine($"Creating test scenario with ID: {scenario.Id}");

            var created = await _testScenarioRepository.CreateAsync(scenario);

            // Verify the object has an ID
            if (string.IsNullOrEmpty(created.Id))
            {
                Console.WriteLine("Error: Created test scenario has no ID");
                return StatusCode(500, new { message = "Failed to generate ID for test scenario" });
            }

            // Return just the ID to avoid potential serialization issues
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating test scenario: {ex.Message}");
            return StatusCode(500, new { message = $"Error creating test scenario: {ex.Message}" });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<TestScenario>> Update(string id, TestScenario scenario)
    {
        if (id != scenario.Id)
            return BadRequest();

        try
        {
            var updated = await _testScenarioRepository.UpdateAsync(scenario);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            _logger.LogInformation("Delete request received for test scenario {TestScenarioId}", id);

            // First, try to retrieve the test scenario to see if it exists and log its details
            TestScenario? testScenario = null;
            try
            {
                testScenario = await _testScenarioRepository.GetByIdAsync(id);
                _logger.LogInformation("Retrieved test scenario {TestScenarioId} with TaskId: '{TaskId}', Name: '{Name}'",
                    id, testScenario.TaskId, testScenario.Name);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Test scenario {TestScenarioId} not found", id);
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving test scenario {TestScenarioId}: {ErrorMessage}", id, ex.Message);
                return StatusCode(500, $"Error retrieving test scenario: {ex.Message}");
            }

            // Log the TaskId to see if it's problematic
            if (!string.IsNullOrEmpty(testScenario.TaskId))
            {
                _logger.LogInformation("Test scenario {TestScenarioId} references TaskId: {TaskId}", id, testScenario.TaskId);

                // Try to check if the clinical task exists
                try
                {
                    await _clinicalTaskRepository.GetByIdAsync(testScenario.TaskId);
                    _logger.LogInformation("Clinical task {TaskId} exists", testScenario.TaskId);
                }
                catch (KeyNotFoundException)
                {
                    _logger.LogWarning("Test scenario {TestScenarioId} references non-existent clinical task {TaskId}", id, testScenario.TaskId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking clinical task {TaskId}: {ErrorMessage}", testScenario.TaskId, ex.Message);
                }
            }

            // Now attempt the delete operation
            _logger.LogInformation("Attempting to delete test scenario {TestScenarioId}", id);
            await _testScenarioRepository.DeleteAsync(id);
            _logger.LogInformation("Successfully deleted test scenario {TestScenarioId}", id);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Test scenario {TestScenarioId} not found during delete", id);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting test scenario {TestScenarioId}: {ErrorMessage}", id, ex.Message);
            _logger.LogError("Full exception details: {ExceptionDetails}", ex.ToString());
            return StatusCode(500, $"An error occurred while deleting the test scenario: {ex.Message}");
        }
    }
} 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using MedBench.Core.DTOs;
using System.Security.Claims;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;


namespace MedBench.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModelsController : ControllerBase
{
    private readonly IModelRepository _modelRepository;
    private readonly ILogger<ModelsController> _logger;

    private readonly IModelRunnerFactory _modelRunnerFactory;

    public ModelsController(
        IModelRepository modelRepository,
        ILogger<ModelsController> logger,
        IModelRunnerFactory modelRunnerFactory)
    {
        _modelRepository = modelRepository;
        _logger = logger;
        _modelRunnerFactory = modelRunnerFactory;
    }

    [HttpGet]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<Model>>> GetAll()
    {
        // GetAllAsync returns models with display-safe settings by default
        var models = await _modelRepository.GetAllAsync();
        return Ok(models);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<Model>> Get(string id)
    {
        try
        {
            // GetByIdAsync returns model with display-safe settings by default
            var model = await _modelRepository.GetByIdAsync(id);
            return Ok(model);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<Model>> Create(Model model)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Forbid();

        model.OwnerId = userId;
        var created = await _modelRepository.CreateAsync(model);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<Model>> Update(string id, Model model)
    {
        try
        {
            var existing = await _modelRepository.GetByIdAsync(id);
            
            model.Id = id;
            var updated = await _modelRepository.UpdateAsync(model);
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
            await _modelRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("rankings")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<ModelRanking>>> GetModelRankings()
    {
        try
        {
            var models = await _modelRepository.GetAllAsync();
            var rankings = models.Select(model => new ModelRanking
            {
                Id = model.Id,
                Name = model.Name,
                Type = model.ModelType,
                EloScore = model.ExperimentResults.EloScore,
                AverageRating = model.ExperimentResults.AverageRating,
                CorrectScore = model.ExperimentResults.CorrectScore,
                ValidationTime = model.ExperimentResults.ValidationTime,
                ExperimentResultsByMetric = model.ExperimentResultsByMetric
            })
            .OrderByDescending(r => r.EloScore)
            .ToList();

            return Ok(rankings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model rankings");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/test")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<string>> TestIntegration(string id)
    {
        try
        {
            _logger.LogInformation($"Testing integration for model ID: {id}");
            
            // Use GetByIdWithSecretsAsync to get the model with actual secrets for testing
            var model = await _modelRepository.GetByIdWithSecretsAsync(id);
            _logger.LogInformation($"Found model: {model.Name} with integration type: {model.IntegrationType}");
            
            var modelRunner = _modelRunnerFactory.CreateModelRunner(model);
            _logger.LogInformation($"Created model runner of type: {modelRunner.GetType().Name}");
            
            var inputData = new List<DataContent>(
                [
                    new DataContent {
                        Type = "text",
                        Content = "Hey there!"
                    },
                    new DataContent {
                        Type = "text",
                        Content = "Is this endpoint working?"
                    }
                ]
            );
            
            if(model.IntegrationType == "cxrreportgen"){
                _logger.LogInformation("Adding test image for CXR Report Gen integration");
                using (var image = new Image<Rgba32>(10, 10))
                using (var ms = new MemoryStream())
                {
                    // Create a checkerboard pattern
                    for (int x = 0; x < 10; x++)
                    for (int y = 0; y < 10; y++)
                    {
                        image[x, y] = (x + y) % 2 == 0 ? Color.White : Color.Black;
                    }
                    
                    image.SaveAsPng(ms);
                    var base64Image = Convert.ToBase64String(ms.ToArray());
                    
                    inputData.Add(new DataContent {
                        Type = "imagedata",
                        Content = base64Image
                    });
                }
            }
            
            // Add dummy model output for evaluator testing
            var outputData = new List<ModelOutput>
            {
                new ModelOutput
                {
                    ModelId = model.Id,
                    Output = new List<DataContent>
                    {
                        new DataContent
                        {
                            Type = "text",
                            Content = "This is a test response from the model integration test endpoint."
                        }
                    }
                }
            };
            
            _logger.LogInformation("Calling GenerateOutput...");
            var output = await modelRunner.GenerateOutput("Is this endpoint working?", inputData, outputData);
            _logger.LogInformation($"GenerateOutput completed successfully. Output length: {output?.Length ?? 0}");

            return Ok(output);
        }
        catch(KeyNotFoundException ex)
        {
            _logger.LogError(ex, $"Model with ID {id} not found");
            return NotFound($"Model with ID {id} not found");
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, $"Error testing integration for model {id}: {ex.Message}");
            return StatusCode(500, $"Error testing integration: {ex.Message}");
        }
    }
} 
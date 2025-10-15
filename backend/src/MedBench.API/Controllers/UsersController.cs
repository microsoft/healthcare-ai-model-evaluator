namespace MedBench.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository userRepository, ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(users.Select(ToDto));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<UserDto>> Get(string id)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            return Ok(ToDto(user));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<UserDto>> Create(MedBench.Core.Models.User user)
    {
        // Normalize email casing
        if (!string.IsNullOrWhiteSpace(user.Email))
            user.Email = user.Email.Trim().ToLowerInvariant();
        var created = await _userRepository.CreateAsync(user);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, ToDto(created));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<UserDto>> Update(string id, MedBench.Core.Models.User user)
    {
        if (id != user.Id)
            return BadRequest();

        try
        {
            // Only update non-auth profile fields to avoid clobbering password changes
            var updated = await _userRepository.UpdateProfileAsync(user);
            return Ok(ToDto(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdministratorRole")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await _userRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("by-email")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<UserDto>> GetByEmail([FromBody] EmailRequest emailRequest)
    {
        try
        {
            var userId = await _userRepository.GetUserIdByEmailAsync(emailRequest.Email);
            if (userId == null)
                return NotFound();

            var user = await _userRepository.GetByIdAsync(userId);
            return Ok(ToDto(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by email");
            return StatusCode(500, "Internal server error");
        }
    }

    public class EmailRequest
    {
        public string Email { get; set; } = "";
    }

    // DTO excluding password-related fields
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public string? Expertise { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsModelReviewer { get; set; }
        public string? ModelId { get; set; }
        public Dictionary<string, string> Stats { get; set; } = new();
    }

    private static UserDto ToDto(MedBench.Core.Models.User u) => new UserDto
    {
        Id = u.Id,
        Name = u.Name,
        Email = u.Email,
        Roles = u.Roles,
        Expertise = u.Expertise,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt,
        IsModelReviewer = u.IsModelReviewer,
        ModelId = u.ModelId,
        Stats = u.Stats
    };
} 
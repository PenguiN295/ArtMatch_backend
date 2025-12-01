
namespace ArtMatch;

using ArtMatch.Entities;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class AppController : ControllerBase
{
    AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    public AppController(AppDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }
    [HttpGet("/test")]
    public IActionResult Test()
    {
        return Ok(_dbContext.Users.Count());
    }
    [HttpPost("/users")]
    public IActionResult CreateUser([FromBody]User user)
    {
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();
        return Ok(user);
    }
    [HttpPost("/register")]
    public IActionResult Register([FromBody]RegisterRequest request)
    {
        var existingUser = _dbContext.Users.FirstOrDefault(u => u.Email == request.Email);
        if (existingUser != null)
        {
            return BadRequest("Email already in use.");
        }
        var passwordHash = _passwordHasher.HashPassword(request.Password!);

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Password = passwordHash,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(newUser);
        _dbContext.SaveChanges();

        return Ok(new { Message = "User registered successfully." });
    }
    [HttpPost("/login")]
    public IActionResult Login([FromBody]RegisterRequest request)
    {
        var user = _dbContext.Users.FirstOrDefault(u => u.Email == request.Email);
        if (user == null || !_passwordHasher.VerifyPassword(request.Password!, user.Password!))
        {
            return Unauthorized("Invalid email or password.");
        }
        return Ok(new { Message = "Login successful." });
    }
    
}
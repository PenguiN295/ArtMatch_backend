
namespace ArtMatch;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class AppController : ControllerBase
{
    AppDbContext _dbContext;
    public AppController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    [HttpGet("/test")]
    public IActionResult Test()
    {
        Console.WriteLine( _dbContext.Users.Count());
        return Ok("It works!");
    }
    [HttpPost("/users")]
    public IActionResult CreateUser(Entities.User user)
    {
        user.Username = "JohnDoe";
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();
        return Ok(user);
    }
}
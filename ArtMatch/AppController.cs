
namespace ArtMatch;

using ArtMatch.Interfaces;
using ArtMatch.Entities;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ArtMatch.Services;
using ArtMatch.DTO;
using System.Net.Http.Headers;

[ApiController]
public class AppController : ControllerBase
{
    AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IImageStorageService _imageStorageService;
    private IAiMicroservice _aiMicroservice;
    private IMapper _mapper;


    private FormFile CreateFormFileFromBase64(string base64File)
    {
        var stream = new MemoryStream();
        var bytes = Convert.FromBase64String(base64File);

        stream.Write(bytes);
        stream.Position = 0;

        return new FormFile(stream, 0, stream.Length, "file", "file-name");
    }
    public AppController(AppDbContext dbContext, IPasswordHasher passwordHasher, IMapper mapper, IAiMicroservice aiMicroservice, IImageStorageService imageStorageService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _aiMicroservice = aiMicroservice;
        _imageStorageService = imageStorageService;
    }
    [HttpPost("/register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        var existingUser = _dbContext.Users.FirstOrDefault(u => u.Email == request.Email);
        if (existingUser != null)
        {
            return BadRequest(new { Message = "Email already in use." });
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

        return Ok(new { Message = "User registered successfully.", UserId = newUser.Id.ToString() });
    }
    [HttpPost("/login")]
    public IActionResult Login([FromBody] RegisterRequest request)
    {
        var user = _dbContext.Users.FirstOrDefault(u => u.Email == request.Email);
        if (user == null || !_passwordHasher.VerifyPassword(request.Password!, user.Password!))
        {
            return Unauthorized(new { Message = "Invalid email or password." });
        }
        return Ok(new { Message = "Login successful.", UserId = user.Id.ToString() });
    }
    [HttpPost("/upload-photo")]
    public IActionResult UploadPhoto([FromBody] PhotoDTO photo)
    {
        var user = _dbContext.Users.FirstOrDefault(u => u.Id == photo.UserId);
        if (user == null)
        {
            return BadRequest(new { Message = "User not found." });
        }
        var newPhoto = new Photo
        {
            Id = Guid.NewGuid(),
            Photo_data = photo.Photo_data,
            UserId = photo.UserId,
            UploadedAt = DateTime.UtcNow
        };

        _dbContext.Photos.Add(newPhoto);
        _dbContext.SaveChanges();

        return Ok(new { Message = "Photo uploaded successfully." });
    }

    [HttpGet("/photos/{userId}")]
    public IActionResult GetPhotos(Guid userId)
    {
        var user = _dbContext.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null)
        {
            return BadRequest(new { Message = "User not found." });
        }
        var photos = _dbContext.Photos
        .Where(p => p.UserId == userId)
        .ToList();
        return Ok(photos);
    }
    [HttpDelete("/delete-photo/{photoId}")]
    public IActionResult DeletePhoto(Guid photoId)
    {
        var photo = _dbContext.Photos.FirstOrDefault(p => p.Id == photoId);
        if (photo == null)
        {
            return BadRequest(new { Message = "Photo not found." });
        }
        _dbContext.Photos.Remove(photo);
        _dbContext.SaveChanges();
        return Ok(new { Message = "Photo deleted successfully." });
    }


    [HttpPost("match-photo")]
    public async Task<IActionResult> MatchPhoto([FromBody] MatchPhoto photoData)
    {
        var photo = CreateFormFileFromBase64(photoData.photoData!);
        if (photo == null || photo.Length == 0)
        {
            return BadRequest(new { Message = "No photo uploaded." });
        }
        byte[] photoBytes;
        using (var ms = new MemoryStream())
        {
            await photo.CopyToAsync(ms);
            photoBytes = ms.ToArray();
        }
        using var ms1 = new MemoryStream(photoBytes);
        var result = await _aiMicroservice.FindMatchAsync(ms1, photo.FileName);
        using var ms2 = new MemoryStream(photoBytes);
        bool canSwap = await _aiMicroservice.CheckFaceExistsAsync(ms2);

        var matchImageUrl = Url.Action(
            nameof(GetMatchedImage),
            "App",
            new { matchId = result.MatchId },
            Request.Scheme
        );
        Console.WriteLine("Match Image URL: " + matchImageUrl);
        return Ok(new
        {
            MatchID = result.MatchId,
            Author = result.metadata.author,
            Category = result.metadata.style,
            Name = result.metadata.name,
            SimilarityDistance = result.SimilarityDistance,
            matched_photo = matchImageUrl,
            canSwap = canSwap
        });
    }

    [HttpGet("image/{category}/{matchId}")]
    public IActionResult GetMatchedImage(string matchId, string category)
    {
        var imagePath = _imageStorageService.GetImagePathByMatchId(matchId, category);

        if (string.IsNullOrEmpty(imagePath))
        {
            return NotFound($"Image for ID {matchId} not found on disk.");
        }
        var imageStream = System.IO.File.OpenRead(imagePath);

        return File(imageStream, "image/jpeg");
    }



    [HttpPost("perform-swap")]
    public async Task<IActionResult> PerformSwap([FromBody] SwapRequest request)
    {

        var userFile = CreateFormFileFromBase64(request.UserPhotoBase64);
        using var userStream = userFile.OpenReadStream();

        var artPath = _imageStorageService.GetImagePathByMatchId(request.MatchId, request.Style);
        Console.WriteLine("Art Path: " + artPath);
        if (string.IsNullOrEmpty(artPath))
        {
            return BadRequest(new { Message = "Art image not found for the provided Match ID." });
        }


        var swappedImageBytes = await _aiMicroservice.SwapFaceAsync(userStream, artPath);

        if (swappedImageBytes == null || swappedImageBytes.Length == 0)
        {
            return BadRequest(new { Message = "Failed to perform face swap." });
        }
        string base64Image = Convert.ToBase64String(swappedImageBytes);
        return Ok(new
        {
            SwappedPhotoBase64 = base64Image
        });
    }
}
namespace ArtMatch.Entities;
public class PhotoDTO
{
    public string? Photo_data { get; set; }
    public Guid UserId { get; set; }

    public DateTime UploadedAt { get; set; }
}
namespace ArtMatch.Entities;
public class Photo
{
    public Guid Id { get; set; }
    public string? Photo_data { get; set; }

    public Guid UserId { get; set; }

    public DateTime UploadedAt { get; set; }
}
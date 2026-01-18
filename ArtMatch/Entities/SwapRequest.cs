namespace ArtMatch.Entities;
public class SwapRequest
{
    public string MatchId { get; set; } = string.Empty;
    public string UserPhotoBase64 { get; set; } = string.Empty;

    public string Style { get; set; } = string.Empty ;
}
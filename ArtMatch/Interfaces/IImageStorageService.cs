namespace ArtMatch.DTO;
public interface IImageStorageService
{
    string GetImagePathByMatchId(string matchId,string style);
}
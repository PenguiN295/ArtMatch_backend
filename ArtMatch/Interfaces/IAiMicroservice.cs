namespace ArtMatch.Interfaces;
using ArtMatch.DTO;
public interface IAiMicroservice
{
    public  Task<AiMatchResult> FindMatchAsync(Stream photoStream, string fileName);
}
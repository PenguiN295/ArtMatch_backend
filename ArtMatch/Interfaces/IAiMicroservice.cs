namespace ArtMatch.Interfaces;
using ArtMatch.DTO;
public interface IAiMicroservice
{
    public  Task<AiMatchResult> FindMatchAsync(Stream photoStream, string fileName);
    public Task<byte[]> SwapFaceAsync(Stream userPhotoStream, string artFilePath);
    public Task<bool> CheckFaceExistsAsync(Stream photoStream);
}
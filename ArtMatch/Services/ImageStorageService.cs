using ArtMatch.DTO;
namespace ArtMatch.Services;
public class ImageStorageService : IImageStorageService
{

    private const string DataRootPath = @"D:\DataSets"; 
    private const string JpegExtension = ".jpg"; 


    public string GetImagePathByMatchId(string matchId, string style)
    {
        var parts = matchId.Split('_');
        if (parts.Length < 3)
        {

            return string.Empty;
        }

        var primaryCategory = parts[0]; 
        var authorName = parts[1];
        var pictureName = string.Join("_", parts.Skip(2)); 
        var filename = $"{authorName}_{pictureName}{JpegExtension}"; 
        var style_ = style;
        
        // 3. Assemble the full path
        var fullPath = Path.Combine(
            DataRootPath, 
            primaryCategory, 
            style_,
            filename
        );
        Console.WriteLine(fullPath);
        var normalizedPath = Path.GetFullPath(fullPath);
        if (!File.Exists(normalizedPath))
        {
            return string.Empty;
        }

        return normalizedPath;
    }
}
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArtMatch.Interfaces;
using ArtMatch.DTO;
public class AiMicroservice : IAiMicroservice
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AiMicroservice(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:8000");


        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
    public async Task<AiMatchResult> FindMatchAsync(Stream photoStream, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            var fileContent = new StreamContent(photoStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            content.Add(fileContent, name: "file", fileName: fileName);

            var response = await _httpClient.PostAsync("/find-match", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"AI Match failed ({response.StatusCode}). Detail: {error}");
            }
            var jsonString = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<AiMatchResult>(jsonString, _jsonOptions);

            if (result == null || result.status == "no_match")
            {
                throw new InvalidOperationException("AI service returned no match or an unparsable result.");
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to process match request.", ex);
        }
    }
    public async Task<byte[]> SwapFaceAsync(Stream userPhotoStream, string artFilePath)
    {
        using var content = new MultipartFormDataContent();

        content.Add(new StringContent(artFilePath), "target_path");

        var fileContent = new StreamContent(userPhotoStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "selfie.jpg");

        var response = await _httpClient.PostAsync("/swap-face", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"AI Swap failed: {error}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }
    public async Task<bool> CheckFaceExistsAsync(Stream photoStream)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(photoStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "check.jpg");

        var response = await _httpClient.PostAsync("/check-face", content);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<bool>(json);
        return result;
    }
}
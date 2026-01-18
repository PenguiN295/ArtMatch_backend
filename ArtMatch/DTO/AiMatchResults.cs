namespace ArtMatch.DTO;
 
 using System.Text.Json.Serialization;
public record AiMatchResult(
    string status,
    [property: JsonPropertyName("match_id")] string MatchId,
    [property: JsonPropertyName("similarity_distance")] double SimilarityDistance,
    AiMatchMetadata metadata
);
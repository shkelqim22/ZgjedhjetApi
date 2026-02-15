using System.Text.Json.Serialization;

namespace ZgjedhjetApi.Models.Documents
{
    public class ZgjedhjetDocument
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("kategoria")]
        public string Kategoria { get; set; } = string.Empty;
        [JsonPropertyName("komuna")]
        public string Komuna { get; set; } = string.Empty;
        [JsonPropertyName("qendra_e_Votimit")]
        public string Qendra_e_Votimit { get; set; } = string.Empty;
        [JsonPropertyName("vendVotimi")]
        public string VendVotimi { get; set; } = string.Empty;
        [JsonPropertyName("partia")]
        public string Partia { get; set; } = string.Empty;
        [JsonPropertyName("vota")]
        public int Vota { get; set; }
    }
}

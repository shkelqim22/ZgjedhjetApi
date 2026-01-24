namespace ZgjedhjetApi.Models.DTOs
{
    public class CsvImportResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int RecordsImported { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}

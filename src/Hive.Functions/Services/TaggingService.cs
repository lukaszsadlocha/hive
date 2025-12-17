using Microsoft.Extensions.Logging;

namespace Hive.Functions.Services;

public class TaggingService : ITaggingService
{
    private readonly ILogger<TaggingService> _logger;

    // Dictionary of categories and keywords
    private static readonly Dictionary<string, List<string>> CategoryKeywords = new()
    {
        ["Finanse"] = new() { "budżet", "przychody", "wydatki", "faktura", "rachunek", "płatność", "koszty", "zysk", "strata", "bilans" },
        ["Prawne"] = new() { "umowa", "kontrakt", "regulamin", "warunki", "prawo", "sąd", "pozew", "ugoda" },
        ["HR"] = new() { "pracownik", "zatrudnienie", "rekrutacja", "benefity", "urlop", "wynagrodzenie", "umowa o pracę" },
        ["IT"] = new() { "aplikacja", "system", "baza danych", "serwer", "backup", "security", "infrastruktura", "kod" },
        ["Marketing"] = new() { "kampania", "reklama", "social media", "seo", "content", "branding", "promocja" },
        ["Sprzedaż"] = new() { "klient", "oferta", "produkt", "usługa", "sprzedaż", "transakcja", "zamówienie" },
        ["Dokumentacja"] = new() { "instrukcja", "manual", "przewodnik", "tutorial", "dokumentacja", "specyfikacja" },
        ["Raport"] = new() { "raport", "analiza", "statystyki", "podsumowanie", "wyniki", "zestawienie" }
    };

    public TaggingService(ILogger<TaggingService> logger)
    {
        _logger = logger;
    }

    public async Task<List<string>> GenerateTagsAsync(string? content, string fileName)
    {
        _logger.LogInformation("Generating auto-tags...");

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Przygotuj tekst do analizy
        var textToAnalyze = $"{fileName} {content}".ToLower();

        // Detect categories based on keywords
        foreach (var category in CategoryKeywords)
        {
            var matchedKeywords = category.Value
                .Where(keyword => textToAnalyze.Contains(keyword.ToLower()))
                .ToList();

            if (matchedKeywords.Any())
            {
                tags.Add(category.Key);
                _logger.LogInformation($"Detected category '{category.Key}' based on keywords: {string.Join(", ", matchedKeywords)}");
            }
        }

        // Detect year (if in name or content)
        var yearMatch = System.Text.RegularExpressions.Regex.Match(textToAnalyze, @"20\d{2}");
        if (yearMatch.Success)
        {
            tags.Add(yearMatch.Value);
        }

        // Detect months (in Polish and English)
        var months = new[] { "styczeń", "luty", "marzec", "kwiecień", "maj", "czerwiec",
                            "lipiec", "sierpień", "wrzesień", "październik", "listopad", "grudzień",
                            "january", "february", "march", "april", "may", "june",
                            "july", "august", "september", "october", "november", "december" };

        foreach (var month in months)
        {
            if (textToAnalyze.Contains(month.ToLower()))
            {
                tags.Add(month);
                break;
            }
        }

        // Wykryj format dokumentu z nazwy pliku
        var extension = Path.GetExtension(fileName).ToLower();
        switch (extension)
        {
            case ".pdf":
                tags.Add("PDF");
                break;
            case ".docx":
            case ".doc":
                tags.Add("Word");
                break;
            case ".xlsx":
            case ".xls":
                tags.Add("Excel");
                break;
            case ".pptx":
            case ".ppt":
                tags.Add("PowerPoint");
                break;
        }

        await Task.CompletedTask; // Dla async signature

        var finalTags = tags.ToList();
        _logger.LogInformation($"Generated {finalTags.Count} auto-tags: {string.Join(", ", finalTags)}");

        return finalTags;
    }
}

/*
 * INTEGRACJA Z AZURE OPENAI (do zaimplementowania w przyszłości):
 *
 * using Azure.AI.OpenAI;
 *
 * var client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
 *
 * var prompt = $"""
 *     Przeanalizuj poniższy tekst i wygeneruj listę tagów kategoryzujących dokument.
 *     Zwróć tylko tagi oddzielone przecinkami.
 *
 *     Tekst: {content}
 *     """;
 *
 * var response = await client.GetChatCompletionsAsync(
 *     new ChatCompletionsOptions
 *     {
 *         Messages = { new ChatMessage(ChatRole.User, prompt) },
 *         MaxTokens = 100
 *     }
 * );
 *
 * var tags = response.Value.Choices[0].Message.Content
 *     .Split(',')
 *     .Select(t => t.Trim())
 *     .ToList();
 */

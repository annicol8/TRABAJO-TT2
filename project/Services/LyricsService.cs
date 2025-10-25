using System.Text.Json;

namespace AudioRecognitionApp.Services
{
    public class LyricsService : ILyricsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LyricsService> _logger;

        public LyricsService(
            IHttpClientFactory httpClientFactory,
            ILogger<LyricsService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string?> GetLyricsAsync(string songTitle, string artistName)
        {
            try
            {
                if (string.IsNullOrEmpty(songTitle) || string.IsNullOrEmpty(artistName))
                {
                    _logger.LogWarning("Título o artista no proporcionados.");
                    return "Información insuficiente para buscar letras.";
                }

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var cleanArtist = Uri.EscapeDataString(artistName.Trim());
                var cleanTitle = Uri.EscapeDataString(songTitle.Trim());

                var lyrics = await TryGetLyricsFromMultipleSources(client, cleanArtist, cleanTitle, songTitle, artistName);

                return lyrics ?? "Letras no encontradas para esta canción.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener letras");
                return "Error al obtener las letras. Por favor, intente nuevamente.";
            }
        }

        private async Task<string?> TryGetLyricsFromMultipleSources(
            HttpClient client,
            string cleanArtist,
            string cleanTitle,
            string originalTitle,
            string originalArtist)
        {
            try
            {
                _logger.LogInformation($"Intentando Lyrics.ovh para {originalArtist} - {originalTitle}");
                var lyricsOvhUrl = $"https://api.lyrics.ovh/v1/{cleanArtist}/{cleanTitle}";

                var response = await client.GetAsync(lyricsOvhUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);

                    if (doc.RootElement.TryGetProperty("lyrics", out var lyricsElement))
                    {
                        var lyrics = lyricsElement.GetString();
                        if (!string.IsNullOrWhiteSpace(lyrics))
                        {
                            _logger.LogInformation("Letras encontradas en Lyrics.ovh");
                            return lyrics.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lyrics.ovh no disponible, probando alternativa");
            }

            try
            {
                _logger.LogInformation($"Intentando API alternativa para {originalArtist} - {originalTitle}");

                var canaradoUrl = $"https://lyrist.vercel.app/api/{cleanArtist}/{cleanTitle}";

                var response = await client.GetAsync(canaradoUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);

                    if (doc.RootElement.TryGetProperty("lyrics", out var lyricsElement))
                    {
                        var lyrics = lyricsElement.GetString();
                        if (!string.IsNullOrWhiteSpace(lyrics))
                        {
                            _logger.LogInformation("Letras encontradas en API alternativa");
                            return lyrics.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API alternativa no disponible");
            }

            _logger.LogWarning("No se pudieron obtener letras de ninguna fuente");
            return $"Letras no encontradas para:\n{originalArtist} - {originalTitle}\n\nPuedes buscarlas manualmente en:\nhttps://www.google.com/search?q={cleanArtist}+{cleanTitle}+lyrics";
        }
    }
}

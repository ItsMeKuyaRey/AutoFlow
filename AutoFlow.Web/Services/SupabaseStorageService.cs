using Microsoft.AspNetCore.Http;

namespace AutoFlow.Web.Services
{
    public class SupabaseStorageService
    {
        private const string BucketName = "autoflow-images";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public SupabaseStorageService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<string?> UploadAsync(
            IFormFile file,
            string folder,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp",
                ".gif"
            };

            var extension = Path.GetExtension(file.FileName);

            if (file.Length > 5 * 1024 * 1024 ||
                !allowedExtensions.Contains(
                    extension,
                    StringComparer.OrdinalIgnoreCase) ||
                !file.ContentType.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var supabaseUrl =
                _configuration["SUPABASE_URL"];

            var supabaseKey =
                _configuration["SUPABASE_SECRET_KEY"];

            if (string.IsNullOrWhiteSpace(supabaseUrl))
            {
                throw new InvalidOperationException(
                    "SUPABASE_URL is not configured.");
            }

            if (string.IsNullOrWhiteSpace(supabaseKey))
            {
                throw new InvalidOperationException(
                    "SUPABASE_SECRET_KEY is not configured.");
            }

            var fileName =
                $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

            var objectPath =
                $"{folder}/{fileName}";

            var endpoint =
                $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/" +
                $"{BucketName}/{objectPath}";

            var client =
                _httpClientFactory.CreateClient();

            await using var fileStream =
                file.OpenReadStream();

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    endpoint);

            request.Headers.Add(
                "apikey",
                supabaseKey);

            request.Headers.Add(
                "Authorization",
                $"Bearer {supabaseKey}");

            request.Headers.Add(
                "x-upsert",
                "false");

            var content =
                new StreamContent(fileStream);

            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    file.ContentType);

            request.Content = content;

            using var response =
                await client.SendAsync(
                    request,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error =
                    await response.Content.ReadAsStringAsync(
                        cancellationToken);

                throw new InvalidOperationException(
                    $"Supabase Storage upload failed " +
                    $"({(int)response.StatusCode}): {error}");
            }

            return
                $"{supabaseUrl.TrimEnd('/')}" +
                $"/storage/v1/object/public/" +
                $"{BucketName}/{objectPath}";
        }

        public async Task DeleteAsync(
            string? imageUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            var marker =
                $"/storage/v1/object/public/{BucketName}/";

            var markerIndex =
                imageUrl.IndexOf(
                    marker,
                    StringComparison.OrdinalIgnoreCase);

            if (markerIndex < 0)
            {
                return;
            }

            var objectPath =
                imageUrl.Substring(markerIndex + marker.Length);

            if (string.IsNullOrWhiteSpace(objectPath))
            {
                return;
            }

            var supabaseUrl =
                _configuration["SUPABASE_URL"];

            var supabaseKey =
                _configuration["SUPABASE_SECRET_KEY"];

            if (string.IsNullOrWhiteSpace(supabaseUrl) ||
                string.IsNullOrWhiteSpace(supabaseKey))
            {
                return;
            }

            var endpoint =
                $"{supabaseUrl.TrimEnd('/')}" +
                $"/storage/v1/object/" +
                $"{BucketName}/{objectPath}";

            var client =
                _httpClientFactory.CreateClient();

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Delete,
                    endpoint);

            request.Headers.Add(
                "apikey",
                supabaseKey);

            request.Headers.Add(
                "Authorization",
                $"Bearer {supabaseKey}");

            using var response =
                await client.SendAsync(
                    request,
                    cancellationToken);

            if (!response.IsSuccessStatusCode &&
                response.StatusCode !=
                    System.Net.HttpStatusCode.NotFound)
            {
                var error =
                    await response.Content.ReadAsStringAsync(
                        cancellationToken);

                throw new InvalidOperationException(
                    $"Supabase Storage delete failed " +
                    $"({(int)response.StatusCode}): {error}");
            }
        }
    }
}

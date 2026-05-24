using System.Net.Http.Json;
using System.Text.Json;
using server.Domain.Interfaces.Holidays;
using server.Domain.Models;

namespace server.Infrastructure.Holidays.External;

internal sealed class BrasilApiHolidayProvider(HttpClient httpClient) : IBrasilApiHolidayProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>> GetHolidaysForYear(
        int year,
        CancellationToken cancellationToken)
    {
        var requestUri = $"api/feriados/v1/{year}";

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>.Failure(
                    $"BrasilAPI returned non-success status {(int)response.StatusCode}");
            }

            var dtos = await response.Content.ReadFromJsonAsync<List<BrasilApiHolidayDto>>(
                JsonOptions,
                cancellationToken);

            if (dtos is null)
            {
                return BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>.Failure(
                    "BrasilAPI returned an empty response body");
            }

            return BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>.SuccessResult(dtos);
        }
        catch (TaskCanceledException)
        {
            return BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>.Failure(
                "BrasilAPI request timed out");
        }
        catch (HttpRequestException ex)
        {
            return BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>.Failure(
                $"BrasilAPI HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>.Failure(
                $"BrasilAPI returned invalid JSON: {ex.Message}");
        }
    }
}

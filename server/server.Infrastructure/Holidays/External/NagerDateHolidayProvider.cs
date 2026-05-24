using System.Net.Http.Json;
using System.Text.Json;
using server.Domain.Interfaces.Holidays;
using server.Domain.Models;

namespace server.Infrastructure.Holidays.External;

internal sealed class NagerDateHolidayProvider(HttpClient httpClient) : INagerDateHolidayProvider
{
    private const string BrazilCountryCode = "BR";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>> GetHolidaysForYear(
        int year,
        CancellationToken cancellationToken)
    {
        var requestUri = $"api/v3/PublicHolidays/{year}/{BrazilCountryCode}";

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.Failure(
                    $"Nager.Date returned non-success status {(int)response.StatusCode}");
            }

            var dtos = await response.Content.ReadFromJsonAsync<List<NagerDateHolidayDto>>(
                JsonOptions,
                cancellationToken);

            if (dtos is null)
            {
                return BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.Failure(
                    "Nager.Date returned an empty response body");
            }

            return BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.SuccessResult(dtos);
        }
        catch (TaskCanceledException)
        {
            return BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.Failure(
                "Nager.Date request timed out");
        }
        catch (HttpRequestException ex)
        {
            return BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.Failure(
                $"Nager.Date HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.Failure(
                $"Nager.Date returned invalid JSON: {ex.Message}");
        }
    }
}

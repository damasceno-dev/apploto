using System.Net;
using server.Communication.Responses;
using Shouldly;

namespace WebApi.Test.Infrastructure;

internal static class PostedReportFlow
{
    extension(HttpClient client)
    {
        public async Task<ResponseFinancialResultJson> FinancialResultAsync(string token, DateTime sourceDate, DateTime? cutoff = null)
        {
            var query = cutoff is { } date ? $"&asOfDate={date:yyyy-MM-dd}" : string.Empty;
            var response = await client.GetAuthAsync($"/report/financial-result?year={sourceDate.Year}&month={sourceDate.Month}" + query, token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseFinancialResultJson>();
        }

        public async Task<ResponsePersonalDifferencesJson> PersonalDifferencesAsync(string token, DateTime sourceDate)
        {
            var response = await client.GetAuthAsync($"/report/personal-differences?year={sourceDate.Year}&month={sourceDate.Month}", token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponsePersonalDifferencesJson>();
        }
    }
}

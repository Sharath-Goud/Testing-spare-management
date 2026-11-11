using System.Globalization;
using System.Text.Json;

namespace TestingSpareManagement.Services
{
    public class ExchangeRateService
    {
        private readonly HttpClient _httpClient;

        public ExchangeRateService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<(decimal Rate, DateTime RateDate)> GetInrToCnyRateAsync(
            DateTime consumptionDate)
        {
            DateTime dateToCheck = consumptionDate.Date;

            // Try the selected date first.
            // If it is a weekend/holiday and no rate exists,
            // move backwards to the latest available business-day rate.
            for (int i = 0; i < 7; i++)
            {
                string date = dateToCheck.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);

                string url =
                    $"https://api.frankfurter.dev/v2/rate/INR/CNY?date={date}";

                try
                {
                    using HttpResponseMessage response =
                        await _httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();

                        using JsonDocument document =
                            JsonDocument.Parse(json);

                        JsonElement root = document.RootElement;

                        if (root.TryGetProperty("rate", out JsonElement rateElement))
                        {
                            decimal rate = rateElement.GetDecimal();

                            if (rate > 0)
                            {
                                DateTime actualRateDate = dateToCheck;

                                if (root.TryGetProperty("date", out JsonElement dateElement))
                                {
                                    if (DateTime.TryParse(
                                        dateElement.GetString(),
                                        out DateTime parsedDate))
                                    {
                                        actualRateDate = parsedDate.Date;
                                    }
                                }

                                return (rate, actualRateDate);
                            }
                        }
                    }
                }
                catch
                {
                    // Try the previous date.
                }

                dateToCheck = dateToCheck.AddDays(-1);
            }

            throw new InvalidOperationException(
                $"Unable to obtain INR to CNY exchange rate for {consumptionDate:yyyy-MM-dd}.");
        }
    }
}
#r "Newtonsoft.Json"

#load "Location.csx"

using Newtonsoft.Json.Linq;

public enum WeatherCondition
{
    Other,
    Clear,
    Precipitation
}

internal class WeatherUnderground
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static IReadOnlyDictionary<string, WeatherCondition> weatherMapping = new Dictionary<string, WeatherCondition>()
    {
        { "Clear", WeatherCondition.Clear },
        { "Overcast", WeatherCondition.Clear },
        { "Cloudy", WeatherCondition.Clear },
        { "Clouds", WeatherCondition.Clear },
        { "Drizzle", WeatherCondition.Precipitation },
        { "Hail", WeatherCondition.Precipitation },
        { "Ice", WeatherCondition.Precipitation },
        { "Mist", WeatherCondition.Precipitation },
        { "Precipitation", WeatherCondition.Precipitation },
        { "Rain", WeatherCondition.Precipitation },
        { "Showers", WeatherCondition.Precipitation },
        { "Snow", WeatherCondition.Precipitation },
        { "Spray", WeatherCondition.Precipitation },
        { "Squall", WeatherCondition.Precipitation },
        { "Thunderstorm", WeatherCondition.Precipitation },
    };

    internal static async Task<WeatherCondition> GetCurrentConditionsAsync(Location location)
    {
        var callString = string.Format("http://api.wunderground.com/api/{0}/conditions/q/{1}/{2}.json", Environment.GetEnvironmentVariable("WeatherUndergroundApiKey"), location.State, location.City);
        var response = await httpClient.GetAsync(callString);
        var conditions = await response.Content.ReadAsAsync<JObject>();

        JToken currentObservation;
        if (!conditions.TryGetValue("current_observation", out currentObservation))
        {
            JToken error = conditions.SelectToken("response.error");

            if (error != null)
            {
                throw new InvalidOperationException($"API returned an error: {error}.");
            }
            else
            {
                throw new ArgumentException("Could not find weather for this location. Try being more specific.");
            }
        }

        return MapToWeatherCondition((string)(currentObservation as JObject).GetValue("weather"));
    }

    private static WeatherCondition MapToWeatherCondition(string weather)
    {
        foreach (var pair in weatherMapping)
        {
            if (weather.Contains(pair.Key))
            {
                return pair.Value;
            }
        }

        return WeatherCondition.Other;
    }
}
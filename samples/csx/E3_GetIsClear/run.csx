#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

#load "..\shared\Location.csx"
#load "..\shared\WeatherUnderground.csx"

public static async Task<bool> Run(Location location)
{
    var currentConditions = await WeatherUnderground.GetCurrentConditionsAsync(location);
    return currentConditions.Equals(WeatherCondition.Clear);
}
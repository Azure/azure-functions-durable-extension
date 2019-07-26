using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

public static class GetIsClear
{
    [FunctionName("E3_GetIsClear")]
    public static async Task<bool> Run([ActivityTrigger] Location location)
    {
        var currentConditions = await WeatherUnderground.GetCurrentConditionsAsync(location);
        return currentConditions.Equals(WeatherCondition.Clear);
    }
}

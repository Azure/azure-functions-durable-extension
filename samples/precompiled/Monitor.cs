// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

/* This sample demonstrates the Monitor workflow. In this pattern, the orchestrator function is
 * used to periodically check something's status and take action as appropriate. While a
 * Timer-triggered function can perform similar polling action, the Monitor has additional
 * capabilities:
 *
 *   - manual termination (via request to the orchestrator termination endpoint)
 *   - termination when some condition is met
 *   - monitoring of multiple arbitrary subjects
 *
 * To run this sample, you'll need to define the following app settings:
 *
 *   - TwilioAccountSid: your Twilio account's SID
 *   - TwilioAuthToken: your Twilio account's auth token
 *   - TwilioPhoneNumber: an SMS-capable Twilio number
 *   - WeatherUndergroundApiKey: a WeatherUnderground API key
 *
 * For Twilio trial accounts, you also need to verify the phone number in your MonitorRequest.
 *
 * Twilio: https://www.twilio.com
 * WeatherUnderground API: https://www.wunderground.com/weather/api/d/docs
 */
namespace VSSample
{
    public static class Monitor
    {
        [FunctionName("E3_Monitor")]
        public static async Task Run([OrchestrationTrigger] IDurableOrchestrationContext monitorContext, ILogger log)
        {
            MonitorRequest input = monitorContext.GetInput<MonitorRequest>();
            if (!monitorContext.IsReplaying) { log.LogInformation($"Received monitor request. Location: {input?.Location}. Phone: {input?.Phone}."); }

            VerifyRequest(input);

            DateTime endTime = monitorContext.CurrentUtcDateTime.AddHours(6);
            if (!monitorContext.IsReplaying) { log.LogInformation($"Instantiating monitor for {input.Location}. Expires: {endTime}."); }

            while (monitorContext.CurrentUtcDateTime < endTime)
            {
                // Check the weather
                if (!monitorContext.IsReplaying) { log.LogInformation($"Checking current weather conditions for {input.Location} at {monitorContext.CurrentUtcDateTime}."); }

                bool isClear = await monitorContext.CallActivityAsync<bool>("E3_GetIsClear", input.Location);

                if (isClear)
                {
                    // It's not raining! Or snowing. Or misting. Tell our user to take advantage of it.
                    if (!monitorContext.IsReplaying) { log.LogInformation($"Detected clear weather for {input.Location}. Notifying {input.Phone}."); }

                    await monitorContext.CallActivityAsync("E3_SendGoodWeatherAlert", input.Phone);
                    break;
                }
                else
                {
                    // Wait for the next checkpoint
                    var nextCheckpoint = monitorContext.CurrentUtcDateTime.AddMinutes(30);
                    if (!monitorContext.IsReplaying) { log.LogInformation($"Next check for {input.Location} at {nextCheckpoint}."); }

                    await monitorContext.CreateTimer(nextCheckpoint, CancellationToken.None);
                }
            }

            log.LogInformation($"Monitor expiring.");
        }

        [FunctionName("E3_GetIsClear")]
        public static async Task<bool> GetIsClear([ActivityTrigger] Location location)
        {
            var currentConditions = await WeatherUnderground.GetCurrentConditionsAsync(location);
            return currentConditions.Equals(WeatherCondition.Clear);
        }

        [FunctionName("E3_SendGoodWeatherAlert")]
        public static void SendGoodWeatherAlert(
            [ActivityTrigger] string phoneNumber,
            ILogger log,
            [TwilioSms(AccountSidSetting = "TwilioAccountSid", AuthTokenSetting = "TwilioAuthToken", From = "%TwilioPhoneNumber%")]
                out CreateMessageOptions message)
        {
            message = new CreateMessageOptions(new PhoneNumber(phoneNumber));
            message.Body = $"The weather's clear outside! Go take a walk!";
        }

        [Deterministic]
        private static void VerifyRequest(MonitorRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request), "An input object is required.");
            }

            if (request.Location == null)
            {
                throw new ArgumentNullException(nameof(request.Location), "A location input is required.");
            }

            if (string.IsNullOrEmpty(request.Phone))
            {
                throw new ArgumentNullException(nameof(request.Phone), "A phone number input is required.");
            }
        }
    }

    public class MonitorRequest
    {
        public Location Location { get; set; }

        public string Phone { get; set; }
    }

    public class Location
    {
        public string State { get; set; }

        public string City { get; set; }

        public override string ToString() => $"{City}, {State}";
    }

    public enum WeatherCondition
    {
        Other,
        Clear,
        Precipitation,
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
            var apiKey = Environment.GetEnvironmentVariable("WeatherUndergroundApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("The WeatherUndergroundApiKey environment variable was not set.");
            }

            var callString = string.Format("http://api.wunderground.com/api/{0}/conditions/q/{1}/{2}.json", apiKey, location.State, location.City);
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
}
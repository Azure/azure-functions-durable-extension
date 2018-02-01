﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json.Linq;
using Twilio;

// To run this sample, you'll need a Weather Underground API key. Sign-up and documentation here: https://www.wunderground.com/weather/api/d/docs
namespace VSSample
{
    public static class Monitor
    {
        [FunctionName("E3_Monitor")]
        public static async Task Run([OrchestrationTrigger] DurableOrchestrationContext monitorContext, TraceWriter log)
        {
            MonitorRequest input = monitorContext.GetInput<MonitorRequest>();
            VerifyRequest(input);

            DateTime endTime = monitorContext.CurrentUtcDateTime.AddHours(6);

            while (monitorContext.CurrentUtcDateTime < endTime)
            {
                // Check the weather
                bool isClear = await monitorContext.CallActivityAsync<bool>("E3_GetIsClear", input.Location);

                if (isClear)
                {
                    // It's not raining! Or snowing. Or misting. Tell our user to take advantage of it.
                    await monitorContext.CallActivityAsync("E3_SendGoodWeatherAlert", input.Phone);
                    break;
                }

                // Wait for the next checkpoint
                var nextCheckpoint = monitorContext.CurrentUtcDateTime.AddMinutes(30);
                await monitorContext.CreateTimer(nextCheckpoint, CancellationToken.None);
            }
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
            TraceWriter log,
            [TwilioSms(AccountSidSetting = "TwilioAccountSid", AuthTokenSetting = "TwilioAuthToken", From = "%TwilioPhoneNumber%")]
#if NETSTANDARD2_0
                out CreateMessageOptions message)
#else
                out SMSMessage message)
#endif
        {
#if NETSTANDARD2_0
            message = new CreateMessageOptions(new PhoneNumber(phoneNumber));
#else
            message = new SMSMessage { To = phoneNumber };
#endif
            message.Body = $"The weather's clear outside! Go take a walk!";
        }

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
    }

    public enum WeatherCondition
    {
        Other,
        Clear,
        Precipitation,
    }
    
    internal class WeatherUnderground
    {
        private static HttpClient httpClient = new HttpClient();
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
                throw new ArgumentException("Could not find weather for this location. Try being more specific.");
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
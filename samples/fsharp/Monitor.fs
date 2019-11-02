// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample

open System
open System.Net.Http
open System.Threading
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open Microsoft.Extensions.Logging
open Newtonsoft.Json.Linq
open Twilio.Rest.Api.V2010.Account
open Twilio.Types
open FSharp.Control.Tasks

type WeatherCondition =  Other | Clear | Precipitation

type Location = {
    State: string
    City: string
}

type MonitorRequest = {
    Location: Location
    Phone: string
}

module WeatherUnderground =
    let private httpClient = new HttpClient()
    let private weatherMapping = 
      [
        ( "Clear", WeatherCondition.Clear )
        ( "Overcast", WeatherCondition.Clear )
        ( "Cloudy", WeatherCondition.Clear )
        ( "Clouds", WeatherCondition.Clear )
        ( "Drizzle", WeatherCondition.Precipitation )
        ( "Hail", WeatherCondition.Precipitation )
        ( "Ice", WeatherCondition.Precipitation )
        ( "Mist", WeatherCondition.Precipitation )
        ( "Precipitation", WeatherCondition.Precipitation )
        ( "Rain", WeatherCondition.Precipitation )
        ( "Showers", WeatherCondition.Precipitation )
        ( "Snow", WeatherCondition.Precipitation )
        ( "Spray", WeatherCondition.Precipitation )
        ( "Squall", WeatherCondition.Precipitation )
        ( "Thunderstorm", WeatherCondition.Precipitation )
      ]

    let private mapToWeatherCondition(weather: string) =
        weatherMapping
        |> Seq.tryFind (fun (k, _) -> weather.Contains k)
        |> Option.map snd
        |> Option.defaultValue WeatherCondition.Other

    let getCurrentConditionsAsync location = task {
        let apiKey = Environment.GetEnvironmentVariable "WeatherUndergroundApiKey"
        if (String.IsNullOrEmpty(apiKey)) then
            failwith "The WeatherUndergroundApiKey environment variable was not set."

        let callString = sprintf "http://api.wunderground.com/api/%s/conditions/q/%s/%s.json" apiKey location.State location.City
        let! response = httpClient.GetAsync callString
        let! conditions = response.Content.ReadAsAsync<JObject>()

        let (success, currentObservation) = conditions.TryGetValue "current_observation"
        if (not success) then
             match conditions.SelectToken("response.error") with
             | null -> "Could not find weather for this location. Try being more specific."
             | e -> sprintf "API returned an error: %O." e
             |> failwith

        let jobject = currentObservation |> JObject
        return jobject.GetValue "weather" |> string |> mapToWeatherCondition
    }

module Monitor =

    [<FunctionName("E3_Monitor")>]
    let Run([<OrchestrationTrigger>] monitorContext: IDurableOrchestrationContext, log: ILogger) = task {
        let logOnce s = if (not monitorContext.IsReplaying) then log.LogInformation s

        let input = monitorContext.GetInput<MonitorRequest>()
        if (obj.ReferenceEquals (input, null)) then
          failwith "An input object is required."         
         else if (obj.ReferenceEquals (input.Location, null)) then
           failwith "A location input is required."
        else if (String.IsNullOrEmpty(input.Phone)) then
          failwith "A phone number input is required."

        let locationText = 
          match input.Location with
          | { City = c; State = s } when not(String.IsNullOrEmpty s) -> sprintf "%s, %s" c s
          | { City = c } -> c
        logOnce (sprintf "Received monitor request. Location: %s. Phone: %s." locationText input.Phone)

        let endTime = monitorContext.CurrentUtcDateTime.AddHours 6.
        logOnce (sprintf "Instantiating monitor for %s. Expires: %A." locationText endTime)

        let rec checkWeather() = task {
          if (monitorContext.CurrentUtcDateTime < endTime) then
        
            // Check the weather
            logOnce (sprintf "Checking current weather conditions for %s at %A." locationText monitorContext.CurrentUtcDateTime)

            let! isClear = monitorContext.CallActivityAsync<bool>("E3_GetIsClear", input.Location)

            if (isClear) then
                // It's not raining! Or snowing. Or misting. Tell our user to take advantage of it.
                logOnce (sprintf "Detected clear weather for %s. Notifying %s." locationText input.Phone)

                do! monitorContext.CallActivityAsync("E3_SendGoodWeatherAlert", input.Phone)               
            else
                // Wait for the next checkpoint
                let nextCheckpoint = monitorContext.CurrentUtcDateTime.AddMinutes 30.
                logOnce (sprintf "Next check for %s at %A." locationText nextCheckpoint)

                do! monitorContext.CreateTimer(nextCheckpoint, CancellationToken.None)
                return! checkWeather()
        }

        do! checkWeather()
        
        log.LogInformation "Monitor expiring."
        return ()
    }

    [<FunctionName("E3_GetIsClear")>]
    let GetIsClear([<ActivityTrigger>] location: Location) = task {
      let! currentConditions = WeatherUnderground.getCurrentConditionsAsync location
      return currentConditions = WeatherCondition.Clear
    }

    [<FunctionName("E3_SendGoodWeatherAlert")>]
    let SendGoodWeatherAlert
      ([<ActivityTrigger>] phoneNumber: string,
       [<TwilioSms(AccountSidSetting = "TwilioAccountSid", AuthTokenSetting = "TwilioAuthToken", From = "%TwilioPhoneNumber%")>]
       messageCollector: ICollector<CreateMessageOptions>) = task {
      let message = CreateMessageOptions(PhoneNumber(phoneNumber))
      message.Body <- "The weather's clear outside! Go take a walk!"
      messageCollector.Add message
    }
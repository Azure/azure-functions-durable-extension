const request = require("request-promise-native");

const clearWeatherConditions = ['Overcast', 'Clear', 'Partly Cloudy', 'Mostly Cloudy', 'Scattered Clouds'];

module.exports = async function (context, location) {
    try {
        const data = await getCurrentConditions(location);
        return clearWeatherConditions.includes(data.weather);
    } catch (err) {
        context.log(`E3_GetIsClear encountered an error: ${err}`);
        throw new Error(err);
    }
}

async function getCurrentConditions(location) {
    const options = {
        url: `https://api.wunderground.com/api/${process.env["WeatherUndergroundApiKey"]}/conditions/q/${location.state}/${location.city}.json`,
        method: 'GET',
        json: true
    };

    const body = await request(options);
    if (body.error) {
        throw body.error;
    } else if (body.response && body.response.error) {
        throw body.response.error;
    } else {
        return body.current_observation;
    }
}
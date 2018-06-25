const request = require("request");

const clearWeatherConditions = ['Overcast', 'Clear', 'Partly Cloudy', 'Mostly Cloudy', 'Scattered Clouds'];

module.exports = function (context, location) {
    getCurrentConditions(location)
        .then(function (data) {
            const isClear = clearWeatherConditions.includes(data.weather);
            context.done(null, isClear);
        })
        .catch(function (err) {
            context.log(`E3_GetIsClear encountered an error: ${err}`);
            context.done(err);
        });
};

function getCurrentConditions(location) {
    return new Promise(function (resolve, reject) {
        const options = {
            url: `https://api.wunderground.com/api/${process.env["WeatherUndergroundApiKey"]}/conditions/q/${location.state}/${location.city}.json`,
            method: 'GET',
            json: true
        };
        request(options, function (err, res, body) {
            if (err) {
                reject(err);
            }
            if (body.error) {
                reject(body.error);
            }
            if (body.response.error) {
                reject(body.response.error);
            }
            resolve(body.current_observation);
        });
    });
}
// var builder = WebApplication.CreateBuilder(args);
// var app = builder.Build();

// app.MapGet("/", () => "Hello World!");

// app.Run();

// using Microsoft.AspNetCore.Builder;
// using Microsoft.Extensions.Hosting;
// using System.Collections.Generic;

// var builder = WebApplication.CreateBuilder(args);
// var app = builder.Build();

// // Simulated weather data for demonstration
// var weatherData = new Dictionary<string, WeatherInfo>(System.StringComparer.OrdinalIgnoreCase)
// {
//     { "London", new WeatherInfo { City = "London", TemperatureC = 15, Condition = "Cloudy" } },
//     { "New York", new WeatherInfo { City = "New York", TemperatureC = 20, Condition = "Sunny" } },
//     { "Paris", new WeatherInfo { City = "Paris", TemperatureC = 18, Condition = "Rainy" } }
// };

// // Endpoint to get weather data by city name
// app.MapGet("/weather/{city}", (string city) =>
// {
//     if (weatherData.TryGetValue(city, out var weather))
//     {
//         return Results.Ok(weather);
//     }
//     return Results.NotFound($"Weather data for {city} not found.");
// });

// app.Run();


// // Weather information model
// public class WeatherInfo
// {
//     public string City { get; set; }
//     public int TemperatureC { get; set; }
//     public string Condition { get; set; }

//     // Calculated property for temperature in Fahrenheit
//     public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
// }

//Install EntityFrameworkCore by running below commands
// dotnet add package Microsoft.EntityFrameworkCore
// dotnet add package Microsoft.EntityFrameworkCore.Sqlite


using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Create builder and register services
var builder = WebApplication.CreateBuilder(args);

// Configure EF Core with SQLite for data persistence
builder.Services.AddDbContext<WeatherDbContext>(options =>
    options.UseSqlite("Data Source=weather.db"));

// Register HttpClient for external API calls
builder.Services.AddHttpClient();

// Build the app
var app = builder.Build();

// Ensure the SQLite database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
    dbContext.Database.EnsureCreated();
}

// Get logger instance
var logger = app.Logger;

// Replace with your actual OpenWeatherMap API key
var apiKey = "44983002ab5aeed9018c07d5ef602df7";

//
// Endpoint: Get current weather data (external API call + persistence)
//
app.MapGet("/weather/{city}", async (string city, HttpClient httpClient, WeatherDbContext dbContext) =>
{
    try
    {
        // Build the OpenWeatherMap API URL (metric units for Celsius)
        var url = $"http://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric";
        
        // Call the external API
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return Results.Problem($"Error fetching weather data for {city} from external API.");
        }
        
        // Deserialize the response into our DTO model
        var weatherDto = await response.Content.ReadFromJsonAsync<OpenWeatherResponse>();
        if (weatherDto == null)
        {
            return Results.Problem("Received invalid weather data.");
        }
        
        // Map the DTO to our entity model and set the retrieval time
        var weatherInfo = new WeatherInfo
        {
            City = weatherDto.Name,
            TemperatureC = weatherDto.Main.Temp,
            Condition = weatherDto.Weather.Count > 0 ? weatherDto.Weather[0].Description : "Unknown",
            RetrievedAt = DateTime.UtcNow
        };

        // Persist the weather data to the database
        dbContext.WeatherInfos.Add(weatherInfo);
        await dbContext.SaveChangesAsync();

        return Results.Ok(weatherInfo);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving weather data for {City}", city);
        return Results.Problem("An error occurred while fetching weather data.");
    }
});

//
// Endpoint: Get weather forecast data (external API call, no persistence)
//
app.MapGet("/weather/forecast/{city}", async (string city, HttpClient httpClient) =>
{
    try
    {
        // OpenWeatherMap forecast API endpoint
        var url = $"http://api.openweathermap.org/data/2.5/forecast?q={city}&appid={apiKey}&units=metric";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return Results.Problem($"Error fetching forecast data for {city}.");
        }
        // For simplicity, return the raw JSON response; you can deserialize and transform as needed.
        var forecastData = await response.Content.ReadAsStringAsync();
        return Results.Ok(forecastData);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving forecast data for {City}", city);
        return Results.Problem("An error occurred while fetching forecast data.");
    }
});

//
// Endpoint: Retrieve historical weather data from the database
//
app.MapGet("/weather/history", async (WeatherDbContext dbContext) =>
{
    var history = await dbContext.WeatherInfos.ToListAsync();
    return Results.Ok(history);
});

// Start the application
app.Run();

//
// Models and DbContext definitions
//

// Entity model for weather information stored in the database
public class WeatherInfo
{
    public int Id { get; set; }
    public string City { get; set; }
    public double TemperatureC { get; set; }
    public string Condition { get; set; }
    public DateTime RetrievedAt { get; set; }
}

// DTOs to capture the external API response
public class OpenWeatherResponse
{
    public string Name { get; set; }
    public MainInfo Main { get; set; }
    public List<WeatherDescription> Weather { get; set; }
}

public class MainInfo
{
    public double Temp { get; set; }
}

public class WeatherDescription
{
    public string Description { get; set; }
}

// EF Core DbContext for accessing weather data
public class WeatherDbContext : DbContext
{
    public WeatherDbContext(DbContextOptions<WeatherDbContext> options) : base(options) { }

    public DbSet<WeatherInfo> WeatherInfos { get; set; }
}

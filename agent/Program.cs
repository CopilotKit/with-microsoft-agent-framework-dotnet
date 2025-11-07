using AGUI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ComponentModel;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AGUIJsonSerializerContext.Default);
});

// Register the proverbs state as a singleton
builder.Services.AddSingleton<ProverbsState>();

// Register the agent factory
builder.Services.AddSingleton<ProverbsAgentFactory>();

var app = builder.Build();

// Map the AG-UI agent endpoint
var agentFactory = app.Services.GetRequiredService<ProverbsAgentFactory>();
app.MapAGUIAgent("/", agentFactory.CreateProverbsAgent);

app.Run();

// =================
// State Management
// =================
public class ProverbsState
{
    public List<string> Proverbs { get; set; } = new List<string>();
}

// =================
// Agent Factory
// =================
public class ProverbsAgentFactory
{
    private readonly IConfiguration _configuration;
    private readonly ProverbsState _state;
    private readonly OpenAIClient _openAiClient;

    public ProverbsAgentFactory(IConfiguration configuration, ProverbsState state)
    {
        _configuration = configuration;
        _state = state;

        // Get the GitHub token from configuration
        var githubToken = _configuration["GitHubToken"]
            ?? throw new InvalidOperationException(
                "GitHubToken not found in configuration. " +
                "Please set it using: dotnet user-secrets set GitHubToken \"<your-token>\" " +
                "or get it using: gh auth token");

        _openAiClient = new OpenAIClient(
            new System.ClientModel.ApiKeyCredential(githubToken),
            new OpenAIClientOptions
            {
                Endpoint = new Uri("https://models.inference.ai.azure.com")
            });
    }

    public AGUIAgent CreateProverbsAgent(RunAgentInput agentInput, HttpContext context)
    {
        var chatClient = _openAiClient.GetChatClient("gpt-4o-mini").AsIChatClient();

        var chatClientAgent = new ChatClientAgent(
            chatClient,
            name: "ProverbsAgent",
            description: @"A helpful assistant that helps manage and discuss proverbs.
            You have tools available to add, set, or retrieve proverbs from the list.
            When discussing proverbs, ALWAYS use the get_proverbs tool to see the current list before mentioning, updating, or discussing proverbs with the user.",
            tools: [
                AIFunctionFactory.Create(GetProverbs, new AIFunctionFactoryOptions { Name = "get_proverbs" }),
                AIFunctionFactory.Create(AddProverbs, new AIFunctionFactoryOptions { Name = "add_proverbs" }),
                AIFunctionFactory.Create(SetProverbs, new AIFunctionFactoryOptions { Name = "set_proverbs" }),
                AIFunctionFactory.Create(GetWeather, new AIFunctionFactoryOptions { Name = "get_weather" })
            ]);

        return new ChatClientAGUIAgent(chatClientAgent);
    }

    // =================
    // Tools
    // =================

    [Description("Get the current list of proverbs.")]
    private List<string> GetProverbs()
    {
        Console.WriteLine($"📖 Getting proverbs: {string.Join(", ", _state.Proverbs)}");
        return _state.Proverbs;
    }

    [Description("Add new proverbs to the list.")]
    private ProverbsStateSnapshot AddProverbs([Description("The proverbs to add")] List<string> proverbs)
    {
        Console.WriteLine($"➕ Adding proverbs: {string.Join(", ", proverbs)}");
        _state.Proverbs.AddRange(proverbs);
        return new ProverbsStateSnapshot { Proverbs = _state.Proverbs };
    }

    [Description("Replace the entire list of proverbs.")]
    private ProverbsStateSnapshot SetProverbs([Description("The new list of proverbs")] List<string> proverbs)
    {
        Console.WriteLine($"📝 Setting proverbs: {string.Join(", ", proverbs)}");
        _state.Proverbs = new List<string>(proverbs);
        return new ProverbsStateSnapshot { Proverbs = _state.Proverbs };
    }

    [Description("Get the weather for a given location. Ensure location is fully spelled out.")]
    private WeatherInfo GetWeather([Description("The location to get the weather for")] string location)
    {
        Console.WriteLine($"🌤️  Getting weather for: {location}");
        return new WeatherInfo
        {
            Temperature = 20,
            Conditions = "sunny",
            Humidity = 50,
            WindSpeed = 10,
            FeelsLike = 25
        };
    }
}

// =================
// Data Models
// =================

public class ProverbsStateSnapshot
{
    [JsonPropertyName("proverbs")]
    public List<string> Proverbs { get; set; } = new();
}

public class WeatherInfo
{
    [JsonPropertyName("temperature")]
    public int Temperature { get; init; }

    [JsonPropertyName("conditions")]
    public string Conditions { get; init; } = string.Empty;

    [JsonPropertyName("humidity")]
    public int Humidity { get; init; }

    [JsonPropertyName("wind_speed")]
    public int WindSpeed { get; init; }

    [JsonPropertyName("feelsLike")]
    public int FeelsLike { get; init; }
}

public partial class Program { }

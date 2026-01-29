// Copyright (c) Microsoft. All rights reserved.

// This sample demonstrates how to use the AG-UI client to connect to a remote AG-UI server
// and display streaming updates including conversation/response metadata, text content, and errors.

using System.CommandLine;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using AGUI.Protocol;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AGUIClient;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Add --message option for non-interactive mode
        Option<string?> messageOption = new("--message", "-m")
        {
            Description = "Send a single message and exit (non-interactive mode)"
        };

        // Create root command with options
        RootCommand rootCommand = new("AGUIClient")
        {
            messageOption
        };

        rootCommand.SetAction((parseResult, ct) =>
        {
            string? message = parseResult.GetValue(messageOption);
            return HandleCommandsAsync(message, ct);
        });

        // Run the command
        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task HandleCommandsAsync(string? singleMessage, CancellationToken cancellationToken)
    {
        // Set up the logging
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        ILogger logger = loggerFactory.CreateLogger("AGUIClient");

        // Retrieve configuration settings
        IConfigurationRoot configRoot = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .Build();

        string serverUrl = configRoot["AGUI_SERVER_URL"] ?? "http://localhost:5100";

        logger.LogInformation("Connecting to AG-UI server at: {ServerUrl}", serverUrl);

        // Create the AG-UI client agent
        using HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        var changeBackground = AIFunctionFactory.Create(
            () =>
            {
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine("Changing color to blue");
            },
            name: "change_background_color",
            description: "Change the console background color to dark blue."
        );

        var readClientClimateSensors = AIFunctionFactory.Create(
            ([Description("The sensors measurements to include in the response")] SensorRequest request) =>
            {
                return new SensorResponse()
                {
                    Temperature = 22.5,
                    Humidity = 45.0,
                    AirQualityIndex = 75
                };
            },
            name: "read_client_climate_sensors",
            description: "Reads the climate sensor data from the client device.",
            serializerOptions: AGUIClientSerializerContext.Default.Options
        );

        var chatClient = new AGUIChatClient(
            httpClient,
            serverUrl,
            jsonSerializerOptions: AGUIClientSerializerContext.Default.Options);

        AIAgent agent = chatClient.AsAIAgent(
            name: "agui-client",
            description: "AG-UI Client Agent",
            tools: [changeBackground, readClientClimateSensors]);

        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        List<ChatMessage> messages = [new(ChatRole.System, "You are a helpful assistant.")];

        bool interactiveMode = singleMessage == null;

        try
        {
            while (true)
            {
                // Get user message (or use command line argument in non-interactive mode)
                string? message;
                if (interactiveMode)
                {
                    Console.Write("\nUser (:q or quit to exit): ");
                    message = Console.ReadLine();
                }
                else
                {
                    message = singleMessage;
                    Console.WriteLine($"\nUser: {message}");
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    Console.WriteLine("Request cannot be empty.");
                    if (!interactiveMode)
                    {
                        break;
                    }
                    continue;
                }

                if (message is ":q" or "quit")
                {
                    break;
                }

                messages.Add(new(ChatRole.User, message));

                // Call RunStreamingAsync to get streaming updates
                string? sessionId = null;
                await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session, cancellationToken: cancellationToken))
                {
                    // Use AsChatResponseUpdate to access ChatResponseUpdate properties
                    ChatResponseUpdate chatUpdate = update.AsChatResponseUpdate();
                    if (chatUpdate.ConversationId != null)
                    {
                        sessionId = chatUpdate.ConversationId;
                    }

                    // Handle AG-UI lifecycle events via RawRepresentation
                    // The RawRepresentation may contain an AgentResponseUpdate -> ChatResponseUpdate -> BaseEvent chain
                    switch (GetAGUIEvent(update))
                    {
                        case RunStartedEvent runStarted:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"\n[Run Started - Session: {sessionId}, Run: {runStarted.RunId}]");
                            Console.ResetColor();
                            continue;

                        case RunFinishedEvent runFinished:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"\n[Run Finished - Session: {sessionId}, Run: {runFinished.RunId}]");
                            Console.ResetColor();
                            continue;

                        case RunErrorEvent runError:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\n[Run Error - Session: {sessionId}, Message: {runError.Message}]");
                            Console.ResetColor();
                            continue;
                    }

                    // Display different content types with appropriate formatting
                    foreach (AIContent content in update.Contents)
                    {
                        switch (content)
                        {
                            case TextContent textContent:
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.Write(textContent.Text);
                                Console.ResetColor();
                                break;

                            case FunctionCallContent functionCallContent:
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"\n[Function Call - Name: {functionCallContent.Name}, Arguments: {PrintArguments(functionCallContent.Arguments)}]");
                                Console.ResetColor();
                                break;

                            case FunctionResultContent functionResultContent:
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                if (functionResultContent.Exception != null)
                                {
                                    Console.WriteLine($"\n[Function Result - Exception: {functionResultContent.Exception}]");
                                }
                                else
                                {
                                    Console.WriteLine($"\n[Function Result - Result: {functionResultContent.Result}]");
                                }
                                Console.ResetColor();
                                break;

                            case ErrorContent errorContent:
                                Console.ForegroundColor = ConsoleColor.Red;
                                string code = errorContent.AdditionalProperties?["Code"] as string ?? "Unknown";
                                Console.WriteLine($"\n[Error - Code: {code}, Message: {errorContent.Message}]");
                                Console.ResetColor();
                                break;
                        }
                    }
                }
                messages.Clear();
                Console.WriteLine();

                // Exit after single message in non-interactive mode
                if (!interactiveMode)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("AGUIClient operation was canceled.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not ThreadAbortException and not AccessViolationException)
        {
            logger.LogError(ex, "An error occurred while running the AGUIClient");
            return;
        }
    }

    private static string PrintArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments == null)
        {
            return "";
        }
        var builder = new StringBuilder().AppendLine();
        foreach (var kvp in arguments)
        {
            builder
                .AppendLine($"   Name: {kvp.Key}")
                .AppendLine($"   Value: {kvp.Value}");
        }
        return builder.ToString();
    }

    // Helper function to extract AG-UI BaseEvent from the RawRepresentation chain
    private static BaseEvent? GetAGUIEvent(AgentResponseUpdate update)
    {
        return FindBaseEvent(update.RawRepresentation);

        static BaseEvent? FindBaseEvent(object? obj)
        {
            return obj switch
            {
                BaseEvent baseEvent => baseEvent,
                AgentResponseUpdate aru => FindBaseEvent(aru.RawRepresentation),
                ChatResponseUpdate cru => FindBaseEvent(cru.RawRepresentation),
                _ => null
            };
        }
    }
}

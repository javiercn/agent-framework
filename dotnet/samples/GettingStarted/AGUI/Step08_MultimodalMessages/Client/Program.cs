// Copyright (c) Microsoft. All rights reserved.

using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

string serverUrl = Environment.GetEnvironmentVariable("AGUI_SERVER_URL") ?? "http://localhost:8888";

Console.WriteLine("""

    ========================================
    AG-UI Multimodal Client
    ========================================

    This client demonstrates multimodal message support in the AG-UI protocol.
    You can send text messages combined with images for analysis.

    Commands:
      /image <path>     - Load an image file to send with your next message
      /url <url>        - Add a URL reference to send with your next message
      /clear            - Clear any loaded media
      /help             - Show this help message
      :q or quit        - Exit the client

    Example workflow:
      1. Type: /image C:\path\to\image.png
      2. Type: What's in this image?
      3. The image will be sent along with your question

    """);

Console.WriteLine($"Connecting to AG-UI server at: {serverUrl}\n");

// Create the AG-UI client agent
using HttpClient httpClient = new()
{
    Timeout = TimeSpan.FromSeconds(120) // Longer timeout for image processing
};

AGUIChatClient chatClient = new(httpClient, serverUrl);

AIAgent agent = chatClient.AsAIAgent(
    name: "agui-multimodal-client",
    description: "AG-UI Multimodal Client Agent");

AgentSession session = await agent.GetNewSessionAsync();
List<ChatMessage> messages =
[
    new(ChatRole.System, "You are a helpful vision-capable assistant that can analyze images and other media content.")
];

// State for pending media content
AIContent? pendingMediaContent = null;
string? pendingMediaDescription = null;

try
{
    while (true)
    {
        // Show pending media indicator
        if (pendingMediaContent != null)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  [📎 Media loaded: {pendingMediaDescription}]");
            Console.ResetColor();
        }

        Console.Write("\nUser (:q or quit to exit): ");
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("Request cannot be empty.");
            continue;
        }

        if (input is ":q" or "quit")
        {
            break;
        }

        // Handle commands
        if (input.StartsWith('/') && await HandleCommand(input))
        {
            continue;
        }

        // Build the message with text and optional media
        List<AIContent> messageContents = [];

        // Add text content first
        messageContents.Add(new TextContent(input));

        // Add pending media if any
        if (pendingMediaContent != null)
        {
            messageContents.Add(pendingMediaContent);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  [Sending message with attached media: {pendingMediaDescription}]");
            Console.ResetColor();

            // Clear pending media after sending
            pendingMediaContent = null;
            pendingMediaDescription = null;
        }

        ChatMessage userMessage = new(ChatRole.User, messageContents);
        messages.Add(userMessage);

        // Stream the response
        Console.WriteLine();

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session))
        {
            // Check for AG-UI lifecycle events via RawRepresentation
            switch (GetAGUIEvent(update))
            {
                case RunStartedEvent runStarted:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Run Started - Session: {runStarted.ThreadId}, Run: {runStarted.RunId}]");
                    Console.ResetColor();
                    continue;

                case RunFinishedEvent runFinished:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n[Run Finished - Session: {runFinished.ThreadId}]");
                    Console.ResetColor();
                    continue;

                case RunErrorEvent runError:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Run Error: {runError.Message}]");
                    Console.ResetColor();
                    continue;
            }

            // Display streaming text content
            foreach (AIContent content in update.Contents)
            {
                if (content is TextContent textContent)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(textContent.Text);
                    Console.ResetColor();
                }
                else if (content is ErrorContent errorContent)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Error: {errorContent.Message}]");
                    Console.ResetColor();
                }
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nAn error occurred: {ex.Message}");
}

// Command handler
async Task<bool> HandleCommand(string command)
{
    string[] parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    string cmd = parts[0];
    string? arg = parts.Length > 1 ? parts[1] : null;

    switch (cmd.ToUpperInvariant())
    {
        case "/IMAGE":
            if (string.IsNullOrWhiteSpace(arg))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Usage: /image <path-to-image>");
                Console.ResetColor();
                return true;
            }

            try
            {
                if (!File.Exists(arg))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"File not found: {arg}");
                    Console.ResetColor();
                    return true;
                }

                byte[] imageBytes = await File.ReadAllBytesAsync(arg);
                string mediaType = GetMediaType(arg);
                string fileName = Path.GetFileName(arg);

                DataContent dataContent = new(imageBytes, mediaType);
                dataContent.AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["filename"] = fileName
                };

                pendingMediaContent = dataContent;
                pendingMediaDescription = $"{fileName} ({FormatFileSize(imageBytes.Length)}, {mediaType})";

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Image loaded: {pendingMediaDescription}");
                Console.WriteLine("  Type your question about the image to send it.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error loading image: {ex.Message}");
                Console.ResetColor();
            }
            return true;

        case "/URL":
            if (string.IsNullOrWhiteSpace(arg))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Usage: /url <url>");
                Console.ResetColor();
                return true;
            }

            try
            {
                Uri uri = new(arg);
                string mediaType = GetMediaTypeFromUrl(arg);

                pendingMediaContent = new UriContent(uri, mediaType);
                pendingMediaDescription = $"URL: {uri.Host}{uri.AbsolutePath}";

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ URL loaded: {arg}");
                Console.WriteLine("  Type your question about the content to send it.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error parsing URL: {ex.Message}");
                Console.ResetColor();
            }
            return true;

        case "/CLEAR":
            pendingMediaContent = null;
            pendingMediaDescription = null;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Media cleared.");
            Console.ResetColor();
            return true;

        case "/HELP":
            Console.WriteLine("""

                Commands:
                  /image <path>     - Load an image file to send with your next message
                  /url <url>        - Add a URL reference to send with your next message
                  /clear            - Clear any loaded media
                  /help             - Show this help message
                  :q or quit        - Exit the client

                Supported image formats: PNG, JPEG, GIF, WebP

                """);
            return true;

        default:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Unknown command: {cmd}. Type /help for available commands.");
            Console.ResetColor();
            return true;
    }
}

// Helper function to get MIME type from file extension
static string GetMediaType(string filePath)
{
    string ext = Path.GetExtension(filePath).ToUpperInvariant();
    return ext switch
    {
        ".PNG" => "image/png",
        ".JPG" or ".JPEG" => "image/jpeg",
        ".GIF" => "image/gif",
        ".WEBP" => "image/webp",
        ".BMP" => "image/bmp",
        ".SVG" => "image/svg+xml",
        ".PDF" => "application/pdf",
        ".MP3" => "audio/mpeg",
        ".WAV" => "audio/wav",
        ".MP4" => "video/mp4",
        ".WEBM" => "video/webm",
        _ => "application/octet-stream"
    };
}

// Helper function to get MIME type from URL
static string GetMediaTypeFromUrl(string url)
{
    try
    {
        string path = new Uri(url).AbsolutePath;
        return GetMediaType(path);
    }
    catch
    {
        return "application/octet-stream";
    }
}

// Helper function to format file size
static string FormatFileSize(long bytes)
{
    string[] sizes = ["B", "KB", "MB", "GB"];
    int order = 0;
    double size = bytes;

    while (size >= 1024 && order < sizes.Length - 1)
    {
        order++;
        size /= 1024;
    }

    return $"{size:0.##} {sizes[order]}";
}

// Helper function to extract AG-UI BaseEvent from the RawRepresentation chain
static BaseEvent? GetAGUIEvent(AgentResponseUpdate update)
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

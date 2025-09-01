using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static ModelContextProtocol.Protocol.ElicitRequestParams;

namespace Mcp.HelloWorld.Server.Tools;

/// <summary>
/// Demonstrates the Elicitation feature by collecting user information about their favorite things.
/// This tool showcases how MCP servers can interactively gather structured input from users
/// through the elicitation protocol, similar to the Node.js "everything" server implementation.
/// </summary>
[McpServerToolType]
public sealed class ElicitationTool
{
    [McpServerTool(Name = "startElicitation"), 
     Description("Demonstrates the Elicitation feature by asking the user to provide information about their favorite color, number, and pets.")]
    public static async Task<string> StartElicitation(
        IMcpServer server,
        CancellationToken cancellationToken = default)
    {
        // Check if the client supports elicitation
        if (server.ClientCapabilities?.Elicitation == null)
        {
            return "❌ Client does not support elicitation feature. Please ensure your MCP client has elicitation capabilities enabled.";
        }

        try
        {
            // Define the schema for collecting user's favorite things
            var favoritesSchema = new RequestSchema
            {
                Properties =
                {
                    ["color"] = new StringSchema
                    {
                        Description = "Your favorite color"
                    },
                    ["number"] = new NumberSchema
                    {
                        Description = "Your favorite number",
                        Minimum = 1,
                        Maximum = 100
                    },
                    ["pets"] = new EnumSchema
                    {
                        Description = "Your favorite type of pets",
                        Enum = new[]
                        {
                            "Dogs",
                            "Cats",
                            "Birds",
                            "Pigs"
                        }
                    }
                }
            };

            // Request user input through elicitation
            var elicitationResult = await server.ElicitAsync(new ElicitRequestParams
            {
                Message = "🎯 Let's learn about your favorite things! Please provide the following information:",
                RequestedSchema = favoritesSchema
            }, cancellationToken);

            // Handle different response actions
            var responseContent = new List<string>();

            switch (elicitationResult.Action)
            {
                case "accept" when elicitationResult.Content != null:
                    responseContent.Add("✅ User provided their favorite things!");
                    
                    // Extract the collected data
                    var content = elicitationResult.Content;
                    var color = content.TryGetValue("color", out var colorElement) && colorElement.ValueKind == JsonValueKind.String 
                        ? colorElement.GetString() : "not specified";
                    var number = content.TryGetValue("number", out var numberElement) && numberElement.ValueKind == JsonValueKind.Number 
                        ? numberElement.GetDouble().ToString() : "not specified";
                    var pets = content.TryGetValue("pets", out var petsElement) && petsElement.ValueKind == JsonValueKind.String 
                        ? petsElement.GetString() : "not specified";

                    responseContent.Add($"Their favorites are:");
                    responseContent.Add($"- Color: {color}");
                    responseContent.Add($"- Number: {number}");
                    responseContent.Add($"- Pets: {pets}");
                    break;

                case "decline":
                    responseContent.Add("❌ User declined to provide their favorite things.");
                    break;

                case "cancel":
                    responseContent.Add("⚠️ User cancelled the elicitation dialog.");
                    break;

                default:
                    responseContent.Add($"🤔 Unexpected elicitation action: {elicitationResult.Action}");
                    break;
            }

            // Add raw result for debugging (similar to Node.js implementation)
            responseContent.Add("");
            responseContent.Add("Raw elicitation result:");
            responseContent.Add(JsonSerializer.Serialize(elicitationResult, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            }));

            return string.Join("\n", responseContent);
        }
        catch (OperationCanceledException)
        {
            return "⚠️ Elicitation operation was cancelled.";
        }
        catch (McpException ex)
        {
            return $"❌ MCP Error during elicitation: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"❌ Unexpected error during elicitation: {ex.Message}";
        }
    }

    [McpServerTool(Name = "interactiveGuessGame"), 
     Description("A simple interactive guessing game that demonstrates multi-step elicitation with game logic.")]
    public static async Task<string> InteractiveGuessGame(
        IMcpServer server,
        CancellationToken cancellationToken = default)
    {
        // Check if the client supports elicitation
        if (server.ClientCapabilities?.Elicitation == null)
        {
            return "❌ Client does not support elicitation feature.";
        }

        try
        {
            // First ask if the user wants to play
            var playSchema = new RequestSchema
            {
                Properties =
                {
                    ["wantsToPlay"] = new BooleanSchema
                    {
                        Description = "Do you want to play the guessing game?"
                    }
                }
            };

            var playResponse = await server.ElicitAsync(new ElicitRequestParams
            {
                Message = "🎮 Welcome to the Number Guessing Game! Do you want to play?",
                RequestedSchema = playSchema
            }, cancellationToken);

            if (playResponse.Action != "accept" || 
                playResponse.Content?.TryGetValue("wantsToPlay", out var playElement) != true ||
                playElement.ValueKind != JsonValueKind.True)
            {
                return "👋 Maybe next time! Thanks for considering the game.";
            }

            // Get player's name
            var nameSchema = new RequestSchema
            {
                Properties =
                {
                    ["playerName"] = new StringSchema
                    {
                        Description = "What's your name?"
                    }
                }
            };

            var nameResponse = await server.ElicitAsync(new ElicitRequestParams
            {
                Message = "🎯 Great! What's your name?",
                RequestedSchema = nameSchema
            }, cancellationToken);

            if (nameResponse.Action != "accept")
            {
                return "👋 Game cancelled. See you next time!";
            }

            var playerName = nameResponse.Content?.TryGetValue("playerName", out var nameElement) == true && 
                           nameElement.ValueKind == JsonValueKind.String 
                ? nameElement.GetString() 
                : "Player";

            // Generate target number
            var random = new Random();
            var targetNumber = random.Next(1, 11);
            var attempts = 0;
            var maxAttempts = 5;

            while (attempts < maxAttempts)
            {
                attempts++;
                var guessSchema = new RequestSchema
                {
                    Properties =
                    {
                        ["guess"] = new NumberSchema
                        {
                            Description = $"Enter your guess (1-10). Attempt {attempts}/{maxAttempts}",
                            Minimum = 1,
                            Maximum = 10
                        }
                    }
                };

                var message = attempts == 1 
                    ? $"🎲 Hi {playerName}! I'm thinking of a number between 1 and 10. Can you guess it?"
                    : $"🎲 Try again, {playerName}! What's your next guess?";

                var guessResponse = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = message,
                    RequestedSchema = guessSchema
                }, cancellationToken);

                if (guessResponse.Action != "accept")
                {
                    return $"🚪 Game ended. The number was {targetNumber}. Thanks for playing, {playerName}!";
                }

                if (guessResponse.Content?.TryGetValue("guess", out var guessElement) != true ||
                    guessElement.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var guess = (int)guessElement.GetDouble();

                if (guess == targetNumber)
                {
                    return $"🎉 Congratulations, {playerName}! You guessed the number {targetNumber} correctly in {attempts} attempts!";
                }

                if (attempts >= maxAttempts)
                {
                    return $"😅 Game over, {playerName}! You've used all {maxAttempts} attempts. The number was {targetNumber}. Better luck next time!";
                }

                // Continue with feedback for next attempt
                // This will be handled in the next iteration of the loop
            }

            return $"🎯 Game completed for {playerName}. Thanks for playing!";
        }
        catch (OperationCanceledException)
        {
            return "⚠️ Game was cancelled.";
        }
        catch (Exception ex)
        {
            return $"❌ Game error: {ex.Message}";
        }
    }
}

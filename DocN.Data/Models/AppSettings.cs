namespace DocN.Data.Models;

public class FileStorageSettings
{
    public string UploadPath { get; set; } = "Uploads";
    public int MaxFileSizeMB { get; set; } = 50;
    public List<string> AllowedExtensions { get; set; } = new();
}

public class AISettings
{
    public string Provider { get; set; } = "Gemini"; // Gemini, OpenAI, AzureOpenAI
    public bool EnableFallback { get; set; } = true;
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4";
    public int MaxTokens { get; set; } = 1000;
}

public class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;
}

public class EmbeddingsSettings
{
    public string Provider { get; set; } = "AzureOpenAI"; // AzureOpenAI, OpenAI, Gemini
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
}

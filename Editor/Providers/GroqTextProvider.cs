using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AIPipeline.Runtime;
using UnityEngine;
using UnityEngine.Networking;

namespace AIPipeline.Editor
{
    public class GroqTextProvider
    {
        private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
        private readonly string _apiKey;
        private readonly string _model;

        public GroqTextProvider(string apiKey, string model = "llama-3.3-70b-versatile")
        {
            _apiKey = apiKey;
            _model = model;
        }

        public async Task<ItemGenerationDto> GenerateItemJsonAsync(string userPrompt, CancellationToken ct)
        {
            string systemPrompt = 
                "You are a Game Design Assistant. Return ONLY a valid JSON object matching this exact schema: " +
                "{\"itemName\": \"string (in Russian)\", \"description\": \"string (in Russian)\", " +
                "\"rarity\": \"Common|Rare|Epic|Legendary\", \"levelRequirement\": int, \"attackPower\": int, " +
                "\"goldValue\": int, \"sdIconPrompt\": \"detailed English prompt for Stable Diffusion 2D RPG icon\"}";

            // Формируем тело запроса с принудительным JSON Mode
            string requestBody = $@"{{
                ""model"": ""{_model}"",
                ""response_format"": {{ ""type"": ""json_object"" }},
                ""messages"": [
                    {{ ""role"": ""system"", ""content"": ""{EscapeJson(systemPrompt)}"" }},
                    {{ ""role"": ""user"", ""content"": ""{EscapeJson(userPrompt)}"" }}
                ],
                ""temperature"": 0.7
            }}";

            using var request = new UnityWebRequest(Endpoint, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (ct.IsCancellationRequested) { request.Abort(); ct.ThrowIfCancellationRequested(); }
                await Task.Delay(50, ct);
            }

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"Groq API Error: {request.error}\n{request.downloadHandler.text}");

            var responseWrapper = JsonUtility.FromJson<GroqResponse>(request.downloadHandler.text);
            string contentJson = responseWrapper.choices[0].message.content;

            return JsonUtility.FromJson<ItemGenerationDto>(contentJson);
        }

        private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        [Serializable] private class GroqResponse { public Choice[] choices; }
        [Serializable] private class Choice { public Message message; }
        [Serializable] private class Message { public string content; }
    }
}
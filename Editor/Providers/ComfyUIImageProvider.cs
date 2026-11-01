using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AIPipeline.Editor
{
    public class ComfyUIImageProvider
    {
        private readonly string _baseUrl;

        public ComfyUIImageProvider(string baseUrl = "http://127.0.0.1:8188")
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<byte[]> GenerateIconBytesAsync(string workflowJsonTemplate, string positivePrompt, CancellationToken ct)
        {
            int seed = UnityEngine.Random.Range(1, 999999999);
            string workflow = workflowJsonTemplate
                .Replace("{{PROMPT}}", positivePrompt.Replace("\"", "\\\""))
                .Replace("\"{{SEED}}\"", seed.ToString());

            string payload = $"{{\"prompt\": {workflow}}}";

            string promptId = await EnqueuePromptAsync(payload, ct);

            string filename = await PollForCompletionAsync(promptId, ct);

            return await DownloadImageBytesAsync(filename, ct);
        }

        private async Task<string> EnqueuePromptAsync(string payload, CancellationToken ct)
        {
            using var req = new UnityWebRequest($"{_baseUrl}/prompt", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Delay(50, ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"ComfyUI /prompt error: {req.error} | {req.downloadHandler.text}");

            var resp = JsonUtility.FromJson<PromptResponse>(req.downloadHandler.text);
            return resp.prompt_id;
        }

        private async Task<string> PollForCompletionAsync(string promptId, CancellationToken ct)
        {
            string historyUrl = $"{_baseUrl}/history/{promptId}";
            for (int attempt = 0; attempt < 120; attempt++)
            {
                await Task.Delay(500, ct);
                using var req = UnityWebRequest.Get(historyUrl);
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Delay(20, ct);

                string json = req.downloadHandler.text;
                var match = Regex.Match(json, "\"filename\"\\s*:\\s*\"([^\"]+\\.png)\"");
                if (match.Success)
                    return match.Groups[1].Value;
            }
            throw new TimeoutException("ComfyUI generation timed out.");
        }

        private async Task<byte[]> DownloadImageBytesAsync(string filename, CancellationToken ct)
        {
            string viewUrl = $"{_baseUrl}/view?filename={UnityWebRequest.EscapeURL(filename)}&type=output";
            using var req = UnityWebRequest.Get(viewUrl);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Delay(50, ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"ComfyUI /view error: {req.error}");

            return req.downloadHandler.data;
        }

        [Serializable] private class PromptResponse { public string prompt_id; }
    }
}
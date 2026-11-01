using System;
using System.Threading;
using System.Threading.Tasks;
using AIPipeline.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace AIPipeline.Editor
{
    public class AiGeneratorWindow : EditorWindow
    {
        private const string PrefKeyGroqApi = "AIPipeline_GroqApiKey";
        private const string PrefKeyGroqModel = "AIPipeline_GroqModel";
        private const string PrefKeyComfyUrl = "AIPipeline_ComfyUrl";
        private const string PrefKeyImageBackend = "AIPipeline_ImageBackend";
        private const string PrefKeyOutputFolder = "AIPipeline_OutputFolder";

        public enum ImageBackendType
        {
            LocalComfyUI = 0,
            CloudFluxPollinations = 1,
            ProceduralMock = 2
        }

        private string _groqApiKey = "";
        private string _groqModel = "openai/gpt-oss-120b";
        private string _comfyUrl = "http://127.0.0.1:8188";
        private ImageBackendType _imageBackend = ImageBackendType.CloudFluxPollinations;
        private TextAsset _workflowJsonAsset;
        private string _outputFolder = "Assets/GeneratedItems";

        private string _promptsInput = "Огненный клинок древнего дракона\nКольцо ледяного лича\nЗелье ярости берсерка";
        private RPGItemConfig _lastGeneratedItem;
        private bool _isGenerating;
        private Vector2 _scrollPos;

        [MenuItem("Tools/AI Content Pipeline/RPG Item Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<AiGeneratorWindow>("AI Item Generator");
            window.minSize = new Vector2(440, 600);
        }

        private void OnEnable()
        {
            _groqApiKey = EditorPrefs.GetString(PrefKeyGroqApi, "");
            _groqModel = EditorPrefs.GetString(PrefKeyGroqModel, "openai/gpt-oss-120b");
            _comfyUrl = EditorPrefs.GetString(PrefKeyComfyUrl, "http://127.0.0.1:8188");
            _imageBackend = (ImageBackendType)EditorPrefs.GetInt(PrefKeyImageBackend, (int)ImageBackendType.CloudFluxPollinations);
            _outputFolder = EditorPrefs.GetString(PrefKeyOutputFolder, "Assets/GeneratedItems");
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefKeyGroqApi, _groqApiKey);
            EditorPrefs.SetString(PrefKeyGroqModel, _groqModel);
            EditorPrefs.SetString(PrefKeyComfyUrl, _comfyUrl);
            EditorPrefs.SetInt(PrefKeyImageBackend, (int)_imageBackend);
            EditorPrefs.SetString(PrefKeyOutputFolder, _outputFolder);
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("AI Content Pipeline (Groq + Multi-Backend)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("API keys are securely stored locally in EditorPrefs.", MessageType.Info);

            EditorGUI.BeginChangeCheck();

            // --- Блок 1: Настройки LLM (Groq) ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("1. LLM Settings (Groq API)", EditorStyles.boldLabel);
            _groqApiKey = EditorGUILayout.PasswordField("Groq API Key (gsk_...)", _groqApiKey);
            _groqModel = EditorGUILayout.TextField("Model ID", _groqModel);

            // --- Блок 2: Выбор бэкенда для иконок ---
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("2. Icon Generation Backend", EditorStyles.boldLabel);
            _imageBackend = (ImageBackendType)EditorGUILayout.EnumPopup("Backend Mode", _imageBackend);

            if (_imageBackend == ImageBackendType.LocalComfyUI)
            {
                _comfyUrl = EditorGUILayout.TextField("ComfyUI Endpoint", _comfyUrl);
                _workflowJsonAsset = (TextAsset)EditorGUILayout.ObjectField(
                    "API Workflow JSON", _workflowJsonAsset, typeof(TextAsset), false);
                EditorGUILayout.HelpBox("Requires local ComfyUI running. If unavailable, tool will fall back to procedural icons.", MessageType.Warning);
            }
            else if (_imageBackend == ImageBackendType.CloudFluxPollinations)
            {
                EditorGUILayout.HelpBox("Blazing fast cloud generation via Pollinations (Flux model). Zero VRAM usage, no API keys required.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Mock Mode: Instant procedural gradient icons based on item rarity.", MessageType.None);
            }

            // --- Блок 3: Промпты и пакетный режим ---
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("3. Batch Prompts (1 line = 1 item)", EditorStyles.boldLabel);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);

            EditorGUILayout.LabelField("Enter item concepts:");
            _promptsInput = EditorGUILayout.TextArea(_promptsInput, GUILayout.Height(85));

            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
            }

            EditorGUILayout.Space(10);

            GUI.enabled = !_isGenerating && !string.IsNullOrWhiteSpace(_groqApiKey);
            if (GUILayout.Button(_isGenerating ? "Generating..." : "Generate Items Batch", GUILayout.Height(38)))
            {
                RunBatchGenerationAsync();
            }
            GUI.enabled = true;

            if (string.IsNullOrWhiteSpace(_groqApiKey))
            {
                EditorGUILayout.HelpBox("Please enter your free Groq API Key (gsk_...) above.", MessageType.Warning);
            }

            // --- Блок 4: Предпросмотр ---
            if (_lastGeneratedItem != null)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("Last Generated Asset Preview", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.ObjectField("ScriptableObject", _lastGeneratedItem, typeof(RPGItemConfig), false);
                EditorGUILayout.LabelField("Name:", _lastGeneratedItem.ItemName);
                EditorGUILayout.LabelField("Rarity:", _lastGeneratedItem.Rarity);
                EditorGUILayout.LabelField("Stats:", $"Lvl {_lastGeneratedItem.LevelRequirement} | ATK: {_lastGeneratedItem.AttackPower} | Gold: {_lastGeneratedItem.GoldValue}");
                EditorGUILayout.LabelField("Description:", _lastGeneratedItem.Description, EditorStyles.wordWrappedLabel);

                if (_lastGeneratedItem.Icon != null)
                {
                    GUILayout.Space(4);
                    Rect rect = GUILayoutUtility.GetRect(96, 96, GUILayout.ExpandWidth(false));
                    EditorGUI.DrawPreviewTexture(rect, _lastGeneratedItem.Icon.texture);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        private async void RunBatchGenerationAsync()
        {
            string[] lines = _promptsInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            _isGenerating = true;
            var cts = new CancellationTokenSource();

            var textProvider = new GroqTextProvider(_groqApiKey.Trim(), _groqModel.Trim());
            var comfyProvider = new ComfyUIImageProvider(_comfyUrl.Trim());

            try
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    string prompt = lines[i].Trim();
                    if (string.IsNullOrEmpty(prompt)) continue;

                    float progress = (float)i / lines.Length;
                    if (EditorUtility.DisplayCancelableProgressBar("AI Content Pipeline", $"[{i + 1}/{lines.Length}] Groq JSON generation...", progress))
                    {
                        cts.Cancel();
                        break;
                    }

                    ItemGenerationDto dto = await textProvider.GenerateItemJsonAsync(prompt, cts.Token);

                    byte[] iconBytes = null;

                    if (_imageBackend == ImageBackendType.LocalComfyUI && _workflowJsonAsset != null)
                    {
                        try
                        {
                            iconBytes = await comfyProvider.GenerateIconBytesAsync(_workflowJsonAsset.text, dto.sdIconPrompt, cts.Token);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AI Pipeline] ComfyUI failed ({ex.Message}). Falling back to procedural icon.");
                        }
                    }
                    else if (_imageBackend == ImageBackendType.CloudFluxPollinations)
                    {
                        try
                        {
                            iconBytes = await DownloadIconFromPollinationsAsync(dto.sdIconPrompt, 512, cts.Token);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AI Pipeline] Pollinations cloud failed ({ex.Message}). Falling back to procedural icon.");
                        }
                    }

                    if (iconBytes == null)
                    {
                        iconBytes = GenerateProceduralFallbackIcon(dto.rarity, 256);
                    }

                    _lastGeneratedItem = ItemAssetFactory.CreateItemAsset(dto, iconBytes, _outputFolder);
                    Repaint();
                }

                Debug.Log($"[AI Pipeline] Batch successfully generated in '{_outputFolder}'.");
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[AI Pipeline] Generation cancelled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Pipeline] Error: {ex.Message}");
                EditorUtility.DisplayDialog("AI Pipeline Error", ex.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isGenerating = false;
                cts.Dispose();
                Repaint();
            }
        }

        private static async Task<byte[]> DownloadIconFromPollinationsAsync(string prompt, int size = 512, CancellationToken ct = default)
        {
            int seed = UnityEngine.Random.Range(1, 999999);
            string encodedPrompt = UnityWebRequest.EscapeURL($"{prompt}, 2d game icon, centered, clean background");
            string url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width={size}&height={size}&seed={seed}&nologo=true&model=flux";

            using var req = UnityWebRequest.Get(url);
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested) { req.Abort(); ct.ThrowIfCancellationRequested(); }
                await Task.Delay(50, ct);
            }

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"Pollinations error: {req.error}");

            return req.downloadHandler.data;
        }

        private static byte[] GenerateProceduralFallbackIcon(string rarity, int size = 256)
        {
            Color baseColor = (rarity?.ToLowerInvariant()) switch
            {
                "common" => new Color(0.45f, 0.48f, 0.52f),
                "rare" => new Color(0.15f, 0.45f, 0.85f),
                "epic" => new Color(0.60f, 0.20f, 0.85f),
                "legendary" => new Color(0.95f, 0.60f, 0.10f),
                _ => new Color(0.25f, 0.65f, 0.45f)
            };

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color darkCorner = baseColor * 0.25f;
            darkCorner.a = 1f;
            Color brightCenter = Color.Lerp(baseColor, Color.white, 0.35f);

            int border = 10;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.7f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < border || x >= size - border || y < border || y >= size - border;
                    if (isBorder)
                    {
                        tex.SetPixel(x, y, brightCenter);
                    }
                    else
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                        Color c = Color.Lerp(brightCenter, darkCorner, Mathf.Clamp01(dist));
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            DestroyImmediate(tex);
            return png;
        }
    }
}
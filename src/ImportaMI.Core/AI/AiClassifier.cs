using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImportaMI.Core.Media;
using Microsoft.ML.OnnxRuntime;
using SkiaSharp;

namespace ImportaMI.Core.AI;

/// <summary>
/// Classificatore AI locale per immagini.
/// Se è presente un modello ONNX (in AppData o nella cartella dell'app), esegue l'inferenza tramite ONNX Runtime.
/// Altrimenti o in sinergia, applica l'analisi visiva avanzata (colorimetria, dominanti, toni pelle, contrasto, orario ed EXIF).
/// </summary>
public sealed class AiClassifier : IDisposable
{
    private static readonly Lazy<AiClassifier> DefaultInstance = new(() => new AiClassifier());
    public static AiClassifier Default => DefaultInstance.Value;

    private readonly InferenceSession? _onnxSession;
    private readonly object _lock = new();

    public AiClassifier(string? modelPath = null)
    {
        var resolvedPath = modelPath ?? ResolveModelPath();
        if (resolvedPath is not null && File.Exists(resolvedPath))
        {
            try
            {
                var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                };
                _onnxSession = new InferenceSession(resolvedPath, options);
            }
            catch
            {
                _onnxSession = null;
            }
        }
    }

    public bool HasOnnxModel => _onnxSession is not null;

    public IReadOnlyList<CategoryScore> ClassifyShot(Shot shot)
    {
        var bytes = FastPreviewExtractor.ExtractBytes(shot);
        if (bytes is null || bytes.Length == 0)
        {
            return [];
        }

        try
        {
            using var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null)
                return [];

            // 1. Prova con modello ONNX se caricato
            if (_onnxSession is not null)
            {
                var onnxResult = ClassifyWithOnnx(bitmap);
                if (onnxResult.Count > 0)
                {
                    return onnxResult;
                }
            }

            // 2. Analisi visiva ad alte prestazioni (colorimetria + EXIF)
            return ClassifyWithVisualAnalysis(bitmap, shot);
        }
        catch
        {
            return [];
        }
    }

    private IReadOnlyList<CategoryScore> ClassifyWithOnnx(SKBitmap bitmap)
    {
        if (_onnxSession is null)
            return [];

        try
        {
            var tensor = ImageTensorHelper.CreateTensorFromBitmap(bitmap, 224, 224);
            var inputName = _onnxSession.InputMetadata.Keys.FirstOrDefault() ?? "input";
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            lock (_lock)
            {
                using var results = _onnxSession.Run(inputs);
                var output = results.FirstOrDefault()?.AsEnumerable<float>()?.ToArray();
                if (output is not null && output.Length > 0)
                {
                    return MapOnnxOutputToCategories(output);
                }
            }
        }
        catch
        {
            // Se l'inferenza ONNX fallisce, si userà l'analisi visiva
        }

        return [];
    }

    private static IReadOnlyList<CategoryScore> MapOnnxOutputToCategories(float[] logits)
    {
        // Se il modello ha un numero di classi corrispondente o mappa a categorie standard
        var scores = new List<CategoryScore>();
        var max = logits.Max();
        var expSum = logits.Select(l => MathF.Exp(l - max)).Sum();

        for (var i = 0; i < Math.Min(PhotoCategories.All.Count, logits.Length); i++)
        {
            var prob = MathF.Exp(logits[i] - max) / expSum;
            scores.Add(new CategoryScore(PhotoCategories.All[i], prob));
        }

        return scores.OrderByDescending(s => s.Confidence).ToList();
    }

    public static IReadOnlyList<CategoryScore> ClassifyWithVisualAnalysis(SKBitmap bitmap, Shot shot)
    {
        // Campionamento rapido di 64x64 pixel per velocità istantanea (<2ms)
        using var small = bitmap.Width == 64 && bitmap.Height == 64
            ? bitmap
            : bitmap.Resize(new SKImageInfo(64, 64, SKColorType.Rgb888x), SKSamplingOptions.Default);

        if (small is null)
            return [];

        var pixels = small.Pixels;
        var total = pixels.Length;
        if (total == 0)
            return [];

        float greenCount = 0;
        float blueCount = 0;
        float warmSunsetCount = 0;
        float skinToneCount = 0;
        float whiteSnowCount = 0;
        float darkCount = 0;
        float brightCount = 0;

        foreach (var p in pixels)
        {
            var r = p.Red;
            var g = p.Green;
            var b = p.Blue;
            var brightness = (r + g + b) / 3.0f;

            if (brightness < 35) darkCount++;
            if (brightness > 215) brightCount++;

            // Verde natura / vegetazione
            if (g > 70 && g > r * 1.15f && g > b * 1.15f)
            {
                greenCount++;
            }

            // Blu cielo o mare
            if (b > 85 && b > r * 1.15f && b > g * 0.95f)
            {
                blueCount++;
            }

            // Toni caldi arancione/oro tipici del tramonto
            if (r > 130 && g > 60 && g < r * 0.85f && b < g * 0.65f)
            {
                warmSunsetCount++;
            }

            // Toni pelle umana (RGB rule euristica)
            if (r > 95 && g > 40 && b > 20 && r > g && g > b && (r - g) > 15 && Math.Abs(r - g) > 15)
            {
                skinToneCount++;
            }

            // Bianco neve (luminoso, R-G-B molto simili con leggera dominante fredda)
            if (r > 195 && g > 195 && b > 200 && Math.Abs(r - g) < 20 && Math.Abs(g - b) < 20)
            {
                whiteSnowCount++;
            }
        }

        var greenRatio = greenCount / total;
        var blueRatio = blueCount / total;
        var sunsetRatio = warmSunsetCount / total;
        var skinRatio = skinToneCount / total;
        var snowRatio = whiteSnowCount / total;
        var darkRatio = darkCount / total;

        var hour = shot.CaptureTime.Hour;
        var isNightTime = hour >= 21 || hour < 6;
        var isGoldenHour = (hour is >= 6 and <= 8) || (hour is >= 18 and <= 20);

        var scores = new Dictionary<string, float>();

        // Tramonto / Alba
        var sunsetScore = sunsetRatio * 2.5f + (isGoldenHour ? 0.35f : 0f);
        if (sunsetScore > 0.15f) scores[PhotoCategories.Sunset] = Math.Clamp(sunsetScore, 0.1f, 0.95f);

        // Mare e Spiaggia
        var beachScore = (blueRatio * 1.8f) + (sunsetRatio * 0.5f);
        if (blueRatio > 0.25f && !isNightTime) scores[PhotoCategories.Beach] = Math.Clamp(beachScore, 0.15f, 0.9f);

        // Montagna / Natura
        if (greenRatio > 0.18f || (greenRatio > 0.10f && blueRatio > 0.15f))
        {
            var natureScore = greenRatio * 1.6f + (blueRatio * 0.5f);
            scores[PhotoCategories.Nature] = Math.Clamp(natureScore, 0.2f, 0.9f);
            if (blueRatio > 0.15f || snowRatio > 0.1f)
            {
                scores[PhotoCategories.Mountain] = Math.Clamp(natureScore * 0.9f, 0.2f, 0.85f);
            }
        }

        // Neve
        if (snowRatio > 0.25f)
        {
            scores[PhotoCategories.Snow] = Math.Clamp(snowRatio * 1.5f, 0.25f, 0.95f);
        }

        // Ritratti / Persone / Matrimonio
        if (skinRatio > 0.12f)
        {
            var portraitScore = skinRatio * 2.2f;
            scores[PhotoCategories.Portrait] = Math.Clamp(portraitScore, 0.2f, 0.9f);

            // Se c'è molta luce o abiti eleganti / fiori (molto bianco + toni caldi) -> Matrimonio
            if (brightCount / (float)total > 0.20f || warmSunsetCount > 0.10f)
            {
                scores[PhotoCategories.Wedding] = Math.Clamp(portraitScore * 0.85f, 0.2f, 0.85f);
            }
        }

        // Notturna o Concerto (sfondo buio con punti luce o evento serale)
        if (darkRatio > 0.45f || isNightTime)
        {
            if (darkRatio > 0.50f && isNightTime)
            {
                scores[PhotoCategories.Night] = Math.Clamp(darkRatio, 0.3f, 0.9f);
            }
            if (isNightTime && (skinRatio > 0.05f || warmSunsetCount > 0.08f))
            {
                scores[PhotoCategories.Concert] = Math.Clamp(darkRatio * 0.8f + 0.2f, 0.25f, 0.85f);
                scores[PhotoCategories.BirthdayParty] = Math.Clamp(darkRatio * 0.6f + 0.2f, 0.2f, 0.8f);
            }
        }

        // Città e Architettura (strutture con contrasti intermedi e poco verde/blu puro)
        if (greenRatio < 0.10f && blueRatio < 0.15f && skinRatio < 0.10f && !isNightTime)
        {
            scores[PhotoCategories.City] = 0.45f;
        }

        if (scores.Count == 0)
        {
            scores[PhotoCategories.Nature] = 0.3f;
        }

        return scores
            .Select(kv => new CategoryScore(kv.Key, kv.Value))
            .OrderByDescending(s => s.Confidence)
            .ToList();
    }

    private static string? ResolveModelPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var custom = Path.Combine(appData, "ImportaMI", "models", "classifier.onnx");
        if (File.Exists(custom))
            return custom;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var inApp = Path.Combine(baseDir, "models", "classifier.onnx");
        if (File.Exists(inApp))
            return inApp;

        return null;
    }

    public void Dispose()
    {
        _onnxSession?.Dispose();
    }
}

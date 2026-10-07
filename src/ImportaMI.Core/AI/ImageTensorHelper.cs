using System;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace ImportaMI.Core.AI;

/// <summary>
/// Prepara i tensori delle immagini per l'inferenza ONNX (ridimensionamento e normalizzazione standard RGB).
/// </summary>
public static class ImageTensorHelper
{
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

    public static DenseTensor<float>? CreateTensorFromBytes(byte[] imageBytes, int width = 224, int height = 224)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap is null)
                return null;

            return CreateTensorFromBitmap(bitmap, width, height);
        }
        catch
        {
            return null;
        }
    }

    public static DenseTensor<float> CreateTensorFromBitmap(SKBitmap source, int width = 224, int height = 224)
    {
        var needDispose = false;
        SKBitmap resized;
        if (source.Width == width && source.Height == height)
        {
            resized = source;
        }
        else
        {
            resized = source.Resize(new SKImageInfo(width, height, SKColorType.Rgb888x), SKSamplingOptions.Default)
                      ?? source;
            needDispose = !ReferenceEquals(resized, source);
        }

        try
        {
            var tensor = new DenseTensor<float>([1, 3, height, width]);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = resized.GetPixel(x, y);
                    tensor[0, 0, y, x] = (pixel.Red / 255.0f - Mean[0]) / Std[0];
                    tensor[0, 1, y, x] = (pixel.Green / 255.0f - Mean[1]) / Std[1];
                    tensor[0, 2, y, x] = (pixel.Blue / 255.0f - Mean[2]) / Std[2];
                }
            }

            return tensor;
        }
        finally
        {
            if (needDispose)
            {
                resized.Dispose();
            }
        }
    }
}

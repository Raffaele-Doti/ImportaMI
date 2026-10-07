using System;
using System.IO;
using System.Linq;
using ImportaMI.Core.AI;
using ImportaMI.Core.Media;
using SkiaSharp;
using Xunit;

namespace ImportaMI.Core.Tests;

public class AiClassifierTests
{
    private static Shot CreateShot(string path, DateTime time) =>
        new([new MediaFile(path, MediaKind.Jpeg, time, 100, CaptureTimeSource.Metadata)]);

    [Fact]
    public void ImageTensorHelper_CreatesValidTensor_FromBitmap()
    {
        using var bitmap = new SKBitmap(100, 100, SKColorType.Rgb888x, SKAlphaType.Opaque);
        bitmap.Erase(SKColors.Blue);

        var tensor = ImageTensorHelper.CreateTensorFromBitmap(bitmap, 224, 224);

        Assert.NotNull(tensor);
        Assert.Equal(4, tensor.Dimensions.Length);
        Assert.Equal(1, tensor.Dimensions[0]);
        Assert.Equal(3, tensor.Dimensions[1]);
        Assert.Equal(224, tensor.Dimensions[2]);
        Assert.Equal(224, tensor.Dimensions[3]);
    }

    [Fact]
    public void AiClassifier_ClassifiesSunset_WhenWarmTonesPresent()
    {
        using var bitmap = new SKBitmap(64, 64, SKColorType.Rgb888x, SKAlphaType.Opaque);
        bitmap.Erase(new SKColor(230, 90, 20));

        var shot = CreateShot("IMG_001.JPG", new DateTime(2026, 7, 15, 19, 30, 0));
        var scores = AiClassifier.ClassifyWithVisualAnalysis(bitmap, shot);

        Assert.NotEmpty(scores);
        Assert.Contains(scores, s => s.Category == PhotoCategories.Sunset);
    }

    [Fact]
    public void AiClassifier_ClassifiesNatureAndMountain_WhenGreenAndBluePresent()
    {
        using var bitmap = new SKBitmap(64, 64, SKColorType.Rgb888x, SKAlphaType.Opaque);
        bitmap.Erase(new SKColor(30, 160, 40));

        var shot = CreateShot("IMG_002.JPG", new DateTime(2026, 7, 15, 12, 0, 0));
        var scores = AiClassifier.ClassifyWithVisualAnalysis(bitmap, shot);

        Assert.NotEmpty(scores);
        Assert.Contains(scores, s => s.Category == PhotoCategories.Nature);
    }

    [Fact]
    public void AiClassifier_ClassifiesBeach_WhenSkyAndWaterBluePresent()
    {
        using var bitmap = new SKBitmap(64, 64, SKColorType.Rgb888x, SKAlphaType.Opaque);
        bitmap.Erase(new SKColor(40, 120, 220));

        var shot = CreateShot("IMG_003.JPG", new DateTime(2026, 8, 10, 14, 0, 0));
        var scores = AiClassifier.ClassifyWithVisualAnalysis(bitmap, shot);

        Assert.NotEmpty(scores);
        Assert.Contains(scores, s => s.Category == PhotoCategories.Beach);
    }

    [Fact]
    public void SessionTagger_AggregatesScoresAndSuggestsName()
    {
        var shot1Scores = new[]
        {
            new CategoryScore(PhotoCategories.Mountain, 0.85f),
            new CategoryScore(PhotoCategories.Nature, 0.70f),
        };
        var shot2Scores = new[]
        {
            new CategoryScore(PhotoCategories.Mountain, 0.90f),
            new CategoryScore(PhotoCategories.Nature, 0.60f),
        };
        var shot3Scores = new[]
        {
            new CategoryScore(PhotoCategories.Sunset, 0.75f),
        };

        var analysis = SessionTagger.Aggregate([shot1Scores, shot2Scores, shot3Scores]);

        Assert.Equal(3, analysis.TotalAnalyzedShots);
        Assert.Equal("Montagna", analysis.SuggestedName);
        Assert.NotEmpty(analysis.Tags);
        Assert.Equal(PhotoCategories.Mountain, analysis.Tags[0].Category);
        Assert.Equal(2, analysis.Tags[0].ShotCount);
    }

    [Fact]
    public void FastPreviewExtractor_ExtractsFromJpegFile()
    {
        using var temp = new TempSource();
        using var bitmap = new SKBitmap(10, 10);
        bitmap.Erase(SKColors.Red);

        var path = Path.Combine(temp.Root, "test.jpg");
        using (var stream = File.OpenWrite(path))
        {
            bitmap.Encode(stream, SKEncodedImageFormat.Jpeg, 80);
        }

        var shot = CreateShot(path, DateTime.Now);
        var bytes = FastPreviewExtractor.ExtractBytes(shot);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }
}

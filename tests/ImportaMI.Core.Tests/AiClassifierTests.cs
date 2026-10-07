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
    public void AiClassifier_LoadsPlaces365OnnxModel_Successfully()
    {
        using var classifier = new AiClassifier();
        Assert.True(classifier.HasOnnxModel);
    }

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

    // ===== Test per la mappatura Places365 =====

    [Fact]
    public void MapPlaces365_MapsBeachCorrectly()
    {
        // Simula logits con alta probabilità per l'indice 48 (beach)
        var logits = new float[365];
        logits[48] = 10.0f; // beach

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.NotEmpty(scores);
        Assert.Equal(PhotoCategories.Beach, scores[0].Category);
        Assert.True(scores[0].Confidence >= 0.5f);
    }

    [Fact]
    public void MapPlaces365_MapsMountainCorrectly()
    {
        var logits = new float[365];
        logits[232] = 10.0f; // mountain

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.NotEmpty(scores);
        Assert.Equal(PhotoCategories.Mountain, scores[0].Category);
    }

    [Fact]
    public void MapPlaces365_MapsRestaurantToFood()
    {
        var logits = new float[365];
        logits[284] = 10.0f; // restaurant

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.NotEmpty(scores);
        Assert.Equal(PhotoCategories.Food, scores[0].Category);
    }

    [Fact]
    public void MapPlaces365_MapsStadiumToSport()
    {
        var logits = new float[365];
        logits[313] = 10.0f; // stadium_football

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.NotEmpty(scores);
        Assert.Equal(PhotoCategories.Sport, scores[0].Category);
    }

    [Fact]
    public void MapPlaces365_MapsSkyscraperToCity()
    {
        var logits = new float[365];
        logits[307] = 10.0f; // skyscraper

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.NotEmpty(scores);
        Assert.Equal(PhotoCategories.City, scores[0].Category);
    }

    [Fact]
    public void MapPlaces365_MapsStageIndoorToConcert()
    {
        var logits = new float[365];
        logits[315] = 10.0f; // stage_indoor

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.NotEmpty(scores);
        Assert.Equal(PhotoCategories.Concert, scores[0].Category);
    }

    [Fact]
    public void MapPlaces365_MapsSnowfieldToSnow()
    {
        var logits = new float[365];
        logits[309] = 10.0f; // snowfield

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.NotEmpty(scores);
        Assert.Equal(PhotoCategories.Snow, scores[0].Category);
    }

    [Fact]
    public void MapPlaces365_MapsMultipleCategories_WhenAmbiguous()
    {
        // Simula un risultato ambiguo: mountain_snowy → Mountain + Snow
        var logits = new float[365];
        logits[234] = 10.0f; // mountain_snowy

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.True(scores.Count >= 2);
        var categories = scores.Select(s => s.Category).ToHashSet();
        Assert.Contains(PhotoCategories.Mountain, categories);
        Assert.Contains(PhotoCategories.Snow, categories);
    }

    [Fact]
    public void MapPlaces365_ReturnsEmpty_WhenUnmappedClass()
    {
        // Indice 43 = basement, non mappato a nessuna categoria
        var logits = new float[365];
        logits[43] = 10.0f;

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.Empty(scores);
    }

    [Fact]
    public void MapPlaces365_ReturnsAtMost5Results()
    {
        // Distribuisci probabilità su molte classi mappate
        var logits = new float[365];
        logits[48]  = 5.0f; // beach
        logits[232] = 5.0f; // mountain
        logits[284] = 5.0f; // restaurant
        logits[307] = 5.0f; // skyscraper
        logits[309] = 5.0f; // snowfield
        logits[315] = 5.0f; // stage_indoor
        logits[313] = 5.0f; // stadium

        var scores = AiClassifier.MapPlaces365ToCategories(logits);

        Assert.True(scores.Count <= 5);
    }

    [Fact]
    public void Places365Map_HasCorrectLabelCount()
    {
        Assert.Equal(365, Places365Map.Labels.Length);
    }

    [Fact]
    public void Places365Map_AllMappingIndicesAreValid()
    {
        foreach (var (index, mappings) in Places365Map.Mapping)
        {
            Assert.InRange(index, 0, 364);
            Assert.NotEmpty(mappings);
            foreach (var (category, weight) in mappings)
            {
                Assert.Contains(category, PhotoCategories.All);
                Assert.InRange(weight, 0.01f, 1.0f);
            }
        }
    }

    [Fact]
    public void SessionTagger_GeneratesCombinedName_WhenTwoProminentCategories()
    {
        var tags = new List<SessionTag>
        {
            new(PhotoCategories.Beach, 10, 0.9f),
            new(PhotoCategories.Sunset, 8, 0.85f),
        };

        var name = SessionTagger.GenerateSuggestedName(tags);

        Assert.Equal("Mare al tramonto", name);
    }

    [Fact]
    public void SessionTagger_SetsUndetermined_WhenNoScores()
    {
        var analysis = SessionTagger.Aggregate([[]]);

        Assert.True(analysis.IsUndetermined);
        Assert.Empty(analysis.Tags);
        Assert.Empty(analysis.SuggestedName);
    }
}

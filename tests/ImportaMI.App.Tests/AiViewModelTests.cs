using System;
using System.Collections.Generic;
using System.Threading;
using ImportaMI.App.ViewModels;
using ImportaMI.Core.AI;
using ImportaMI.Core.Media;
using ImportaMI.Core.Scanning;
using ImportaMI.Core.Sessions;
using Xunit;

namespace ImportaMI.App.Tests;

public class AiViewModelTests
{
    private static MediaFile CreateFile(string name = "IMG_001.JPG") =>
        new(name, MediaKind.Jpeg, DateTime.Now, 100, CaptureTimeSource.Metadata);

    private static Shot CreateShot(string name = "IMG_001.JPG") =>
        new([CreateFile(name)]);

    private static ScanResult CreateScan(string name = "IMG_001.JPG") =>
        new()
        {
            Root = "test-root",
            Files = [CreateFile(name)],
            Issues = [],
            OtherFiles = 0,
        };

    [Fact]
    public void AiTagViewModel_ApplyCommand_InvokesCallback()
    {
        string? appliedName = null;
        var tag = new SessionTag(PhotoCategories.Mountain, 5, 0.85f);
        var vm = new AiTagViewModel(tag, name => appliedName = name);

        Assert.Equal(PhotoCategories.Mountain, vm.Category);
        Assert.Equal(5, vm.ShotCount);
        Assert.Contains(PhotoCategories.Mountain, vm.Label);

        vm.ApplyCommand.Execute(null);

        Assert.Equal(PhotoCategories.Mountain, appliedName);
    }

    [Fact]
    public void SessionItemViewModel_UpdateAiTags_AutoFillsName_WhenEmptyAndNotEdited()
    {
        var shot = CreateShot();
        var session = new ShootingSession([shot]);
        var mockOwner = new SessionsViewModel(CreateScan(), "test", "dest", () => { }, () => { });
        var item = new SessionItemViewModel(session, "", true, false, mockOwner);

        Assert.Empty(item.Name);
        Assert.False(item.HasAiTags);

        var tags = new List<SessionTag> { new(PhotoCategories.Beach, 1, 0.9f) };
        item.UpdateAiTags(tags, "Mare", isUserEdited: false);

        Assert.True(item.HasAiTags);
        Assert.Single(item.AiTags);
        Assert.Equal("Mare", item.Name);
        Assert.Equal("Mare", item.SuggestedName);
    }

    [Fact]
    public void SessionItemViewModel_UpdateAiTags_DoesNotOverwrite_WhenUserEdited()
    {
        var shot = CreateShot();
        var session = new ShootingSession([shot]);
        var mockOwner = new SessionsViewModel(CreateScan(), "test", "dest", () => { }, () => { });
        var item = new SessionItemViewModel(session, "Il mio nome personalizzato", true, false, mockOwner);

        var tags = new List<SessionTag> { new(PhotoCategories.Mountain, 1, 0.9f) };
        item.UpdateAiTags(tags, "Montagna", isUserEdited: true);

        Assert.True(item.HasAiTags);
        Assert.Equal("Il mio nome personalizzato", item.Name);
    }

    [Fact]
    public void SessionsViewModel_NextCommand_DisabledWhileAiAnalyzing()
    {
        var vm = new SessionsViewModel(CreateScan(), "test", "dest", () => { }, () => { });
        SpinWait.SpinUntil(() => !vm.IsAiAnalyzing, TimeSpan.FromSeconds(2));

        vm.IsCheckingArchive = false;
        Assert.True(vm.HasSelection);

        vm.IsAiAnalyzing = true;
        Assert.False(vm.NextCommand.CanExecute(null));

        vm.IsAiAnalyzing = false;
        Assert.True(vm.NextCommand.CanExecute(null));
    }
}

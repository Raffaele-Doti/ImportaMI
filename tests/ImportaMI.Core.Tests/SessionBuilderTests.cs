using ImportaMI.Core.Media;
using ImportaMI.Core.Sessions;
using static ImportaMI.Core.Tests.TestData;

namespace ImportaMI.Core.Tests;

public class SessionBuilderTests
{
    // Stesso esempio della sezione "How photos are catalogued" del README.
    private static readonly Shot[] ReadmeExample =
    [
        RawShot("2026-09-27 10:30:00"), // matrimonio
        RawShot("2026-09-27 11:10:00"),
        RawShot("2026-09-27 12:05:00"),
        RawShot("2026-09-27 15:30:00"), // carnevale, lo stesso giorno
        RawShot("2026-09-27 15:31:00"),
        RawShot("2026-10-02 23:35:00"), // concerto oltre la mezzanotte
        RawShot("2026-10-02 23:58:00"),
        RawShot("2026-10-03 00:20:00"),
    ];

    [Fact]
    public void Starts_with_one_session_per_day()
    {
        var sessions = SessionBuilder.Build(ReadmeExample, SessionBuilder.DayStarts(ReadmeExample));

        Assert.Equal([5, 2, 1], sessions.Select(s => s.Shots.Count));
    }

    [Fact]
    public void Choosing_a_photo_splits_the_day_into_two_sessions()
    {
        var starts = SessionBuilder.DayStarts(ReadmeExample);
        starts.Add(SessionBuilder.KeyOf(ReadmeExample[3])); // "il carnevale inizia da questa foto"

        var sessions = SessionBuilder.Build(ReadmeExample, starts);

        Assert.Equal([3, 2, 2, 1], sessions.Select(s => s.Shots.Count));
        Assert.Equal(At("2026-09-27 15:30:00"), sessions[1].Start);
    }

    [Fact]
    public void Removing_a_start_merges_with_the_previous_session()
    {
        var starts = SessionBuilder.DayStarts(ReadmeExample);
        starts.Remove(SessionBuilder.KeyOf(ReadmeExample[7])); // il concerto continua dopo mezzanotte

        var sessions = SessionBuilder.Build(ReadmeExample, starts);

        Assert.Equal([5, 3], sessions.Select(s => s.Shots.Count));
        Assert.Equal(At("2026-10-02 23:35:00"), sessions[1].Start);
    }

    [Fact]
    public void Session_key_survives_a_split_after_its_first_photo()
    {
        var before = SessionBuilder.Build(ReadmeExample, SessionBuilder.DayStarts(ReadmeExample));
        var starts = SessionBuilder.DayStarts(ReadmeExample);
        starts.Add(SessionBuilder.KeyOf(ReadmeExample[3]));

        var after = SessionBuilder.Build(ReadmeExample, starts);

        Assert.Equal(before[0].Key, after[0].Key);
    }

    [Fact]
    public void Order_of_input_does_not_matter_and_the_first_shot_always_starts()
    {
        var shuffled = ReadmeExample.Reverse().ToArray();

        var sessions = SessionBuilder.Build(shuffled, new HashSet<string>());

        Assert.Equal(ReadmeExample.Length, Assert.Single(sessions).Shots.Count);
        Assert.Equal(At("2026-09-27 10:30:00"), sessions[0].Start);
    }

    [Fact]
    public void Empty_input_returns_no_sessions()
    {
        Assert.Empty(SessionBuilder.Build([], new HashSet<string>()));
    }
}

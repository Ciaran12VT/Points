using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Points.Tests.Architecture;

public sealed class AndroidDeadAirAlertImplementationTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string AndroidRoot = Path.Combine(
        RepositoryRoot,
        "Points",
        "Platforms",
        "Android");

    [Fact]
    public void ForegroundService_DeclaresApprovedTypesAndPermissions()
    {
        var manifest = XDocument.Load(Path.Combine(AndroidRoot, "AndroidManifest.xml"));
        XNamespace android = "http://schemas.android.com/apk/res/android";

        var services = manifest.Descendants("service")
            .Where(service =>
                (string?)service.Attribute(android + "name")
                    == "com.companyname.points.ActiveCardForegroundService")
            .ToList();

        var service = Assert.Single(services);
        Assert.Equal("false", (string?)service.Attribute(android + "exported"));
        Assert.Equal(
            "specialUse|mediaPlayback",
            (string?)service.Attribute(android + "foregroundServiceType"));

        var specialUseProperty = Assert.Single(service.Elements("property"));
        Assert.Equal(
            "android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE",
            (string?)specialUseProperty.Attribute(android + "name"));
        Assert.False(string.IsNullOrWhiteSpace(
            (string?)specialUseProperty.Attribute(android + "value")));

        var permissions = manifest.Descendants("uses-permission")
            .Select(permission => (string?)permission.Attribute(android + "name"))
            .Where(name => name != null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("android.permission.FOREGROUND_SERVICE", permissions);
        Assert.Contains("android.permission.FOREGROUND_SERVICE_SPECIAL_USE", permissions);
        Assert.Contains("android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK", permissions);
        Assert.Contains("android.permission.WAKE_LOCK", permissions);
        Assert.DoesNotContain("android.permission.FOREGROUND_SERVICE_DATA_SYNC", permissions);
    }

    [Fact]
    public void DeadAirRuntime_KeepsCriticalPlatformGuardsWired()
    {
        var presenter = ReadAndroidSource("ActiveCardNotificationService.cs");
        var service = ReadAndroidSource("ActiveCardForegroundService.cs");
        var stateStore = ReadAndroidSource("DeadAirAlertStateStore.cs");
        var soundController = ReadAndroidSource("DeadAirAlertSoundController.cs");

        Assert.Contains("ExtraDeadAirAlertNoiseRequested", presenter, StringComparison.Ordinal);
        Assert.Contains("request.AlertNoiseRequested", presenter, StringComparison.Ordinal);

        Assert.Contains("StartCommandResult.RedeliverIntent", service, StringComparison.Ordinal);
        Assert.Contains("(flags & StartCommandFlags.Redelivery) != 0", service, StringComparison.Ordinal);
        Assert.Contains("DeadAirAlertState.Restore(restoredMilestones, wasEligible)", service, StringComparison.Ordinal);
        Assert.Contains("DeadAirAlertState.Initial(restoredMilestones)", service, StringComparison.Ordinal);
        Assert.Contains("TryReadOptionalBooleanExtra", service, StringComparison.Ordinal);
        Assert.Contains("DeadAirAlertPolicy.Evaluate", service, StringComparison.Ordinal);
        Assert.Contains("ActiveCardNotificationVisibility.IsVisible", service, StringComparison.Ordinal);
        Assert.Contains("SystemClock.ElapsedRealtime()", service, StringComparison.Ordinal);
        Assert.Contains("_deadAirAlertEvaluationPosted", service, StringComparison.Ordinal);
        Assert.Contains("NotificationCompat.ForegroundServiceImmediate", service, StringComparison.Ordinal);
        Assert.Contains("generation != _deadAirAlertGeneration", service, StringComparison.Ordinal);
        Assert.Contains("ForegroundService.TypeSpecialUse", service, StringComparison.Ordinal);
        Assert.Contains("ForegroundService.TypeMediaPlayback", service, StringComparison.Ordinal);

        Assert.Contains(".Commit()", stateStore, StringComparison.Ordinal);
        Assert.Contains("startedAtUtc.Ticks", stateStore, StringComparison.Ordinal);
        Assert.Contains("AudioUsageKind.Alarm", soundController, StringComparison.Ordinal);
        Assert.Contains("SetAcceptsDelayedFocusGain(acceptDelayed)", soundController, StringComparison.Ordinal);
        Assert.Contains("focusEpoch != Volatile.Read(ref _focusEpoch)", soundController, StringComparison.Ordinal);
        Assert.Contains("new AudioFocusListener(this, focusEpoch)", soundController, StringComparison.Ordinal);
        Assert.Contains("loop: -1", soundController, StringComparison.Ordinal);
        Assert.Contains("_continuousPlaybackChanged(_generation, true)", soundController, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("dead_air_alert_short.wav", 11_025, 0)]
    [InlineData("dead_air_alert_long.wav", 33_075, 0)]
    [InlineData("dead_air_alert_cycle.wav", 44_100, 11_025)]
    public void AlertWaveAsset_HasExpectedPcmTiming(
        string fileName,
        int expectedSampleCount,
        int expectedTrailingSilentSamples)
    {
        var path = Path.Combine(AndroidRoot, "Resources", "raw", fileName);
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(reader.ReadBytes(4)));
        Assert.Equal((uint)(stream.Length - 8), reader.ReadUInt32());
        Assert.Equal("WAVE", Encoding.ASCII.GetString(reader.ReadBytes(4)));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(reader.ReadBytes(4)));
        Assert.Equal(16u, reader.ReadUInt32());
        Assert.Equal(1, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        Assert.Equal(44_100u, reader.ReadUInt32());
        Assert.Equal(88_200u, reader.ReadUInt32());
        Assert.Equal(2, reader.ReadUInt16());
        Assert.Equal(16, reader.ReadUInt16());
        Assert.Equal("data", Encoding.ASCII.GetString(reader.ReadBytes(4)));

        var dataByteCount = reader.ReadUInt32();
        Assert.Equal((uint)(expectedSampleCount * sizeof(short)), dataByteCount);

        var samples = new short[expectedSampleCount];
        for (var index = 0; index < samples.Length; index++)
            samples[index] = reader.ReadInt16();

        Assert.Contains(samples, sample => sample != 0);
        if (expectedTrailingSilentSamples > 0)
        {
            Assert.All(
                samples[^expectedTrailingSilentSamples..],
                sample => Assert.Equal((short)0, sample));
        }
    }

    private static string ReadAndroidSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(AndroidRoot, fileName));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Points.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate repository root from the test output directory.");
    }
}

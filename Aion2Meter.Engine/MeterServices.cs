using Aion2Meter.Engine.Capture;
using AionDpsMeter.Services.Extensions;
using AionDpsMeter.Services.Models;
using AionDpsMeter.Services.PacketCapture;
using AionDpsMeter.Services.Services;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Settings;
using AionDpsMeter.Services.Services.Timed;
using Microsoft.Extensions.DependencyInjection;

namespace Aion2Meter.Engine;

public sealed class MeterOptions
{
    /// <summary>Ordner für Einstellungen, Kampf-Historie und Mitschnitte.</summary>
    public required string DataDir { get; init; }

    /// <summary>Statt live mitzulesen diesen Mitschnitt abspielen.</summary>
    public string? ReplayFile { get; init; }

    /// <summary>Abspielgeschwindigkeit, 0 = so schnell wie möglich.</summary>
    public double ReplaySpeed { get; init; } = 1;
}

public static class MeterServices
{
    /// <summary>
    /// Verdrahtet die Upstream-Pipeline (RATmeter) plus unsere Ergänzungen.
    /// Achtung: Upstreams Einstellungsdatei liegt relativ zum Arbeitsverzeichnis –
    /// der Aufrufer setzt deshalb vorher Environment.CurrentDirectory = DataDir.
    /// </summary>
    public static IServiceCollection AddAion2Meter(this IServiceCollection services, MeterOptions options)
    {
        Directory.CreateDirectory(options.DataDir);
        services.AddSingleton(options);
        services.AddCombatHistoryPersistence(Path.Combine(options.DataDir, "kampf-historie.db"));

        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<FilePacketWriter>();
        services.AddSingleton<TcpStreamBuffer>();

        if (options.ReplayFile is { } replay)
            services.AddSingleton<IPacketCaptureDevice>(sp =>
                new ReplayCaptureDevice(sp.GetRequiredService<TcpStreamBuffer>(), replay, options.ReplaySpeed));
        else
            services.AddSingleton<IPacketCaptureDevice, CaptureDevice>();

        services.AddSingleton<EntityTracker>();
        services.AddKeyedSingleton<ITimedEventTracker, BuffTimedEventTracker>("Buffs");
        services.AddKeyedSingleton<ITimedEventTracker, SkillCdTimedEventTracker>("SkillCd");
        services.AddSingleton<CombatSessionManager>();
        services.AddPacketProcessingRouting();
        services.AddSingleton<IPacketService, PacketPipelineService>();
        services.AddSingleton<RingRecorder>();
        services.AddSingleton<ResearchRecorder>();
        services.AddSingleton<RunTracker>();
        services.AddSingleton<PartyRoster>();
        services.AddSingleton<ConnectionMonitor>();
        services.AddSingleton<RunArchive>();
        services.AddSingleton<TimerService>();
        services.AddSingleton<ChecklistService>();
        services.AddSingleton<SelfIdentity>();
        services.AddSingleton<OwnSide>();
        services.AddSingleton<ZoneTracker>();
        services.AddSingleton<DeathRecap>();
        services.AddSingleton<BuffWatcher>();
        services.AddSingleton<StaggerTracker>();
        services.AddSingleton<LiveTimers>();
        services.AddSingleton(_ => MeterSettings.Load(options.DataDir));
        services.AddSingleton<TrackingMode>();
        return services;
    }
}

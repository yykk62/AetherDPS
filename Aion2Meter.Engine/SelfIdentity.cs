using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Timed;

namespace Aion2Meter.Engine;

/// <summary>
/// Dein Charakter – auch ohne Teleport.
/// 1. Schickt das Spiel deinen Namen (Login/Teleport), wird er dauerhaft gespeichert.
/// 2. Startet der Meter mitten im Spiel, fehlt dieses Paket. Dann: Abklingzeit-Pakete kommen
///    nur für DEINE Skills. Trifft eine Einheit kurz davor ODER danach mit genau diesem Skill,
///    ist das eine Stimme für „das bist du“ (das Spiel schickt die Abklingzeit meist schon beim
///    Auslösen, also VOR dem Treffer). Ab 2 Stimmen gilt die Einheit als du und bekommt den
///    gespeicherten Namen (passt die Klasse nicht, z. B. Zweitcharakter, heißt sie „Du“).
/// </summary>
public sealed class SelfIdentity
{
    public const string Placeholder = "Du";
    private static readonly TimeSpan PairWindow = TimeSpan.FromSeconds(6);
    private const int VotesNeeded = 2;

    private readonly EntityTracker entities;
    private readonly MeterSettings settings;
    private readonly Lock gate = new();
    private readonly Dictionary<int, (int EntityId, int ClassId, DateTime At)> lastUserOfSkill = [];
    private readonly Dictionary<int, DateTime> recentCooldowns = [];
    private readonly Dictionary<int, int> votes = [];
    private bool applying;

    public int? SelfId { get; private set; }
    public bool Guessed { get; private set; }
    public int CooldownPackets { get; private set; }

    public event EventHandler? Changed;

    public SelfIdentity(CombatSessionManager sessions, EntityTracker entities, MeterSettings settings)
    {
        this.entities = entities;
        this.settings = settings;
        entities.UserIdentified += OnUserIdentified;
        sessions.DamageRecorded += OnDamage;
        sessions.SkillCooldownRecorded += OnCooldown;
    }

    public string? Name => SelfId is int id ? entities.GetPlayerEntity(id)?.Name : null;

    private bool Known => SelfId is int id && entities.IsCurrentPlayer(id);

    /// <summary>
    /// Beim Start: Charakter aus der letzten Sitzung wiederherstellen – nur wenn das Spiel seitdem
    /// nicht neu gestartet wurde (die Session-ID gilt bis Logout/Teleport).
    /// </summary>
    public bool Restore(DateTime? gameStartedAt)
    {
        if (settings.CharacterName is null || settings.CharacterSessionId <= 0 || settings.CharacterSeenAt is not DateTime seen) return false;
        if (gameStartedAt is not DateTime started || started > seen) return false;
        try
        {
            applying = true;
            entities.SetSessionPlayerName(settings.CharacterSessionId, settings.CharacterName, 0, settings.CharacterServer ?? "", isUser: true);
        }
        finally
        {
            applying = false;
        }
        SelfId = settings.CharacterSessionId;
        Guessed = true;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void OnUserIdentified(int sessionId, string name, string server)
    {
        SelfId = sessionId;
        Guessed = applying;
        if (!applying && name != Placeholder)
        {
            settings.CharacterName = name;
            settings.CharacterServer = server;
            settings.CharacterSessionId = sessionId;
            settings.CharacterSeenAt = DateTime.Now;
            settings.Save();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnDamage(PlayerDamage hit)
    {
        if (hit.Skill is null || hit.SourceSummon is not null) return;
        int classId = (int)(hit.CharacterClass?.Id ?? 0);
        int entityId = hit.SourceEntity.Id;
        bool vote;
        lock (gate)
        {
            lastUserOfSkill[hit.Skill.Id] = (entityId, classId, hit.DateTime);
            vote = recentCooldowns.TryGetValue(hit.Skill.Id, out var cdAt) && hit.DateTime - cdAt <= PairWindow && hit.DateTime >= cdAt;
        }
        if (vote) Vote(entityId, classId);

        // Wiederhergestellter oder geschätzter Charakter trifft mit fremder Klasse → war falsch
        if (entityId == SelfId && Guessed && classId > 0 && settings.CharacterClassId > 0 && classId != settings.CharacterClassId)
        {
            try { applying = true; entities.SetSessionPlayerName(entityId, $"Player_{entityId}", 0, "", isUser: false); }
            finally { applying = false; }
            SelfId = null;
            Guessed = false;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Klasse des sicher erkannten Charakters merken
        if (entityId == SelfId && !Guessed && classId > 0 && settings.CharacterClassId != classId)
        {
            settings.CharacterClassId = classId;
            settings.Save();
        }
    }

    private void OnCooldown(TimedEvent cd)
    {
        (int EntityId, int ClassId, DateTime At) user;
        bool vote;
        lock (gate)
        {
            CooldownPackets++;
            recentCooldowns[(int)cd.Id] = DateTime.Now;
            vote = lastUserOfSkill.TryGetValue((int)cd.Id, out user) && DateTime.Now - user.At <= PairWindow;
        }
        if (vote) Vote(user.EntityId, user.ClassId);
    }

    private void Vote(int entityId, int classId)
    {
        if (entities.IsCurrentPlayer(entityId))
        {
            SelfId = entityId;
            return;
        }
        if (Known) return;   // schon sicher erkannt (Name vom Spiel)

        int count;
        lock (gate) count = votes[entityId] = votes.GetValueOrDefault(entityId) + 1;
        if (count < VotesNeeded || SelfId == entityId) return;

        bool sameCharacter = settings.CharacterName is not null
                             && (settings.CharacterClassId == 0 || settings.CharacterClassId == classId);
        var name = sameCharacter ? settings.CharacterName! : Placeholder;
        try
        {
            applying = true;
            entities.SetSessionPlayerName(entityId, name, 0, sameCharacter ? settings.CharacterServer ?? "" : "", isUser: true);
        }
        finally
        {
            applying = false;
        }
        SelfId = entityId;
        Guessed = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

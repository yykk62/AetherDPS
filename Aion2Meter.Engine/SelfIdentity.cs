using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Timed;

namespace Aion2Meter.Engine;

/// <summary>
/// Dein Charakter – auch ohne Teleport.
/// 1. Schickt das Spiel deinen Namen (Login/Teleport), wird er dauerhaft gespeichert.
/// 2. Startet der Meter mitten im Spiel, fehlt dieses Paket. Dann: Abklingzeit-Pakete kommen
///    nur für DEINE Skills – wer kurz vorher mit genau diesem Skill getroffen hat, bist du.
///    Dieser Einheit wird der gespeicherte Name gegeben (passt die Klasse nicht, z. B. bei
///    einem Zweitcharakter, heißt sie „Du“, bis das Spiel den echten Namen schickt).
/// </summary>
public sealed class SelfIdentity
{
    public const string Placeholder = "Du";
    private static readonly TimeSpan VoteWindow = TimeSpan.FromSeconds(10);

    private readonly EntityTracker entities;
    private readonly MeterSettings settings;
    private readonly Lock gate = new();
    private readonly Dictionary<int, (int EntityId, int ClassId, DateTime At)> lastUserOfSkill = [];
    private bool applying;

    public int? SelfId { get; private set; }
    public bool Guessed { get; private set; }

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

    private void OnUserIdentified(int sessionId, string name, string server)
    {
        SelfId = sessionId;
        Guessed = applying;
        if (!applying && name != Placeholder && (settings.CharacterName != name || settings.CharacterServer != server))
        {
            settings.CharacterName = name;
            settings.CharacterServer = server;
            settings.Save();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnDamage(PlayerDamage hit)
    {
        if (hit.Skill is null || hit.SourceSummon is not null) return;
        int classId = (int)(hit.CharacterClass?.Id ?? 0);
        lock (gate) lastUserOfSkill[hit.Skill.Id] = (hit.SourceEntity.Id, classId, hit.DateTime);

        // Klasse des eigenen (sicher erkannten) Charakters merken
        if (hit.SourceEntity.Id == SelfId && !Guessed && classId > 0 && settings.CharacterClassId != classId)
        {
            settings.CharacterClassId = classId;
            settings.Save();
        }
    }

    private void OnCooldown(TimedEvent cd)
    {
        (int EntityId, int ClassId, DateTime At) user;
        lock (gate)
        {
            if (!lastUserOfSkill.TryGetValue((int)cd.Id, out user) || DateTime.Now - user.At > VoteWindow) return;
        }
        if (entities.IsCurrentPlayer(user.EntityId))
        {
            SelfId = user.EntityId;
            return;
        }
        if (SelfId is int known && entities.IsCurrentPlayer(known)) return;   // schon sicher erkannt

        bool sameCharacter = settings.CharacterName is not null
                             && (settings.CharacterClassId == 0 || settings.CharacterClassId == user.ClassId);
        var name = sameCharacter ? settings.CharacterName! : Placeholder;
        try
        {
            applying = true;
            entities.SetSessionPlayerName(user.EntityId, name, 0, sameCharacter ? settings.CharacterServer ?? "" : "", isUser: true);
        }
        finally
        {
            applying = false;
        }
        SelfId = user.EntityId;
        Guessed = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

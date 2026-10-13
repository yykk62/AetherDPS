using AionDpsMeter.Core.GameData.Repositories;
using AionDpsMeter.Core.GameData.Services;
using AionDpsMeter.Core.Models;

namespace Aion2Meter.Engine;

/// <summary>
/// Deutsche Namen für Skills, Mobs und Klassen. Quelle der Skill-/Mob-Namen:
/// A2Tools (GPL-3.0), siehe tools/update-german-data.ps1. Fehlt ein Eintrag,
/// bleibt der englische Name aus den Upstream-Daten stehen.
/// </summary>
public static class GermanGameData
{
    private static readonly Dictionary<int, string> ClassNames = new()
    {
        [10] = "Geist",
        [11] = "Gladiator",
        [12] = "Templer",
        [13] = "Assassine",
        [14] = "Waldläufer",
        [15] = "Magier",
        [16] = "Beschwörer",
        [17] = "Kleriker",
        [18] = "Kantor",
        [19] = "Faustkämpfer",
    };

    private static MobRepository? mobs;

    public static int PatchedSkillCount { get; private set; }

    /// <summary>Einmal beim Start aufrufen, bevor die Paketverarbeitung läuft.</summary>
    public static void Apply(string? dataDir = null)
    {
        dataDir ??= Path.Combine(AppContext.BaseDirectory, "GameData", "de");
        var skillsPath = Path.Combine(dataDir, "skills.json");
        var mobsPath = Path.Combine(dataDir, "mobs.json");

        if (File.Exists(skillsPath))
        {
            var german = new SkillRepository();
            german.Load(skillsPath);
            var skills = GameDataProvider.Instance.Skills.Skills;
            int patched = 0;
            foreach (var (id, skill) in skills.ToList())
            {
                if (!german.Skills.TryGetValue(id, out var de) || string.IsNullOrWhiteSpace(de.Name))
                    continue;
                skills[id] = new Skill
                {
                    Id = skill.Id,
                    ClassId = skill.ClassId,
                    Name = de.Name,
                    Icon = skill.Icon,
                    SpecializationFlags = skill.SpecializationFlags,
                };
                patched++;
            }
            PatchedSkillCount = patched;
        }

        if (File.Exists(mobsPath))
        {
            var repo = new MobRepository();
            repo.Load(mobsPath);
            mobs = repo;
        }
    }

    public static string MobName(Mob mob) =>
        mobs is not null && mobs.IsKnownMob(mob.MobCode) ? mobs.GetName(mob.MobCode) : mob.Name;

    public static string ClassName(int classId, string fallback) =>
        ClassNames.TryGetValue(classId, out var name) ? name : fallback;
}

using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTQuestingBotsServer.Globals;

namespace SPTQuestingBotsServer.Controllers;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader)]
public class BotLocationController(
    ISptLogger<BotLocationController> logger,
    ConfigServer configServer,
    DatabaseServer databaseServer,
    DatabaseService databaseService) : IOnLoad
{
    // PMC roles as strings — AdditionalHostilitySettings.BotRole is string
    private static readonly string[] PmcRoles = ["pmcBEAR", "pmcUSEC"];

    private readonly LocationConfig _locationConfig = configServer.GetConfig<LocationConfig>();
    private readonly PmcConfig      _pmcConfig      = configServer.GetConfig<PmcConfig>();
    private readonly BotConfig      _botConfig      = configServer.GetConfig<BotConfig>();

    public static int BasePScavConversionChance { get; private set; } = -1;

    public Task OnLoad()
    {
        if (!ModConfig.Enabled)
            return Task.CompletedTask;

        CachePScavChance();

        if (!ModConfig.BotSpawnsEnabled)
            return Task.CompletedTask;

        logger.Info("[QuestingBots] Configuring bot spawning...");

        AdjustAllBotHostilityChances();
        DisablePvEBossWaves();
        DisableCustomBotWaves();
        UseEFTBotCaps();
        RemoveBlacklistedBrainTypes();

        logger.Info("[QuestingBots] Configuring bot spawning...done.");
        return Task.CompletedTask;
    }

    // ----------------------------------------------------------------
    // PScav conversion chance
    // ----------------------------------------------------------------

    private void CachePScavChance()
    {
        // ChanceAssaultScavHasPlayerScavName is int (not nullable)
        BasePScavConversionChance = _botConfig.ChanceAssaultScavHasPlayerScavName;

        if (ModConfig.AdjustPScavChanceEnabled || (ModConfig.BotSpawnsEnabled && ModConfig.PScavsEnabled))
        {
            _botConfig.ChanceAssaultScavHasPlayerScavName = 0;
            logger.Info($"[QuestingBots] PScav chance set to 0 (controlled by QB). Base was {BasePScavConversionChance}%");
        }
    }

    public void AdjustPScavChanceFactor(double factor)
    {
        if (BasePScavConversionChance < 0) return;
        var newChance = (int)Math.Round(BasePScavConversionChance * factor);
        _botConfig.ChanceAssaultScavHasPlayerScavName = newChance;
        logger.Info($"[QuestingBots] Adjusted PScav spawn chance to {newChance}%");
    }

    // ----------------------------------------------------------------
    // Hostility adjustments
    // ----------------------------------------------------------------

    private void AdjustAllBotHostilityChances()
    {
        var cfgNode = ModConfig.ConfigJson?["bot_spawns"]?["pmc_hostility_adjustments"];
        if (cfgNode?["enabled"]?.GetValue<bool>() != true) return;

        logger.Info("[QuestingBots] Adjusting bot hostility chances...");

        bool alwaysVsScavs    = cfgNode["pmcs_always_hostile_against_scavs"]?.GetValue<bool>() ?? true;
        bool alwaysVsPmcs     = cfgNode["pmcs_always_hostile_against_pmcs"]?.GetValue<bool>() ?? true;
        int  scavEnemyChance  = cfgNode["global_scav_enemy_chance"]?.GetValue<int>() ?? 100;

        string[] pmcEnemyRoles = cfgNode["pmc_enemy_roles"] is JsonNode rolesNode
            ? JsonSerializer.Deserialize<string[]>(rolesNode.ToJsonString()) ?? []
            : [];

        // Per-location AdditionalHostilitySettings
        var locations = databaseService.GetLocations().GetDictionary();
        foreach (var (_, location) in locations)
        {
            var settings = location?.Base?.BotLocationModifier?.AdditionalHostilitySettings;
            if (settings is null) continue;
            foreach (var s in settings)
            {
                if (!PmcRoles.Contains(s.BotRole)) continue;
                AdjustBotHostilitySettings(s, alwaysVsScavs, alwaysVsPmcs, scavEnemyChance, pmcEnemyRoles);
            }
        }

        // SPT pmcConfig.HostilitySettings
        foreach (var (_, hs) in _pmcConfig.HostilitySettings)
        {
            hs.SavageEnemyChance = scavEnemyChance;
            if (alwaysVsScavs) hs.SavagePlayerBehaviour = "AlwaysEnemies";
            foreach (var ce in hs.ChancedEnemies ?? [])
                if (pmcEnemyRoles.Contains(ce.Role)) ce.EnemyChance = 100;
            if (alwaysVsPmcs) { hs.BearEnemyChance = 100; hs.UsecEnemyChance = 100; }
        }

        // Make Scavs enemy to PMCs at difficulty level
        if (alwaysVsScavs)
        {
            var botTypes = databaseServer.GetTables().Bots?.Types;
            if (botTypes is not null)
            {
                foreach (var scavRole in new[] { "assault", "assaultgroup", "marksman" })
                {
                    if (!botTypes.TryGetValue(scavRole, out var botDef)) continue;
                    foreach (var (_, diff) in botDef.BotDifficulty)
                    {
                        diff.Mind.EnemyBotTypes ??= [];
                        foreach (var pmcRole in new[] { WildSpawnType.pmcBEAR, WildSpawnType.pmcUSEC })
                            if (!diff.Mind.EnemyBotTypes.Contains(pmcRole))
                                diff.Mind.EnemyBotTypes.Add(pmcRole);
                    }
                }
            }
        }

        logger.Info("[QuestingBots] Adjusting bot hostility chances...done.");
    }

    private static void AdjustBotHostilitySettings(
        AdditionalHostilitySettings s,
        bool alwaysVsScavs, bool alwaysVsPmcs,
        int scavEnemyChance, string[] pmcEnemyRoles)
    {
        if (s.SavageEnemyChance.HasValue)  s.SavageEnemyChance  = scavEnemyChance;
        if (alwaysVsScavs)                 s.SavagePlayerBehaviour = "AlwaysEnemies";

        foreach (var ce in s.ChancedEnemies ?? [])
            ce.EnemyChance = pmcEnemyRoles.Contains(ce.Role) ? 100 : 0;

        if (alwaysVsPmcs)
        {
            s.BearEnemyChance = 100;
            s.UsecEnemyChance = 100;
            // Add missing PMC roles
            foreach (var pmcRole in PmcRoles)
            {
                if (!pmcEnemyRoles.Contains(pmcRole)) continue;
                s.ChancedEnemies ??= [];
                if (!s.ChancedEnemies.Any(ce => ce.Role == pmcRole))
                    s.ChancedEnemies.Add(new ChancedEnemy { EnemyChance = 100, Role = pmcRole });
            }
        }
    }

    // ----------------------------------------------------------------
    // Disable PvE boss waves
    // ----------------------------------------------------------------

    private void DisablePvEBossWaves()
    {
        int removed = 0;
        var locations = databaseService.GetLocations().GetDictionary();
        foreach (var (_, location) in locations)
        {
            var spawns = location?.Base?.BossLocationSpawn;
            if (spawns is null) continue;
            int before = spawns.Count;
            location!.Base!.BossLocationSpawn = spawns.Where(s => !PmcRoles.Contains(s.BossName)).ToList();
            removed += before - location.Base.BossLocationSpawn.Count;
        }
        if (removed > 0)
            logger.Info($"[QuestingBots] Disabled {removed} PvE boss waves");
    }

    // ----------------------------------------------------------------
    // Disable custom bot waves
    // ----------------------------------------------------------------

    private void DisableCustomBotWaves()
    {
        ClearBossWaves(_locationConfig.CustomWaves?.Boss, "boss");
        ClearScavWaves(_locationConfig.CustomWaves?.Normal, "Scav");
        ClearBossWaves(_pmcConfig.CustomPmcWaves, "PMC");

        if (ModConfig.DisableRogueDelay &&
            _locationConfig.RogueLighthouseSpawnTimeSettings?.WaitTimeSeconds > -1)
        {
            _locationConfig.RogueLighthouseSpawnTimeSettings.WaitTimeSeconds = -1;
            logger.Info("[QuestingBots] Removed SPT Rogue spawn delay");
        }
    }

    private void ClearBossWaves(Dictionary<string, List<BossLocationSpawn>>? waves, string label)
    {
        if (waves is null) return;
        int total = waves.Values.Sum(v => v.Count);
        foreach (var key in waves.Keys.ToList()) waves[key] = [];
        if (total > 0) logger.Info($"[QuestingBots] Disabled {total} custom {label} waves");
    }

    // CustomWaves.Normal is Dictionary<string, List<Wave>> — separate method
    private void ClearScavWaves(Dictionary<string, List<Wave>>? waves, string label)
    {
        if (waves is null) return;
        int total = waves.Values.Sum(v => v.Count);
        foreach (var key in waves.Keys.ToList()) waves[key] = [];
        if (total > 0) logger.Info($"[QuestingBots] Disabled {total} custom {label} waves");
    }

    // ----------------------------------------------------------------
    // EFT bot caps
    // ----------------------------------------------------------------

    private void UseEFTBotCaps()
    {
        var capCfg = ModConfig.ConfigJson?["bot_spawns"]?["bot_cap_adjustments"];
        bool useEft       = capCfg?["use_EFT_bot_caps"]?.GetValue<bool>() ?? true;
        bool onlyDecrease = capCfg?["only_decrease_bot_caps"]?.GetValue<bool>() ?? true;

        Dictionary<string, int> mapAdjustments = [];
        var adjNode = capCfg?["map_specific_adjustments"];
        if (adjNode is not null)
            mapAdjustments = JsonSerializer.Deserialize<Dictionary<string, int>>(adjNode.ToJsonString()) ?? [];

        var locations = databaseService.GetLocations().GetDictionary();
        if (_botConfig.MaxBotCap is null) return;

        foreach (var locationKey in _botConfig.MaxBotCap.Keys.ToList())
        {
            if (!locations.TryGetValue(locationKey, out var locationData) ||
                locationData?.Base is null) continue;

            int originalSpt = _botConfig.MaxBotCap[locationKey];
            int eftCap      = locationData.Base.BotMax;
            bool shouldChange = (originalSpt > eftCap) || !onlyDecrease;

            if (useEft && shouldChange)
                _botConfig.MaxBotCap[locationKey] = eftCap;

            int fixedAdj = mapAdjustments.TryGetValue(locationKey, out var adj) ? adj : 0;
            _botConfig.MaxBotCap[locationKey] += fixedAdj;

            int newCap = _botConfig.MaxBotCap[locationKey];
            if (newCap != originalSpt)
                logger.Info($"[QuestingBots] Bot cap {locationKey}: {originalSpt} → {newCap} (EFT:{eftCap}, adj:{fixedAdj})");
        }
    }

    // ----------------------------------------------------------------
    // Remove blacklisted brain types
    // ----------------------------------------------------------------

    private void RemoveBlacklistedBrainTypes()
    {
        var bad = ModConfig.BlacklistedPmcBotBrains;
        if (bad.Length == 0) return;

        int removed = 0;

        foreach (var (_, mapDict) in _pmcConfig.PmcType ?? [])
            foreach (var (_, brains) in mapDict)
                foreach (var brain in bad)
                    if (brains.Remove(brain)) removed++;

        foreach (var (_, brains) in _botConfig.PlayerScavBrainType ?? [])
            foreach (var brain in bad)
                if (brains.Remove(brain)) removed++;

        logger.Info($"[QuestingBots] Removed {removed} blacklisted brain type entries");
    }
}

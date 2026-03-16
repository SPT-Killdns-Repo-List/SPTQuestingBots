using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTQuestingBotsServer.Globals;

namespace SPTQuestingBotsServer.Controllers;

/// <summary>
/// Validates config arrays and disables QB spawning system if a competing
/// spawning mod is detected among the loaded server mods.
///
/// Uses LauncherController.GetLoadedServerMods() which returns
/// Dictionary&lt;string, AbstractModMetadata&gt; keyed by mod Name —
/// the same names that were used in the original mod.ts check via
/// presptModLoader.getImportedModsNames().
///
/// Runs at PostSptModLoader so all mods are already registered.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader)]
public class SpawnSystemController(
    ISptLogger<SpawnSystemController> logger,
    LauncherController launcherController) : IOnLoad
{
    // Mod Names as they appear in AbstractModMetadata.Name.
    // These match the names used in the original mod.ts spawningModNames array.
    private static readonly string[] ConflictingSpawningModNames =
    [
        "SWAG",
        "DewardianDev-MOAR",
        "PreyToLive-BetterSpawnsPlus",
        "RealPlayerSpawn",
        "Acid's Bot Placement System",
    ];

    public Task OnLoad()
    {
        if (!ModConfig.Enabled)
            return Task.CompletedTask;

        ValidateConfigArrays();
        CheckForConflictingSpawningMods();

        return Task.CompletedTask;
    }

    // ----------------------------------------------------------------
    // Conflicting mod detection — mirrors shouldDisableSpawningSystem()
    // ----------------------------------------------------------------

    private void CheckForConflictingSpawningMods()
    {
        if (!ModConfig.BotSpawnsEnabled)
            return;

        // GetLoadedServerMods() returns Dictionary<string, AbstractModMetadata>
        // where the key IS the mod Name (same as metadata.Name)
        var loadedMods = launcherController.GetLoadedServerMods();

        foreach (var modName in ConflictingSpawningModNames)
        {
            if (loadedMods.ContainsKey(modName))
            {
                logger.Warning($"[QuestingBots] '{modName}' detected – disabling QB spawning system.");

                // Patch the in-memory config so the BepInEx plugin receives
                // bot_spawns.enabled = false when it calls /QuestingBots/GetConfig
                var botSpawns = ModConfig.ConfigJson?["bot_spawns"]?.AsObject();
                if (botSpawns is not null)
                {
                    botSpawns.Remove("enabled");
                    botSpawns.Add("enabled", false);
                }

                // Update the static flag too so BotLocationController sees it
                ModConfig.SetBotSpawnsEnabled(false);
                return;
            }
        }
    }

    // ----------------------------------------------------------------
    // Array validation — mirrors areArraysValid()
    // ----------------------------------------------------------------

    private void ValidateConfigArrays()
    {
        var cfg = ModConfig.ConfigJson;
        if (cfg is null) return;

        bool ok = true;
        ok &= CheckArray(cfg, "questing.bot_quests.eft_quests.level_range",                 leftMustBeInt: true);
        ok &= CheckArray(cfg, "bot_spawns.pmcs.fraction_of_max_players_vs_raidET",          leftMustBeInt: false);
        ok &= CheckArray(cfg, "bot_spawns.pmcs.bots_per_group_distribution",                leftMustBeInt: true);
        ok &= CheckArray(cfg, "bot_spawns.pmcs.bot_difficulty_as_online",                   leftMustBeInt: true);
        ok &= CheckArray(cfg, "bot_spawns.player_scavs.bots_per_group_distribution",        leftMustBeInt: true);
        ok &= CheckArray(cfg, "bot_spawns.player_scavs.bot_difficulty_as_online",           leftMustBeInt: true);
        ok &= CheckArray(cfg, "adjust_pscav_chance.chance_vs_time_remaining_fraction",      leftMustBeInt: false);

        if (!ok)
            logger.Error("[QuestingBots] Config validation failed – some features may not work correctly.");
        else
            logger.Info("[QuestingBots] Config arrays validated successfully.");
    }

    private bool CheckArray(JsonNode root, string dotPath, bool leftMustBeInt)
    {
        JsonNode? node = root;
        foreach (var key in dotPath.Split('.'))
        {
            node = node?[key];
            if (node is null)
            {
                logger.Error($"[QuestingBots] Config path '{dotPath}' not found.");
                return false;
            }
        }

        double[][]? array;
        try { array = JsonSerializer.Deserialize<double[][]>(node.ToJsonString()); }
        catch { array = null; }

        if (array is null || array.Length == 0)
        {
            logger.Error($"[QuestingBots] '{dotPath}' must be a non-empty 2-column array.");
            return false;
        }

        foreach (var row in array)
        {
            if (row.Length != 2)
            {
                logger.Error($"[QuestingBots] '{dotPath}': every row must have exactly 2 columns.");
                return false;
            }
            if (leftMustBeInt && row[0] != Math.Floor(row[0]))
            {
                logger.Error($"[QuestingBots] '{dotPath}' has non-integer left column. Check config.json.");
                return false;
            }
        }
        return true;
    }
}

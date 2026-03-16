using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTQuestingBotsServer.Globals;

namespace SPTQuestingBotsServer.Controllers;

/// <summary>
/// Validates config arrays on server startup.
/// Runs at PostSptModLoader so all mods are loaded.
///
/// Note: conflicting-mod detection (SWAG, MOAR, etc.) is handled on the
/// BepInEx client side via Chainloader — the server has no reliable API
/// to list loaded server mods at this stage.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader)]
public class SpawnSystemController(
    ISptLogger<SpawnSystemController> logger) : IOnLoad
{
    public Task OnLoad()
    {
        if (!ModConfig.Enabled)
            return Task.CompletedTask;

        ValidateConfigArrays();
        return Task.CompletedTask;
    }

    // ----------------------------------------------------------------
    // Array validation — mirrors areArraysValid() from mod.ts
    // ----------------------------------------------------------------

    private void ValidateConfigArrays()
    {
        var cfg = ModConfig.ConfigJson;
        if (cfg is null) return;

        bool ok = true;
        ok &= CheckArray(cfg, "questing.bot_quests.eft_quests.level_range",                    leftMustBeInt: true);
        ok &= CheckArray(cfg, "bot_spawns.pmcs.fraction_of_max_players_vs_raidET",             leftMustBeInt: false);
        ok &= CheckArray(cfg, "bot_spawns.pmcs.bots_per_group_distribution",                   leftMustBeInt: true);
        ok &= CheckArray(cfg, "bot_spawns.pmcs.bot_difficulty_as_online",                      leftMustBeInt: true);
        ok &= CheckArray(cfg, "bot_spawns.player_scavs.bots_per_group_distribution",           leftMustBeInt: true);
        ok &= CheckArray(cfg, "bot_spawns.player_scavs.bot_difficulty_as_online",              leftMustBeInt: true);
        ok &= CheckArray(cfg, "adjust_pscav_chance.chance_vs_time_remaining_fraction",         leftMustBeInt: false);

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

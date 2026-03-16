using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTQuestingBotsServer.Globals;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PreSptModLoader)]
public class ModConfig(
    ISptLogger<ModConfig> logger,
    ModHelper modHelper) : IOnLoad
{
    public static JsonNode? ConfigJson           { get; private set; }
    public static JsonNode? EftQuestSettingsJson { get; private set; }
    public static JsonNode? ZoneAndItemPositionsJson { get; private set; }

    public static bool     Enabled                  { get; private set; } = true;
    public static bool     BotSpawnsEnabled          { get; private set; } = true;
    public static bool     AdjustPScavChanceEnabled  { get; private set; } = false;
    public static bool     PScavsEnabled             { get; private set; } = true;
    public static bool     DisableRogueDelay         { get; private set; } = true;
    public static string[] BlacklistedPmcBotBrains   { get; private set; } = [];

    public static string ModPath { get; private set; } = string.Empty;

    public async Task OnLoad()
    {
        ModPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

        ConfigJson               = await LoadJson("config/config.json");
        EftQuestSettingsJson     = await LoadJson("config/eftQuestSettings.json");
        ZoneAndItemPositionsJson = await LoadJson("config/zoneAndItemQuestPositions.json");

        if (ConfigJson is null)
        {
            logger.Error("[QuestingBots] config.json failed to load – mod will be disabled");
            Enabled = false;
            return;
        }

        Enabled                 = ConfigJson["enabled"]?.GetValue<bool>() ?? true;
        BotSpawnsEnabled        = ConfigJson["bot_spawns"]?["enabled"]?.GetValue<bool>() ?? true;
        AdjustPScavChanceEnabled= ConfigJson["adjust_pscav_chance"]?["enabled"]?.GetValue<bool>() ?? false;
        PScavsEnabled           = ConfigJson["bot_spawns"]?["player_scavs"]?["enabled"]?.GetValue<bool>() ?? true;
        DisableRogueDelay       = ConfigJson["bot_spawns"]?["limit_initial_boss_spawns"]?["disable_rogue_delay"]?.GetValue<bool>() ?? true;

        var brainsNode = ConfigJson["bot_spawns"]?["blacklisted_pmc_bot_brains"];
        BlacklistedPmcBotBrains = brainsNode is not null
            ? JsonSerializer.Deserialize<string[]>(brainsNode.ToJsonString()) ?? []
            : [];

        logger.Info($"[QuestingBots] Config loaded. Enabled={Enabled}, BotSpawns={BotSpawnsEnabled}");
    }

    private async Task<JsonNode?> LoadJson(string relativePath)
    {
        var fullPath = Path.Combine(ModPath, relativePath);
        if (!File.Exists(fullPath))
        {
            logger.Warning($"[QuestingBots] File not found: {fullPath}");
            return null;
        }
        var text = await File.ReadAllTextAsync(fullPath);
        return JsonNode.Parse(text);
    }
}

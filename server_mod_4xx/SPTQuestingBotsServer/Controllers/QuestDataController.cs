using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTQuestingBotsServer.Globals;

namespace SPTQuestingBotsServer.Controllers;

[Injectable]
public class QuestDataController(
    ISptLogger<QuestDataController> logger,
    DatabaseService databaseService,
    ConfigServer configServer)
{
    /// <summary>Returns all EFT quest templates — mirrors questHelper.getQuestsFromDb()</summary>
    public IEnumerable<object> GetAllQuestTemplates()
    {
        var quests = databaseService.GetQuests();
        if (quests is null)
        {
            logger.Warning("[QuestingBots] Quest templates not found in DB");
            return [];
        }
        return quests.Values.Cast<object>();
    }

    /// <summary>Returns eftQuestSettings.json contents</summary>
    public JsonNode? GetEFTQuestSettings() => ModConfig.EftQuestSettingsJson;

    /// <summary>Returns zoneAndItemQuestPositions.json contents</summary>
    public JsonNode? GetZoneAndItemQuestPositions() => ModConfig.ZoneAndItemPositionsJson;

    /// <summary>Returns per-map scav-raid time settings — mirrors iLocationConfig.scavRaidTimeSettings.maps</summary>
    public object? GetScavRaidSettings()
    {
        var locationConfig = configServer.GetConfig<LocationConfig>();
        return locationConfig.ScavRaidTimeSettings?.Maps;
    }

    /// <summary>Returns USEC chance 0-100 — mirrors iPmcConfig.isUsec</summary>
    public double GetUSECChance()
    {
        var pmcConfig = configServer.GetConfig<PmcConfig>();
        return pmcConfig.IsUsec;
    }
}

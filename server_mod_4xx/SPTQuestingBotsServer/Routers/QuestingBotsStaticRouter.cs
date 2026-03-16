using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using SPTQuestingBotsServer.Controllers;
using SPTQuestingBotsServer.Globals;

namespace SPTQuestingBotsServer.Routers;

[Injectable]
public class QuestingBotsStaticRouter : StaticRouter
{
    private static JsonUtil              _jsonUtil  = null!;
    private static QuestDataController   _questData = null!;
    private static BotLocationController _botLoc    = null!;

    public QuestingBotsStaticRouter(
        JsonUtil jsonUtil,
        QuestDataController questData,
        BotLocationController botLoc)
        : base(jsonUtil, GetRoutes())
    {
        _jsonUtil   = jsonUtil;
        _questData  = questData;
        _botLoc     = botLoc;
    }

    private static List<RouteAction> GetRoutes() =>
    [
        new RouteAction("/QuestingBots/GetConfig",
            async (url, info, sessionId, output) =>
                ModConfig.ConfigJson?.ToJsonString() ?? "{}"
        ),

        new RouteAction("/QuestingBots/GetAllQuestTemplates",
            async (url, info, sessionId, output) =>
                _jsonUtil.Serialize(new { templates = _questData.GetAllQuestTemplates() })
        ),

        new RouteAction("/QuestingBots/GetEFTQuestSettings",
            async (url, info, sessionId, output) =>
                _jsonUtil.Serialize(new { settings = _questData.GetEFTQuestSettings() })
        ),

        new RouteAction("/QuestingBots/GetZoneAndItemQuestPositions",
            async (url, info, sessionId, output) =>
                _jsonUtil.Serialize(new { zoneAndItemPositions = _questData.GetZoneAndItemQuestPositions() })
        ),

        new RouteAction("/QuestingBots/GetScavRaidSettings",
            async (url, info, sessionId, output) =>
                _jsonUtil.Serialize(new { maps = _questData.GetScavRaidSettings() })
        ),

        new RouteAction("/QuestingBots/GetUSECChance",
            async (url, info, sessionId, output) =>
                _jsonUtil.Serialize(new { usecChance = _questData.GetUSECChance() })
        ),

        // Dynamic — URL prefix match, factor appended at the end
        new RouteAction("/QuestingBots/AdjustPScavChance/",
            async (url, info, sessionId, output) =>
            {
                var parts = url.TrimEnd('/').Split('/');
                if (double.TryParse(
                        parts[^1],
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double factor))
                    _botLoc.AdjustPScavChanceFactor(factor);

                return _jsonUtil.Serialize(new { resp = "OK" });
            }
        ),
    ];
}

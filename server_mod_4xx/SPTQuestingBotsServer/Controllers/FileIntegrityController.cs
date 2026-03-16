using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTQuestingBotsServer.Globals;

namespace SPTQuestingBotsServer.Controllers;

/// <summary>
/// Checks for leftover files from old mod versions and warns the user.
/// Replaces doesFileIntegrityCheckPass() from mod.ts (SPT 3.x).
/// Runs immediately after ModConfig (PreSptModLoader + 1).
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PreSptModLoader + 1)]
public class FileIntegrityController(
    ISptLogger<FileIntegrityController> logger) : IOnLoad
{
    public Task OnLoad()
    {
        var modRoot = ModConfig.ModPath;
        if (string.IsNullOrEmpty(modRoot))
            return Task.CompletedTask;

        // Warn about obsolete folders that were part of the 3.x user/mods layout
        if (Directory.Exists(Path.Combine(modRoot, "quests")))
        {
            logger.Warning(
                "[QuestingBots] Found obsolete 'quests' folder in the server mod directory. " +
                "Only quest files in 'BepInEx/plugins/DanW-SPTQuestingBots/quests' will be used.");
        }

        if (Directory.Exists(Path.Combine(modRoot, "log")))
        {
            logger.Warning(
                "[QuestingBots] Found obsolete 'log' folder in the server mod directory. " +
                "Logs are now saved in 'BepInEx/plugins/DanW-SPTQuestingBots/log'.");
        }

        // Check for the old flat DLL from the very first BepInEx release of this mod
        // (before the subfolder layout was introduced)
        string oldDllPath = Path.GetFullPath(
            Path.Combine(modRoot, "..", "..", "..", "BepInEx", "plugins", "SPTQuestingBots.dll"));

        if (File.Exists(oldDllPath))
        {
            logger.Error(
                "[QuestingBots] Please remove BepInEx/plugins/SPTQuestingBots.dll from the " +
                "previous version of this mod and restart the server, or it will NOT work correctly.");
        }

        return Task.CompletedTask;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SPT.Reflection.Patching;
using Comfort.Common;
using EFT;
using SPTQuestingBots.Components.Spawning;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Patches
{
    public class BotOwnerBrainActivatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotOwner).GetMethod("method_10", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        protected static void PatchPostfix(BotOwner __instance)
        {
            registerBot(__instance);

            if (BotGenerator.GetAllGeneratedBotProfileIDs().Contains(__instance.Profile.Id))
            {
                reduceBotCounts(__instance);
            }

            if (shouldMakeBotGroupHostileTowardAllBosses(__instance))
            {
                Controllers.BotRegistrationManager.MakeBotGroupHostileTowardAllBosses(__instance);
            }

            // Fix for bots getting stuck in Standby when enemy PMC's are near them
            __instance.StandBy.CanDoStandBy = false;
        }

        private static void registerBot(BotOwner __instance)
        {
            string roleName = __instance.Profile.Info.Settings.Role.ToString();

            LoggingController.LogInfo("Initial spawn type for bot " + __instance.GetText() + ": " + roleName);
            if (__instance.WillBeAPMC())
            {
                Controllers.BotRegistrationManager.RegisterPMC(__instance);
            }
            else if (__instance.WillBeABoss())
            {
                Controllers.BotRegistrationManager.RegisterBoss(__instance);
            }

            Controllers.BotRegistrationManager.WriteMessageForNewBotSpawn(__instance);

            BotLogic.HiveMind.BotHiveMindMonitor.RegisterBot(__instance);
            Singleton<GameWorld>.Instance.GetComponent<Components.DebugData>().RegisterBot(__instance);

            if (__instance.IsARegisteredPMC() || __instance.WillBeAPlayerScav())
            {
                registerBotAsHumanPlayer(__instance);
            }
        }

        private static void registerBotAsHumanPlayer(BotOwner __instance)
        {
            if (!ConfigController.Config.BotSpawns.Enabled)
            {
                return;
            }

            // When SAIN is installed, it manages bot registration in BotSpawner itself.
            // Calling AddPlayer here would trigger SAIN's PlayerSpawnTracker at the wrong time,
            // causing BotComponent.OnDisable() NRE due to uninitialized SAINActivationClass.
            if (BotLogic.ExternalMods.ExternalModHandler.SAINModInfo.IsInstalled)
            {
                return;
            }

            BotSpawner botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;

            botSpawnerClass.AddPlayer(__instance.GetPlayer());
            // Use reflection to subscribe to OnPlayerDead to avoid CS0229 ambiguity from spt-custom.dll
            var playerObj = __instance.GetPlayer();
            var eventInfo = playerObj.GetType().GetEvent("OnPlayerDead", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, typeof(BotOwnerBrainActivatePatch).GetMethod("deletePlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
            eventInfo.AddEventHandler(playerObj, handler);
        }

        private static void deletePlayer(Player player, IPlayer lastAgressor, DamageInfoStruct damage, EBodyPart part)
        {
            BotSpawner botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;

            try
            {
                botSpawnerClass.DeletePlayer(player.GetPlayer());
            }
            catch (Exception ex)
            {
                LoggingController.LogError("Could not delete player " + player.GetText() + ": " + ex.Message);
                LoggingController.LogError(ex.StackTrace);
            }
        }

        private static bool shouldMakeBotGroupHostileTowardAllBosses(BotOwner bot)
        {
            BotType botType = Controllers.BotRegistrationManager.GetBotType(bot);

            float chance = ConfigController.Config.ChanceOfBeingHostileTowardBosses.GetValue(botType) ?? 0;

            System.Random random = new System.Random();
            if (random.Next(1, 100) <= chance)
            {
                return true;
            }

            return false;
        }

        private static void reduceBotCounts(BotOwner bot)
        {
            LoggingController.LogDebug("Adjusting EFT bot counts for " + bot.GetText() + "...");

            BotSpawner botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;

            if (bot.Profile.Info.Settings.IsFollower())
            {
                botSpawnerClass.FollowersBotsCount--;
            }
            else if (bot.Profile.Info.Settings.IsBoss())
            {
                botSpawnerClass.BossBotsCount--;
            }

            botSpawnerClass.AllBotsCount--;
        }
    }
}

using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace Zombs_R_Cute_NoVehicleDamage
{
    // [HarmonyDebug]
    // [HarmonyPatch(typeof(ZombieManager), nameof(ZombieManager.respawnZombies))]
    public static class ZombieManager_respawnZombie_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var newCodes = new List<CodeInstruction>();
            bool found = false;

            for (int i = 0; i < codes.Count; i++)
            {
                newCodes.Add(codes[i]);
                var j = i + 1;
                if (!found &&
                    codes[j++].opcode == OpCodes.Call &&
                    codes[j++].opcode == OpCodes.Callvirt &&
                    codes[j++].opcode == OpCodes.Ldc_I4_1 &&
                    codes[j].opcode == OpCodes.Bne_Un)
                {
                    newCodes.Add(new CodeInstruction(OpCodes.Ldarg_0));// this
                    newCodes.Add(new CodeInstruction(OpCodes.Ldloc_0));// region
                    newCodes.Add(new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(ZombieManager_respawnZombie_Patch), nameof(Improved_respawnZombies))));
                    // newCodes.Add(new CodeInstruction(OpCodes.Ldarg_0));// this
                    // newCodes.Add(new CodeInstruction(OpCodes.Ret));
                    i++;
                    found = true;
                    
                    break;

                }
            }

            foreach (var instruction in newCodes)
            {
                yield return instruction;
            }

            Logger.LogWarning("Improved_respawnZombies_Patch: Patch applied");
        }

        private static float lastTime = 0;
        public static void Improved_respawnZombies(ZombieRegion region)
        {   
            // if(!region.hasBeacon) return;
            var curTime = Time.realtimeSinceStartup - lastTime;
            lastTime = Time.realtimeSinceStartup;            
            
            Logger.Log($"Time: {curTime}\n" +
                       "Region:\n" +
                       $"zombies: {region.zombies.Count}\n" +
                       $"alive: {region.alive}\n" +
                       $"respawnZombieIndex: {region.respawnZombieIndex}\n\n");
            
        }
    }
}
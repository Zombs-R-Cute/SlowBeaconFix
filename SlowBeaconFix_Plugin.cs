using System.Collections.Generic;
using HarmonyLib;
using Rocket.Core.Plugins;
using SDG.NetTransport;
using SDG.Unturned;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;
using Random = UnityEngine.Random;

namespace Zombs_R_Cute_SlowBeaconFix
{
    [HarmonyPatch]
    public class SlowBeaconFix_Plugin : RocketPlugin<SlowBeaconFix_PluginConfiguration>
    {
        private static bool _debug;
        private static int _maximumZombiesToSpawn;

        protected override void Load()
        {
            _debug = Configuration.Instance.Debug;
            _maximumZombiesToSpawn = Configuration.Instance.MaximumZombiesToSpawn;

            Harmony harmony = new Harmony("Beacon Fix");
            harmony.PatchAll();
            Logger.Log($"Slow Beacon Fix Plugin Applied\n" +
                       $"Debug: {_debug}\n" +
                       $"Maximum Zombies: {_maximumZombiesToSpawn}\n\n");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ZombieManager), nameof(ZombieManager.respawnZombies))]
        public static bool ZombieManager_respawnZombies(ZombieManager __instance, byte ___respawnZombiesBound,
            float ___lastWave, bool ____waveReady, int ____waveIndex, int ____waveRemaining,
            ClientStaticMethod<bool, int> ___SendWave)
        {
            var _canRegionSpawnZombiesFromTable = AccessTools.Method(typeof(ZombieManager),
                nameof(ZombieManager.canRegionSpawnZombiesFromTable));

            if (__instance == null)
                return false;

            ZombieRegion region = ZombieManager.regions[___respawnZombiesBound];
            if (region.hasBeacon)
            {
                var remaining = BeaconManager.checkBeacon(___respawnZombiesBound).getRemaining();
                if (_debug)
                    Logger.Log("Region:\n" +
                               $"location zombie maximum: {region.zombies.Count}\n" +
                               $"remaining in horde: {remaining}\n" +
                               $"currently alive: {region.alive}\n\n\n");

                if (!LevelNavigation.flagData[___respawnZombiesBound].spawnZombies ||
                    region.zombies.Count <= 0 || region.hasBeacon &&
                    BeaconManager.checkBeacon(___respawnZombiesBound).getRemaining() == 0)
                    return false;

                if (remaining > _maximumZombiesToSpawn)
                    remaining = _maximumZombiesToSpawn;

                for (int i = 0; i < region.zombies.Count && remaining > 0; i++)
                {
                    Zombie zomby = region.zombies[(i + region.respawnZombieIndex) % region.zombies.Count];
                    if (!zomby.isDead)
                        continue;

                    remaining--;

                    ZombieSpawnpoint zombieSpawnpoint = LevelZombies.zombies[___respawnZombiesBound][
                        Random.Range(0, LevelZombies.zombies[___respawnZombiesBound].Count)];
                    if (!SafezoneManager.checkPointValid(zombieSpawnpoint.point))
                        continue;
                    for (ushort index = 0; index < region.zombies.Count; ++index)
                    {
                        if (!region.zombies[index].isDead &&
                            (region.zombies[index].transform.position - zombieSpawnpoint.point)
                            .sqrMagnitude <
                            4.0)
                            continue;
                    }

                    byte num2 = zombieSpawnpoint.type;
                    ZombieTable table1 = LevelZombies.tables[num2];
                    if (!canRegionSpawnZombiesFromTable(region, table1))
                        continue;
                    EZombieSpeciality speciality = EZombieSpeciality.NORMAL;
                    if ((region.hasBeacon
                            ? (BeaconManager.checkBeacon(___respawnZombiesBound).getRemaining() == 1 ? 1 : 0)
                            : (table1.isMega ? 1 : 0)) != 0)
                    {
                        if (!table1.isMega)
                        {
                            for (byte index = 0; index < LevelZombies.tables.Count; ++index)
                            {
                                ZombieTable table2 = LevelZombies.tables[index];
                                if (table2.isMega)
                                {
                                    num2 = index;
                                    table1 = table2;
                                    break;
                                }
                            }
                        }

                        region.lastMega = Time.realtimeSinceStartup;
                        region.hasMega = true;
                        speciality = EZombieSpeciality.MEGA;
                    }
                    else if (Level.info.type == ELevelType.SURVIVAL)
                        speciality = generateZombieSpeciality(___respawnZombiesBound, table1);

                    int maxBossZombies = LevelNavigation.flagData[___respawnZombiesBound].maxBossZombies;
                    if (maxBossZombies >= 0 && speciality.IsBoss()) // && region.aliveBossZombieCount >= maxBossZombies)
                        continue;
                    if (region.hasBeacon)
                        BeaconManager.checkBeacon(___respawnZombiesBound).spawnRemaining();
                    byte shirt;
                    byte pants;
                    byte hat;
                    byte gear;
                    GetSpawnClothingParameters(table1, out shirt, out pants, out hat, out gear);
                    Vector3 position = zombieSpawnpoint.point + new Vector3(0.0f, 0.5f, 0.0f);
                    zomby.sendRevive(num2, (byte)speciality, shirt, pants, hat, gear, position,
                        Random.Range(0.0f, 360f));
                    if (Level.info.type != ELevelType.HORDE)
                        continue;
                    --____waveRemaining;
                }

                return false;
            }

            // normal spawn
            {
                if (Level.info.type == ELevelType.HORDE)
                {
                    if (ZombieManager.waveRemaining > 0 || region.alive > 0)
                        ___lastWave = Time.realtimeSinceStartup;
                    if (ZombieManager.waveRemaining == 0)
                    {
                        if (region.alive > 0)
                            return false;
                        if (Time.realtimeSinceStartup - (double)___lastWave > 10.0 ||
                            ZombieManager.waveIndex == 0)
                        {
                            if (!ZombieManager.waveReady)
                            {
                                ____waveReady = true;
                                ++____waveIndex;
                                ____waveRemaining =
                                    (int)Mathf.Ceil(Mathf.Pow(ZombieManager.waveIndex + 5, 1.5f));
                                ___SendWave.InvokeAndLoopback(ENetReliability.Reliable,
                                    Provider.GatherRemoteClientConnections(),
                                    ZombieManager.waveReady, ZombieManager.waveIndex);
                            }
                        }
                        else
                        {
                            if (!ZombieManager.waveReady)
                                return false;
                            ____waveReady = false;
                            ___SendWave.InvokeAndLoopback(ENetReliability.Reliable,
                                Provider.GatherRemoteClientConnections(),
                                ZombieManager.waveReady, ZombieManager.waveIndex);
                            return false;
                        }
                    }
                }

                if (!LevelNavigation.flagData[___respawnZombiesBound].spawnZombies ||
                    region.zombies.Count <= 0 || region.hasBeacon &&
                    BeaconManager.checkBeacon(___respawnZombiesBound).getRemaining() == 0)
                    return false;
                if (region.respawnZombieIndex >= region.zombies.Count)
                    region.respawnZombieIndex = (ushort)(region.zombies.Count - 1);
                Zombie zomby = region.zombies[region.respawnZombieIndex];
                ++region.respawnZombieIndex;
                if (region.respawnZombieIndex >= region.zombies.Count)
                    region.respawnZombieIndex = 0;
                if (!zomby.isDead)
                    return false;
                float num1 = Provider.modeConfigData.Zombies.Respawn_Day_Time;
                if (region.hasBeacon)
                    num1 = Provider.modeConfigData.Zombies.Respawn_Beacon_Time;
                else if (LightingManager.isFullMoon)
                    num1 = Provider.modeConfigData.Zombies.Respawn_Night_Time;
                if (Time.realtimeSinceStartup - (double)zomby.lastDead <= num1)
                    return false;
                ZombieSpawnpoint zombieSpawnpoint = LevelZombies.zombies[___respawnZombiesBound][
                    Random.Range(0, LevelZombies.zombies[___respawnZombiesBound].Count)];
                if (!SafezoneManager.checkPointValid(zombieSpawnpoint.point))
                    return false;
                for (ushort index = 0; index < region.zombies.Count; ++index)
                {
                    if (!region.zombies[index].isDead &&
                        (region.zombies[index].transform.position - zombieSpawnpoint.point).sqrMagnitude <
                        4.0)
                        return false;
                }

                byte num2 = zombieSpawnpoint.type;
                ZombieTable table1 = LevelZombies.tables[num2];
                if (!canRegionSpawnZombiesFromTable(region, table1))
                    return false;
                EZombieSpeciality speciality = EZombieSpeciality.NORMAL;
                if ((region.hasBeacon
                        ? (BeaconManager.checkBeacon(___respawnZombiesBound).getRemaining() == 1 ? 1 : 0)
                        : (table1.isMega ? 1 : 0)) != 0)
                {
                    if (!table1.isMega)
                    {
                        for (byte index = 0; index < LevelZombies.tables.Count; ++index)
                        {
                            ZombieTable table2 = LevelZombies.tables[index];
                            if (table2.isMega)
                            {
                                num2 = index;
                                table1 = table2;
                                break;
                            }
                        }
                    }

                    region.lastMega = Time.realtimeSinceStartup;
                    region.hasMega = true;
                    speciality = EZombieSpeciality.MEGA;
                }
                else if (Level.info.type == ELevelType.SURVIVAL)
                    speciality = generateZombieSpeciality(___respawnZombiesBound, table1);

                int maxBossZombies = LevelNavigation.flagData[___respawnZombiesBound].maxBossZombies;
                if (maxBossZombies >= 0 && speciality.IsBoss()) // && region.aliveBossZombieCount >= maxBossZombies)
                    return false;
                if (region.hasBeacon)
                    BeaconManager.checkBeacon(___respawnZombiesBound).spawnRemaining();
                byte shirt;
                byte pants;
                byte hat;
                byte gear;
                GetSpawnClothingParameters(table1, out shirt, out pants, out hat, out gear);
                Vector3 position = zombieSpawnpoint.point + new Vector3(0.0f, 0.5f, 0.0f);
                zomby.sendRevive(num2, (byte)speciality, shirt, pants, hat, gear, position,
                    Random.Range(0.0f, 360f));
                if (Level.info.type != ELevelType.HORDE)
                    return false;
                --____waveRemaining;
            }
            return false;
        }

        public static bool canRegionSpawnZombiesFromTable(ZombieRegion region, ZombieTable table)
        {
            if (region.hasBeacon)
                return !table.isMega;
            if (!table.isMega)
                return true;
            return !region.hasMega && Time.realtimeSinceStartup - (double)region.lastMega > 600.0;
        }


        public static EZombieSpeciality generateZombieSpeciality(byte bound, ZombieTable table)
        {
            ZombieSpecialityWeightedRandom zombieSpecialityTable = new ZombieSpecialityWeightedRandom();
            ZombieDifficultyAsset zombieDifficultyAsset = ZombieManager.getDifficultyInBound(bound);
            if (zombieDifficultyAsset == null || !zombieDifficultyAsset.Overrides_Spawn_Chance)
                zombieDifficultyAsset = table.resolveDifficulty();

            if (zombieDifficultyAsset != null && zombieDifficultyAsset.Overrides_Spawn_Chance)
            {
                zombieSpecialityTable.add(EZombieSpeciality.CRAWLER,
                    zombieDifficultyAsset.Crawler_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.SPRINTER,
                    zombieDifficultyAsset.Sprinter_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.FLANKER_FRIENDLY,
                    zombieDifficultyAsset.Flanker_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BURNER, zombieDifficultyAsset.Burner_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.ACID, zombieDifficultyAsset.Acid_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BOSS_ELECTRIC,
                    zombieDifficultyAsset.Boss_Electric_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BOSS_WIND,
                    zombieDifficultyAsset.Boss_Wind_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BOSS_FIRE,
                    zombieDifficultyAsset.Boss_Fire_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.SPIRIT, zombieDifficultyAsset.Spirit_Chance);
                if (Level.isLoaded && LightingManager.isNighttime)
                {
                    zombieSpecialityTable.add(EZombieSpeciality.DL_RED_VOLATILE,
                        zombieDifficultyAsset.DL_Red_Volatile_Chance);
                    zombieSpecialityTable.add(EZombieSpeciality.DL_BLUE_VOLATILE,
                        zombieDifficultyAsset.DL_Blue_Volatile_Chance);
                }

                zombieSpecialityTable.add(EZombieSpeciality.BOSS_ELVER_STOMPER,
                    zombieDifficultyAsset.Boss_Elver_Stomper_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BOSS_KUWAIT,
                    zombieDifficultyAsset.Boss_Kuwait_Chance);
            }
            else
            {
                zombieSpecialityTable.add(EZombieSpeciality.CRAWLER,
                    Provider.modeConfigData.Zombies.Crawler_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.SPRINTER,
                    Provider.modeConfigData.Zombies.Sprinter_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.FLANKER_FRIENDLY,
                    Provider.modeConfigData.Zombies.Flanker_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BURNER,
                    Provider.modeConfigData.Zombies.Burner_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.ACID,
                    Provider.modeConfigData.Zombies.Acid_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BOSS_ELECTRIC,
                    Provider.modeConfigData.Zombies.Boss_Electric_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BOSS_WIND,
                    Provider.modeConfigData.Zombies.Boss_Wind_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BOSS_FIRE,
                    Provider.modeConfigData.Zombies.Boss_Fire_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.SPIRIT,
                    Provider.modeConfigData.Zombies.Spirit_Chance);
                if (Level.isLoaded && LightingManager.isNighttime)
                {
                    zombieSpecialityTable.add(EZombieSpeciality.DL_RED_VOLATILE,
                        Provider.modeConfigData.Zombies.DL_Red_Volatile_Chance);
                    zombieSpecialityTable.add(EZombieSpeciality.DL_BLUE_VOLATILE,
                        Provider.modeConfigData.Zombies.DL_Blue_Volatile_Chance);
                }

                zombieSpecialityTable.add(EZombieSpeciality.BOSS_ELVER_STOMPER,
                    Provider.modeConfigData.Zombies.Boss_Elver_Stomper_Chance);
                zombieSpecialityTable.add(EZombieSpeciality.BOSS_KUWAIT,
                    Provider.modeConfigData.Zombies.Boss_Kuwait_Chance);
            }

            zombieSpecialityTable.add(EZombieSpeciality.NORMAL,
                1f - zombieSpecialityTable.totalWeight);
            return zombieSpecialityTable.get();
        }

        public class ZombieSpecialityWeightedRandom :
            IComparer<ZombieSpecialityWeightedRandom.Entry>
        {
            public List<Entry> entries;

            public float totalWeight { get; set; }

            public void clear()
            {
                entries.Clear();
                totalWeight = 0.0f;
            }

            public void add(EZombieSpeciality value, float weight)
            {
                weight = Mathf.Max(weight, 0.0f);
                Entry entry =
                    new Entry(value, weight);
                int index = entries.BinarySearch(entry,
                    this);
                if (index < 0)
                    index = ~index;
                entries.Insert(index, entry);
                totalWeight += weight;
            }

            public EZombieSpeciality get()
            {
                if (entries.Count < 1)
                    return EZombieSpeciality.NONE;
                float num = Random.value * totalWeight;
                foreach (Entry entry in entries)
                {
                    if (num < (double)entry.weight)
                        return entry.value;
                    num -= entry.weight;
                }

                return entries[0].value;
            }

            public void log()
            {
                UnturnedLog.info("Entries: {0} Total Weight: {1}", entries.Count,
                    totalWeight);
                foreach (Entry entry in entries)
                    UnturnedLog.info("{0}: {1}", entry.value, entry.weight);
            }

            public int Compare(
                Entry lhs,
                Entry rhs)
            {
                return -lhs.weight.CompareTo(rhs.weight);
            }

            public ZombieSpecialityWeightedRandom()
            {
                entries = new List<Entry>();
                totalWeight = 0.0f;
            }

            public struct Entry
            {
                public EZombieSpeciality value;
                public float weight;

                public Entry(EZombieSpeciality value, float weight)
                {
                    this.value = value;
                    this.weight = weight;
                }
            }
        }

        public static void GetSpawnClothingParameters(ZombieTable table,
            out byte shirt,
            out byte pants,
            out byte hat,
            out byte gear)
        {
            shirt = byte.MaxValue;
            if (table.slots[0].table.Count > 0 && Random.value < (double)table.slots[0].chance)
                shirt = (byte)Random.Range(0, table.slots[0].table.Count);
            pants = byte.MaxValue;
            if (table.slots[1].table.Count > 0 && Random.value < (double)table.slots[1].chance)
                pants = (byte)Random.Range(0, table.slots[1].table.Count);
            hat = byte.MaxValue;
            if (table.slots[2].table.Count > 0 && Random.value < (double)table.slots[2].chance)
                hat = (byte)Random.Range(0, table.slots[2].table.Count);
            gear = byte.MaxValue;
            if (table.slots[3].table.Count <= 0 || Random.value >= (double)table.slots[3].chance)
                return;
            gear = (byte)Random.Range(0, table.slots[3].table.Count);
        }
    }
}
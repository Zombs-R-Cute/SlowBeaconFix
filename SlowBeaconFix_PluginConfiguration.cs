using System.Diagnostics;
using Rocket.API;

namespace Zombs_R_Cute_SlowBeaconFix
{
    public class SlowBeaconFix_PluginConfiguration :IRocketPluginConfiguration
    {
        public bool Debug;
        public int MaximumZombiesToSpawn;
        
        public void LoadDefaults()
        {
            Debug = false;
            MaximumZombiesToSpawn = 20;
        }
    }
}
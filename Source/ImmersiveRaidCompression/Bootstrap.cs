using HarmonyLib;
using Verse;

namespace ImmersiveRaidCompression
{
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        public const string HarmonyId = "langyejia.immersiveraidcompression";

        static Bootstrap()
        {
            Harmony harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            Log.Message("[Immersive Raid Compression] Loaded human, pirate reinforcement, mechanoid, phased mech-wave, manhunter, mech-cluster, ordinary-infestation, and fleshbeast compression.");
        }
    }
}

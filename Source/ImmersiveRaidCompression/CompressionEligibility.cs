using RimWorld;

namespace ImmersiveRaidCompression
{
    public static class CompressionEligibility
    {
        public static CompressionPlan PlanFor(PawnGroupMakerParms parms, int originalCount)
        {
            CompressionSettings settings = CompressionMod.Settings;
            if (settings == null || parms == null)
            {
                return null;
            }

            if (parms.groupKind != PawnGroupKindDefOf.Combat || parms.raidStrategy == null)
            {
                return null;
            }

            if (parms.points < settings.minimumRaidPoints || parms.faction?.HostileTo(Faction.OfPlayer) != true)
            {
                return null;
            }

            if (settings.enableHumanRaids
                && parms.faction.def.humanlikeFaction
                && originalCount > settings.humanSoftPawnCap)
            {
                return new CompressionPlan(settings.humanSoftPawnCap, HumanRaidCompressionPolicy.Instance);
            }

            if (settings.enableMechanoidRaids
                && parms.faction.def == FactionDefOf.Mechanoid
                && originalCount > settings.mechanoidSoftPawnCap)
            {
                return new CompressionPlan(settings.mechanoidSoftPawnCap, MechanoidRaidCompressionPolicy.Instance);
            }

            return null;
        }
    }

    public sealed class CompressionPlan
    {
        public int TargetCount { get; }
        public ICompressionPolicy Policy { get; }

        public CompressionPlan(int targetCount, ICompressionPolicy policy)
        {
            TargetCount = targetCount;
            Policy = policy;
        }
    }
}

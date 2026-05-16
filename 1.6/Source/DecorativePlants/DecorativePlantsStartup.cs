using Verse;

namespace DecorativePlants
{
    [StaticConstructorOnStartup]
    public static class DecorativePlantsStartup
    {
        static DecorativePlantsStartup()
        {
            Log.Message("[Decorative Plants] Startup hook registered runtime generation after long events.");
            LongEventHandler.ExecuteWhenFinished(ThingDefGenerator_DecorativePlants.RegisterRuntimeDefs);
        }
    }
}

using UnityEngine;
using Verse;

namespace DecorativePlants
{
    public sealed class DecorativePlantsMod : Mod
    {
        public static DecorativePlantsSettings Settings;

        public DecorativePlantsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DecorativePlantsSettings>();
            Log.Message("[Decorative Plants] Mod loaded. Waiting for implied ThingDef generation.");
        }

        public override string SettingsCategory()
        {
            return "DP_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("DP_RestartRequired".Translate());
            listing.GapLine();
            listing.CheckboxLabeled("DP_IncludeTrees".Translate(), ref Settings.includeTrees);
            listing.CheckboxLabeled("DP_IncludeSowablePlants".Translate(), ref Settings.includeCrops);
            listing.CheckboxLabeled("DP_IncludeNonSowablePlants".Translate(), ref Settings.includeNonSowablePlants);
            listing.CheckboxLabeled("DP_IncludeModdedPlants".Translate(), ref Settings.includeModdedPlants);
            listing.CheckboxLabeled("DP_CopyVanillaGlow".Translate(), ref Settings.copyVanillaGlow);
            listing.CheckboxLabeled("DP_UseOriginalFlammability".Translate(), ref Settings.useOriginalFlammability);
            listing.CheckboxLabeled("DP_UseOriginalBeauty".Translate(), ref Settings.useOriginalBeauty);
            Settings.beautyMultiplier = listing.SliderLabeled("DP_BeautyMultiplier".Translate(Settings.beautyMultiplier.ToString("0.##")), Settings.beautyMultiplier, 0f, 5f);
            Settings.workToBuild = Mathf.RoundToInt(listing.SliderLabeled("DP_WorkToBuild".Translate(Settings.workToBuild.ToString()), Settings.workToBuild, 1f, 1000f));
            listing.End();
        }
    }

    public sealed class DecorativePlantsSettings : ModSettings
    {
        public bool includeTrees = false;
        public bool includeCrops = true;
        public bool includeNonSowablePlants = true;
        public bool includeModdedPlants = true;
        public bool copyVanillaGlow = false;
        public float beautyMultiplier = 1f;
        public int workToBuild = 80;
        public bool useOriginalFlammability = true;
        public bool useOriginalBeauty = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref includeTrees, "includeTrees", false);
            Scribe_Values.Look(ref includeCrops, "includeCrops", true);
            Scribe_Values.Look(ref includeNonSowablePlants, "includeNonSowablePlants", true);
            Scribe_Values.Look(ref includeModdedPlants, "includeModdedPlants", true);
            Scribe_Values.Look(ref copyVanillaGlow, "copyVanillaGlow", false);
            Scribe_Values.Look(ref beautyMultiplier, "beautyMultiplier", 1f);
            Scribe_Values.Look(ref workToBuild, "workToBuild", 80);
            Scribe_Values.Look(ref useOriginalFlammability, "useOriginalFlammability", true);
            Scribe_Values.Look(ref useOriginalBeauty, "useOriginalBeauty", true);
        }
    }
}

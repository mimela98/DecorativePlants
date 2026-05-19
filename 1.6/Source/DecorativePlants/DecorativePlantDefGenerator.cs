using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DecorativePlants
{
    public static class ThingDefGenerator_DecorativePlants
    {
        private const string DefNamePrefix = "DecorativePlant_";
        private const string CategoryDefName = "DecorativePlants";
        private static bool registeredAtRuntime;

        public static IEnumerable<ThingDef> ImpliedThingDefs(bool hotReload)
        {
            List<ThingDef> generatedDefs = BuildImpliedDefs(hotReload, out int plantCount, out int blueprintCount, out int frameCount);
            LogGeneration("implied generator", plantCount, blueprintCount, frameCount, generatedDefs.Count);
            return generatedDefs;
        }

        public static void RegisterRuntimeDefs()
        {
            if (registeredAtRuntime)
            {
                return;
            }

            registeredAtRuntime = true;

            try
            {
                List<ThingDef> generatedDefs = BuildImpliedDefs(hotReload: false, out int plantCount, out int blueprintCount, out int frameCount);
                List<ThingDef> addedDefs = new List<ThingDef>();
                AssignShortHashes(generatedDefs);

                foreach (ThingDef def in generatedDefs)
                {
                    if (DefDatabase<ThingDef>.GetNamedSilentFail(def.defName) == null)
                    {
                        DefDatabase<ThingDef>.Add(def);
                        addedDefs.Add(def);
                    }
                }

                foreach (ThingDef def in addedDefs)
                {
                    def.PostLoad();
                    def.ResolveReferences();
                    DisableAtlasingOnGraphicTree(def.graphic);
                    ResolveGhostGraphic(def);
                }

                DefDatabase<ThingDef>.InitializeShortHashDictionary();

                DesignationCategoryDef category = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(CategoryDefName);
                RefreshArchitectCategory(category, addedDefs);

                LogGeneration("runtime registration", plantCount, blueprintCount, frameCount, addedDefs.Count);
            }
            catch (Exception ex)
            {
                Log.Error("[Decorative Plants] Runtime decorative plant generation failed:\n" + ex);
            }
        }

        private static void RefreshArchitectCategory(DesignationCategoryDef category, List<ThingDef> addedDefs)
        {
            if (category == null)
            {
                return;
            }

            category.DirtyCache();
            typeof(DesignationCategoryDef).GetMethod("ResolveDesignators", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(category, Array.Empty<object>());

            List<ThingDef> buildableDefs = addedDefs
                .Where(def => def.defName.StartsWith(DefNamePrefix, StringComparison.Ordinal) && !def.IsBlueprint && !def.IsFrame && def.designationCategory == category)
                .ToList();

            ReplaceDecorativeBuildDesignators(category, buildableDefs);
            int designatorCount = CountBuildDesignators(category, buildableDefs);
            if (designatorCount < buildableDefs.Count)
            {
                AppendMissingBuildDesignators(category, buildableDefs);
                ReplaceDecorativeBuildDesignators(category, buildableDefs);
                designatorCount = CountBuildDesignators(category, buildableDefs);
            }

            category.DirtyCache();
            Log.Message($"[Decorative Plants] Architect category '{CategoryDefName}' has {designatorCount}/{buildableDefs.Count} decorative plant build designators after refresh.");
        }

        private static int CountBuildDesignators(DesignationCategoryDef category, List<ThingDef> buildableDefs)
        {
            HashSet<ThingDef> targets = new HashSet<ThingDef>(buildableDefs);
            int count = 0;
            foreach (Designator designator in category.AllResolvedDesignators)
            {
                if (designator is Designator_Build build && GetBuildDesignatorThingDef(build) is ThingDef thingDef && targets.Contains(thingDef))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AppendMissingBuildDesignators(DesignationCategoryDef category, List<ThingDef> buildableDefs)
        {
            FieldInfo resolvedDesignatorsField = typeof(DesignationCategoryDef).GetField("resolvedDesignators", BindingFlags.NonPublic | BindingFlags.Instance);
            if (resolvedDesignatorsField == null)
            {
                Log.Warning("[Decorative Plants] Could not access DesignationCategoryDef.resolvedDesignators; Architect category may remain empty.");
                return;
            }

            List<Designator> designators = resolvedDesignatorsField.GetValue(category) as List<Designator>;
            if (designators == null)
            {
                designators = new List<Designator>();
                resolvedDesignatorsField.SetValue(category, designators);
            }

            HashSet<ThingDef> existing = new HashSet<ThingDef>();
            foreach (Designator designator in designators)
            {
                if (designator is Designator_Build build && GetBuildDesignatorThingDef(build) is ThingDef thingDef)
                {
                    existing.Add(thingDef);
                }
            }

            foreach (ThingDef def in buildableDefs)
            {
                if (!existing.Contains(def))
                {
                    designators.Add(new Designator_BuildDecorativePlant(def));
                }
            }
        }

        private static void ReplaceDecorativeBuildDesignators(DesignationCategoryDef category, List<ThingDef> buildableDefs)
        {
            FieldInfo resolvedDesignatorsField = typeof(DesignationCategoryDef).GetField("resolvedDesignators", BindingFlags.NonPublic | BindingFlags.Instance);
            if (!(resolvedDesignatorsField?.GetValue(category) is List<Designator> designators))
            {
                return;
            }

            HashSet<ThingDef> targets = new HashSet<ThingDef>(buildableDefs);
            for (int i = 0; i < designators.Count; i++)
            {
                if (designators[i] is Designator_Build build &&
                    !(designators[i] is Designator_BuildDecorativePlant) &&
                    GetBuildDesignatorThingDef(build) is ThingDef thingDef &&
                    targets.Contains(thingDef))
                {
                    designators[i] = new Designator_BuildDecorativePlant(thingDef);
                }
            }
        }

        private static ThingDef GetBuildDesignatorThingDef(Designator_Build build)
        {
            FieldInfo entDefField = typeof(Designator_Build).GetField("entDef", BindingFlags.NonPublic | BindingFlags.Instance);
            return entDefField?.GetValue(build) as ThingDef;
        }

        private static void AssignShortHashes(List<ThingDef> generatedDefs)
        {
            MethodInfo giveShortHash = typeof(ShortHashGiver).GetMethod("GiveShortHash", BindingFlags.NonPublic | BindingFlags.Static);
            if (giveShortHash == null)
            {
                Log.Warning("[Decorative Plants] Could not find ShortHashGiver.GiveShortHash; generated plants may have invalid short hashes.");
                return;
            }

            HashSet<ushort> takenHashes = new HashSet<ushort>();
            foreach (ThingDef existing in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (existing.shortHash != 0)
                {
                    takenHashes.Add(existing.shortHash);
                }
            }

            foreach (ThingDef def in generatedDefs)
            {
                if (def.shortHash == 0)
                {
                    giveShortHash.Invoke(null, new object[] { def, typeof(ThingDef), takenHashes });
                }
                else
                {
                    takenHashes.Add(def.shortHash);
                }
            }
        }

        private static List<ThingDef> BuildImpliedDefs(bool hotReload, out int plantCount, out int blueprintCount, out int frameCount)
        {
            DecorativePlantsSettings settings = DecorativePlantsMod.Settings ?? LoadedModManager.GetMod<DecorativePlantsMod>()?.GetSettings<DecorativePlantsSettings>() ?? new DecorativePlantsSettings();
            DesignationCategoryDef category = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(CategoryDefName);
            ThingDef woodLog = DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog");
            List<ThingDef> generatedDefs = new List<ThingDef>();
            plantCount = 0;
            blueprintCount = 0;
            frameCount = 0;

            if (category == null)
            {
                Log.Warning("[Decorative Plants] Designation category 'DecorativePlants' was not found. Generated plants may not appear in the Architect menu.");
            }

            foreach (ThingDef source in DefDatabase<ThingDef>.AllDefsListForReading.ToList())
            {
                if (!ShouldGenerateForPlant(source, settings))
                {
                    continue;
                }

                ThingDef generated = MakeDecorativePlantDef(source, settings, category, woodLog);
                if (generated != null)
                {
                    plantCount++;
                    generatedDefs.Add(generated);
                    foreach (ThingDef constructionDef in MakeConstructionDefs(generated, hotReload))
                    {
                        if (constructionDef.IsBlueprint)
                        {
                            blueprintCount++;
                        }
                        else if (constructionDef.IsFrame)
                        {
                            frameCount++;
                        }

                        generatedDefs.Add(constructionDef);
                    }
                }
            }

            return generatedDefs;
        }

        private static void LogGeneration(string path, int plantCount, int blueprintCount, int frameCount, int totalCount)
        {
            DecorativePlantsSettings settings = DecorativePlantsMod.Settings ?? new DecorativePlantsSettings();
            Log.Message($"[Decorative Plants] Generated {plantCount} decorative plant ThingDefs, {blueprintCount} blueprints, {frameCount} frames ({totalCount} total ThingDefs) via {path}. Settings: includeTrees={settings.includeTrees}, includeSowablePlants={settings.includeCrops}, includeNonSowablePlants={settings.includeNonSowablePlants}, includeModdedPlants={settings.includeModdedPlants}, copyVanillaGlow={settings.copyVanillaGlow}.");
        }

        private static bool ShouldGenerateForPlant(ThingDef source, DecorativePlantsSettings settings)
        {
            if (source?.plant == null || source.graphicData == null || string.IsNullOrEmpty(source.defName))
            {
                return false;
            }

            if (source.defName.StartsWith(DefNamePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (DefDatabase<ThingDef>.GetNamedSilentFail(DefNamePrefix + SanitizeDefName(source.defName)) != null)
            {
                return false;
            }

            if (!settings.includeModdedPlants && source.modContentPack != null && !source.modContentPack.IsCoreMod && !source.modContentPack.IsOfficialMod)
            {
                return false;
            }

            if (!settings.includeTrees && IsTree(source))
            {
                return false;
            }

            bool sowable = IsSowable(source);
            if (!settings.includeCrops && sowable)
            {
                return false;
            }

            if (!settings.includeNonSowablePlants && !sowable)
            {
                return false;
            }

            return true;
        }

        private static ThingDef MakeDecorativePlantDef(ThingDef source, DecorativePlantsSettings settings, DesignationCategoryDef category, ThingDef woodLog)
        {
            string safeName = SanitizeDefName(source.defName);
            ThingDef def = new ThingDef
            {
                defName = DefNamePrefix + safeName,
                label = "DP_DecorativePlantLabel".Translate(source.label).Resolve(),
                description = "DP_DecorativePlantDescription".Translate(source.label).Resolve(),
                thingClass = typeof(Building_DecorativePlant),
                category = ThingCategory.Building,
                drawerType = DrawerType.MapMeshOnly,
                graphicData = CopyGraphicData(source),
                selectable = true,
                useHitPoints = true,
                destroyable = true,
                rotatable = false,
                passability = source.passability,
                pathCost = 0,
                fillPercent = 0.05f,
                altitudeLayer = AltitudeLayer.Building,
                canGenerateDefaultDesignator = true,
                designationCategory = category,
                drawStyleCategory = DefDatabase<DrawStyleCategoryDef>.GetNamedSilentFail("FilledRectangle"),
                neverMultiSelect = false,
                leaveResourcesWhenKilled = false,
                minifiedDef = DefDatabase<ThingDef>.GetNamedSilentFail("MinifiedThing"),
                tickerType = TickerType.Never,
                size = source.size,
                building = new BuildingProperties
                {
                    claimable = true,
                    alwaysDeconstructible = true,
                    isInert = true,
                    ai_neverTrashThis = true,
                    isTargetable = false,
                    blueprintGraphicData = MakeBlueprintGraphicData(source)
                },
                statBases = new List<StatModifier>(),
                costList = new List<ThingDefCountClass>(),
                comps = new List<CompProperties>(),
                generated = true,
                modContentPack = LoadedModManager.GetMod<DecorativePlantsMod>()?.Content,
                uiIconScale = source.uiIconScale > 0f ? source.uiIconScale : 1f,
                uiIconPath = source.uiIconPath,
                uiIcon = source.uiIcon,
                uiIconMaterial = source.uiIconMaterial,
                uiIconColor = source.uiIconColor,
                uiIconColorTwo = source.uiIconColorTwo
            };

            AddStat(def, StatDefOf.MaxHitPoints, SafeStat(source, StatDefOf.MaxHitPoints, 20f, round: true));
            AddStat(def, StatDefOf.Flammability, settings.useOriginalFlammability ? SafeStat(source, StatDefOf.Flammability, 1f) : 1f);
            AddStat(def, StatDefOf.Beauty, ResolveBeauty(source, settings));
            AddStat(def, StatDefOf.WorkToBuild, Mathf.Max(1, settings.workToBuild));

            if (woodLog != null)
            {
                def.costList.Add(new ThingDefCountClass(woodLog, 1));
            }

            if (settings.copyVanillaGlow)
            {
                CompProperties_Glower glower = source.GetCompProperties<CompProperties_Glower>();
                if (glower != null)
                {
                    def.comps.Add(CopyGlower(glower));
                }
            }

            RegisterRenderInfo(source, def);
            return def;
        }

        private static IEnumerable<ThingDef> MakeConstructionDefs(ThingDef def, bool hotReload)
        {
            MethodInfo newBlueprint = typeof(ThingDefGenerator_Buildings).GetMethod("NewBlueprintDef_Thing", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo newFrame = typeof(ThingDefGenerator_Buildings).GetMethod("NewFrameDef_Thing", BindingFlags.NonPublic | BindingFlags.Static);

            if (newBlueprint == null || newFrame == null)
            {
                Log.Warning("[Decorative Plants] Could not find RimWorld blueprint/frame generators; generated plants may not be buildable.");
                yield break;
            }

            ThingDef blueprint = (ThingDef)newBlueprint.Invoke(null, new object[] { def, false, null, hotReload });
            if (blueprint != null)
            {
                blueprint.generated = true;
                blueprint.modContentPack = def.modContentPack;
                yield return blueprint;
            }

            ThingDef frame = (ThingDef)newFrame.Invoke(null, new object[] { def, hotReload });
            if (frame != null)
            {
                frame.generated = true;
                frame.modContentPack = def.modContentPack;
                yield return frame;
            }
        }

        private static GraphicData CopyGraphicData(ThingDef source)
        {
            GraphicData sourceGraphic = source.graphicData;
            GraphicData copy = new GraphicData();
            copy.CopyFrom(sourceGraphic);
            copy.name = null;
            copy.allowAtlasing = false;
            ApplyMaturePlantVisualSize(source, copy);
            return copy;
        }

        private static GraphicData MakeBlueprintGraphicData(ThingDef source)
        {
            GraphicData copy = CopyGraphicData(source);
            copy.shaderType = ShaderTypeDefOf.Transparent;
            copy.color = new Color(1f, 1f, 1f, 0.45f);
            copy.colorTwo = Color.white;
            if (source.plant != null && !source.plant.visualSizeRange.IsZeros)
            {
                bool cluster = source.plant.maxMeshCount > 1 && source.plant.visualSizeRange.Average < 0.75f;
                if (!cluster && source.plant.visualSizeRange.max > 1.05f)
                {
                    float offset = Mathf.Max(0f, (copy.drawSize.y - 1f) * 0.5f);
                    copy.drawOffset += new Vector3(0f, 0f, offset);
                }
            }

            copy.shadowData = null;
            return copy;
        }

        private static void ApplyMaturePlantVisualSize(ThingDef source, GraphicData copy)
        {
            if (source.plant == null || source.plant.visualSizeRange.IsZeros)
            {
                return;
            }

            bool cluster = source.plant.maxMeshCount > 1 && source.plant.visualSizeRange.Average < 0.75f;
            if (cluster)
            {
                return;
            }

            float visualScale = source.plant.visualSizeRange.max;
            if (visualScale <= 0f || Mathf.Approximately(visualScale, 1f))
            {
                return;
            }

            Vector2 baseDrawSize = copy.drawSize;
            if (baseDrawSize == Vector2.zero)
            {
                baseDrawSize = Vector2.one;
            }

            copy.drawSize = baseDrawSize * visualScale;
        }

        private static void DisableAtlasingOnGraphicTree(Graphic graphic)
        {
            if (graphic == null)
            {
                return;
            }

            DisableAtlasingOnGraphicTree(graphic, new HashSet<Graphic>());
        }

        private static void DisableAtlasingOnGraphicTree(Graphic graphic, HashSet<Graphic> visited)
        {
            if (graphic == null || !visited.Add(graphic))
            {
                return;
            }

            if (graphic.data != null)
            {
                graphic.data.allowAtlasing = false;
            }

            for (Type type = graphic.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (typeof(Graphic).IsAssignableFrom(field.FieldType))
                    {
                        DisableAtlasingOnGraphicTree(field.GetValue(graphic) as Graphic, visited);
                    }
                    else if (field.FieldType.IsArray && typeof(Graphic).IsAssignableFrom(field.FieldType.GetElementType()))
                    {
                        if (field.GetValue(graphic) is Array graphics)
                        {
                            foreach (object item in graphics)
                            {
                                DisableAtlasingOnGraphicTree(item as Graphic, visited);
                            }
                        }
                    }
                }
            }
        }

        private static void RegisterRenderInfo(ThingDef source, ThingDef generated)
        {
            if (source.plant == null)
            {
                return;
            }

            bool hasVisualRange = !source.plant.visualSizeRange.IsZeros;
            DecorativePlantRenderData.Register(generated.defName, new DecorativePlantRenderInfo
            {
                cluster = hasVisualRange && source.plant.maxMeshCount > 1 && source.plant.visualSizeRange.Average < 0.75f,
                anchorBottom = hasVisualRange && !(source.plant.maxMeshCount > 1 && source.plant.visualSizeRange.Average < 0.75f) && source.plant.visualSizeRange.max > 1.05f,
                meshCount = source.plant.maxMeshCount,
                minVisualSize = hasVisualRange ? source.plant.visualSizeRange.min : 1f,
                maxVisualSize = hasVisualRange ? source.plant.visualSizeRange.max : 1f
            });
        }

        private static void ResolveGhostGraphic(ThingDef def)
        {
            if (!DecorativePlantRenderData.TryGet(def.defName, out DecorativePlantRenderInfo info))
            {
                return;
            }

            info.ghostGraphic = def.building?.blueprintGraphicData?.Graphic;
        }

        private static CompProperties_Glower CopyGlower(CompProperties_Glower source)
        {
            return new CompProperties_Glower
            {
                compClass = typeof(CompGlower),
                glowRadius = source.glowRadius,
                overlightRadius = source.overlightRadius,
                glowColor = source.glowColor,
                colorPickerEnabled = source.colorPickerEnabled,
                darklightToggle = source.darklightToggle,
                overrideIsCavePlant = source.overrideIsCavePlant
            };
        }

        private static float ResolveBeauty(ThingDef source, DecorativePlantsSettings settings)
        {
            float beauty = settings.useOriginalBeauty ? SafeStat(source, StatDefOf.Beauty, 1f) : 1f;
            if (Mathf.Approximately(beauty, 0f))
            {
                beauty = 1f;
            }

            return Mathf.Max(0f, beauty * Mathf.Max(0f, settings.beautyMultiplier));
        }

        private static float SafeStat(ThingDef source, StatDef stat, float fallback, bool round = false)
        {
            try
            {
                float value = source.GetStatValueAbstract(stat);
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f && stat == StatDefOf.MaxHitPoints)
                {
                    return fallback;
                }

                return round ? Mathf.Round(value) : value;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static void AddStat(ThingDef def, StatDef stat, float value)
        {
            def.statBases.Add(new StatModifier
            {
                stat = stat,
                value = value
            });
        }

        private static bool IsSowable(ThingDef source)
        {
            return source.plant.sowTags != null && source.plant.sowTags.Count > 0;
        }

        private static bool IsTree(ThingDef source)
        {
            return source.plant.forceIsTree || source.plant.treeCategory != TreeCategory.None;
        }

        private static string SanitizeDefName(string defName)
        {
            StringBuilder builder = new StringBuilder(defName.Length);
            for (int i = 0; i < defName.Length; i++)
            {
                char c = defName[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            string sanitized = builder.ToString().Trim('_');
            return string.IsNullOrEmpty(sanitized) ? "UnnamedPlant" : sanitized;
        }
    }
}

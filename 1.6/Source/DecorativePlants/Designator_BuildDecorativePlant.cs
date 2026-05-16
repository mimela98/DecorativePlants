using RimWorld;
using UnityEngine;
using Verse;

namespace DecorativePlants
{
    public class Designator_BuildDecorativePlant : Designator_Build
    {
        private readonly ThingDef decorativeDef;

        public Designator_BuildDecorativePlant(ThingDef entDef) : base(entDef)
        {
            decorativeDef = entDef;
        }

        public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
        {
            ThingStyleDef styleDef = ThingStyleDefForPreview;
            Color color = parms.lowLight ? Command.LowLightIconColor : IconDrawColor;
            Widgets.DefIcon(rect, PlacingDef, StuffDef, 0.55f, styleDef, drawPlaceholder: false, color, overrideMaterial);
        }

        protected override void DrawGhost(Color ghostCol)
        {
            Graphic ghostGraphic = null;
            if (DecorativePlantRenderData.TryGet(decorativeDef.defName, out DecorativePlantRenderInfo info))
            {
                ghostGraphic = info.ghostGraphic;
            }

            GhostDrawer.DrawGhostThing(UI.MouseCell(), placingRot, decorativeDef, ghostGraphic, ghostCol, AltitudeLayer.Blueprint, null, drawPlaceWorkers: true, StuffDef);
        }
    }
}

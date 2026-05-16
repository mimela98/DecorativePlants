using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DecorativePlants
{
    public sealed class DecorativePlantRenderInfo
    {
        public bool cluster;
        public bool anchorBottom;
        public int meshCount;
        public float minVisualSize;
        public float maxVisualSize;
        public Graphic ghostGraphic;
    }

    public static class DecorativePlantRenderData
    {
        private static readonly Dictionary<string, DecorativePlantRenderInfo> dataByDefName = new Dictionary<string, DecorativePlantRenderInfo>();

        public static void Register(string defName, DecorativePlantRenderInfo info)
        {
            dataByDefName[defName] = info;
        }

        public static bool TryGet(string defName, out DecorativePlantRenderInfo info)
        {
            return dataByDefName.TryGetValue(defName, out info);
        }
    }
}

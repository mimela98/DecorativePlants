using System;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace DecorativePlants
{
    public class Building_DecorativePlant : Building
    {
        public override void Print(SectionLayer layer)
        {
            if (!DecorativePlantRenderData.TryGet(def.defName, out DecorativePlantRenderInfo info))
            {
                base.Print(layer);
                return;
            }

            Graphic graphic = Graphic;
            if (graphic == null)
            {
                return;
            }

            if (!info.cluster)
            {
                if (info.anchorBottom)
                {
                    PrintBottomAnchored(layer, graphic);
                    PrintComps(layer);
                    return;
                }

                base.Print(layer);
                return;
            }

            int maxCount = Mathf.Max(1, info.meshCount);
            int gridSize = GridSizeForMeshCount(maxCount);
            if (gridSize <= 0)
            {
                base.Print(layer);
                return;
            }

            float cellSize = 1f / gridSize;
            Rand.PushState();
            Rand.Seed = ClusterSeed();
            int count = Mathf.Clamp(Mathf.RoundToInt(maxCount * Rand.Range(0.85f, 1f)), 1, maxCount);
            int[] indices = ShuffledIndices(maxCount);
            Vector3 clusterDrift = Gen.RandomHorizontalVector(cellSize * 0.18f);
            for (int i = 0; i < count; i++)
            {
                Material material = MaterialForClusterPiece(graphic);
                if (material == null)
                {
                    continue;
                }

                int index = indices[i];
                int row = index / gridSize;
                int col = index % gridSize;
                float pieceSize = Mathf.Max(0.05f, info.maxVisualSize * Rand.Range(0.92f, 1.05f));
                Vector3 center = Position.ToVector3();
                center.y = def.Altitude;
                center.x += 0.5f * cellSize + row * cellSize;
                center.z += 0.5f * cellSize + col * cellSize;
                center += clusterDrift;
                center += Gen.RandomHorizontalVector(cellSize * 0.4f);
                center += graphic.DrawOffset(Rotation);

                PrintPlane(layer, center, Vector2.one * pieceSize, material, Rand.Bool);
            }
            Rand.PopState();

            PrintComps(layer);
        }

        private void PrintBottomAnchored(SectionLayer layer, Graphic graphic)
        {
            Material material = graphic.MatAt(Rotation, this);
            if (material == null)
            {
                return;
            }

            Vector2 size = graphic.drawSize;
            if (size == Vector2.zero)
            {
                size = Vector2.one;
            }

            Rand.PushState();
            Rand.Seed = Position.GetHashCode();
            Vector3 center = this.TrueCenter() + Gen.RandomHorizontalVector(0.05f) + graphic.DrawOffset(Rotation);
            Rand.PopState();

            float bottomZ = Position.z;
            if (center.z - size.y * 0.5f < bottomZ)
            {
                center.z = bottomZ + size.y * 0.5f;
            }

            PrintPlane(layer, center, size, material, false);
        }

        private void PrintComps(SectionLayer layer)
        {
            if (AllComps == null)
            {
                return;
            }

            for (int i = 0; i < AllComps.Count; i++)
            {
                AllComps[i].PostPrintOnto(layer);
            }
        }

        private int ClusterSeed()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 486187739 + Position.x;
                hash = hash * 486187739 + Position.z;
                hash = hash * 486187739 + def.shortHash;
                hash ^= (hash << 13);
                hash ^= (hash >> 17);
                hash ^= (hash << 5);
                return hash;
            }
        }

        private static int GridSizeForMeshCount(int meshCount)
        {
            switch (meshCount)
            {
                case 1:
                    return 1;
                case 4:
                    return 2;
                case 9:
                    return 3;
                case 16:
                    return 4;
                case 25:
                    return 5;
                default:
                    return -1;
            }
        }

        private int[] ShuffledIndices(int count)
        {
            int[] indices = new int[count];
            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            indices.Shuffle();
            return indices;
        }

        private Material MaterialForClusterPiece(Graphic graphic)
        {
            if (graphic is Graphic_Random random)
            {
                int index = Rand.Range(0, random.SubGraphicsCount);
                return random.SubGraphicAtIndex(index).MatSingle;
            }

            return graphic.MatAt(Rotation, this);
        }

        private static void PrintPlane(SectionLayer layer, Vector3 center, Vector2 size, Material material, bool flipUv)
        {
            Printer_Plane.PrintPlane(layer, center, size, material, 0f, flipUv, null, null, 0.1f, 0f);
        }
    }
}

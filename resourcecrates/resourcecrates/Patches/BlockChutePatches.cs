using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using resourcecrates.BlockEntities;
using resourcecrates.Util;

namespace resourcecrates.Patches
{
    public static class BlockChutePatches
    {
        public static void Apply(Harmony harmony)
        {
            DebugLogger.Log("BlockChutePatches.Apply START");

            harmony.PatchAll(typeof(BlockChutePatches).Assembly);

            DebugLogger.Log("BlockChutePatches.Apply END");
        }

        [HarmonyPatch]
        public static class HasConnectorPatch
        {
            public static MethodBase TargetMethod()
            {
                DebugLogger.Log("BlockChutePatches.HasConnectorPatch.TargetMethod START");

                var blockChuteType = AccessTools.TypeByName("Vintagestory.GameContent.BlockChute");
                if (blockChuteType == null)
                {
                    DebugLogger.Error("BlockChutePatches.HasConnectorPatch.TargetMethod | Could not find type Vintagestory.GameContent.BlockChute");
                    return null;
                }

                var method = AccessTools.Method(
                    blockChuteType,
                    "HasConnector",
                    new[] { typeof(IBlockAccessor), typeof(BlockPos), typeof(BlockFacing), typeof(BlockFacing).MakeByRefType() }
                );

                DebugLogger.Log($"BlockChutePatches.HasConnectorPatch.TargetMethod END -> found={method != null}");
                return method;
            }

            public static void Postfix(
                IBlockAccessor ba,
                BlockPos pos,
                BlockFacing face,
                ref BlockFacing vert,
                ref bool __result)
            {
                if (__result) return;
                if (ba == null || pos == null || face == null) return;

                if (ba.GetBlockEntity(pos) is not BlockEntityResourceCrate)
                {
                    return;
                }

                vert = null;
                __result = true;

                DebugLogger.Log(
                    $"BlockChutePatches.HasConnectorPatch.Postfix | forced true for resource crate at {pos}"
                );
            }
        }

        [HarmonyPatch]
        public static class CanStayPatch
        {
            public static MethodBase TargetMethod()
            {
                DebugLogger.Log("BlockChutePatches.CanStayPatch.TargetMethod START");

                var blockChuteType = AccessTools.TypeByName("Vintagestory.GameContent.BlockChute");
                if (blockChuteType == null)
                {
                    DebugLogger.Error("BlockChutePatches.CanStayPatch.TargetMethod | Could not find type Vintagestory.GameContent.BlockChute");
                    return null;
                }

                var method = AccessTools.Method(
                    blockChuteType,
                    "CanStay",
                    new[] { typeof(IWorldAccessor), typeof(BlockPos) }
                );

                DebugLogger.Log($"BlockChutePatches.CanStayPatch.TargetMethod END -> found={method != null}");
                return method;
            }

            public static void Postfix(
                IWorldAccessor world,
                BlockPos pos,
                ref bool __result)
            {
                if (__result) return;
                if (world?.BlockAccessor == null || pos == null) return;

                var ba = world.BlockAccessor;
                var chuteBlock = ba.GetBlock(pos);
                if (chuteBlock == null) return;

                var pullFacesProp = AccessTools.Property(chuteBlock.GetType(), "PullFaces");
                var pushFacesProp = AccessTools.Property(chuteBlock.GetType(), "PushFaces");

                string[] pullFaces = pullFacesProp?.GetValue(chuteBlock) as string[];
                string[] pushFaces = pushFacesProp?.GetValue(chuteBlock) as string[];

                BlockPos npos = new BlockPos(pos.dimension);

                if (pullFaces != null)
                {
                    foreach (string val in pullFaces)
                    {
                        BlockFacing facing = BlockFacing.FromCode(val);
                        if (facing == null) continue;

                        npos.Set(pos).Add(facing);

                        if (ba.GetBlockEntity(npos) is BlockEntityResourceCrate)
                        {
                            __result = true;
                            DebugLogger.Log(
                                $"BlockChutePatches.CanStayPatch.Postfix | forced true via pull face {facing.Code} at {npos}"
                            );
                            return;
                        }
                    }
                }

                if (pushFaces != null)
                {
                    foreach (string val in pushFaces)
                    {
                        BlockFacing facing = BlockFacing.FromCode(val);
                        if (facing == null) continue;

                        npos.Set(pos).Add(facing);

                        if (ba.GetBlockEntity(npos) is BlockEntityResourceCrate)
                        {
                            __result = true;
                            DebugLogger.Log(
                                $"BlockChutePatches.CanStayPatch.Postfix | forced true via push face {facing.Code} at {npos}"
                            );
                            return;
                        }
                    }
                }
            }
        }
    }
}
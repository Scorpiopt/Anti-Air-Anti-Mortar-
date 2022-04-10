using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace AntiAirAntiMortar
{
    public class CompProperties_AAProjectileInterceptor : CompProperties_ProjectileInterceptor
    {
        public CompProperties_AAProjectileInterceptor()
        {
            this.compClass = typeof(CompAAProjectileInterceptor);
        }
    }

    public enum InterceptMode
    {
        DropPods,
        Air
    }
    [StaticConstructorOnStartup]
    public class CompAAProjectileInterceptor : CompProjectileInterceptor
    {
        public InterceptMode interceptMode;
        public new CompProperties_AAProjectileInterceptor Props => base.props as CompProperties_AAProjectileInterceptor;

        public CompPowerTrader compPower;

        public CompRefuelable compRefuelable;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compPower = this.parent.TryGetComp<CompPowerTrader>();
            compRefuelable = this.parent.TryGetComp<CompRefuelable>();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
            {
                yield return g;
            }
            if (this.parent.Faction == Faction.OfPlayer)
            {
                var mode = interceptMode == InterceptMode.DropPods ? "DropPods".Translate() : "Missiles".Translate();
                yield return new Command_Action
                {
                    defaultLabel = "InterceptMode".Translate(mode),
                    icon = ContentFinder<Texture2D>.Get("UI/SwitchMode"),
                    action = delegate
                    {
                        if (interceptMode == InterceptMode.Air)
                        {
                            interceptMode = InterceptMode.DropPods;
                        }
                        else
                        {
                            interceptMode = InterceptMode.Air;
                        }
                    }
                };
            }
        }


        public override void CompTick()
        {
            base.CompTick();
            if (this.interceptMode == InterceptMode.DropPods && this.Active)
            {
                foreach (var target in this.parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveDropPod)
                    .Where(t => t is DropPodIncoming pod && (pod.Contents.innerContainer.OfType<Pawn>().FirstOrDefault()?.HostileTo(parent.Faction) ?? false)
                    && Mathf.Abs((t.DrawPos - this.parent.DrawPos).magnitude) <= Props.radius).ToList())
                {
                    if (target != null)
                    {
                        GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel), target.DrawPos.ToIntVec3(), this.parent.Map, 
                            ThingPlaceMode.Near);
                        target.Destroy();
                        this.compRefuelable.ConsumeFuel(1f);
                    }
                }
            }

        }
        [HarmonyPatch(typeof(CompProjectileInterceptor), "Active", MethodType.Getter)]
        public class Active_Patch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(CompProjectileInterceptor __instance)
            {
                if (__instance is CompAAProjectileInterceptor comp)
                {
                    if (!comp.compPower.PowerOn || !comp.compRefuelable.HasFuel)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (this.compRefuelable.HasFuel && this.compPower.PowerOn)
            {
                if (this.interceptMode == InterceptMode.Air)
                {
                    stringBuilder.AppendLine("InterceptsProjectiles".Translate("InterceptsProjectiles_AerialProjectiles".Translate()));
                    stringBuilder.AppendLine("InterceptsProjectiles".Translate("InterceptsProjectiles_GroundProjectiles".Translate()));
                }
                else
                {
                    stringBuilder.AppendLine("InterceptsDropPods".Translate());
                }
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }

        [HarmonyPatch(typeof(CompProjectileInterceptor), "InterceptsProjectile")]
        public class InterceptsProjectile_Patch
        {
            private static void Postfix(ref bool __result, CompProjectileInterceptor __instance, 
                CompProperties_ProjectileInterceptor props, Projectile projectile)
            {
                if (__instance is CompAAProjectileInterceptor comp)
                {
                    if (comp.interceptMode == InterceptMode.DropPods)
                    {
                        __result = false;
                    }
                    else if (projectile is Projectile_Explosive)
                    {
                        __result = true;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CompProjectileInterceptor), "CheckIntercept")]
        public class CheckIntercept_Patch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(CompProjectileInterceptor __instance)
            {
                if (__instance is CompAAProjectileInterceptor comp)
                {
                    if (!comp.compPower.PowerOn || !comp.compRefuelable.HasFuel)
                    {
                        return false;
                    }
                }

                return true;
            }
            private static void Postfix(bool __result, CompProjectileInterceptor __instance, Projectile projectile, Vector3 lastExactPos, Vector3 newExactPos)
            {
                if (__result && __instance is CompAAProjectileInterceptor comp && projectile != null)
                {
                    comp.compRefuelable.ConsumeFuel(1f);
                }
            }
        }

        [HarmonyPatch(typeof(ThingListGroupHelper), "Includes")]
        public static class Includes_Patch
        {
            public static void Postfix(ThingRequestGroup group, ThingDef def, ref bool __result)
            {
                if (group == ThingRequestGroup.ProjectileInterceptor && __result is false)
                {
                    __result = def.HasComp(typeof(CompAAProjectileInterceptor));
                }
            }
        }


        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref interceptMode, "interceptMode");
        }
    }
}

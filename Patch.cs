// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.AI.BT;
using PhantomBrigade.Combat.Systems;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	[HarmonyPatch]
	static partial class Patch
	{
		[HarmonyPatch(typeof(InputUILinkDashPainting), "Execute", new System.Type[] { typeof(List<InputEntity>) })]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Iuildp_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
			=> UILinkPaintingPatch.Transpiler(instructions, generator, OpCodes.Blt);

		[HarmonyPatch(typeof(InputUILinkMeleePainting), "Redraw")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Iuilmp_RedrawTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
			=> UILinkPaintingPatch.Transpiler(instructions, generator, OpCodes.Blt_S);

		[HarmonyPatch(typeof(BTDataUtility), nameof(BTDataUtility.LoadBehavior))]
		[HarmonyPrefix]
		static void Btdu_LoadBehaviorPrefix(string p_name)
		{
			if (LoggingToggles.BTInjection)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) loading BehaviorTree | behavior: {2}",
					ModLink.modIndex,
					ModLink.modID,
					p_name);
			}
		}
	}
}

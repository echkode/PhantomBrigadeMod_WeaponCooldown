// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Data;
using PhantomBrigade.Input.Systems;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(InputUILinkPathPainting), "PathCallback")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Iuilpp_PathCallbackTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// If action start time is past max placement time, show late placement warning toast.

			var cm = new CodeMatcher(instructions, generator);
			var turnStartTimeLocal = generator.DeclareLocal(typeof(float));
			var maxMethodInfo = AccessTools.DeclaredMethod(typeof(Mathf), nameof(Mathf.Max), new System.Type[] { typeof(float), typeof(float) });
			var getSimMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(DataShortcuts), nameof(DataShortcuts.sim));
			var maxMatch = new CodeMatch(OpCodes.Call, maxMethodInfo);
			var storeTurnStartTime = new CodeInstruction(OpCodes.Stloc_S, turnStartTimeLocal);
			var loadTurnStartTime = new CodeInstruction(OpCodes.Ldloc_S, turnStartTimeLocal);
			var getSim = new CodeInstruction(OpCodes.Call, getSimMethodInfo);
			var loadMaxPlacementTime = CodeInstruction.LoadField(typeof(DataContainerSettingsSimulation), nameof(DataContainerSettingsSimulation.maxActionTimePlacement));
			var add = new CodeInstruction(OpCodes.Add);
			var showToast = CodeInstruction.Call(typeof(UILinkPaintingPatch), nameof(UILinkPaintingPatch.ShowWarningLate));

			cm.MatchStartForward(maxMatch)
				.Advance(-1)
				.InsertAndAdvance(storeTurnStartTime)
				.InsertAndAdvance(loadTurnStartTime)
				.Advance(2);
			var loadActionStartTime = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);
			cm.Advance(1)
				.InsertAndAdvance(loadActionStartTime)
				.InsertAndAdvance(loadTurnStartTime)
				.InsertAndAdvance(getSim)
				.InsertAndAdvance(loadMaxPlacementTime)
				.InsertAndAdvance(add);
			cm.CreateLabel(out var skipToastLabel);
			var skipToast = new CodeInstruction(OpCodes.Ble_Un_S, skipToastLabel);
			cm.InsertAndAdvance(skipToast)
				.InsertAndAdvance(showToast);

			return cm.InstructionEnumeration();
		}
	}
}

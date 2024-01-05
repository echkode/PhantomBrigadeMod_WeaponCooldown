// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(CombatUtilities), nameof(CombatUtilities.ClampTimeInCurrentTurn))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Cu_ClampTimeInCurrentTurnTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var cm = new CodeMatcher(instructions, generator);
			var turnLengthLocal = generator.DeclareLocal(typeof(float));
			var convertMatch = new CodeMatch(OpCodes.Conv_R4);
			var dupeInstruction = new CodeInstruction(OpCodes.Dup);
			var storeLocInstruction = new CodeInstruction(OpCodes.Stloc, turnLengthLocal.LocalIndex);
			var loadConstantMatch = new CodeMatch(OpCodes.Ldc_R4);

			// Store turn length in a local variable.
			cm.MatchEndForward(convertMatch)
				.Advance(1)
				.MatchEndForward(convertMatch)
				.Advance(1);
			cm.InsertAndAdvance(dupeInstruction);
			cm.InsertAndAdvance(storeLocInstruction);

			// Replace hard-coded constant with turn length from local variable.
			cm.MatchStartForward(loadConstantMatch);
			cm.Opcode = OpCodes.Ldloc;
			cm.Operand = turnLengthLocal.LocalIndex;

			return cm.InstructionEnumeration();
		}
	}
}

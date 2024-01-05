using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.AI.BT;
using PBCombatAIBindingSystem = PhantomBrigade.AI.Systems.CombatAIBindingSystem;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(PBCombatAIBindingSystem), "Execute", new System.Type[] { typeof(List<CombatEntity>) })]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Caibs_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Run loaded behavior tree through routine to add weapon ready checks.

			var cm = new CodeMatcher(instructions, generator);
			var hasAIBehaviorKeyMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatEntity), nameof(CombatEntity.hasAIBehaviorKey));
			var loadBehaviorMethodInfo = AccessTools.DeclaredMethod(typeof(BTDataUtility), nameof(BTDataUtility.LoadBehavior));
			var replaceBehaviorKeyMethodInfo = AccessTools.DeclaredMethod(typeof(CombatEntity), nameof(CombatEntity.ReplaceAIBehaviorKey));
			var hasAIBehaviorKeyMatch = new CodeMatch(OpCodes.Callvirt, hasAIBehaviorKeyMethodInfo);
			var loadBehaviorMatch = new CodeMatch(OpCodes.Call, loadBehaviorMethodInfo);
			var addReadyCheck = CodeInstruction.Call(typeof(CombatAIBindingSystem), nameof(CombatAIBindingSystem.AddCheckWeaponReady));
			var replaceBehaviorKey = new CodeInstruction(OpCodes.Callvirt, replaceBehaviorKeyMethodInfo);

			cm.MatchStartForward(hasAIBehaviorKeyMatch)
				.Advance(-1);
			var hasBehaviorKeyInstructions = cm.Instructions(2);
			var loadCombatEntity = hasBehaviorKeyInstructions[0].Clone();

			cm.MatchStartForward(loadBehaviorMatch)
				.Advance(-1);
			var loadName = cm.Instruction.Clone();

			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.CreateLabel(out var skipNameLabel);
			var skipName = new CodeInstruction(OpCodes.Brtrue_S, skipNameLabel);

			cm.Insert(hasBehaviorKeyInstructions)
				.AddLabels(labels)
				.Advance(hasBehaviorKeyInstructions.Count)
				.InsertAndAdvance(skipName)
				.InsertAndAdvance(loadCombatEntity)
				.InsertAndAdvance(loadName)
				.InsertAndAdvance(replaceBehaviorKey);

			cm.Advance(2);
			var storeBehaviorTree = cm.Instruction.Clone();
			cm.Advance(1);
			var loadBehaviorTree = cm.Instruction.Clone();
			cm.Advance(2)
				.InsertAndAdvance(loadName)
				.InsertAndAdvance(loadBehaviorTree)
				.InsertAndAdvance(addReadyCheck)
				.InsertAndAdvance(storeBehaviorTree);

			return cm.InstructionEnumeration();
		}
	}
}

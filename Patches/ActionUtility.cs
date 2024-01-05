using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.Data;
using PBActionUtility = PhantomBrigade.ActionUtility;
using PBDataHelperAction = PhantomBrigade.Data.DataHelperAction;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(PBActionUtility), "GetLastActionTime")]
		[HarmonyPrefix]
		static void Au_GetLastActionTimePrefix(bool primaryOnly)
		{
			if (primaryOnly)
			{
				return;
			}
			Debug.LogErrorFormat(
				"Mod {0} ({1}) ActionUtility.GetLastActionTime() shouldn't be called with primaryOnly = false",
				ModLink.modIndex,
				ModLink.modID);
		}

		[HarmonyPatch(typeof(PBActionUtility), nameof(PBActionUtility.CrashEntity))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Au_CrashEntityTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Don't create crash action if the unit is wrecked or concussed.

			var cm = new CodeMatcher(instructions, generator);
			var getLinkedEntityMethodInfo = AccessTools.DeclaredMethod(
				typeof(IDUtility),
				nameof(IDUtility.GetLinkedPersistentEntity),
				new System.Type[] { typeof(CombatEntity) });
			var instantiateActionMethodInfo = AccessTools.DeclaredMethod(
				typeof(PBDataHelperAction),
				nameof(PBDataHelperAction.InstantiateAction),
				new System.Type[]
				{
					typeof(CombatEntity),
					typeof(string),
					typeof(float),
					typeof(bool).MakeByRefType(),
					typeof(bool),
				});
			var isConcussedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatEntity), nameof(CombatEntity.isConcussed));
			var isWreckedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(PersistentEntity), nameof(PersistentEntity.isWrecked));
			var getLinkedEntityMatch = new CodeMatch(OpCodes.Call, getLinkedEntityMethodInfo);
			var instantiateActionMatch = new CodeMatch(OpCodes.Call, instantiateActionMethodInfo);
			var loadCombatUnitMatch = new CodeMatch(OpCodes.Ldarg_0);
			var isConcussed = new CodeInstruction(OpCodes.Callvirt, isConcussedMethodInfo);
			var isWrecked = new CodeInstruction(OpCodes.Callvirt, isWreckedMethodInfo);

			cm.Start();
			var loadCombatEntity = cm.Instruction.Clone();

			cm.MatchEndForward(getLinkedEntityMatch)
				.Advance(2);
			var loadUnitEntity = cm.Instruction.Clone();

			cm.MatchEndForward(instantiateActionMatch)
				.Advance(2);
			cm.CreateLabel(out var skipInstantiateLabel);
			var skipInstantiate = new CodeInstruction(OpCodes.Brtrue_S, skipInstantiateLabel);

			cm.Advance(-1)
				.MatchStartBackwards(loadCombatUnitMatch);
			cm.CreateLabel(out var skipWreckCheckLabel);
			var skipWreckCheck = new CodeInstruction(OpCodes.Brfalse_S, skipWreckCheckLabel);

			cm.InsertAndAdvance(loadCombatEntity)
				.InsertAndAdvance(isConcussed)
				.InsertAndAdvance(skipInstantiate)
				.InsertAndAdvance(loadUnitEntity)
				.InsertAndAdvance(skipWreckCheck)
				.InsertAndAdvance(loadUnitEntity)
				.InsertAndAdvance(isWrecked)
				.InsertAndAdvance(skipInstantiate);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBActionUtility), nameof(PBActionUtility.CrashEntity))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Au_CrashEntityTranspiler2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// CrashEntity() calls DestroyActionsFromTime() which removes all actions after the start of the crash.
			// Activation lockout represents a cooling off period for a weapon and whether a unit crashes or not
			// shouldn't change the time it takes for the weapon to cool off.
			//
			// Consequently, patching in a new routine that will exempt actions with activation lockout from being removed
			// if the lockout period extends beyond the crash duration.
			//
			// CrashEntity() is also called when a unit is destroyed. In this case we want to remove all actions.
			//
			// Removing actions is done in two stages. The first pass is a direct replacement for DestroyActionsFromTime()
			// which either removes all actions if the unit is wrecked (destroyed) or will keep actions with activation lockouts.
			// This is because the duration of the crash isn't known at the time of the call.
			//
			// A crash is represented by an action that's created after DestroyActionsFromTime() is called. Patch in a call after
			// the crash action is created so that we can remove actions with activation lockouts that expire before the crash ends.

			var cm = new CodeMatcher(instructions, generator);
			var getLinkedEntityMethodInfo = AccessTools.DeclaredMethod(
				typeof(IDUtility),
				nameof(IDUtility.GetLinkedPersistentEntity),
				new System.Type[] { typeof(CombatEntity) });
			var destroyActionsFromTimeMethodInfo = AccessTools.DeclaredMethod(typeof(PBActionUtility), nameof(PBActionUtility.DestroyActionsFromTime));
			var isWreckedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(PersistentEntity), nameof(PersistentEntity.isWrecked));
			var getLinkedEntityMatch = new CodeMatch(OpCodes.Call, getLinkedEntityMethodInfo);
			var destroyActionsFromTimeMatch = new CodeMatch(OpCodes.Call, destroyActionsFromTimeMethodInfo);
			var popMatch = new CodeMatch(OpCodes.Pop);
			var callDestroyActions = CodeInstruction.Call(typeof(ActionUtility), nameof(ActionUtility.DestroyActionsFromTime));
			var callDisposeLockoutActions = CodeInstruction.Call(typeof(ActionUtility), nameof(ActionUtility.DisposeLockoutActionsPendingCrash));
			var isWrecked = new CodeInstruction(OpCodes.Callvirt, isWreckedMethodInfo);

			cm.Start();
			var loadCombatUnit = cm.Instruction.Clone();

			cm.MatchEndForward(getLinkedEntityMatch)
				.Advance(2);
			var loadUnitEntity = cm.Instruction.Clone();

			cm.MatchStartForward(destroyActionsFromTimeMatch)
				.SetInstructionAndAdvance(callDestroyActions)
				.MatchEndForward(popMatch)
				.Advance(1);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.CreateLabel(out var skipDisposeActions);
			var jumpFalse = new CodeInstruction(OpCodes.Brfalse_S, skipDisposeActions);
			var jumpTrue = new CodeInstruction(OpCodes.Brtrue_S, skipDisposeActions);

			cm.Insert(loadUnitEntity.Clone())
				.AddLabels(labels)
				.Advance(1)
				.InsertAndAdvance(jumpFalse)
				.InsertAndAdvance(loadUnitEntity)
				.InsertAndAdvance(isWrecked)
				.InsertAndAdvance(jumpTrue)
				.InsertAndAdvance(loadCombatUnit)
				.InsertAndAdvance(callDisposeLockoutActions);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBActionUtility), "CreatePathAction")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Au_CreatePathActionTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var cm = new CodeMatcher(instructions, generator);
			var lastActionTimeMethodInfo = AccessTools.DeclaredMethod(typeof(PBActionUtility), nameof(PBActionUtility.GetLastActionTime));
			var getSimMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(DataShortcuts), nameof(DataShortcuts.sim));
			var minMethodInfo = AccessTools.DeclaredMethod(typeof(Mathf), nameof(Mathf.Min), new System.Type[] { typeof(float), typeof(float) });
			var lastActionTimeMatch = new CodeMatch(OpCodes.Call, lastActionTimeMethodInfo);
			var load1Match = new CodeMatch(OpCodes.Ldc_I4_1);
			var minMatch = new CodeMatch(OpCodes.Call, minMethodInfo);
			var getSim = new CodeInstruction(OpCodes.Call, getSimMethodInfo);
			var loadMaxTimePlacement = CodeInstruction.LoadField(typeof(DataContainerSettingsSimulation), nameof(DataContainerSettingsSimulation.maxActionTimePlacement));
			var add = new CodeInstruction(OpCodes.Add);
			var turnEnd = cm.MatchEndForward(lastActionTimeMatch)
				.MatchStartForward(load1Match)
				.Advance(-2)
				.InstructionsInRange(cm.Pos, cm.Pos + 5);

			cm.Advance(2)
				.RemoveInstruction()  // OpCodes.Ldc_I4_1
				.RemoveInstruction()  // OpCodes.Add
				.Advance(2)
				.InsertAndAdvance(getSim)
				.InsertAndAdvance(loadMaxTimePlacement)
				.Insert(add)
				.MatchStartForward(minMatch)
				.Advance(-3)
				.RemoveInstruction();  // OpCodes.Ldloc_S 11
			foreach (var ci in turnEnd)
			{
				cm.InsertAndAdvance(ci);
			}

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBActionUtility), nameof(PBActionUtility.GetLastSecondaryTrackAction))]
		[HarmonyPrefix]
		static bool Au_GetLastSecondaryTrackActionPrefix(CombatEntity selectedEntity, ref float __result)
		{
			__result = ActionUtility.GetLastSecondaryTrackAction(selectedEntity);
			return false;
		}

	}
}

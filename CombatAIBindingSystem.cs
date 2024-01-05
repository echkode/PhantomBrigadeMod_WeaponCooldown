// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;

using HarmonyLib;

using PhantomBrigade.AI.BT;
using PhantomBrigade.AI.BT.Nodes;
using PhantomBrigade.AI.BT.Samples;
using PhantomBrigade.AI.Components;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	public static class CombatAIBindingSystem
	{
		public static BehaviorTree AddCheckWeaponReady(string name, BehaviorTree bt)
		{
			if (!patchableBehaviors.Contains(name))
			{
				return bt;
			}

			if (LoggingToggles.BTInjection)
			{
				nodeComments.Clear();
				injections.Clear();
			}

			var root = bt.RootNode;
			if (root is IBTNodeParent parent)
			{
				WalkNode(parent);
			}

			if (LoggingToggles.BTInjection && injections.Count != 0)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) BehaviorTree injected BTNodes: CheckWeaponReady condition | behavior: {2}\n  {3}",
					ModLink.modIndex,
					ModLink.modID,
					name,
					string.Join("\n  ", injections));
			}

			return bt;
		}

		static void WalkNode(IBTNodeParent parent)
		{
			var depth = nodeComments.Count;
			if (LoggingToggles.BTInjection)
			{
				var comment = "<node>";
				if (!string.IsNullOrWhiteSpace(((BTNode)parent).Comment))
				{
					comment = ((BTNode)parent).Comment;
				}
				nodeComments.Add(string.Format("{0} -- {1}", ((BTNode)parent).DebugText, comment));
				depth += 1;
			}

			for (var i = parent.NumChildren - 1; i >= 0 ; i -= 1)
			{
				var child = parent.GetChild(i);
				if (child is BTAction_UseEquipment useEquipment)
				{
					var traverseNode = new Traverse(useEquipment);
					var actionType = traverseNode.Field<AIActionType>("m_actionType").Value;
					if (!weaponActions.Contains(actionType))
					{
						continue;
					}

					var checkWeaponReady = new BTCondition_CheckWeaponReady(actionType, true);
					var traverseParent = new Traverse(parent);
					var children = traverseParent.Field<List<BTNode>>("m_children").Value;
					children.Insert(i, checkWeaponReady);
					if (LoggingToggles.BTInjection)
					{
						injections.Add(string.Format("index: {0} | node: {1}", i, string.Join("/", nodeComments)));
					}
					continue;
				}
				if (child is IBTNodeParent np)
				{
					WalkNode(np);
					if (LoggingToggles.BTInjection)
					{
						nodeComments.RemoveRange(depth, nodeComments.Count - depth);
					}
				}
			}
		}

		static readonly List<string> nodeComments = new List<string>();
		static readonly List<string> injections = new List<string>();

		static readonly HashSet<string> patchableBehaviors = new HashSet<string>()
		{
			nameof(DefensiveDestination),
			nameof(Flanker),
			nameof(StandoffAttack2),
		};

		static readonly HashSet<AIActionType> weaponActions = new HashSet<AIActionType>()
		{
			AIActionType.AttackMain,
			AIActionType.AttackSecondary,
		};
	}
}

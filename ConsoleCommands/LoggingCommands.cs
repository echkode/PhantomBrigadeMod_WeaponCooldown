﻿// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Reflection;

using HarmonyLib;

using QFSW.QC;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class ConsoleCommands
	{
		[ConsoleCommand("log", "act-lockout-stat", "Toggle logging access to activation lockout stat")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Console command registered through reflection")]
		static void ToggleActivationLockoutStat()
		{
			FlipLoggingToggle(nameof(LoggingToggles.ActivationLockoutStat));
		}

		[ConsoleCommand("log", "bt-injection", "Toggle logging injection of BTNodes into combat behavior trees")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Console command registered through reflection")]
		static void ToggleBTInjection()
		{
			FlipLoggingToggle(nameof(LoggingToggles.BTInjection));
		}

		[ConsoleCommand("log", "btnode-updates", "Toggle logging in OnUpdate() of custom BTNodes")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Console command registered through reflection")]
		static void ToggleBTUpdates()
		{
			FlipLoggingToggle(nameof(LoggingToggles.BTUpdates));
		}

		[ConsoleCommand("log", "action-widget-depth", "Toggle logging of algorithm to fix depth of action widgets on combat timeline")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Console command registered through reflection")]
		static void ToggleActionWidgetDepths()
		{
			FlipLoggingToggle(nameof(LoggingToggles.ActionWidgetDepths));
		}

		[ConsoleCommand("log", "action-widget-depth-verbose", "Toggle verbose logging of algorithm to fix depth of action widgets on combat timeline")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Console command registered through reflection")]
		static void ToggleActionWidgetDepthsVerbose()
		{
			FlipLoggingToggle(nameof(LoggingToggles.ActionWidgetDepthsVerbose));
		}

		[ConsoleCommand("log", "action-drag", "Toggle logging when dragging action widgets on combat timeline")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Console command registered through reflection")]
		static void ToggleActionDrag()
		{
			FlipLoggingToggle(nameof(LoggingToggles.ActionDrag));
		}

		[ConsoleCommand("log", "ai-invoke", "Toggle logging of AI behavior")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Console command registered through reflection")]
		static void ToggleAIBehaviorInvoke()
		{
			FlipLoggingToggle(nameof(LoggingToggles.AIBehaviorInvoke));
		}

		static void FlipLoggingToggle(string fieldName)
		{
			var fieldInfo = AccessTools.DeclaredField(typeof(LoggingToggles), fieldName);
			if (fieldInfo == null)
			{
				return;
			}

			var toggle = (bool)fieldInfo.GetValue(null);
			toggle = !toggle;
			fieldInfo.SetValue(null, toggle);

			var labelAttribute = fieldInfo.GetCustomAttribute<ConsoleOutputLabelAttribute>();
			var label = labelAttribute != null
				? labelAttribute.Label
				: fieldName;
			QuantumConsole.Instance.LogToConsole($"{label} logging: " + (toggle ? "on" : "off"));
		}
	}
}

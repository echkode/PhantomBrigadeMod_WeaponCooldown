using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using HarmonyLib;

using QFSW.QC;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	using CommandList = List<(string QCName, string Description, MethodInfo Method)>;

	static partial class ConsoleCommands
	{
		internal static void Register()
		{
			if (commandsRegistered)
			{
				return;
			}

			var registeredFunctions = new StringBuilder();
			var k = 0;

			foreach (var (qcName, desc, method) in Commands())
			{
				if (method == null)
				{
					Debug.LogWarningFormat(
						"Mod {0} ({1}) failed to find method for QC command: {2}",
						ModLink.modIndex,
						ModLink.modID,
						qcName);
					continue;
				}

				var functionName = $"{method.DeclaringType.Name}.{method.Name}";
				var commandName = commandPrefix + qcName;
				var commandInfo = new CommandAttribute(
					commandName,
					desc,
					MonoTargetType.Single);
				var commandData = new CommandData(method, commandInfo);
				if (!QuantumConsoleProcessor.TryAddCommand(commandData))
				{
					Debug.LogFormat(
						"Mod {0} ({1}) failed to register QC command: {2} --> {3}",
						ModLink.modIndex,
						ModLink.modID,
						qcName,
						functionName);
					continue;
				}
				registeredFunctions.AppendFormat("\n  {0} --> {1}", commandName, functionName);
				k += 1;
			}

			Debug.LogFormat("Mod {0} ({1}) loaded QC commands | count: {2}{3}",
				ModLink.modIndex,
				ModLink.modID,
				k,
				registeredFunctions);

			commandsRegistered = true;
		}

		static CommandList Commands() => AccessTools.GetDeclaredMethods(typeof(ConsoleCommands))
			.Where(mi => mi.GetCustomAttribute<ConsoleCommandAttribute>() != null)
			.Select(mi =>
			{
				var attr = mi.GetCustomAttribute<ConsoleCommandAttribute>();
				return ($"{attr.Prefix}.{attr.Name}", attr.Description, mi);
			})
			.OrderBy(x => x.Item1)
			.ToList();

		const string commandPrefix = "ek.";
		static bool commandsRegistered;
	}
}

namespace EchKode.PBMods.WeaponCooldown
{
	public static class LoggingToggles
	{
		[ConsoleOutputLabel("Activation lockout stat references")]
		internal static bool ActivationLockoutStat = false;

		[ConsoleOutputLabel("BehaviorTree node injection")]
		internal static bool BTInjection = false;

		[ConsoleOutputLabel("BehaviorTree updates")]
		internal static bool BTUpdates = false;

		[ConsoleOutputLabel("Action widget depths")]
		internal static bool ActionWidgetDepths = false;

		[ConsoleOutputLabel("Action widget depths (verbose)")]
		internal static bool ActionWidgetDepthsVerbose = false;

		[ConsoleOutputLabel("Action drag")]
		internal static bool ActionDrag = false;

		[ConsoleOutputLabel("AI behavior invoke")]
		public static bool AIBehaviorInvoke = false;
	}
}

using System;

// Mirrors the <Symbols> in VSCommandTable.vsct. Keep the two in step.
namespace UltimateStartPage
{
    internal sealed partial class PackageGuids
    {
        public const string PackageString = "74a93348-7740-4656-91c2-242c45d647c5";
        public static readonly Guid Package = new Guid(PackageString);

        public const string CommandSetString = "9040fe26-0285-4c97-b455-80d491327ae5";
        public static readonly Guid CommandSet = new Guid(CommandSetString);

        public const string ToolWindowString = "f67fba6a-b070-4b62-bbba-2d13705ef66a";

        /// <summary>Output window pane that the extension logs to.</summary>
        public static readonly Guid OutputPane = new Guid("b5c1f6e2-3a07-4f7d-9d53-2c8a1e6f4b90");
    }

    internal sealed partial class PackageIds
    {
        public const int FileMenuGroup = 0x1020;
        public const int ShowStartPage = 0x0100;
    }
}

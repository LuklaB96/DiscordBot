namespace DiscordBot.AssemblyHelpers
{
    internal class AssemblyData
    {
        public readonly string AssemblyVersion;
        public readonly string AssemblyName;
        public readonly object Plugin;

        public AssemblyData(string assemblyVersion,string assemblyName, object plugin) 
        {
            AssemblyVersion = assemblyVersion;
            AssemblyName = assemblyName;
            Plugin = plugin;
        }
    }
}

using PluginTest.Interfaces;

namespace DiscordBot.AssemblyHelpers
{
    internal class AssemblyData
    {
        public readonly string AssemblyVersion;
        public readonly string AssemblyName;
        public readonly ICommand Plugin;

        public AssemblyData(string assemblyVersion,string assemblyName, ICommand plugin) 
        {
            AssemblyVersion = assemblyVersion;
            AssemblyName = assemblyName;
            Plugin = plugin;
        }
    }
}

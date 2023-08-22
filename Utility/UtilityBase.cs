using DiscordBot.Managers;
using PluginTest.Interfaces;
using System;
using System.Threading.Tasks;

namespace DiscordBot.Utility
{
    public abstract class UtilityBase
    {
        public UtilityBase(ILogger logger, IDatabase database, AssemblyManager assemblyManager)
        {
            Logger = logger;
            Database = database;
            this.assemblyManager = assemblyManager;
        }
        protected ILogger Logger { get; }
        protected IDatabase Database { get; }
        protected AssemblyManager assemblyManager { get; }
    }
}

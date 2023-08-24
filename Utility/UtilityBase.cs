using DiscordBot.Managers;
using PluginTest.Interfaces;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using DiscordBot.Structures;

namespace DiscordBot.Utility
{
    public abstract class UtilityBase
    {
        protected Logger Logger { get; }
        protected Database Database { get; }
        protected AssemblyManager assemblyManager { get; }
        public UtilityBase(IServiceProvider serviceProvider, AssemblyManager assemblyManager)
        {
            Logger = serviceProvider.GetService<Logger>();
            Database = serviceProvider.GetService<Database>();
            this.assemblyManager = assemblyManager;
        }
        
    }
}

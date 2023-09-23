using DiscordBot.Managers;
using System;
using Microsoft.Extensions.DependencyInjection;
using DiscordBot.Structures;

namespace DiscordBot.Utility
{
    public abstract class UtilityBase
    {
        protected Logger Logger { get; }
        protected Database Database { get; }
        protected AssemblyManager AssemblyManager { get; }
        /// <summary>
        /// Utility base class that is mainly used for inheritance.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="assemblyManager"></param>
        public UtilityBase(IServiceProvider serviceProvider, AssemblyManager assemblyManager)
        {
            Logger = serviceProvider.GetService<Logger>();
            Database = serviceProvider.GetService<Database>();
            this.AssemblyManager = assemblyManager;
        }
        
    }
}

using Discord;
using DiscordBot.Managers;
using DiscordBot.Utility;
using PluginTest;
using PluginTest.Enums;
using PluginTest.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Threading.Tasks;

namespace DiscordBot.Structures
{
    /**
     * TO DO:
     * 
     * 
     */
    public class Database : IDatabase
    {
        private static readonly string connectionString = string.Intern(@"Data Source=bot.db");
        private readonly ILogger Logger;
        public Database() 
        {
            Logger = new Logger();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task Initalize()
        {
            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();

            await CreateTableAsync(conn, "plugin_properties", "assembly_name TEXT PRIMARY KEY,plugin_name TEXT, plugin_alias TEXT");
            await CreateTableAsync(conn, "guildsettings", "guild_id TEXT PRIMARY KEY, prefix TEXT");
            await CreateTableAsync(conn, "command_info", "command_name TEXT PRIMARY KEY, plugin_name TEXT, assembly_name TEXT");
            await CreateTableAsync(conn, "modal_info", "modal_name TEXT PRIMARY KEY, plugin_name TEXT, assembly_name TEXT");
            await CreateTableAsync(conn, "message_info", "message_id TEXT PRIMARY KEY, plugin_name TEXT");
            
            conn.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public async Task<int> UpdateQueryAsync(string query, List<KeyValuePair<string, string>> parameters = null, Config config = null)
        {
            string q = query;
            if (query.Contains("#") && config != null)
            {
                q = ParseQueryTableName(query, config.pluginName);
            }

            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();
            int result = 0;
            using (SQLiteCommand cmd = new SQLiteCommand(conn))
            {

                cmd.CommandText = q;
                if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);

                try
                {
                    result = await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Logger.Log("Database", $"Failed to update the data in database: {ex.Message}",LogLevel.Error);
                }

            }
            conn.Close();
            return result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public async Task<int> DeleteQueryAsync(string query, List<KeyValuePair<string, string>> parameters = null, Config config = null)
        {
            string q = query;
            if (query.Contains("#") && config != null)
            {
                q = ParseQueryTableName(query, config.pluginName);
            }

            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();
            int result = 0;
            using (SQLiteCommand cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText = q;

                if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                try
                {
                    result = await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Logger.Log("Database", $"Delete query failed: {ex.Message}", LogLevel.Error);
                }
            }
            conn.Close();
            return result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public async Task<int> InsertQueryAsync(string query, List<KeyValuePair<string, string>> parameters = null, Config config = null)
        {
            string q = query;
            if (query.Contains("#") && config != null)
            {
                q = ParseQueryTableName(query, config.pluginName);
            }
            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();
            int result = 0;
            using (SQLiteCommand cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText = q;

                if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);

                try
                {
                    result = await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Logger.Log("Database", $"Insert query failed: {ex.Message}", LogLevel.Error);
                }
            }
            conn.Close();
            return result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private async Task<List<string>> DataReaderAsync(SQLiteCommand cmd)
        {
            List<string> items = new List<string>();
            try
            {
                using SQLiteDataReader reader = cmd.ExecuteReader();
                while (await reader.ReadAsync())
                {
                    for (int i = 0; i < reader.FieldCount; i++) items.Add(reader.GetValue(i).ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Database", $"Failed to read data from the database: {ex.Message}", LogLevel.Error);
            }
            return items;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public async Task<List<string>> SelectQueryAsync(string query, List<KeyValuePair<string,string>> parameters = null, Config config = null)
        {
            string q = query;
            if (query.Contains("#") && config != null)
            {
                q = ParseQueryTableName(query, config.pluginName);
            }

            List<string> items;
            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();
            using (SQLiteCommand cmd = new SQLiteCommand(q, conn))
            {
                if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                    items = await DataReaderAsync(cmd);
            }
            conn.Close();
            return items;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="pluginName"></param>
        /// <param name="tableName"></param>
        /// <param name="columnProperties"></param>
        /// <returns></returns>
        public async Task CheckCreatePluginTable(string pluginName, string tableName, string columnProperties)
        {
            columnProperties = ParseQueryTableName(columnProperties, pluginName);
            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();
            using SQLiteCommand cmd = new SQLiteCommand(conn);
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + pluginName + "_" + tableName + " (" + columnProperties + ")";
            await cmd.ExecuteNonQueryAsync();
            conn.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="pluginName"></param>
        /// <returns></returns>
        private string ParseQueryTableName(string query, string pluginName)
        {
            return query.Replace("#", pluginName + "_");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public bool TableAlreadyExists(string tableName)
        {
            string query = "SELECT name FROM sqlite_master WHERE type='table' AND name='" + tableName + "';";
            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();
            SQLiteCommand command = new SQLiteCommand(query, conn);
            SQLiteDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                conn.Close();
                return true;
            }
            conn.Close();
            return false;
            
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="tableName"></param>
        /// <param name="columnProperties"></param>
        /// <returns></returns>
        private async Task CreateTableAsync(SQLiteConnection conn, string tableName, string columnProperties)
        {
            using SQLiteCommand cmd = new SQLiteCommand(conn);
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + tableName + " (" + columnProperties + ")";
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Logger.Log("Database",$"Error while creating a table: {ex.Message}",LogLevel.Error);
            }
        }
        //public async Task CreateTableAsync(SQLiteConnection conn, string tableName)
        //{
        //    using SQLiteCommand cmd = new SQLiteCommand(conn);
        //    switch (tableName)
        //    {
        //        case "giveaways":
        //            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + tableName + " (id TEXT PRIMARY KEY, guild_id TEXT, channel_id TEXT, message_id TEXT, message_owner TEXT, max_winners INTEGER, winner TEXT, entries INTEGER DEFAULT 0 NOT NULL, ends INTEGER DEFAULT 1 NOT NULL, ended INTEGER DEFAULT 0 NOT NULL, closed INTEGER DEFAULT 0 NOT NULL, created_at TEXT)";
        //            break;
        //        case "polls":
        //            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + tableName + " (id TEXT PRIMARY KEY, guild_id TEXT, channel_id TEXT, message_id TEXT, message_owner TEXT, bot_reactions TEXT, ends TEXT, closed INTEGER DEFAULT 0 NOT NULL, created_at TEXT)";
        //            break;
        //        case "autorole":
        //            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + tableName + " (guild_id TEXT PRIMARY KEY, roles TEXT)";
        //            break;
        //        case "reactionrole":
        //            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + tableName + " (id INTEGER PRIMARY KEY, guild_id TEXT, name TEXT, emotes TEXT, roles TEXT)";
        //            break;
        //        case "reactionrole_message":
        //            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + tableName + " (id INTEGER PRIMARY KEY, guild_id TEXT, channel_id TEXT, message_id TEXT, reactionrole_id TEXT, FOREIGN KEY (reactionrole_id) REFERENCES reactionrole(id))";
        //            break;
        //        case "guildsettings":
        //            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + tableName + " (guild_id TEXT PRIMARY KEY, prefix TEXT)";
        //            break;
        //        case "giveaway_users":
        //            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + tableName + " (id INTEGER PRIMARY KEY, giveaway_id TEXT, user_id TEXT)";
        //            break;
        //        case "suggestions":
        //            cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + tableName + " (id INTEGER PRIMARY KEY, guild_id TEXT, user_id TEXT, suggestion TEXT)";
        //            break;
        //        default:
        //            Console.WriteLine("Wrong table name");
        //            break;
        //    }

        //    await cmd.ExecuteNonQueryAsync();
        //}
    }
}

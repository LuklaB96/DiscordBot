using DiscordBot.Utility;
using DiscordPluginAPI;
using DiscordPluginAPI.Enums;
using DiscordPluginAPI.Helpers;
using DiscordPluginAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
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
        private static readonly string connectionString = @"Data Source=bot.db";
        private readonly ILogger Logger;
        public Database() 
        {
            Logger = new Logger();
        }
        /// <summary>
        /// Creates all missing tables in Database.
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
        /// Asynchronously executes an SQL query to update data in a SQLite database.
        /// </summary>
        /// <param name="query">The SQL query to be executed.</param>
        /// <param name="parameters">Optional query parameters to be used in the query.</param>
        /// <param name="config">Optional configuration for parsing the query based on the plugin.</param>
        /// <returns>An integer representing the number of rows affected by the query.</returns>
        public async Task<int> UpdateQueryAsync(string query, QueryParametersBuilder parameters = null, Config config = null)
        {
            string q = query;
            //If the plugin uses the bot's main database, the bot must parse information about the plugin in a given query.
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

                if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters.GetAll()) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);

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
        /// Asynchronously executes a series of SQL queries in a transaction to update data in a SQLite database.
        /// </summary>
        /// <param name="transactionBuilder">Optional builder for constructing the transaction queries.</param>
        /// <param name="queries">Optional dictionary of queries to execute.</param>
        /// <param name="config">Optional configuration for parsing queries based on the plugin.</param>
        /// <returns>An integer representing the total number of rows affected by the queries in the transaction.</returns>aram>
        /// <returns></returns>
        public async Task<int> UpdateTransactionQueryAsync(DatabaseTransactionBuilder transactionBuilder = null, Dictionary<string, List<KeyValuePair<string, string>>> queries = null, Config config = null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            IReadOnlyCollection<string> queryList = null;

            if (queries == null && transactionBuilder == null) return 0;
            if (queries != null) queryList = queries.Keys.ToList();
            if (transactionBuilder != null) queryList = transactionBuilder.GetQueries();

            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();
            int failed = 0, succeed = 0;

            using (var transaction = conn.BeginTransaction())
            {
                foreach (var query in queryList)
                {

                    try
                    {
                        var parameters = transactionBuilder.GetValue(query);
                        string q = query;
                        //If the plugin uses the bot's main database, the bot must parse information about the plugin in a given query.
                        if (query.Contains("#") && config != null)
                        {
                            q = ParseQueryTableName(query, config.pluginName);
                        }

                        using (var cmd = new SQLiteCommand(q, conn, transaction))
                        {
                            if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                            succeed += await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    catch (SQLiteException e)
                    {
                        if (e.ErrorCode == 19)
                        {
                            failed++;
                            continue;
                        }
                        transaction.Rollback();
                        Logger.Log("Database", $"Failed to update records in transaction, rolling back {succeed} queries, error: {e.Message}", LogLevel.Error);
                        break;
                    }
                }
                transaction.Commit();
                sw.Stop();
                Logger.Log("Database", $"Total records in update transaction: {succeed + failed}, failed: {failed}, succeed: {succeed}, operation time: {sw.ElapsedMilliseconds} ms", LogLevel.Info);
            }
            return succeed;
        }
        /// <summary>
        /// Asynchronously executes an SQL query to delete data from a SQLite database.
        /// </summary>
        /// <param name="query">The SQL query used for deleting data.</param>
        /// <param name="parameters">Optional query parameters to be used in the query.</param>
        /// <param name="config">Optional configuration for parsing the query based on the plugin.</param>
        /// <returns>An integer representing the number of rows affected by the delete query.</returns>
        public async Task<int> DeleteQueryAsync(string query, QueryParametersBuilder parameters = null, Config config = null)
        {
            string q = query;
            //If the plugin uses the bot's main database, the bot must parse information about the plugin in a given query.
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

                if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters.GetAll()) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
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
        /// Asynchronously executes a series of SQL queries in a transaction to delete data from a SQLite database.
        /// </summary>
        /// <param name="transactionBuilder">Optional builder for constructing the transaction queries.</param>
        /// <param name="queries">Optional dictionary of delete queries to execute.</param>
        /// <param name="config">Optional configuration for parsing queries based on the plugin.</param>
        /// <returns>An integer representing the total number of rows affected by the delete queries in the transaction.</returns>
        public async Task<int> DeleteTransactionQueryAsync(DatabaseTransactionBuilder transactionBuilder = null, Dictionary<string, List<KeyValuePair<string, string>>> queries = null, Config config = null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            IReadOnlyCollection<string> queryList = null;

            if (queries == null && transactionBuilder == null) return 0;
            if (queries != null) queryList = queries.Keys.ToList();
            if (transactionBuilder != null) queryList = transactionBuilder.GetQueries();

            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();
            int failed = 0, succeed = 0;

            using (var transaction = conn.BeginTransaction())
            {
                foreach (var query in queryList)
                {

                    try
                    {
                        var parameters = transactionBuilder.GetValue(query);
                        string q = query;
                        //If the plugin uses the bot's main database, the bot must parse information about the plugin in a given query.
                        if (query.Contains("#") && config != null)
                        {
                            q = ParseQueryTableName(query, config.pluginName);
                        }

                        using (var cmd = new SQLiteCommand(q, conn, transaction))
                        {
                            if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                            succeed += await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    catch (SQLiteException e)
                    {
                        if (e.ErrorCode == 19)
                        {
                            failed++;
                            continue;
                        }
                        transaction.Rollback();
                        Logger.Log("Database", $"Failed to delete records in transaction, rolling back {succeed} queries, error: {e.Message}", LogLevel.Error);
                        break;
                    }
                }
                transaction.Commit();
                sw.Stop();
                Logger.Log("Database", $"Total records in delete transaction: {succeed + failed}, failed: {failed}, succeed: {succeed}, operation time: {sw.ElapsedMilliseconds} ms", LogLevel.Info);
            }
            return succeed;
        }
        /// <summary>
        /// Asynchronously executes an SQL query to insert data into a SQLite database.
        /// </summary>
        /// <param name="query">The SQL query used for inserting data.</param>
        /// <param name="parameters">Optional query parameters to be used in the query.</param>
        /// <param name="config">Optional configuration for parsing the query based on the plugin.</param>
        /// <returns>An integer representing the number of rows affected by the insert query.</returns>
        public async Task<int> InsertQueryAsync(string query, QueryParametersBuilder parameters = null, Config config = null)
        {
            string q = query;
            //If the plugin uses the bot's main database, the bot must parse information about the plugin in a given query.
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

                if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters.GetAll()) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);

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
        /// Asynchronously executes a series of SQL queries in a transaction to insert data into a SQLite database.
        /// </summary>
        /// <param name="transactionBuilder">Optional builder for constructing the transaction insert queries.</param>
        /// <param name="queries">Optional dictionary of insert queries to execute.</param>
        /// <param name="config">Optional configuration for parsing queries based on the plugin.</param>
        /// <returns>An integer representing the total number of rows affected by the insert queries in the transaction.</returns>
        public async Task<int> InsertTransactionQueryAsync(DatabaseTransactionBuilder transactionBuilder = null, Dictionary<string, List<KeyValuePair<string, string>>> queries = null, Config config = null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            IReadOnlyCollection<string> queryList = null;

            if (queries == null && transactionBuilder == null) return 0;
            if (queries != null) queryList = queries.Keys.ToList();
            if (transactionBuilder != null) queryList = transactionBuilder.GetQueries();

            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();
            int failed = 0, succeed = 0;

            using (var transaction = conn.BeginTransaction())
            {
                foreach(var query in queryList)
                {
                    
                    try
                    {
                        var parameters = transactionBuilder.GetValue(query);
                        string q = query;
                        //If the plugin uses the bot's main database, the bot must parse information about the plugin in a given query.
                        if (query.Contains("#") && config != null)
                        {
                            q = ParseQueryTableName(query, config.pluginName);
                        }

                        using (var cmd = new SQLiteCommand(q, conn, transaction))
                        {
                            if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                            succeed += await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    catch (SQLiteException e)
                    {
                        if (e.ErrorCode == 19)
                        {
                            failed++;
                            continue;
                        }
                        transaction.Rollback();
                        Logger.Log("Database", $"Failed to update records in transaction, rolling back {succeed} queries, error: {e.Message}", LogLevel.Error);
                        break;
                    }
                }
                transaction.Commit();
                sw.Stop();
                Logger.Log("Database", $"Total records in transaction: {succeed + failed}, failed: {failed}, succeed: {succeed}, operation time: {sw.ElapsedMilliseconds} ms", LogLevel.Info);
            }
            return succeed;
        }
        /// <summary>
        /// Reads all values processed by the query, converts all values to a <see cref="string"/> type variable
        /// </summary>
        /// <param name="cmd">The SQL command used for data retrieval.</param>
        /// <returns>A <see cref="List{T}"/> where T is <see cref="string"/> representing the retrieved data.</returns>
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
        /// Asynchronously executes an SQL query to retrieve data from a SQLite database and returns the result as a <see cref="List{T}"/> where T is <see cref="string"/>.
        /// </summary>
        /// <param name="query">The SQL query used for data retrieval.</param>
        /// <param name="parameters">Optional query parameters to be used in the query.</param>
        /// <param name="config">Optional configuration for parsing the query based on the plugin.</param>
        /// <returns>A <see cref="List{T}"/> where T is <see cref="string"/> representing the retrieved data.</returns>
        public async Task<List<string>> SelectQueryAsync(string query, QueryParametersBuilder parameters = null, Config config = null)
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
                if (parameters != null) foreach (KeyValuePair<string, string> parameter in parameters.GetAll()) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                    items = await DataReaderAsync(cmd);
            }
            conn.Close();
            return items;
        }
        /// <summary>
        /// Asynchronously checks and creates a table in a SQLite database for a given plugin if it doesn't already exist.
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
        /// Parses a query string by replacing the '#' symbol with the specified plugin name, helping plugins to identify themselves.
        /// </summary>
        /// <param name="query">The query string to be parsed.</param>
        /// <param name="pluginName">The name of the plugin for identification.</param>
        /// <returns>The parsed query string with the plugin name.</returns>
        private string ParseQueryTableName(string query, string pluginName)
        {
            return query.Replace("#", pluginName + "_");
        }
        /// <summary>
        /// Checks if a table with the specified name already exists in the SQLite database.
        /// </summary>
        /// <param name="tableName">The name of the table to check for existence.</param>
        /// <returns><see langword="True"/> if the table exists, <see langword="false"/> otherwise.</returns>
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
        /// Creates a table, columnProperties format: "column_name1 TEXT PRIMARY KEY, column_name2 TEXT" etc.
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
    }
}

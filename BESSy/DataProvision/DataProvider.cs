using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Files;
using BESSy.Factories;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.AccessControl;
using BESSy.Extensions;
using BESSy.Parallelization;
using System.Security.Principal;
using BESSy.Serialization;

namespace BESSy.DataProvision
{
    public class DataProvider : IDisposable
    {
        public DataProvider(string fileNamePath, string connectionString, IFormatter providerFormatter)
            : this(fileNamePath, connectionString, providerFormatter, new DataProviderSettings(connectionString))
        {

        }

        public DataProvider(string fileNamePath, string connectionString, IFormatter providerFormatter, DataProviderSettings settings)
        {
            _settings = settings;
            _providerFormatter = providerFormatter;
        }

        object _syncRoot = new object();
        IFormatter _providerFormatter;
        DataProviderSettings _settings;

        IDictionary<string, IStreamingRepository> _databases = new Dictionary<string, IStreamingRepository>();
        protected string _ioError = "File property {0}, could not be found or accessed: {1}.";

        protected virtual void EnsureDirectory(string dbName)
        {
            var wholePath = Path.Combine(_settings.WorkingDirectory, dbName);

            if (!Directory.Exists(wholePath))
                Directory.CreateDirectory(wholePath);
        }

        protected virtual FileStream OpenOrCreateFileReadWrite(string fileName)
        {
            return new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, Environment.SystemPageSize, false);
        }

        protected virtual FileStream OpenOrCreateFileRead(string fileName)
        {
            return new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read, Environment.SystemPageSize, false);
        }

        protected virtual void SaveSettings()
        {
            using (var fileStream = OpenOrCreateFileRead
                (Path.Combine(_settings.WorkingDirectory, "dataprovider.settings")))
            {
                using (var settingsStream = _providerFormatter.FormatObjStream(_settings))
                {
                    SaveSettings(fileStream, settingsStream);
                }
            }
        }

        protected virtual void SaveSettings(FileStream fileStream, Stream settingsStream)
        {
#if DEBUG
            if (settingsStream == null)
                throw new ArgumentNullException("segmentSeed");
#endif
            lock (_syncRoot)
            {
                try
                {
                    settingsStream.Position = 0;

                    fileStream.Position = 0;

                    settingsStream.WriteAllTo(fileStream, (int)settingsStream.Length);

                    var position = fileStream.Position;

                    fileStream.Flush();
                }
                catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
                catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            }
        }

        #region Create database

        public void CreateDatabase(CreateDatabaseCommand command)
        {

        }

        #endregion

        #region Publisher Subscriber Commands

        public void AddReplication(ReplicationCommand command)
        {
            if (!_databases.ContainsKey(command.DatabaseName))
                throw new ReplicationFactoryException(string.Format("Database not found, or not attached: {0}", command.DatabaseName));

            var database = _databases[command.DatabaseName];            
        }


        public void RemoveReplication(ReplicationCommand command)
        {


            
        }

        #endregion

        #region Attach / Detach Database

        public void AttachDatabase(string connectionString)
        {

        }

        public void DetachDatabase(string connectionString)
        {

        }

        #endregion

        #region Update Security

        public void DeleteUser()
        {

        }

        public void UpdateUser()
        {

        }

        protected void AddOrUpdateUsers()
        {

        }

        public void AddOrUpdateUser()
        {

        }

        #endregion

        #region Open Database

        protected void CreateFromDatafile(string dbName)
        {
            try
            {
                var formatter = _settings.ConsolidatedFormatter;

                using (var dataFile = OpenOrCreateFileRead(Path.Combine(_settings.WorkingDirectory, dbName, dbName + ".datafile")))
                {
                    using (var database = OpenOrCreateFileReadWrite(Path.Combine(_settings.WorkingDirectory, dbName, dbName + ".database")))
                    {
                        byte[] buffer = new byte[Environment.SystemPageSize];

                        var read = dataFile.Read(buffer, 0, buffer.Length);

                        while (read > 0)
                        {
                            var unformatted = formatter.Unformat(buffer);

                            database.Write(unformatted, 0, unformatted.Length);

                            read = dataFile.Read(buffer, 0, buffer.Length);
                        }

                        database.Flush();
                        database.Close();
                    }

                    dataFile.Close();
                }

                try { File.Delete(Path.Combine(_settings.WorkingDirectory, dbName, dbName + ".datafile")); }
                catch (Exception ex) { Trace.TraceError("Error deleting datafile {0}: {1}", dbName, ex); }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error taking database {0} offline: {1}", dbName, ex);
                throw;
            }
        }

        public object OpenDatabase(string connectionString)
        {
            var fileName = DatabaseFromStringFactory.GetFileName(connectionString);

            var dbName = Path.GetFileNameWithoutExtension(fileName);

            return OpenDatabase(dbName, connectionString);
        }

        public object OpenDatabase(string databaseName, string connectionString)
        {
            IStreamingRepository database = null;

            var path = Path.Combine(_settings.WorkingDirectory, databaseName + ".database");

            EnsureDirectory(databaseName);

            lock (_syncRoot)
            {
                if (_databases.ContainsKey(databaseName.ToLower()) 
                    && _databases[databaseName.ToLower()] != null)
                    return _databases[databaseName.ToLower()];

                if (!File.Exists(Path.Combine(_settings.WorkingDirectory, databaseName, databaseName + ".database")))
                    if (File.Exists(Path.Combine(_settings.WorkingDirectory, databaseName, databaseName + ".datafile")))
                        CreateFromDatafile(databaseName);
                    else
                        throw new DatabaseFactoryException("Database not found");

                database = DatabaseFromStringFactory.Create(connectionString, path) as IStreamingRepository;

                if (database == null)
                    throw new InvalidOperationException(string.Format("Could not create database with property: {0} and for connection string: \r\n{0}", databaseName, connectionString));

                database.Load();

                if (_databases.ContainsKey(databaseName.ToLower()))
                    _databases[databaseName.ToLower()] = database;
                else
                    _databases.Add(databaseName.ToLower(), database);

                SaveSettings();
            }

            return database;
        }

        #endregion

        #region Close Database

        protected virtual void CloseDatabase(string name, string fileName, IStreamingRepository database)
        {

        }

        public void CloseDatabaseFromConnection(string connectionString)
        {

        }

        public void CloseDatabase(string databaseName)
        {
            
        }

        #endregion

        protected virtual void CompressDatabase(string name, string fileName, IStreamingRepository database)
        {
            try
            {
                using (var dataFile = OpenOrCreateFileReadWrite(Path.Combine(_settings.WorkingDirectory, name, name + ".datafile")))
                {
                    foreach (var s in database.AsStreaming())
                    {
                        using (var f = _settings.ConsolidatedFormatter.Format(s))
                        {
                            f.WriteAllTo(dataFile);
                        }

                        s.Dispose();
                    }

                    dataFile.Flush();
                    dataFile.Close();
                }

                File.Delete(Path.Combine(_settings.WorkingDirectory, name, name + ".database"));
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error taking database {0} offline: {1}", name, ex);
            }
        }

        public void CompressDatabaseFromConnection(string connectionString)
        {
            var fileName = DatabaseFromStringFactory.GetFileName(connectionString);
            var dbName = Path.GetFileNameWithoutExtension(fileName).ToLower();

            IStreamingRepository db = null;

            lock (_syncRoot)
            {
                if (!_databases.ContainsKey(dbName))
                    return;

                db = _databases[dbName];

                if (db == null)
                    return;
            }

            CompressDatabase(dbName, fileName, db);
        }

        public void CompressDatabase(string databaseName)
        {
            if (databaseName == null)
                return;

            var dbName = databaseName.ToLower();

            IStreamingRepository db = null;

            lock (_syncRoot)
            {
                if (!_databases.ContainsKey(dbName))
                    return;

                db = _databases[dbName];

                if (db == null)
                    return;
            }

            CompressDatabase(dbName, dbName + ".database", db);
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                Parallel.ForEach(_databases, new Action<KeyValuePair<string, IStreamingRepository>>
                    (delegate(KeyValuePair<string, IStreamingRepository> database)
                {
                    try
                    {
                        if (database.Value != null)
                            CloseDatabase(database.Key, database.Key + ".database", database.Value);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("DataProvider was unable to close database {0}", ex);
                    }
                }));

                _databases.Clear();
            }
        }
    }
}

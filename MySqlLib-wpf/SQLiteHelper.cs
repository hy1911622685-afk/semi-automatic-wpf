using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MySqlLib.Wpf
{
    /// <summary>
    /// SQLite数据库操作封装类
    /// </summary>
    public class SQLiteHelper : IDisposable
    {
        private string _connectionString;
        private SQLiteConnection _connection;
        private bool _disposed = false;


        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dbPath">数据库文件路径</param>
        /// <param name="password">数据库密码（可选）</param>
        public SQLiteHelper(string dbPath, string password = null)
        {
            if (string.IsNullOrEmpty(dbPath))
                throw new ArgumentException("数据库路径不能为空");

            // 如果数据库文件不存在，自动创建
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            _connectionString = BuildConnectionString(dbPath, password);
            _connection = new SQLiteConnection(_connectionString);
        }

        /// <summary>
        /// 使用默认数据库路径（应用程序目录下的data.db）
        /// </summary>
        public SQLiteHelper() : this(GetDefaultDbPath()) { }

        private static string GetDefaultDbPath()
        {
            string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(appDir, "data.db");
        }

        private string BuildConnectionString(string dbPath, string password)
        {
            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder
            {
                DataSource = dbPath,
                Version = 3,
                Pooling = true, // 启用连接池
                FailIfMissing = false, // 数据库不存在时不抛出异常
                BinaryGUID = true
            };

            if (!string.IsNullOrEmpty(password))
            {
                builder.Password = password;
            }

            return builder.ToString();
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 打开数据库连接
        /// </summary>
        public void OpenConnection()
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        public void CloseConnection()
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }
        }

        /// <summary>
        /// 获取数据库连接状态
        /// </summary>
        public ConnectionState ConnectionState => _connection.State;

        #endregion

        #region 基本操作

        /// <summary>
        /// 执行非查询SQL语句（创建表、插入、更新、删除）
        /// </summary>
        public int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using (var command = CreateCommand(sql, parameters)) // using 块结束时会自动调用 conn.Close() 和 conn.Dispose()
            {
                OpenConnection();
                return command.ExecuteNonQuery();
            }

        }

        /// <summary>
        /// 执行查询，返回DataTable
        /// </summary>
        public DataTable ExecuteDataTable(string sql, params SQLiteParameter[] parameters)
        {
            using (var command = CreateCommand(sql, parameters)) // using 块结束时会自动调用 conn.Close() 和 conn.Dispose()
            {
                OpenConnection();
                using (var adapter = new SQLiteDataAdapter(command))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        /// <summary>
        /// 执行查询，返回第一行第一列的值
        /// </summary>
        public object ExecuteScalar(string sql, params SQLiteParameter[] parameters)
        {
            using (var command = CreateCommand(sql, parameters)) // using 块结束时会自动调用 conn.Close() 和 conn.Dispose()
            {
                OpenConnection();
                return command.ExecuteScalar();
            }
        }

        /// <summary>
        /// 执行查询，返回SQLiteDataReader（需要手动关闭）
        /// </summary>
        public SQLiteDataReader ExecuteReader(string sql, params SQLiteParameter[] parameters)
        {
            using (var command = CreateCommand(sql, parameters)) // using 块结束时会自动调用 conn.Close() 和 conn.Dispose()
            {
                OpenConnection();
                return command.ExecuteReader(CommandBehavior.CloseConnection);
            }
        }

        /// <summary>
        /// 执行事务操作
        /// </summary>
        public bool ExecuteTransaction(Action<SQLiteTransaction> action)
        {
            OpenConnection();
            using (var transaction = _connection.BeginTransaction()) // using 块结束时会自动调用 conn.Close() 和 conn.Dispose()
            {
                try
                {
                    action?.Invoke(transaction);
                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    return false;
                }
            }
        }

        /// <summary>
        /// 使用事务 批量插入数据  
        /// </summary>
        public int BatchInsert(string tableName, DataTable data, int batchSize = 1000)
        {
            if (data == null || data.Rows.Count == 0)
                return 0;

            int affectedRows = 0;
            OpenConnection();

            using (var transaction = _connection.BeginTransaction()) // using 块结束时会自动调用 conn.Close() 和 conn.Dispose()
            {
                try
                {
                    // 构建插入语句
                    var columns = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                    var parameters = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => "@" + c.ColumnName));

                    string sql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";

                    for (int i = 0; i < data.Rows.Count; i += batchSize)
                    {
                        int endIndex = Math.Min(i + batchSize, data.Rows.Count);
                        for (int j = i; j < endIndex; j++)
                        {
                            using (var cmd = new SQLiteCommand(sql, _connection, transaction))
                            {
                                foreach (DataColumn column in data.Columns)
                                {
                                    cmd.Parameters.AddWithValue("@" + column.ColumnName, data.Rows[j][column]);
                                }

                                affectedRows += cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    transaction.Commit();
                    return affectedRows;
                }
                catch
                {
                    transaction.Rollback();
                    return 0;
                }

            }
        }

        #endregion

        #region 表操作

        /// <summary>
        /// 检查表是否存在
        /// </summary>
        public bool TableExists(string tableName)
        {
            OpenConnection();
            string sql = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
            var result = ExecuteScalar(sql, new SQLiteParameter("@tableName", tableName));
            CloseConnection();
            return Convert.ToInt32(result) > 0;
        }

        /// <summary>
        /// 创建表（如果不存在）
        /// </summary>
        public bool CreateTable(string createTableSql)
        {
            OpenConnection();
            bool rlt = ExecuteNonQuery(createTableSql) >= 0;
            CloseConnection();
            return rlt;
        }

        /// <summary>
        /// 删除表
        /// </summary>
        public bool DropTable(string tableName)
        {
            OpenConnection();
            string sql = $"DROP TABLE IF EXISTS {tableName}";
            bool rlt = ExecuteNonQuery(sql) >= 0;
            CloseConnection();
            return rlt;
        }

        /// <summary>
        /// 获取所有表名
        /// </summary>
        public List<string> GetTableNames()
        {
            OpenConnection();
            string sql = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
            var dt = ExecuteDataTable(sql);
            var list = dt.Rows.Cast<DataRow>().Select(r => r["name"].ToString()).ToList();
            CloseConnection();
            return list;
        }

        /// <summary>
        /// 获取表结构
        /// </summary>
        public DataTable GetTableSchema(string tableName)
        {
            OpenConnection();
            string sql = $"PRAGMA table_info({tableName})";
            var table = ExecuteDataTable(sql);
            CloseConnection();
            return table;
        }

        #endregion

        #region 实体操作（简易ORM）

        /// <summary>
        /// 插入实体
        /// </summary>
        public int Insert<T>(T entity) where T : class, new()
        {
            OpenConnection();
            var type = typeof(T);
            var properties = type.GetProperties()
                .Where(p => p.CanRead && !IsPrimaryKeyAutoIncrement(p))
                .ToArray();

            var columns = string.Join(", ", properties.Select(p => p.Name));
            var parameters = string.Join(", ", properties.Select(p => "@" + p.Name));

            string sql = $"INSERT INTO {type.Name} ({columns}) VALUES ({parameters})";

            var sqlParams = properties.Select(p => new SQLiteParameter("@" + p.Name, p.GetValue(entity) ?? DBNull.Value)).ToArray();

            int num = ExecuteNonQuery(sql, sqlParams);
            CloseConnection();
            return num;
        }

        /// <summary>
        /// 更新实体
        /// </summary>
        public int Update<T>(T entity, string where = null) where T : class, new()
        {
            OpenConnection();
            var type = typeof(T);
            var properties = type.GetProperties()
                .Where(p => p.CanRead && !IsPrimaryKeyAutoIncrement(p))
                .ToArray();

            var setClause = string.Join(", ", properties.Select(p => $"{p.Name}=@{p.Name}"));

            string sql = $"UPDATE {type.Name} SET {setClause}";
            if (!string.IsNullOrEmpty(where))
            {
                sql += $" WHERE {where}";
            }

            var sqlParams = properties.Select(p => new SQLiteParameter("@" + p.Name, p.GetValue(entity) ?? DBNull.Value)).ToArray();

            int num = ExecuteNonQuery(sql, sqlParams);
            CloseConnection();
            return num;
        }

        /// <summary>
        /// 查询所有记录
        /// </summary>
        public List<T> QueryAll<T>() where T : class, new()
        {
            OpenConnection();
            string sql = $"SELECT * FROM {typeof(T).Name}";
            var dataList = Query<T>(sql);
            CloseConnection();
            return dataList;
        }

        /// <summary>
        /// 根据条件查询
        /// </summary>
        public List<T> Query<T>(string sql, params SQLiteParameter[] parameters) where T : class, new()
        {
            OpenConnection();
            var dt = ExecuteDataTable(sql, parameters);
            var dataList = DataTableToList<T>(dt);
            CloseConnection();
            return dataList;
        }

        /// <summary>
        /// 根据ID查询
        /// </summary>
        public T QueryById<T>(object id) where T : class, new()
        {
            OpenConnection();
            var type = typeof(T);
            string sql = $"SELECT * FROM {type.Name} WHERE Id = @Id";
            var result = Query<T>(sql, new SQLiteParameter("@Id", id));
            var data = result.FirstOrDefault();
            CloseConnection();
            return data;
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        public int Delete<T>(string where, params SQLiteParameter[] parameters) where T : class, new()
        {
            OpenConnection();
            string sql = $"DELETE FROM {typeof(T).Name} WHERE {where}";
            int num = ExecuteNonQuery(sql, parameters);
            CloseConnection();
            return num;
        }

        /// <summary>
        /// DataTable转List
        /// </summary>
        private List<T> DataTableToList<T>(DataTable dt) where T : class, new()
        {
            List<T> list = new List<T>();
            if (dt == null || dt.Rows.Count == 0) return list;

            var properties = typeof(T).GetProperties();

            foreach (DataRow row in dt.Rows)
            {
                T obj = new T();
                foreach (var prop in properties)
                {
                    if (dt.Columns.Contains(prop.Name) && row[prop.Name] != DBNull.Value)
                    {
                        prop.SetValue(obj, Convert.ChangeType(row[prop.Name], prop.PropertyType));
                    }
                }
                list.Add(obj);
            }

            return list;
        }

        private bool IsPrimaryKeyAutoIncrement(PropertyInfo property)
        {
            // 这里可以根据需要添加更复杂的判断逻辑
            return property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 创建SQLite命令
        /// </summary>
        private SQLiteCommand CreateCommand(string sql, SQLiteParameter[] parameters)
        {
            var command = new SQLiteCommand(sql, _connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            return command;
        }

        /// <summary>
        /// 备份数据库
        /// </summary>
        public void BackupDatabase(string backupPath)
        {
            OpenConnection();
            using (var backup = new SQLiteConnection($"Data Source={backupPath};Version=3;"))
            {
                backup.Open();
                _connection.BackupDatabase(backup, "main", "main", -1, null, 0);
            }
        }

        /// <summary>
        /// 获取数据库文件大小
        /// </summary>
        public long GetDatabaseSize()
        {
            var dbPath = new SQLiteConnectionStringBuilder(_connectionString).DataSource;
            if (File.Exists(dbPath))
            {
                return new FileInfo(dbPath).Length;
            }
            return 0;
        }

        /// <summary>
        /// 压缩数据库（VACUUM）
        /// </summary>
        public void Vacuum()
        {
            ExecuteNonQuery("VACUUM");
        }

        #endregion

        #region IDisposable实现

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_connection != null)
                    {
                        if (_connection.State != ConnectionState.Closed)
                            _connection.Close();
                        _connection.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        ~SQLiteHelper()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// SQLite参数构建辅助类
    /// </summary>
    public static class SQLiteParameterBuilder
    {
        public static SQLiteParameter Create(string name, object value, DbType dbType = DbType.String)
        {
            return new SQLiteParameter(name, value) { DbType = dbType };
        }

        public static SQLiteParameter Create(string name, string value)
        {
            return Create(name, value, DbType.String);
        }

        public static SQLiteParameter Create(string name, int value)
        {
            return Create(name, value, DbType.Int32);
        }

        public static SQLiteParameter Create(string name, DateTime value)
        {
            return Create(name, value, DbType.DateTime);
        }

        public static SQLiteParameter Create(string name, bool value)
        {
            return Create(name, value, DbType.Boolean);
        }

        public static SQLiteParameter Create(string name, decimal value)
        {
            return Create(name, value, DbType.Decimal);
        }

        public static SQLiteParameter Create(string name, byte[] value)
        {
            return Create(name, value, DbType.Binary);
        }
    }
}
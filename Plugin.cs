using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuickLook.Common.Plugin;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.Plugin.SqliteViewer
{
    class Setting
    {
        public double width = 800;
        public double height = 600;
        public int logLevel = 2;
        private readonly JObject jsonObj;

        public Setting(string file)
        {
            jsonObj = File.Exists(file) ? JObject.Parse(File.ReadAllText(file)) : new JObject();
            width = GetDouble("width", 800);
            height = GetDouble("height", 600);
            logLevel = GetInt("log_level", 2);
        }

        public string GetString(string key, string defaultValue = "未提供")
        {
            if (jsonObj.ContainsKey(key))
            {
                return (string)jsonObj[key];
            }
            else
            {
                return defaultValue;
            }
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (jsonObj.ContainsKey(key))
            {
                return (int)jsonObj[key];
            }
            else
            {
                return defaultValue;
            }
        }

        public double GetDouble(string key, double defaultValue = 0)
        {
            if (jsonObj.ContainsKey(key))
            {
                return (double)jsonObj[key];
            }
            else
            {
                return defaultValue;
            }
        }

        public bool GetBoolean(string key, bool defaultValue = false)
        {
            if (jsonObj.ContainsKey(key))
            {
                return (bool)jsonObj[key];
            }
            else
            {
                return defaultValue;
            }
        }

        public JArray GetArray(string key)
        {
            return (JArray)jsonObj[key];
        }
    }

    public class Plugin : IViewer
    {
        public int Priority => 0;
        private readonly string[] _Extensions = [".db", ".sqlite", ".sqlite3"];

        private string pluginDir;
        private string settingPath;
        private string pluginStaticDir;
        private string tmplHtmlFilePath;
        private string htmlFilePath;

        private Setting setting;

        public void Init()
        {
            // 这里进行变量初始化, 后边竟然访问不到, 不知道为啥, 所以把变量初始化放prepare了
            varInit();
            if (!File.Exists(settingPath))
            {
                Dictionary<string, object> settingMap = new Dictionary<string, object> {
                    { "width",  800},
                    { "height",  600},
                    { "log_level", 2 },
                };
                File.WriteAllText(settingPath, JsonConvert.SerializeObject(settingMap, Formatting.Indented));
            }

            if (!File.Exists(htmlFilePath))
            {
                // 检查模板文件是否存在
                if (!File.Exists(tmplHtmlFilePath))
                {
                    throw new FileNotFoundException($"模板文件未找到: {tmplHtmlFilePath}");
                }

                // 读取模板文件内容
                string templateContent = File.ReadAllText(tmplHtmlFilePath);

                // 替换占位符
                templateContent = templateContent.Replace("[[PLUGIN_STATIC_DIR]]", pluginStaticDir).Replace("\\", "/");

                // 将内容写入 index.html
                File.WriteAllText(htmlFilePath, templateContent);
                Logger.Instance.Info($"已根据模板文件生成新的 HTML 文件: {htmlFilePath}");
            }
            setting = new Setting(settingPath);
            var logger = Logger.Instance;
            logger.Level = (Logger.LogLevel)setting.logLevel;
            logger.Debug($"     settingPath: {settingPath}");
            logger.Debug($" pluginStaticDir: {pluginStaticDir}");
            logger.Debug($"tmplHtmlFilePath: {tmplHtmlFilePath}");
            logger.Debug($"    htmlFilePath: {htmlFilePath}");
        }

        public void varInit()
        {
            pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Logger.Instance.Debug($"插件目录: {pluginDir}");

            settingPath = Path.Combine(pluginDir, "setting.json");
            pluginStaticDir = Path.Combine(pluginDir, "static");
            tmplHtmlFilePath = Path.Combine(pluginStaticDir, "tmpl_index.html");
            htmlFilePath = Path.Combine(pluginStaticDir, "index.html");

            setting = new Setting(settingPath);
            var logger = Logger.Instance;
            logger.Level = (Logger.LogLevel)setting.logLevel;
        }

        public bool CanHandle(string path)
        {
            return !Directory.Exists(path) && _Extensions.Contains(Path.GetExtension(path).ToLower());
        }

        public void Prepare(string path, ContextObject context)
        {
            // 这里是每次查看时调用的, 后边可以访问到
            varInit();
            context.PreferredSize = new Size
            {
                Width = setting.width,
                Height = setting.height,
            };
        }

        public void Cleanup()
        {
        }

        public void View(string path, ContextObject context)
        {
            try
            {
                // 调用实际渲染方法
                var viewerContent = GetViewerContent(path);

                // 设置预览内容和标题
                context.ViewerContent = viewerContent;
                context.Title = Path.GetFileName(path);
            }
            catch (Exception ex)
            {
                // 如果发生错误，显示错误信息
                context.ViewerContent = new TextBlock
                {
                    Text = $"无法加载文件: {ex.Message}",
                    TextWrapping = System.Windows.TextWrapping.Wrap
                };
                context.Title = "错误";
            }

            // 标记加载完成
            context.IsBusy = false;
        }

        public FrameworkElement GetViewerContent(string filePath)
        {
            // 创建 WebBrowser 控件
#if false
            var webBrowser = new WebBrowser
#else
            var webBrowser = new WebpagePanel
#endif
            {
                Margin = new Thickness(10),
                MinHeight = 300,
                MinWidth = 600
            };

            // 设置 ObjectForScripting
            webBrowser.ObjectForScripting = new ScriptHandler(filePath);
            // 加载 HTML 页面
            Logger.Instance.Debug($"htmlFilePath: {htmlFilePath}");
            webBrowser.Navigate(new Uri(htmlFilePath));
            return webBrowser;
        }
    }

    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class ScriptHandler
    {
        private readonly string _filePath;

        public ScriptHandler(string filePath)
        {
            _filePath = filePath;
        }

        public string LoadTableDataBySql(string sql)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_filePath};Mode=ReadOnly;"))
                {
                    connection.Open();

                    // 将表数据转换为 JSON
                    List<Dictionary<string, object>> data = getTableData(connection, sql);
                    Dictionary<string, object> result = new Dictionary<string, object> {
                        { "status", true },
                        { "message", "ok" },
                        { "data", data }
                    };
                    Logger.Instance.Debug($"通过sql加载表数据: {sql}, JSON 数据已生成");
                    return JsonConvert.SerializeObject(result, Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                Dictionary<string, object> result = new Dictionary<string, object> {
                    { "status", false },
                    { "message", ex.Message },
                    { "data", null }
                };
                Logger.Instance.Error($"加载表数据失败: {ex.Message}");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
        }

        public string LoadTableData(string input, bool isTableName)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_filePath};Mode=ReadOnly;"))
                {
                    connection.Open();

                    string sql;
                    if (isTableName)
                    {
                        sql = $"select * from `{input}` limit 5";
                    }
                    else
                    {
                        sql = input;
                    }
                    // 将表数据转换为 JSON
                    List<Dictionary<string, object>> data = getTableData(connection, sql);
                    Dictionary<string, object> result = new Dictionary<string, object> {
                        { "status", true },
                        { "message", "ok" },
                        { "data", data }
                    };
                    Logger.Instance.Debug($"通过sql加载表数据: {sql}, JSON 数据已生成");
                    return JsonConvert.SerializeObject(result, Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                Dictionary<string, object> result = new Dictionary<string, object> {
                    { "status", false },
                    { "message", ex.Message },
                    { "data", null }
                };
                Logger.Instance.Error($"加载表数据失败: {ex.Message}");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
        }

        public string GetTableNames()
        {
            using (var connection = new SQLiteConnection($"Data Source={_filePath};Mode=ReadOnly;"))
            {
                connection.Open();
                var query = connection.CreateCommand();
                query.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                var tableNames = new List<string>();
                using (var reader = query.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tableNames.Add(reader.GetString(0));
                    }
                }
                //return tableNames.ToArray();
                return JsonConvert.SerializeObject(tableNames, Formatting.Indented);
            }
        }

        public int GetTableRecordCount(string tableName)
        {
            using (var connection = new SQLiteConnection($"Data Source={_filePath};Mode=ReadOnly;"))
            {
                connection.Open();
                var query = connection.CreateCommand();
                query.CommandText = $"SELECT count(*) FROM {tableName}";
                var recordCount = (long)query.ExecuteScalar();
                return (int)recordCount;
            }
        }

        public string GetTableColumns(string tableName)
        {
            using (var connection = new SQLiteConnection($"Data Source={_filePath};Mode=ReadOnly;"))
            {
                connection.Open();
                var query = connection.CreateCommand();
                query.CommandText = $"PRAGMA table_info({tableName});";
                var columnNames = new List<string>();
                using (var reader = query.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader.GetString(1);
                        columnNames.Add(columnName);
                    }
                }
                return JsonConvert.SerializeObject(columnNames, Formatting.Indented);
            }
        }

        public List<Dictionary<string, object>> getTableData(SQLiteConnection connection, string sql)
        {
            var query = connection.CreateCommand();
            query.CommandText = sql;

            var data = new List<Dictionary<string, object>>();
            using (var reader = query.ExecuteReader())
            {
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            object columnValue = reader.GetValue(i);

                            if (columnValue == DBNull.Value)
                            {
                                columnValue = null; // 或者 "(空)"
                            }

                            row[columnName] = columnValue;
                        }
                        data.Add(row);
                    }
                }
            }
            return data;
        }
    }
}

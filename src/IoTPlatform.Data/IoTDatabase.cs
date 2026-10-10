using XCode;
using XCode.DataAccessLayer;

namespace IoTPlatform.Data;

/// <summary>数据库初始化</summary>
public static class IoTDatabase
{
    public const String ConnName = "IoT";

    /// <summary>注册连接并同步完成反向工程（建表/加字段），避免首次并发访问时表尚未创建</summary>
    public static void Init(String connectionString, String provider = "SQLite")
    {
        NewLifeHostPaths.Apply();
        if (SqliteSettings.IsSqlite(provider, connectionString))
            connectionString = SqliteSettings.EnhanceConnectionString(connectionString);
        DAL.AddConnStr(ConnName, connectionString, null, provider);
        EntityFactory.InitConnection(ConnName);
        SqliteSettings.Apply(ConnName, provider);
        NormalizeThingModel();
    }

    /// <summary>
    /// 物模型唯一索引从 (ProductId, Identifier) 扩成 (ProductId, FunctionBlockId, Identifier)。
    /// 已有行的模块列补成空字符串（默认模块），并丢掉旧的唯一索引，避免自定义模块无法使用相同标识符。
    /// </summary>
    private static void NormalizeThingModel()
    {
        var dal = DAL.Create(ConnName);
        foreach (var table in new[] { "ThingProperty", "ThingEvent", "ThingService" })
        {
            try { dal.Execute($"Update {table} Set FunctionBlockId='' Where FunctionBlockId Is Null"); } catch { }
            try { dal.Execute($"Drop Index If Exists IU_{table}_ProductId_Identifier"); } catch { }
        }
    }
}

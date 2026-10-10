using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace IoTPlatform.Data;

public partial class ThingProperty : Entity<ThingProperty>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static ThingProperty()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(ProductId));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();

        // Thing-model rows change rarely; keep the table cache warm so Find* helpers
        // do not reload Select * From ThingProperty every EntityCacheExpire (default 10s).
        Meta.Cache.Expire = 600;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Identifier.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Identifier), "标识符不能为空！");
        FunctionBlockId = ThingKey.Block(FunctionBlockId);

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        // 保留2位小数
        //Min = Math.Round(Min, 2);
        //Max = Math.Round(Max, 2);
        //Step = Math.Round(Step, 2);
        //Scaling = Math.Round(Scaling, 2);
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;

        // 检查唯一索引（同一模块内标识符唯一，默认模块的 FunctionBlockId 为空字符串）
        CheckExist(method == DataMethod.Insert, nameof(ProductId), nameof(FunctionBlockId), nameof(Identifier));

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化ThingProperty[产品属性]数据……");

    //    var entity = new ThingProperty();
    //    entity.ProductId = 0;
    //    entity.Identifier = "abc";
    //    entity.Name = "abc";
    //    entity.DataType = "abc";
    //    entity.Unit = "abc";
    //    entity.Min = 0.0;
    //    entity.Max = 0.0;
    //    entity.Step = 0.0;
    //    entity.Length = 0;
    //    entity.AccessMode = "abc";
    //    entity.Address = "abc";
    //    entity.Scaling = 0.0;
    //    entity.Mapping = "abc";
    //    entity.Sort = 0;
    //    entity.Enable = true;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化ThingProperty[产品属性]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Category From ThingProperty Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<ThingProperty> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    static readonly ConcurrentDictionary<Int32, IReadOnlyDictionary<String, ThingProperty>> ProductCache = new();

    /// <summary>
    /// Product thing-model properties, cached in memory until <see cref="InvalidateCache"/> .
    /// Avoids <c>Select * From ThingProperty</c> on every uplink (entity cache expires every 10s by default).
    /// </summary>
    public static IReadOnlyDictionary<String, ThingProperty> GetCachedByProductId(Int32 productId)
    {
        if (productId < 0) return new Dictionary<String, ThingProperty>(StringComparer.OrdinalIgnoreCase);

        return ProductCache.GetOrAdd(productId, id =>
        {
            var list = FindAll(_.ProductId == id);
            var map = new Dictionary<String, ThingProperty>(list.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var item in list)
            {
                if (item.Identifier.IsNullOrEmpty()) continue;

                map[ThingKey.Qualify(item.FunctionBlockId, item.Identifier)] = item;
            }

            return map;
        });
    }

    /// <summary>Drop one product (or the whole cache) after the thing model changes.</summary>
    public static void InvalidateCache(Int32? productId = null)
    {
        if (productId is > 0) ProductCache.TryRemove(productId.Value, out var unused);
        else ProductCache.Clear();
    }
    #endregion
}

using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.ThingModel;
using NewLife;
using NewLife.Data;
using NewLife.IoT.ThingSpecification;
using NewLife.Security;
using XCode;

namespace IoTPlatform.Services;

/// <summary>产品与物模型管理</summary>
public class ProductService(MqttBrokerCatalog brokers)
{
    #region 产品
    public PagedResult<Product> Query(String? keyword, String? protocol, Int32 pageIndex = 1, Int32 pageSize = 20)
    {
        var exp = new WhereExpression();
        if (!protocol.IsNullOrEmpty()) exp &= Product._.Protocol == protocol;
        if (!keyword.IsNullOrEmpty()) exp &= Product._.Name.Contains(keyword) | Product._.Code.Contains(keyword) | Product._.Category.Contains(keyword);

        var page = new PageParameter { PageIndex = Math.Max(1, pageIndex), PageSize = Math.Clamp(pageSize, 1, 500), Sort = nameof(Product.Id), Desc = false };
        var list = Product.FindAll(exp, page);

        return new PagedResult<Product>(list, page.TotalCount, page.PageIndex, page.PageSize);
    }

    public IList<Product> GetAll() => Product.FindAll(null, nameof(Product.Id), null, 0, 0);

    public Product Get(Int32 id) => Product.FindById(id) ?? throw PlatformException.NotFound("Product");

    public Product Create(ProductInput input)
    {
        Validate(input);

        var code = input.Code.IsNullOrEmpty() ? "p" + Rand.NextString(10, false).ToLowerInvariant() : input.Code.Trim();
        if (Product.FindByCode(code) != null) throw new PlatformException(409, "Product code '{0}' already exists", code);

        var product = new Product { Code = code };
        Apply(product, input);
        product.Insert();

        return product;
    }

    public Product Update(Int32 id, ProductInput input)
    {
        Validate(input);

        var product = Get(id);
        if (!input.Code.IsNullOrEmpty() && !input.Code.EqualIgnoreCase(product.Code))
        {
            if (product.DeviceCount > 0 || Device.FindAllByProductId(product.Id).Count > 0)
                throw new PlatformException(409, "Product code cannot be changed while devices exist (it is part of MQTT topics)");
            if (Product.FindByCode(input.Code) != null) throw new PlatformException(409, "Product code '{0}' already exists", input.Code);

            product.Code = input.Code.Trim();
        }

        Apply(product, input);
        product.Update();

        return product;
    }

    public void Delete(Int32 id)
    {
        var product = Get(id);
        var devices = Device.FindAllByProductId(product.Id).Count;
        if (devices > 0) throw new PlatformException(409, "Product still has {0} device(s); delete or move them first", devices);

        using var tran = Product.Meta.CreateTrans();
        ThingProperty.FindAllByProductId(id).Delete();
        ThingEvent.FindAllByProductId(id).Delete();
        ThingService.FindAllByProductId(id).Delete();
        product.Delete();
        tran.Commit();
        ThingProperty.InvalidateCache(id);
    }

    /// <summary>刷新产品设备数</summary>
    public void RefreshDeviceCount(Int32 productId)
    {
        var product = Product.FindById(productId);
        if (product == null) return;

        product.DeviceCount = (Int32)Device.FindCount(Device._.ProductId == productId);
        product.Update();
    }

    private static void Validate(ProductInput input)
    {
        if (!AccessProtocols.All.Contains(input.Protocol, StringComparer.OrdinalIgnoreCase))
            throw new PlatformException("Unsupported protocol '{0}', expected one of {1}", input.Protocol, String.Join("/", AccessProtocols.All));

        if (input.Protocol.EqualIgnoreCase(AccessProtocols.Driver) && input.DriverName.IsNullOrEmpty())
            throw new PlatformException("DriverName is required for Driver protocol products");

        if (!input.DriverParameter.IsNullOrWhiteSpace() && JsonDefaults.ParseObject(input.DriverParameter) == null)
            throw new PlatformException("DriverParameter must be a JSON object");
    }

    private void Apply(Product product, ProductInput input)
    {
        product.Name = input.Name.Trim();
        product.Category = input.Category;
        product.Protocol = AccessProtocols.All.First(e => e.EqualIgnoreCase(input.Protocol));
        product.Codec = input.Codec.IsNullOrEmpty() ? "Json" : input.Codec;
        product.DriverName = product.Protocol == AccessProtocols.Driver ? input.DriverName : null;
        product.DriverParameter = product.Protocol == AccessProtocols.Driver ? input.DriverParameter : null;
        product.PollInterval = input.PollInterval;
        product.OnlineTimeout = input.OnlineTimeout;
        product.BrokerProfileId = product.Protocol == AccessProtocols.Mqtt ? brokers.Canonicalize(input.BrokerProfileId) : null;
        if (product.Protocol == AccessProtocols.Mqtt) brokers.EnsureKnown(product.BrokerProfileId);
        product.Enable = input.Enable;
        product.Remark = input.Remark;
    }
    #endregion

    #region 物模型
    public IList<ThingProperty> GetProperties(Int32 productId) =>
        ThingProperty.GetCachedByProductId(productId).Values.OrderBy(e => e.Sort).ThenBy(e => e.Id).ToList();

    public IList<ThingEvent> GetEvents(Int32 productId) => ThingEvent.FindAllByProductId(productId).OrderBy(e => e.Sort).ThenBy(e => e.Id).ToList();

    public IList<ThingService> GetServices(Int32 productId) => ThingService.FindAllByProductId(productId).OrderBy(e => e.Sort).ThenBy(e => e.Id).ToList();

    /// <summary>新增或修改属性，id=0 表示新增</summary>
    public ThingProperty SaveProperty(Int32 productId, Int32 id, ThingPropertyInput input)
    {
        Get(productId);
        var entity = id > 0 ? ThingProperty.FindById(id) ?? throw PlatformException.NotFound("Property") : new ThingProperty { ProductId = productId };
        if (entity.ProductId != productId) throw PlatformException.NotFound("Property");

        var block = ThingKey.Block(input.FunctionBlockId);
        TslRules.EnsureIdentifier(input.Identifier);
        TslRules.EnsureName(input.Name);
        TslRules.EnsureBlock(block);
        var exist = ThingProperty.FindByProductIdAndFunctionBlockIdAndIdentifier(productId, block, input.Identifier.Trim());
        if (exist != null && exist.Id != entity.Id) throw new PlatformException(409, "Property '{0}' already exists", ThingKey.Qualify(block, input.Identifier));

        if (input.Max < input.Min) throw new PlatformException("Max must be greater than or equal to Min");
        if (TslTypes.Normalize(input.DataType) == "array" && input.Length > 512)
            throw new PlatformException("Array size must be between 1 and 512");

        entity.Identifier = input.Identifier.Trim();
        entity.Name = input.Name.IsNullOrEmpty() ? entity.Identifier : input.Name.Trim();
        entity.DataType = TslTypes.Canonical(input.DataType);
        entity.Unit = input.Unit;
        entity.UnitName = input.UnitName;
        entity.Min = input.Min;
        entity.Max = input.Max;
        entity.Step = input.Step;
        entity.Length = input.Length;
        entity.AccessMode = TslTypes.CanonicalAccess(input.AccessMode);
        entity.Required = input.Required;
        entity.FunctionBlockId = block;
        entity.Address = input.Address;
        entity.PointType = input.PointType;
        entity.Scaling = input.Scaling;
        entity.Mapping = input.Mapping;
        entity.Specs = BuildPropertySpecs(input, entity.DataType);
        entity.Sort = input.Sort;
        entity.Enable = input.Enable;
        entity.Remark = input.Remark;
        entity.Save();
        RememberBlock(productId, block, input.FunctionBlockName);
        ThingProperty.InvalidateCache(productId);

        return entity;
    }

    public ThingEvent SaveEvent(Int32 productId, Int32 id, ThingEventInput input)
    {
        Get(productId);
        var entity = id > 0 ? ThingEvent.FindById(id) ?? throw PlatformException.NotFound("Event") : new ThingEvent { ProductId = productId };
        if (entity.ProductId != productId) throw PlatformException.NotFound("Event");

        var block = ThingKey.Block(input.FunctionBlockId);
        TslRules.EnsureIdentifier(input.Identifier);
        TslRules.EnsureName(input.Name);
        TslRules.EnsureBlock(block);
        if (input.Identifier.Trim().Equals("property", StringComparison.OrdinalIgnoreCase))
            throw new PlatformException("Event identifier 'property' is reserved by the Alink property topic");

        var exist = ThingEvent.FindByProductIdAndFunctionBlockIdAndIdentifier(productId, block, input.Identifier.Trim());
        if (exist != null && exist.Id != entity.Id) throw new PlatformException(409, "Event '{0}' already exists", ThingKey.Qualify(block, input.Identifier));

        entity.Identifier = input.Identifier.Trim();
        entity.Name = input.Name.IsNullOrEmpty() ? entity.Identifier : input.Name.Trim();
        entity.Type = TslTypes.CanonicalEvent(input.Type);
        entity.Required = input.Required;
        entity.FunctionBlockId = block;
        entity.OutputData = JsonDefaults.Serialize(CleanParameters(input.OutputData));
        entity.Sort = input.Sort;
        entity.Enable = input.Enable;
        entity.Remark = input.Remark;
        entity.Save();
        RememberBlock(productId, block, null);

        return entity;
    }

    public ThingService SaveService(Int32 productId, Int32 id, ThingServiceInput input)
    {
        Get(productId);
        var entity = id > 0 ? ThingService.FindById(id) ?? throw PlatformException.NotFound("Service") : new ThingService { ProductId = productId };
        if (entity.ProductId != productId) throw PlatformException.NotFound("Service");

        var block = ThingKey.Block(input.FunctionBlockId);
        TslRules.EnsureIdentifier(input.Identifier);
        TslRules.EnsureName(input.Name);
        TslRules.EnsureBlock(block);
        var exist = ThingService.FindByProductIdAndFunctionBlockIdAndIdentifier(productId, block, input.Identifier.Trim());
        if (exist != null && exist.Id != entity.Id) throw new PlatformException(409, "Service '{0}' already exists", ThingKey.Qualify(block, input.Identifier));

        entity.Identifier = input.Identifier.Trim();
        entity.Name = input.Name.IsNullOrEmpty() ? entity.Identifier : input.Name.Trim();
        entity.CallType = TslTypes.CanonicalCall(input.CallType);
        entity.Required = input.Required;
        entity.FunctionBlockId = block;
        entity.InputData = JsonDefaults.Serialize(CleanParameters(input.InputData));
        entity.OutputData = JsonDefaults.Serialize(CleanParameters(input.OutputData));
        entity.Sort = input.Sort;
        entity.Enable = input.Enable;
        entity.Remark = input.Remark;
        entity.Save();
        RememberBlock(productId, block, null);

        return entity;
    }

    public void DeleteProperty(Int32 productId, Int32 id)
    {
        DeleteThing(ThingProperty.FindById(id), productId, "Property");
        ThingProperty.InvalidateCache(productId);
    }

    public void DeleteEvent(Int32 productId, Int32 id) => DeleteThing(ThingEvent.FindById(id), productId, "Event");

    public void DeleteService(Int32 productId, Int32 id) => DeleteThing(ThingService.FindById(id), productId, "Service");

    private static void DeleteThing(IEntity? entity, Int32 productId, String what)
    {
        if (entity == null || (Int32)entity[nameof(ThingProperty.ProductId)]! != productId) throw PlatformException.NotFound(what);

        entity.Delete();
    }

    public static List<ThingParameter> ParseParameters(String? json) => JsonDefaults.Deserialize<List<ThingParameter>>(json) ?? [];

    private static List<ThingParameter> CleanParameters(IEnumerable<ThingParameter> items)
    {
        var list = new List<ThingParameter>();
        foreach (var item in items)
        {
            if (item.Identifier.IsNullOrWhiteSpace()) continue;

            TslRules.EnsureIdentifier(item.Identifier, "Parameter");
            TslRules.EnsureName(item.Name);
            item.Identifier = item.Identifier.Trim();
            item.DataType = TslTypes.Canonical(item.DataType);
            if (item.DataType == "array" && !item.ItemType.IsNullOrEmpty())
                item.Specs = TslDocument.StoreSpecs(item.DataType, item.Min, item.Max, item.Step, item.Unit, item.UnitName, item.Length, item.Mapping, item.Specs);
            else if (item.DataType != "struct")
                item.Specs = TslDocument.StoreSpecs(item.DataType, item.Min, item.Max, item.Step, item.Unit, item.UnitName, item.Length, item.Mapping, item.Specs);
            list.Add(item);
        }

        return list;
    }

    private static String? BuildPropertySpecs(ThingPropertyInput input, String dataType)
    {
        if (dataType == "array" && !input.ItemType.IsNullOrEmpty())
        {
            var item = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = TslTypes.Canonical(input.ItemType),
                ["specs"] = new System.Text.Json.Nodes.JsonObject(),
            };
            input.Specs = new System.Text.Json.Nodes.JsonObject { ["size"] = input.Length.ToString(), ["item"] = item }.ToJsonString();
        }

        return TslDocument.StoreSpecs(dataType, input.Min, input.Max, input.Step, input.Unit, input.UnitName, input.Length, input.Mapping, input.Specs);
    }

    private static void RememberBlock(Int32 productId, String block, String? name)
    {
        if (block.Length == 0) return;

        var product = Product.FindById(productId);
        if (product == null) return;

        var blocks = TslDocument.ParseCatalog(product.FunctionBlocks);
        var exist = blocks.FirstOrDefault(e => e.FunctionBlockId.Equals(block, StringComparison.OrdinalIgnoreCase));
        if (exist == null) blocks.Add(new TslFunctionBlock { FunctionBlockId = block, FunctionBlockName = name.IsNullOrEmpty() ? block : name });
        else if (!name.IsNullOrEmpty()) exist.FunctionBlockName = name;
        else return;

        product.FunctionBlocks = TslDocument.StoreCatalog(blocks);
        product.Update();
    }
    #endregion

    #region 物模型导入导出（NewLife.IoT ThingSpec）
    /// <summary>按 NewLife.IoT 物模型规范生成 ThingSpec，可直接交给驱动或导出给其它 NewLife IoT 平台</summary>
    public ThingSpec GetSpecification(Int32 productId)
    {
        var product = Get(productId);
        var spec = new ThingSpec
        {
            Profile = new Profile { ProductKey = product.Code, Version = product.UpdateTime.ToString("yyyyMMddHHmmss") },
            Properties = GetProperties(productId).Where(e => e.Enable).Select(ToPropertySpec).ToArray(),
            Events = GetEvents(productId).Where(e => e.Enable).Select(e => new EventSpec
            {
                Id = e.Identifier,
                Name = e.Name,
                Type = e.Type,
                Description = e.Remark,
                OutputData = ParseParameters(e.OutputData).Select(ToParamSpec).ToArray(),
            }).ToArray(),
            Services = GetServices(productId).Where(e => e.Enable).Select(e => new ServiceSpec
            {
                Id = e.Identifier,
                Name = e.Name,
                Type = e.CallType,
                Description = e.Remark,
                InputData = ParseParameters(e.InputData).Select(ToParamSpec).ToArray(),
                OutputData = ParseParameters(e.OutputData).Select(ToParamSpec).ToArray(),
            }).ToArray(),
        };

        return spec;
    }

    /// <summary>导入 ThingSpec Json，按标识符新增或覆盖</summary>
    public (Int32 Properties, Int32 Events, Int32 Services) ImportSpecification(Int32 productId, String json)
    {
        Get(productId);

        var spec = new ThingSpec();
        try
        {
            spec.FromJson(json);
        }
        catch (Exception ex)
        {
            throw new PlatformException("Invalid ThingSpec JSON: {0}", ex.Message);
        }

        Int32 ps = 0, es = 0, ss = 0;
        foreach (var p in spec.Properties ?? [])
        {
            if (p.Id.IsNullOrEmpty()) continue;

            var old = ThingLookup.Property(productId, p.Id);
            var specs = p.DataType?.Specs;
            SaveProperty(productId, old?.Id ?? 0, new ThingPropertyInput
            {
                Identifier = p.Id,
                Name = p.Name,
                DataType = p.DataType?.Type ?? "string",
                Unit = specs?.Unit,
                Min = specs?.Min ?? 0,
                Max = specs?.Max ?? 0,
                Step = specs?.Step ?? 0,
                Length = specs?.Length ?? 0,
                AccessMode = p.AccessMode ?? "r",
                Address = p.Address,
                Scaling = p.Scaling is 1f or 0f ? 0 : p.Scaling,
                Mapping = specs?.Mapping == null ? null : String.Join(",", specs.Mapping.Select(e => $"{e.Key}={e.Value}")),
                Sort = ps,
                Enable = true,
            });
            ps++;
        }

        foreach (var e in spec.Events ?? [])
        {
            if (e.Id.IsNullOrEmpty()) continue;

            var old = ThingLookup.Event(productId, e.Id);
            SaveEvent(productId, old?.Id ?? 0, new ThingEventInput
            {
                Identifier = e.Id,
                Name = e.Name,
                Type = e.Type ?? "info",
                OutputData = (e.OutputData ?? []).Select(FromParamSpec).ToList(),
                Sort = es,
            });
            es++;
        }

        foreach (var s in spec.Services ?? [])
        {
            if (s.Id.IsNullOrEmpty()) continue;

            var old = ThingLookup.Service(productId, s.Id);
            SaveService(productId, old?.Id ?? 0, new ThingServiceInput
            {
                Identifier = s.Id,
                Name = s.Name,
                CallType = s.Type ?? "async",
                InputData = (s.InputData ?? []).Select(FromParamSpec).ToList(),
                OutputData = (s.OutputData ?? []).Select(FromParamSpec).ToList(),
                Sort = ss,
            });
            ss++;
        }

        return (ps, es, ss);
    }

    /// <summary>导出阿里云 TSL JSON。block 为空导出默认模块；full 为 true 时把自定义模块功能嵌进 functionBlocks</summary>
    public String ExportTsl(Int32 productId, String? block = null, Boolean full = false)
    {
        var product = Get(productId);
        return TslDocument.Export(product, GetProperties(productId), GetEvents(productId), GetServices(productId), block, full);
    }

    /// <summary>导入物模型 JSON。阿里云 TSL 与 NewLife.IoT ThingSpec 都能识别，按模块和标识符新增或覆盖</summary>
    public (Int32 Properties, Int32 Events, Int32 Services) ImportModel(Int32 productId, String json)
    {
        if (json.IsNullOrWhiteSpace()) throw new PlatformException("Invalid TSL JSON: {0}", "empty");

        try
        {
            if (TslDocument.LooksLikeAliyun(json)) return ImportTsl(productId, TslDocument.Parse(json));
        }
        catch (PlatformException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not PlatformException)
        {
            throw new PlatformException("Invalid TSL JSON: {0}", ex.Message);
        }

        return ImportSpecification(productId, json);
    }

    private (Int32 Properties, Int32 Events, Int32 Services) ImportTsl(Int32 productId, TslImport model)
    {
        var root = ThingKey.Block(model.FunctionBlockId);
        if (root.Length > 0) RememberBlock(productId, root, model.FunctionBlockName);

        foreach (var block in model.FunctionBlocks)
            RememberBlock(productId, block.FunctionBlockId, block.FunctionBlockName);

        var count = ImportFeatures(productId, root, model.Properties, model.Events, model.Services);
        foreach (var block in model.FunctionBlocks)
        {
            var more = ImportFeatures(productId, ThingKey.Block(block.FunctionBlockId), block.Properties, block.Events, block.Services);
            count = (count.Properties + more.Properties, count.Events + more.Events, count.Services + more.Services);
        }

        return count;
    }

    private (Int32 Properties, Int32 Events, Int32 Services) ImportFeatures(Int32 productId, String block, IList<ThingPropertyInput> properties, IList<ThingEventInput> events, IList<ThingServiceInput> services)
    {
        Int32 ps = 0, es = 0, ss = 0;
        foreach (var input in properties)
        {
            input.FunctionBlockId = block;
            var old = ThingProperty.FindByProductIdAndFunctionBlockIdAndIdentifier(productId, block, input.Identifier);
            if (old != null)
            {
                input.Address ??= old.Address;
                input.PointType ??= old.PointType;
                if (input.Scaling == 0) input.Scaling = old.Scaling;
                input.Sort = old.Sort;
            }
            else input.Sort = ps;

            SaveProperty(productId, old?.Id ?? 0, input);
            ps++;
        }

        foreach (var input in events)
        {
            input.FunctionBlockId = block;
            var old = ThingEvent.FindByProductIdAndFunctionBlockIdAndIdentifier(productId, block, input.Identifier);
            input.Sort = old?.Sort ?? es;
            SaveEvent(productId, old?.Id ?? 0, input);
            es++;
        }

        foreach (var input in services)
        {
            input.FunctionBlockId = block;
            var old = ThingService.FindByProductIdAndFunctionBlockIdAndIdentifier(productId, block, input.Identifier);
            input.Sort = old?.Sort ?? ss;
            SaveService(productId, old?.Id ?? 0, input);
            ss++;
        }

        return (ps, es, ss);
    }

    public static PropertySpec ToPropertySpec(ThingProperty p)
    {
        var spec = PropertySpec.Create(p.Identifier, p.Name ?? p.Identifier, p.DataType ?? "string", p.Length, p.Address);
        spec.AccessMode = p.AccessMode;
        if (p.Scaling != 0 && p.Scaling != 1) spec.Scaling = (Single)p.Scaling;

        var specs = spec.DataType!.Specs ??= new DataSpecs();
        specs.Min = p.Min;
        specs.Max = p.Max;
        specs.Step = p.Step;
        specs.Unit = p.Unit;
        var mapping = ParseMapping(p.Mapping);
        if (mapping.Count > 0) specs.Mapping = mapping;

        return spec;
    }

    /// <summary>解析枚举映射 "0=关,1=开"</summary>
    public static IDictionary<String, String> ParseMapping(String? mapping)
    {
        var dic = new Dictionary<String, String>();
        if (mapping.IsNullOrWhiteSpace()) return dic;

        foreach (var item in mapping.Split(',', ';'))
        {
            var kv = item.Split('=', 2);
            if (kv.Length == 2 && !kv[0].Trim().IsNullOrEmpty()) dic[kv[0].Trim()] = kv[1].Trim();
        }

        return dic;
    }

    private static PropertySpec ToParamSpec(ThingParameter p) => PropertySpec.Create(p.Identifier, p.Name ?? p.Identifier, p.DataType);

    private static ThingParameter FromParamSpec(PropertySpec p) => new() { Identifier = p.Id, Name = p.Name, DataType = p.DataType?.Type ?? "string" };
    #endregion
}

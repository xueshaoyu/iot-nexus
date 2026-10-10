using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Services;
using NewLife;

namespace IoTPlatform.ThingModel;

/// <summary>自定义模块</summary>
public sealed class TslFunctionBlock
{
    public String FunctionBlockId { get; set; } = "";

    public String? FunctionBlockName { get; set; }

    public List<ThingPropertyInput> Properties { get; set; } = [];

    public List<ThingEventInput> Events { get; set; } = [];

    public List<ThingServiceInput> Services { get; set; } = [];
}

/// <summary>一次 TSL 导入的内容。根上的功能属于 <see cref="FunctionBlockId"/>（空为默认模块）</summary>
public sealed class TslImport
{
    public String? ProductKey { get; set; }

    public String? FunctionBlockId { get; set; }

    public String? FunctionBlockName { get; set; }

    public List<ThingPropertyInput> Properties { get; set; } = [];

    public List<ThingEventInput> Events { get; set; } = [];

    public List<ThingServiceInput> Services { get; set; } = [];

    public List<TslFunctionBlock> FunctionBlocks { get; set; } = [];
}

/// <summary>阿里云物模型 TSL JSON 的解析与导出。可直接粘贴控制台导出的 JSON</summary>
public static class TslDocument
{
    public const String Schema = "https://iotx-tsl.oss-ap-southeast-1.aliyuncs.com/schema.json";

    /// <summary>是否像阿里云 TSL。NewLife ThingSpec 也使用 identifier，靠 schema 和 specs.mapping / address 区分</summary>
    public static Boolean LooksLikeAliyun(String json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return false;

        var schema = Text(root, "schema") ?? "";
        if (schema.Contains("feifan", StringComparison.OrdinalIgnoreCase)) return false;
        if (schema.Contains("iotx-tsl", StringComparison.OrdinalIgnoreCase) || schema.Contains("aliyuncs.com", StringComparison.OrdinalIgnoreCase))
            return true;
        if (root.TryGetProperty("functionBlockId", out _) || root.TryGetProperty("functionBlocks", out _))
            return true;
        if (HasThingSpecMarker(root)) return false;

        return HasIdentifier(root, "properties") || HasIdentifier(root, "events") || HasIdentifier(root, "services");
    }

    public static TslImport Parse(String json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new PlatformException("Invalid TSL JSON: {0}", ex.Message);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new PlatformException("Invalid TSL JSON: {0}", "root must be an object");

            var model = new TslImport
            {
                ProductKey = ReadProfileKey(root),
                FunctionBlockId = Text(root, "functionBlockId"),
                FunctionBlockName = Text(root, "functionBlockName"),
            };

            ReadFeatures(root, ThingKey.Block(model.FunctionBlockId), model.Properties, model.Events, model.Services);

            if (root.TryGetProperty("functionBlocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in blocks.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object) continue;

                    var id = Text(block, "functionBlockId");
                    if (id.IsNullOrEmpty()) continue;

                    var item = new TslFunctionBlock
                    {
                        FunctionBlockId = id,
                        FunctionBlockName = Text(block, "functionBlockName"),
                    };
                    ReadFeatures(block, id, item.Properties, item.Events, item.Services);
                    model.FunctionBlocks.Add(item);
                }
            }

            return model;
        }
    }

    /// <summary>导出阿里云 TSL。blockId 指定自定义模块；includeBlocks 把模块功能嵌进 functionBlocks，便于本平台往返</summary>
    public static String Export(Product product, IList<ThingProperty> properties, IList<ThingEvent> events, IList<ThingService> services, String? blockId, Boolean includeBlocks)
    {
        var block = ThingKey.Block(blockId);
        var root = new JsonObject
        {
            ["schema"] = Schema,
            ["profile"] = new JsonObject { ["productKey"] = product.Code },
        };

        if (block.Length > 0)
        {
            root["functionBlockId"] = block;
            root["functionBlockName"] = BlockName(product, block) ?? block;
        }

        root["properties"] = new JsonArray(properties.Where(e => ThingKey.Block(e.FunctionBlockId) == block).Select(ToProperty).ToArray<JsonNode?>());
        root["events"] = new JsonArray(events.Where(e => ThingKey.Block(e.FunctionBlockId) == block).Select(ToEvent).ToArray<JsonNode?>());
        root["services"] = new JsonArray(services.Where(e => ThingKey.Block(e.FunctionBlockId) == block).Select(ToService).ToArray<JsonNode?>());

        var catalog = FunctionBlocks(product, properties, events, services);
        if (block.Length == 0 && catalog.Count > 0)
        {
            root["functionBlocks"] = new JsonArray(catalog.Select(b =>
            {
                var node = new JsonObject
                {
                    ["productKey"] = product.Code,
                    ["functionBlockId"] = b.Id,
                    ["functionBlockName"] = b.Name ?? b.Id,
                };
                if (includeBlocks)
                {
                    node["properties"] = new JsonArray(properties.Where(e => ThingKey.Block(e.FunctionBlockId).Equals(b.Id, StringComparison.OrdinalIgnoreCase)).Select(ToProperty).ToArray<JsonNode?>());
                    node["events"] = new JsonArray(events.Where(e => ThingKey.Block(e.FunctionBlockId).Equals(b.Id, StringComparison.OrdinalIgnoreCase)).Select(ToEvent).ToArray<JsonNode?>());
                    node["services"] = new JsonArray(services.Where(e => ThingKey.Block(e.FunctionBlockId).Equals(b.Id, StringComparison.OrdinalIgnoreCase)).Select(ToService).ToArray<JsonNode?>());
                }

                return (JsonNode?)node;
            }).ToArray());
        }

        return root.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true });
    }

    public static List<(String Id, String? Name)> FunctionBlocks(Product product, IEnumerable<ThingProperty> properties, IEnumerable<ThingEvent> events, IEnumerable<ThingService> services)
    {
        var map = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ParseCatalog(product.FunctionBlocks))
            map[item.FunctionBlockId] = item.FunctionBlockName;

        foreach (var id in properties.Select(e => ThingKey.Block(e.FunctionBlockId))
            .Concat(events.Select(e => ThingKey.Block(e.FunctionBlockId)))
            .Concat(services.Select(e => ThingKey.Block(e.FunctionBlockId))))
        {
            if (id.Length > 0 && !map.ContainsKey(id)) map[id] = id;
        }

        return map.Select(e => (e.Key, e.Value)).ToList();
    }

    public static List<TslFunctionBlock> ParseCatalog(String? json)
    {
        if (json.IsNullOrWhiteSpace()) return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            var list = new List<TslFunctionBlock>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var id = Text(item, "functionBlockId") ?? Text(item, "FunctionBlockId");
                if (id.IsNullOrEmpty()) continue;

                list.Add(new TslFunctionBlock { FunctionBlockId = id, FunctionBlockName = Text(item, "functionBlockName") ?? Text(item, "FunctionBlockName") });
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static String StoreCatalog(IEnumerable<TslFunctionBlock> blocks) =>
        JsonSerializer.Serialize(blocks
            .Where(e => !e.FunctionBlockId.IsNullOrWhiteSpace())
            .GroupBy(e => e.FunctionBlockId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Dictionary<String, String?> { ["functionBlockId"] = g.Key, ["functionBlockName"] = g.Last().FunctionBlockName ?? g.Key })
            .ToList(), JsonDefaults.Options);

    public static JsonNode ToProperty(ThingProperty p)
    {
        var node = new JsonObject
        {
            ["identifier"] = p.Identifier,
            ["name"] = p.Name ?? p.Identifier,
            ["accessMode"] = TslTypes.CanonicalAccess(p.AccessMode is "r" or "w" or "rw" ? p.AccessMode : "r"),
            ["required"] = p.Required,
            ["dataType"] = DataTypeNode(TslExportType(p.DataType), SpecsNode(p)),
        };
        if (!p.Remark.IsNullOrEmpty()) node["desc"] = p.Remark;

        return node;
    }

    public static JsonNode ToEvent(ThingEvent e)
    {
        var node = new JsonObject
        {
            ["identifier"] = e.Identifier,
            ["name"] = e.Name ?? e.Identifier,
            ["desc"] = e.Remark ?? "",
            ["type"] = TslTypes.CanonicalEvent(e.Type),
            ["required"] = e.Required,
            ["method"] = $"thing.event.{e.Identifier}.post",
            ["outputData"] = new JsonArray(ProductService.ParseParameters(e.OutputData).Select(ToParameter).ToArray<JsonNode?>()),
        };
        return node;
    }

    public static JsonNode ToService(ThingService s)
    {
        return new JsonObject
        {
            ["identifier"] = s.Identifier,
            ["name"] = s.Name ?? s.Identifier,
            ["desc"] = s.Remark ?? "",
            ["required"] = s.Required,
            ["callType"] = TslTypes.CanonicalCall(s.CallType),
            ["method"] = $"thing.service.{s.Identifier}",
            ["inputData"] = new JsonArray(ProductService.ParseParameters(s.InputData).Select(ToParameter).ToArray<JsonNode?>()),
            ["outputData"] = new JsonArray(ProductService.ParseParameters(s.OutputData).Select(ToParameter).ToArray<JsonNode?>()),
        };
    }

    public static JsonNode ToParameter(ThingParameter p) => new JsonObject
    {
        ["identifier"] = p.Identifier,
        ["name"] = p.Name ?? p.Identifier,
        ["dataType"] = DataTypeNode(TslExportType(p.DataType), SpecsNode(p)),
    };

    /// <summary>由属性列和 Specs 还原 TSL specs 节点</summary>
    public static JsonNode SpecsNode(ThingProperty p)
    {
        var type = TslTypes.Normalize(p.DataType);
        if ((type is "struct" or "array") && !String.IsNullOrWhiteSpace(p.Specs))
        {
            try { return JsonNode.Parse(p.Specs!) ?? new JsonObject(); }
            catch (JsonException) { }
        }

        return SpecsNode(type, p.Min, p.Max, p.Step, p.Unit, p.UnitName, p.Length, p.Mapping, p.Specs);
    }

    public static JsonNode SpecsNode(ThingParameter p)
    {
        var type = TslTypes.Normalize(p.DataType);
        if ((type is "struct" or "array") && !String.IsNullOrWhiteSpace(p.Specs))
        {
            try { return JsonNode.Parse(p.Specs!) ?? new JsonObject(); }
            catch (JsonException) { }
        }

        return SpecsNode(type, p.Min, p.Max, p.Step, p.Unit, p.UnitName, p.Length, p.Mapping, p.Specs);
    }

    /// <summary>把界面上的简单规格写回 Specs 列。struct/array 保留调用方给出的 JSON</summary>
    public static String? StoreSpecs(String? dataType, Double min, Double max, Double step, String? unit, String? unitName, Int32 length, String? mapping, String? specs)
    {
        var type = TslTypes.Normalize(dataType);
        if (type is "struct" or "array")
        {
            if (specs.IsNullOrWhiteSpace()) return type == "array" ? SpecsNode(type, min, max, step, unit, unitName, length, mapping, null).ToJsonString() : "[]";

            return specs;
        }

        var node = SpecsNode(type, min, max, step, unit, unitName, length, mapping, null);
        return node.ToJsonString();
    }

    private static JsonNode SpecsNode(String type, Double min, Double max, Double step, String? unit, String? unitName, Int32 length, String? mapping, String? specs)
    {
        switch (type)
        {
            case "int":
            case "float":
            case "double":
                var num = new JsonObject();
                if (HasRange(min, max))
                {
                    num["min"] = Num(min);
                    num["max"] = Num(max);
                }
                if (step > 0) num["step"] = Num(step);
                if (!unit.IsNullOrEmpty()) num["unit"] = unit;
                if (!unitName.IsNullOrEmpty()) num["unitName"] = unitName;
                return num;
            case "text":
                var text = new JsonObject { ["length"] = length > 0 ? length.ToString(CultureInfo.InvariantCulture) : "10240" };
                return text;
            case "date":
                return new JsonObject();
            case "bool":
            case "enum":
                var map = new JsonObject();
                foreach (var (k, v) in ProductService.ParseMapping(mapping)) map[k] = v;
                if (map.Count == 0 && type == "bool")
                {
                    map["0"] = "0";
                    map["1"] = "1";
                }
                return map;
            case "array":
                var itemType = "text";
                JsonNode? itemSpecs = null;
                if (!specs.IsNullOrWhiteSpace())
                {
                    try
                    {
                        var parsed = JsonNode.Parse(specs!);
                        if (parsed is JsonObject obj && obj["item"] is JsonObject item)
                        {
                            itemType = item["type"]?.ToString() ?? itemType;
                            itemSpecs = item["specs"];
                        }
                    }
                    catch (JsonException) { }
                }
                var array = new JsonObject { ["size"] = length > 0 ? length.ToString(CultureInfo.InvariantCulture) : "10" };
                array["item"] = new JsonObject { ["type"] = TslExportType(itemType), ["specs"] = itemSpecs ?? new JsonObject() };
                return array;
            default:
                return new JsonObject();
        }
    }

    private static void ReadFeatures(JsonElement root, String block, List<ThingPropertyInput> properties, List<ThingEventInput> events, List<ThingServiceInput> services)
    {
        if (root.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in props.EnumerateArray())
            {
                var id = Text(p, "identifier");
                if (id.IsNullOrEmpty()) continue;

                var (type, spec) = ReadDataType(p);
                properties.Add(new ThingPropertyInput
                {
                    Identifier = id,
                    Name = Text(p, "name"),
                    AccessMode = Text(p, "accessMode") ?? "r",
                    Required = Bool(p, "required"),
                    DataType = type,
                    Unit = spec.Unit,
                    UnitName = spec.UnitName,
                    Min = spec.Min,
                    Max = spec.Max,
                    Step = spec.Step,
                    Length = spec.Length,
                    Mapping = spec.Mapping,
                    Specs = spec.Raw,
                    ItemType = spec.ItemType,
                    FunctionBlockId = block,
                    Remark = Text(p, "desc"),
                    Enable = true,
                });
            }
        }

        if (root.TryGetProperty("events", out var evs) && evs.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in evs.EnumerateArray())
            {
                var id = Text(e, "identifier");
                if (id.IsNullOrEmpty() || id.Equals("post", StringComparison.OrdinalIgnoreCase) && (Text(e, "method") ?? "").Contains("property.post", StringComparison.OrdinalIgnoreCase))
                    continue;

                events.Add(new ThingEventInput
                {
                    Identifier = id,
                    Name = Text(e, "name"),
                    Type = Text(e, "type") ?? "info",
                    Required = Bool(e, "required"),
                    FunctionBlockId = block,
                    Remark = Text(e, "desc"),
                    OutputData = ReadParameters(e, "outputData"),
                    Enable = true,
                });
            }
        }

        if (root.TryGetProperty("services", out var svs) && svs.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in svs.EnumerateArray())
            {
                var id = Text(s, "identifier");
                if (id.IsNullOrEmpty() || id.Equals("set", StringComparison.OrdinalIgnoreCase) || id.Equals("get", StringComparison.OrdinalIgnoreCase))
                    continue;

                services.Add(new ThingServiceInput
                {
                    Identifier = id,
                    Name = Text(s, "name"),
                    CallType = Text(s, "callType") ?? "async",
                    Required = Bool(s, "required"),
                    FunctionBlockId = block,
                    Remark = Text(s, "desc"),
                    InputData = ReadParameters(s, "inputData"),
                    OutputData = ReadParameters(s, "outputData"),
                    Enable = true,
                });
            }
        }
    }

    private static List<ThingParameter> ReadParameters(JsonElement owner, String name)
    {
        var list = new List<ThingParameter>();
        if (!owner.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in arr.EnumerateArray())
        {
            var id = Text(item, "identifier");
            if (id.IsNullOrEmpty()) continue;

            var (type, spec) = ReadDataType(item);
            list.Add(new ThingParameter
            {
                Identifier = id,
                Name = Text(item, "name"),
                DataType = type,
                Min = spec.Min,
                Max = spec.Max,
                Step = spec.Step,
                Unit = spec.Unit,
                UnitName = spec.UnitName,
                Length = spec.Length,
                Mapping = spec.Mapping,
                ItemType = spec.ItemType,
                Specs = spec.Raw,
            });
        }

        return list;
    }

    private readonly record struct SpecBits(Double Min, Double Max, Double Step, String? Unit, String? UnitName, Int32 Length, String? Mapping, String? ItemType, String? Raw);

    private static (String Type, SpecBits Spec) ReadDataType(JsonElement owner)
    {
        if (!owner.TryGetProperty("dataType", out var dataType)) return ("text", default);

        if (dataType.ValueKind == JsonValueKind.String) return (TslTypes.Canonical(dataType.GetString()), default);

        var type = dataType.TryGetProperty("type", out var t) ? TslTypes.Canonical(t.GetString()) : "text";
        if (!dataType.TryGetProperty("specs", out var specs)) return (type, default);

        var raw = specs.GetRawText();
        if (specs.ValueKind == JsonValueKind.Array) return (type, new SpecBits(0, 0, 0, null, null, 0, null, null, raw));
        if (specs.ValueKind != JsonValueKind.Object) return (type, new SpecBits(0, 0, 0, null, null, 0, null, null, type is "struct" or "array" ? raw : null));

        var min = Num(specs, "min");
        var max = Num(specs, "max");
        var step = Num(specs, "step");
        var unit = Text(specs, "unit");
        var unitName = Text(specs, "unitName");
        var length = specs.TryGetProperty("length", out _) ? (Int32)Num(specs, "length") : (Int32)Num(specs, "size");
        if (type == "array" && specs.TryGetProperty("size", out _)) length = (Int32)Num(specs, "size");

        String? itemType = null;
        if (specs.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.Object)
            itemType = item.TryGetProperty("type", out var it) ? it.GetString() : null;

        var mapping = new List<String>();
        if (type is "bool" or "enum")
        {
            foreach (var p in specs.EnumerateObject())
            {
                if (p.Name is "min" or "max" or "step" or "unit" or "unitName" or "length" or "size" or "item") continue;
                mapping.Add($"{p.Name}={JsonText(p.Value)}");
            }
        }

        return (type, new SpecBits(min, max, step, unit, unitName, length, mapping.Count == 0 ? null : String.Join(",", mapping), itemType, type is "struct" or "array" ? raw : null));
    }

    private static JsonObject DataTypeNode(String type, JsonNode specs) => new() { ["type"] = type, ["specs"] = specs };

    private static String TslExportType(String? type)
    {
        var normalized = TslTypes.Normalize(type);
        return normalized is "int" or "float" or "double" or "text" or "date" or "bool" or "enum" or "struct" or "array" ? normalized : "text";
    }

    private static Boolean HasRange(Double min, Double max) => max > min || min != 0 || max != 0;

    private static String Num(Double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static Double Num(JsonElement obj, String name)
    {
        if (!obj.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        if (v.ValueKind == JsonValueKind.String && Double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;

        return 0;
    }

    private static String? Text(JsonElement obj, String name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static Boolean Bool(JsonElement obj, String name)
    {
        if (!obj.TryGetProperty(name, out var v)) return false;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => v.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true,
            _ => false,
        };
    }

    private static String JsonText(JsonElement v) => v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText().Trim('"');

    private static String? ReadProfileKey(JsonElement root)
    {
        if (root.TryGetProperty("profile", out var profile) && profile.ValueKind == JsonValueKind.Object)
            return Text(profile, "productKey");

        return Text(root, "productKey");
    }

    private static String? BlockName(Product product, String block) =>
        ParseCatalog(product.FunctionBlocks).FirstOrDefault(e => e.FunctionBlockId.Equals(block, StringComparison.OrdinalIgnoreCase))?.FunctionBlockName;

    /// <summary>NewLife.IoT ThingSpec 的特有字段。阿里云 bool/enum 的规格是扁平映射，不会出现 specs.mapping</summary>
    private static Boolean HasThingSpecMarker(JsonElement root)
    {
        foreach (var array in new[] { "properties", "events", "services" })
        {
            if (!root.TryGetProperty(array, out var items) || items.ValueKind != JsonValueKind.Array) continue;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (item.TryGetProperty("address", out _) || item.TryGetProperty("scaling", out _)) return true;
                if (!item.TryGetProperty("dataType", out var dataType) || dataType.ValueKind != JsonValueKind.Object) continue;
                if (!dataType.TryGetProperty("specs", out var specs) || specs.ValueKind != JsonValueKind.Object) continue;
                if (specs.TryGetProperty("mapping", out _) || specs.TryGetProperty("order", out _)) return true;
            }
        }

        return false;
    }

    private static Boolean HasIdentifier(JsonElement root, String array)
    {
        if (!root.TryGetProperty(array, out var items) || items.ValueKind != JsonValueKind.Array) return false;

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("identifier", out _)) return true;
        }

        return false;
    }
}

using System.Collections;
using System.Globalization;
using System.Text.Json;
using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Services;
using NewLife;

namespace IoTPlatform.ThingModel;

/// <summary>按物模型校验上报值和下发值：类型、取值范围、长度、枚举、struct 与 array</summary>
public static class TslValueChecker
{
    public readonly record struct Result(Boolean Ok, Object? Value, String? Error);

    public static Result Check(ThingProperty? definition, Object? raw)
    {
        var value = ThingValueConverter.Convert(definition, raw);
        if (definition == null) return new(true, value, null);

        return CheckCore(TslTypes.Normalize(definition.DataType), value, definition.Min, definition.Max, definition.Length, definition.Mapping, definition.Specs);
    }

    public static Result Check(ThingParameter parameter, Object? raw)
    {
        var shadow = new ThingProperty
        {
            DataType = parameter.DataType,
            Min = parameter.Min,
            Max = parameter.Max,
            Step = parameter.Step,
            Length = parameter.Length,
            Mapping = parameter.Mapping,
            Specs = parameter.Specs,
            Enable = true,
        };
        return Check(shadow, raw);
    }

    /// <summary>把 struct 展开成 identifier.child，供规则条件引用</summary>
    public static void Flatten(IDictionary<String, Object?> target, String prefix, Object? value, Int32 depth = 0)
    {
        target[prefix] = value;
        if (depth >= 3 || value is not IDictionary<String, Object?> dic) return;

        foreach (var (key, child) in dic)
        {
            if (key.IsNullOrWhiteSpace()) continue;

            Flatten(target, prefix + "." + key, child, depth + 1);
        }
    }

    private static Result CheckCore(String type, Object? value, Double min, Double max, Int32 length, String? mapping, String? specs)
    {
        if (value == null) return new(true, null, null);

        switch (type)
        {
            case "int":
                if (!ThingValueConverter.TryDouble(value, out var i)) return Fail("not a number");
                var n = (Int64)Math.Round(i);
                if (n is < Int32.MinValue or > Int32.MaxValue) return Fail("out of int32 range");
                if (OutOfRange(n, min, max)) return Fail("out of range");
                return new(true, n, null);
            case "float":
            case "double":
                if (!ThingValueConverter.TryDouble(value, out var d)) return Fail("not a number");
                if (OutOfRange(d, min, max)) return Fail("out of range");
                return new(true, value, null);
            case "bool":
                if (value is not Boolean) return Fail("not a boolean");
                return new(true, value, null);
            case "enum":
                var text = JsonDefaults.ToText(value) ?? "";
                var map = ProductService.ParseMapping(mapping);
                if (map.Count > 0 && !map.Keys.Any(k => k.Equals(text, StringComparison.OrdinalIgnoreCase)))
                    return Fail("enum value is not defined");
                return new(true, value, null);
            case "text":
                var s = JsonDefaults.ToText(value) ?? "";
                var maxLen = length > 0 ? length : 10240;
                if (s.Length > maxLen) return Fail($"text longer than {maxLen}");
                return new(true, s, null);
            case "date":
                if (!ThingValueConverter.TryDateMillis(value, out _)) return Fail("not a valid date");
                return new(true, value, null);
            case "struct":
                if (value is not IDictionary<String, Object?> fields) return Fail("expected object");
                var children = ReadStruct(specs);
                if (children.Count == 0) return new(true, fields, null);
                var known = children.ToDictionary(e => e.Identifier, StringComparer.OrdinalIgnoreCase);
                foreach (var key in fields.Keys)
                {
                    if (!known.ContainsKey(key)) return Fail($"unknown field {key}");
                }
                var copy = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, child) in fields)
                {
                    var check = Check(known[key], child);
                    if (!check.Ok) return Fail($"{key}: {check.Error}");
                    copy[key] = check.Value;
                }
                return new(true, copy, null);
            case "array":
                if (value is not IList list) return Fail("expected array");
                var size = length > 0 ? length : ReadArraySize(specs);
                if (size > 512) size = 512;
                if (size > 0 && list.Count > size) return Fail($"array size exceeds {size}");
                var item = ReadItem(specs);
                if (item == null) return new(true, list, null);
                var items = new List<Object?>();
                foreach (var element in list)
                {
                    var check = Check(item, element);
                    if (!check.Ok) return Fail(check.Error);
                    items.Add(check.Value);
                }
                return new(true, items, null);
            default:
                return new(true, value, null);
        }
    }

    private static Boolean OutOfRange(Double value, Double min, Double max)
    {
        if (max == 0 && min == 0) return false;
        if (max < min) return false;

        return value < min || value > max;
    }

    private static Result Fail(String? error) => new(false, null, error);

    private static List<ThingParameter> ReadStruct(String? specs)
    {
        if (specs.IsNullOrWhiteSpace()) return [];

        try
        {
            using var doc = JsonDocument.Parse(specs);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            var list = new List<ThingParameter>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var id = item.TryGetProperty("identifier", out var ident) ? ident.GetString() : null;
                if (id.IsNullOrEmpty()) continue;

                var parameter = new ThingParameter { Identifier = id!, Name = item.TryGetProperty("name", out var name) ? name.GetString() : id };
                if (item.TryGetProperty("dataType", out var dataType) && dataType.ValueKind == JsonValueKind.Object)
                {
                    parameter.DataType = dataType.TryGetProperty("type", out var t) ? t.GetString() ?? "text" : "text";
                    if (dataType.TryGetProperty("specs", out var child)) parameter.Specs = child.GetRawText();
                    ApplyFlat(parameter);
                }
                list.Add(parameter);
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Int32 ReadArraySize(String? specs)
    {
        if (specs.IsNullOrWhiteSpace()) return 0;

        try
        {
            using var doc = JsonDocument.Parse(specs);
            if (doc.RootElement.TryGetProperty("size", out var size))
            {
                if (size.ValueKind == JsonValueKind.Number) return size.GetInt32();
                if (size.ValueKind == JsonValueKind.String && Int32.TryParse(size.GetString(), out var n)) return n;
            }
        }
        catch (JsonException) { }

        return 0;
    }

    private static ThingParameter? ReadItem(String? specs)
    {
        if (specs.IsNullOrWhiteSpace()) return null;

        try
        {
            using var doc = JsonDocument.Parse(specs);
            if (!doc.RootElement.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) return null;

            var parameter = new ThingParameter
            {
                Identifier = "item",
                DataType = item.TryGetProperty("type", out var t) ? t.GetString() ?? "text" : "text",
            };
            if (item.TryGetProperty("specs", out var child)) parameter.Specs = child.GetRawText();
            ApplyFlat(parameter);
            return parameter;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>struct/array 子规格里的 min/max/length/枚举写回参数，便于递归校验</summary>
    private static void ApplyFlat(ThingParameter parameter)
    {
        if (parameter.Specs.IsNullOrWhiteSpace()) return;

        try
        {
            using var doc = JsonDocument.Parse(parameter.Specs);
            var specs = doc.RootElement;
            if (specs.ValueKind == JsonValueKind.Array) return;
            if (specs.ValueKind != JsonValueKind.Object) return;

            parameter.Min = ReadNum(specs, "min");
            parameter.Max = ReadNum(specs, "max");
            parameter.Step = ReadNum(specs, "step");
            if (specs.TryGetProperty("length", out var length)) parameter.Length = (Int32)ReadNum(specs, "length");
            else if (specs.TryGetProperty("size", out _)) parameter.Length = (Int32)ReadNum(specs, "size");
            parameter.Unit = specs.TryGetProperty("unit", out var unit) && unit.ValueKind == JsonValueKind.String ? unit.GetString() : parameter.Unit;

            if (TslTypes.Normalize(parameter.DataType) is "bool" or "enum")
            {
                var pairs = new List<String>();
                foreach (var p in specs.EnumerateObject())
                {
                    if (p.Name is "min" or "max" or "step" or "unit" or "unitName" or "length" or "size" or "item") continue;
                    pairs.Add($"{p.Name}={(p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText())}");
                }
                if (pairs.Count > 0) parameter.Mapping = String.Join(",", pairs);
            }
        }
        catch (JsonException) { }
    }

    private static Double ReadNum(JsonElement specs, String name)
    {
        if (!specs.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        if (v.ValueKind == JsonValueKind.String && Double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;

        return 0;
    }
}

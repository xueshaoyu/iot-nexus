namespace IoTPlatform.Data;

/// <summary>物模型标识。默认模块只用 identifier，自定义模块用 <c>functionBlockId:identifier</c></summary>
public static class ThingKey
{
    /// <summary>空模块归一成空字符串，表示默认模块</summary>
    public static String Block(String? functionBlockId) => functionBlockId?.Trim() ?? "";

    /// <summary>上报、影子和规则里使用的键</summary>
    public static String Qualify(String? functionBlockId, String identifier)
    {
        var block = Block(functionBlockId);
        return block.Length == 0 ? identifier : $"{block}:{identifier}";
    }

    /// <summary>把 <c>block:identifier</c> 拆开。没有冒号时视为默认模块</summary>
    public static (String Block, String Identifier) Split(String? key)
    {
        if (String.IsNullOrWhiteSpace(key)) return ("", "");

        var text = key.Trim();
        var i = text.IndexOf(':');
        if (i <= 0 || i >= text.Length - 1) return ("", text);

        return (text[..i].Trim(), text[(i + 1)..].Trim());
    }
}

/// <summary>按默认模块或 <c>block:identifier</c> 查找物模型功能</summary>
public static class ThingLookup
{
    public static ThingProperty? Property(Int32 productId, String? key)
    {
        var (block, id) = ThingKey.Split(key);
        return ThingProperty.FindByProductIdAndFunctionBlockIdAndIdentifier(productId, ThingKey.Block(block), id);
    }

    public static ThingEvent? Event(Int32 productId, String? key)
    {
        var (block, id) = ThingKey.Split(key);
        return ThingEvent.FindByProductIdAndFunctionBlockIdAndIdentifier(productId, ThingKey.Block(block), id);
    }

    public static ThingService? Service(Int32 productId, String? key)
    {
        var (block, id) = ThingKey.Split(key);
        return ThingService.FindByProductIdAndFunctionBlockIdAndIdentifier(productId, ThingKey.Block(block), id);
    }
}

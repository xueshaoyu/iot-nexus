using System.Globalization;

namespace IoTPlatform.Protocols.Drivers;

/// <summary>寄存器位域。从点位地址的后缀解析，驱动仍按整寄存器读写</summary>
/// <remarks>
/// <para>第 0 位是最低位。地址示例：</para>
/// <para><c>4x10.3</c> 或 <c>4x10:3</c>：第 3 位，宽度 1。</para>
/// <para><c>4x10.0:4</c>：从第 0 位起的 4 位。</para>
/// <para><c>4x10.0-3</c>：第 0 位到第 3 位，含两端。</para>
/// </remarks>
public readonly record struct BitField(Int32 Start, Int32 Width)
{
    public const Int32 MaxBits = 64;

    /// <summary>地址是否带位域后缀。普通地址（如 <c>4x10</c>）返回 false</summary>
    public static Boolean TryParse(String? address, out BitField field)
    {
        field = default;
        if (!Split(address, out _, out var spec)) return false;
        return TryParseSpec(spec, out field);
    }

    /// <summary>去掉位域后缀后的寄存器地址。没有位域时原样返回</summary>
    public static String RegisterAddress(String? address)
    {
        if (!Split(address, out var register, out var spec) || !TryParseSpec(spec, out _)) return address ?? "";
        return register;
    }

    /// <summary>从寄存器原始整数中取出位域，结果为无符号整数</summary>
    public static Object? Extract(Object? raw, BitField field)
    {
        if (!TryBits(raw, out var bits)) return raw;
        var value = (bits >> field.Start) & Mask(field.Width);
        return value <= Int64.MaxValue ? (Int64)value : value;
    }

    /// <summary>把位域值写回寄存器的比特位，其它位保持不变</summary>
    public static UInt64 Insert(UInt64 register, BitField field, UInt64 value)
    {
        var mask = Mask(field.Width) << field.Start;
        return (register & ~mask) | ((value & Mask(field.Width)) << field.Start);
    }

    /// <summary>
    /// 交给 Modbus 驱动的写入值。驱动会把物模型整数编码成字节，再用大端读回成寄存器；
    /// 16 位默认序在这一来一回里被对调。这里直接给大端字节，读回后仍是原来的整数。
    /// </summary>
    public static Byte[] ToDriverBytes(Object sample, UInt64 bits)
    {
        if (sample is Byte or SByte)
            return [(Byte)bits];

        if (sample is UInt16 or Int16)
        {
            var value = (UInt16)bits;
            return [(Byte)(value >> 8), (Byte)value];
        }

        if (sample is UInt32 or Int32 or Single)
        {
            var value = (UInt32)bits;
            return [(Byte)(value >> 24), (Byte)(value >> 16), (Byte)(value >> 8), (Byte)value];
        }

        var wide = bits;
        return
        [
            (Byte)(wide >> 56), (Byte)(wide >> 48), (Byte)(wide >> 40), (Byte)(wide >> 32),
            (Byte)(wide >> 24), (Byte)(wide >> 16), (Byte)(wide >> 8), (Byte)wide,
        ];
    }

    /// <summary>把合并后的寄存器值装回读取时的整数类型，避免把 16 位寄存器写成更宽的类型</summary>
    public static Object Box(Object sample, UInt64 bits) => sample switch
    {
        Byte => (Byte)bits,
        SByte => (SByte)(Byte)bits,
        UInt16 => (UInt16)bits,
        Int16 => (Int16)(UInt16)bits,
        UInt32 => (UInt32)bits,
        Int32 => (Int32)(UInt32)bits,
        UInt64 => bits,
        Int64 => (Int64)bits,
        _ => bits <= Int64.MaxValue ? (Int64)bits : bits,
    };

    /// <summary>把下发值换成位域整数。布尔 true 为 1，小数四舍五入</summary>
    public static Boolean TryFieldValue(Object? value, out UInt64 fieldValue)
    {
        fieldValue = 0;
        switch (value)
        {
            case Boolean b:
                fieldValue = b ? 1UL : 0UL;
                return true;
            case Byte or SByte or UInt16 or Int16 or UInt32 or Int32 or UInt64 or Int64:
                return TryBits(value, out fieldValue);
            default:
                if (value is not IConvertible || value is String) return false;
                try
                {
                    var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    if (Double.IsNaN(number) || Double.IsInfinity(number) || number < 0 || number > UInt64.MaxValue) return false;
                    fieldValue = (UInt64)Math.Round(number);
                    return true;
                }
                catch (FormatException) { return false; }
                catch (InvalidCastException) { return false; }
                catch (OverflowException) { return false; }
        }
    }

    /// <summary>按整数的二进制补码取比特，避免有符号数在移位前被符号扩展弄乱</summary>
    public static Boolean TryBits(Object? raw, out UInt64 bits)
    {
        switch (raw)
        {
            case Boolean v: bits = v ? 1UL : 0UL; return true;
            case Byte v: bits = v; return true;
            case SByte v: bits = (Byte)v; return true;
            case UInt16 v: bits = v; return true;
            case Int16 v: bits = (UInt16)v; return true;
            case UInt32 v: bits = v; return true;
            case Int32 v: bits = (UInt32)v; return true;
            case UInt64 v: bits = v; return true;
            case Int64 v: bits = (UInt64)v; return true;
            default:
                bits = 0;
                return false;
        }
    }

    private static UInt64 Mask(Int32 width) => width >= MaxBits ? UInt64.MaxValue : (1UL << width) - 1;

    private static Boolean Split(String? address, out String register, out String spec)
    {
        register = "";
        spec = "";
        if (String.IsNullOrWhiteSpace(address)) return false;

        var dot = address.IndexOf('.');
        var colon = address.IndexOf(':');
        var cut = dot < 0 ? colon : colon < 0 ? dot : Math.Min(dot, colon);
        if (cut <= 0 || cut >= address.Length - 1) return false;

        register = address[..cut];
        spec = address[(cut + 1)..];
        return register.Length > 0 && spec.Length > 0;
    }

    private static Boolean TryParseSpec(String spec, out BitField field)
    {
        field = default;
        var dash = spec.IndexOf('-');
        var colon = spec.IndexOf(':');
        if (dash >= 0 && colon >= 0) return false;

        String startText;
        var width = 1;
        if (dash > 0)
        {
            startText = spec[..dash];
            if (!Int32.TryParse(spec[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var end)) return false;
            if (!Int32.TryParse(startText, NumberStyles.None, CultureInfo.InvariantCulture, out var start)) return false;
            if (end < start) return false;
            width = end - start + 1;
            return TryCreate(start, width, out field);
        }

        if (colon > 0)
        {
            startText = spec[..colon];
            if (!Int32.TryParse(spec[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out width)) return false;
        }
        else
        {
            startText = spec;
        }

        if (!Int32.TryParse(startText, NumberStyles.None, CultureInfo.InvariantCulture, out var bit)) return false;
        return TryCreate(bit, width, out field);
    }

    private static Boolean TryCreate(Int32 start, Int32 width, out BitField field)
    {
        field = default;
        if (start < 0 || width < 1 || start > MaxBits - 1 || width > MaxBits || start + width > MaxBits) return false;
        field = new BitField(start, width);
        return true;
    }
}

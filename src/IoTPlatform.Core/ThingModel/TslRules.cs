using System.Text.RegularExpressions;
using IoTPlatform.Services;

namespace IoTPlatform.ThingModel;

/// <summary>阿里云物模型标识符规则：字母或下划线开头，仅含字母、数字、下划线，最长 50</summary>
public static partial class TslRules
{
    public const Int32 MaxIdentifierLength = 50;

    public const Int32 MaxNameLength = 30;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    public static void EnsureIdentifier(String? identifier, String what = "Identifier")
    {
        var text = identifier?.Trim() ?? "";
        if (!IdentifierPattern().IsMatch(text))
            throw new PlatformException("{0} must start with a letter or underscore and contain only letters, digits and underscore (max 50)", what);
    }

    public static void EnsureName(String? name)
    {
        if (!String.IsNullOrEmpty(name) && name.Trim().Length > MaxNameLength)
            throw new PlatformException("Name must be at most {0} characters", MaxNameLength);
    }

    public static void EnsureBlock(String? functionBlockId)
    {
        if (String.IsNullOrWhiteSpace(functionBlockId)) return;

        EnsureIdentifier(functionBlockId, "Function block");
    }
}

using System.Runtime.Serialization;

namespace MedBench.Core.Extensions;

public static class EnumExtensions
{
    public static string GetEnumMemberValue<T>(this T value) where T : Enum
    {
        var enumType = typeof(T);
        var name = Enum.GetName(enumType, value);
        if (name == null) return value.ToString();
        
        var enumMemberAttribute = ((EnumMemberAttribute[])enumType.GetField(name)!
            .GetCustomAttributes(typeof(EnumMemberAttribute), false))
            .FirstOrDefault();
        
        return enumMemberAttribute?.Value ?? value.ToString();
    }
} 
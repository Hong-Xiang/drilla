namespace DualDrill.Common;

public static class CommonExtension
{
    public static string Capitalize(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        char firstChar = char.ToUpper(value[0]);
        string capitalizedValue = firstChar + value[1..];
        return capitalizedValue;
    }
}

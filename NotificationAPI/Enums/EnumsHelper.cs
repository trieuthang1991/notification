namespace NotificationAPI.Enums;

public class EnumsHelper
{
    public static Dictionary<int, string> EnumToDictionary<T>() where T : Enum
    {
        return Enum.GetValues(typeof(T))
            .Cast<T>()
            .Where(t => Convert.ToInt32(t) > 0)
            .ToDictionary(t => Convert.ToInt32(t), t => t.ToString());
    }
}


using Newtonsoft.Json.Linq;

namespace SRT2Speech.Core.Extensions
{
    public static class CommonExtensions
    {
        public static T GetSafeValue<T>(this JObject data, string key)
        {
            if (data.TryGetValue(key, out JToken value))
            {
                return value.Value<T>();
            }

            return default(T);
        }
    }
}

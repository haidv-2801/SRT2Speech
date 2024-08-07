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

        public static IEnumerable<IEnumerable<T>> ChunkBy<T>(this IEnumerable<T> source, int chunkSize)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be a positive number.");

            List<T> list = source.ToList();
            int totalChunks = (int)Math.Ceiling((double)list.Count / chunkSize);

            for (int i = 0; i < totalChunks; i++)
            {
                yield return list.GetRange(i * chunkSize, Math.Min(chunkSize, list.Count - i * chunkSize));
            }
        }
    }
}

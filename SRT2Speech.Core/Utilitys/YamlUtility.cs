using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SRT2Speech.Core.Utilitys
{
    public class YamlUtility
    {
        private static IDeserializer deserializer
        {
            get
            {
                return new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
            }
        }

        private static ISerializer serializer
        {
            get
            {
                return new SerializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
            }
        }

        public static T Deserialize<T>(string data)
        {
            return deserializer.Deserialize<T>(data);
        }

        public static string Serialize<T>(T data)
        {
            return serializer.Serialize(data);
        }
    }
}

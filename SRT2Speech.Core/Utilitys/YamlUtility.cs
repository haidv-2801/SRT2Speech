using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SRT2Speech.Core.Utilitys
{
    public class YamlUtility
    {
        private static IDeserializer builder
        {
            get
            {
                return new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
            }
        }

        public static T Deserialize<T>(string data)
        {
            return builder.Deserialize<T>(data);
        }
    }
}

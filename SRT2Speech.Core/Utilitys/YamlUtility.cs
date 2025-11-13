using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SRT2Speech.Core.Utilitys
{
    public class YamlUtility
    {
        // Default deserializer using CamelCaseNamingConvention
        private static IDeserializer deserializer
        {
            get
            {
                return new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
            }
        }

        // Default serializer using CamelCaseNamingConvention
        private static ISerializer serializer
        {
            get
            {
                return new SerializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
            }
        }

        // Deserialize with specific naming convention
        public static T Deserialize<T>(string data, INamingConvention namingConvention = null)
        {
            if (namingConvention == null)
            {
                return deserializer.Deserialize<T>(data);
            }
            else
            {
                var customDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(namingConvention)
                    .Build();
                return customDeserializer.Deserialize<T>(data);
            }
        }

        // Serialize with specific naming convention
        public static string Serialize<T>(T data, INamingConvention namingConvention = null)
        {
            if (namingConvention == null)
            {
                return serializer.Serialize(data);
            }
            else
            {
                var customSerializer = new SerializerBuilder()
                    .WithNamingConvention(namingConvention)
                    .Build();
                return customSerializer.Serialize(data);
            }
        }

        // Deserialize with automatic naming convention detection
        public static T DeserializeAuto<T>(string data)
        {
            // Try different naming conventions in order of preference
            var conventions = new INamingConvention[]
            {
                CamelCaseNamingConvention.Instance,
                PascalCaseNamingConvention.Instance,
                UnderscoredNamingConvention.Instance,
                HyphenatedNamingConvention.Instance,
                NullNamingConvention.Instance // No transformation
            };

            foreach (var convention in conventions)
            {
                try
                {
                    var customDeserializer = new DeserializerBuilder()
                        .WithNamingConvention(convention)
                        .IgnoreUnmatchedProperties()
                        .Build();
                    return customDeserializer.Deserialize<T>(data);
                }
                catch
                {
                    // Try next convention
                    continue;
                }
            }

            // If all conventions fail, throw the original exception
            return deserializer.Deserialize<T>(data);
        }

        // Serialize to different naming conventions
        public static string SerializeToCamelCase<T>(T data)
        {
            return Serialize(data, CamelCaseNamingConvention.Instance);
        }

        public static string SerializeToPascalCase<T>(T data)
        {
            return Serialize(data, PascalCaseNamingConvention.Instance);
        }

        public static string SerializeToUnderscored<T>(T data)
        {
            return Serialize(data, UnderscoredNamingConvention.Instance);
        }

        public static string SerializeToHyphenated<T>(T data)
        {
            return Serialize(data, HyphenatedNamingConvention.Instance);
        }

        // Deserialize from different naming conventions
        public static T DeserializeFromCamelCase<T>(string data)
        {
            return Deserialize<T>(data, CamelCaseNamingConvention.Instance);
        }

        public static T DeserializeFromPascalCase<T>(string data)
        {
            return Deserialize<T>(data, PascalCaseNamingConvention.Instance);
        }

        public static T DeserializeFromUnderscored<T>(string data)
        {
            return Deserialize<T>(data, UnderscoredNamingConvention.Instance);
        }

        public static T DeserializeFromHyphenated<T>(string data)
        {
            return Deserialize<T>(data, HyphenatedNamingConvention.Instance);
        }
    }
}

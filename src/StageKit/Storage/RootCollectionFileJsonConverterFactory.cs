using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StageKit;

internal sealed class RootCollectionFileJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return GetCollectionBase(typeToConvert) is not null;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var collectionBase = GetCollectionBase(typeToConvert)
            ?? throw new NotSupportedException($"Unsupported collection settings type: {typeToConvert}.");
        var itemType = collectionBase.GetGenericArguments()[1];
        return (JsonConverter)Activator.CreateInstance(
            typeof(RootCollectionFileJsonConverter<,>).MakeGenericType(typeToConvert, itemType), nonPublic: true)!;
    }

    private static Type? GetCollectionBase(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(RootCollectionFile<,>))
            {
                return current;
            }
        }

        return null;
    }

    private sealed class RootCollectionFileJsonConverter<T, TItem> : JsonConverter<T>
        where T : class, IList<TItem>, new()
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var result = new T();

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                PopulateItems(document.RootElement, result, options);
                return result;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Collection settings must be an object or legacy array.");
            }

            foreach (var property in GetPersistedProperties(options))
            {
                var name = GetJsonName(property, options);
                if (!document.RootElement.TryGetProperty(name, out var element)) continue;

                if (property.Name == "Items")
                {
                    PopulateItems(element, result, options);
                }
                else if (property.SetMethod is not null)
                {
                    property.SetValue(result, element.Deserialize(property.PropertyType, options));
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var property in GetPersistedProperties(options))
            {
                var propertyValue = property.GetValue(value);
                writer.WritePropertyName(GetJsonName(property, options));
                JsonSerializer.Serialize(writer, propertyValue, property.PropertyType, options);
            }
            writer.WriteEndObject();
        }

        private static void PopulateItems(JsonElement element, T result, JsonSerializerOptions options)
        {
            var items = element.Deserialize<TItem[]>(options);
            if (items is null) return;
            foreach (var item in items) result.Add(item);
        }

        private static IEnumerable<PropertyInfo> GetPersistedProperties(JsonSerializerOptions options)
        {
            return typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.GetMethod is not null &&
                    property.GetIndexParameters().Length == 0 &&
                    property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always &&
                    (property.SetMethod is not null ||
                     property.Name == "Items" ||
                     !options.IgnoreReadOnlyProperties ||
                     property.IsDefined(typeof(JsonIncludeAttribute))));
        }

        private static string GetJsonName(PropertyInfo property, JsonSerializerOptions options)
        {
            return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                   options.PropertyNamingPolicy?.ConvertName(property.Name) ??
                   property.Name;
        }
    }
}
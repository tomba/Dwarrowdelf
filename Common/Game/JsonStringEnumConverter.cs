using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dwarrowdelf
{
	public class JsonStringEnumConverter<T> : JsonConverter<T> where T : struct, Enum
	{
		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var value = reader.GetString();
			return Enum.TryParse<T>(value, true, out var result) ? result : throw new JsonException($"Unable to convert \"{value}\" to {typeof(T)}.");
		}

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
	}
}

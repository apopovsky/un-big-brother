using System.Text;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace UnTaskAlert.Common
{
	public class CosmosJsonNetSerializer(JsonSerializerSettings serializerSettings) : CosmosSerializer
	{
		private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
		private readonly JsonSerializer _serializer = JsonSerializer.Create(serializerSettings);

		public override T FromStream<T>(Stream stream)
		{
			using (stream)
			{
				if (typeof(Stream).IsAssignableFrom(typeof(T)))
				{
					return (T)(object)(stream);
				}

				using var sr = new StreamReader(stream);
				using var jsonTextReader = new JsonTextReader(sr);
				return _serializer.Deserialize<T>(jsonTextReader);
			}
		}

		public override Stream ToStream<T>(T input)
		{
			var streamPayload = new MemoryStream();
			using var streamWriter = new StreamWriter(streamPayload, encoding: DefaultEncoding, bufferSize: 1024, leaveOpen: true);
			using JsonWriter writer = new JsonTextWriter(streamWriter);
			writer.Formatting = Formatting.None;
			_serializer.Serialize(writer, input);

			streamPayload.Position = 0;
			return streamPayload;
		}
	}
}
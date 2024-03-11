using System.Text.Json.Serialization;
using ksqlDB.RestApi.Client.KSql.RestApi.Responses.Statements;

namespace ksqlDB.RestApi.Client.KSql.RestApi.Responses.Topics;

#nullable enable
public record TopicsExtendedResponse : StatementResponseBase
{
  [JsonPropertyName("topics")]
  public TopicExtended[]? Topics { get; set; }
}

﻿namespace Kafka.DotNet.ksqlDB.KSql.RestApi.Responses.Queries
{
  public record StatusCount
  {
    //TODO: other statuses
    public int Running { get; set; }
  }
}
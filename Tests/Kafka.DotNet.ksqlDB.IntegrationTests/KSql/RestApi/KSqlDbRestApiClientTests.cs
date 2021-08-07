﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kafka.DotNet.ksqlDB.KSql.Linq;
using Kafka.DotNet.ksqlDB.KSql.Linq.Statements;
using Kafka.DotNet.ksqlDB.KSql.Query.Context;
using Kafka.DotNet.ksqlDB.KSql.RestApi;
using Kafka.DotNet.ksqlDB.KSql.RestApi.Enums;
using Kafka.DotNet.ksqlDB.KSql.RestApi.Extensions;
using Kafka.DotNet.ksqlDB.KSql.RestApi.Serialization;
using Kafka.DotNet.ksqlDB.KSql.RestApi.Statements;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kafka.DotNet.ksqlDB.IntegrationTests.KSql.RestApi
{
  [TestClass]
  public class KSqlDbRestApiClientTests
  {
    private IKSqlDbRestApiClient restApiClient;

    [TestInitialize]
    public void Initialize()
    {
      var ksqlDbUrl = @"http:\\localhost:8088";

      var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));

      restApiClient = new KSqlDbRestApiClient(httpClientFactory);
    }

    private string SuccessStatus => "SUCCESS";

    [TestMethod]
    public async Task ExecuteStatementAsync()
    {
      //Arrange
      KSqlDbStatement ksqlDbStatement = new(CreateTableStatement());

      //Act
      var httpResponseMessage = await restApiClient.ExecuteStatementAsync(ksqlDbStatement);

      //Assert
      httpResponseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

      string responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
      var responseObject = JsonSerializer.Deserialize<StatementResponse[]>(responseContent);

      responseObject?[0].CommandStatus.Status.Should().Be(SuccessStatus);
      responseObject?[0].CommandStatus.Message.Should().Be("Table created");
    }

    [TestMethod]
    [ExpectedException(typeof(TaskCanceledException))]
    public async Task ExecuteStatementAsync_Cancelled_ThrowsTaskCanceledException()
    {
      //Arrange
      KSqlDbStatement ksqlDbStatement = new(CreateTableStatement());
      var cts = new CancellationTokenSource();

      //Act
      var httpResponseMessageTask = restApiClient.ExecuteStatementAsync(ksqlDbStatement, cts.Token);
      cts.Cancel();

      HttpResponseMessage httpResponseMessage = await httpResponseMessageTask;
    }

    private string CreateTableStatement(string tableName = "TestTable")
    {
      return $@"CREATE OR REPLACE TABLE {tableName} (
        title VARCHAR PRIMARY KEY,
        id INT,
        release_year INT
      ) WITH (
        KAFKA_TOPIC='{tableName}',
        PARTITIONS=1,
        VALUE_FORMAT = 'JSON'
      );";
    }

    [TestMethod]
    public async Task CreateTable()
    {
      //Arrange
      var metadata = GetEntityCreationMetadata(nameof(MyMoviesTable));

      //Act
      var httpResponseMessage = await restApiClient.CreateTableAsync<MyMoviesTable>(metadata, ifNotExists: true);

      //Assert
      httpResponseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

      string responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
      var responseObject = JsonSerializer.Deserialize<StatementResponse[]>(responseContent);
    }

    private static EntityCreationMetadata GetEntityCreationMetadata(string topicName)
    {
      EntityCreationMetadata metadata = new()
      {
        KeyFormat = SerializationFormats.Json,
        KafkaTopic = topicName,
        Partitions = 1,
        Replicas = 1,
        WindowType = WindowType.Tumbling,
        WindowSize = "10 SECONDS",
        Timestamp = nameof(MyMoviesTable.Timestamp),
        TimestampFormat = "yyyy-MM-dd''T''HH:mm:ssX"
      };

      return metadata;
    }

    [TestMethod]
    public async Task CreateOrReplaceStream()
    {
      //Arrange
      var metadata = GetEntityCreationMetadata(nameof(MyMoviesStreamTest));

      //Act
      var httpResponseMessage = await restApiClient.CreateOrReplaceStreamAsync<MyMoviesStreamTest>(metadata);

      //Assert
      httpResponseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

      string responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
      var responseObject = JsonSerializer.Deserialize<StatementResponse[]>(responseContent);

      responseObject?[0].CommandStatus.Status.Should().Be("SUCCESS");
      responseObject?[0].CommandStatus.Message.Should().Be("Stream created");
    }

    #region Connectors

    private string SinkConnectorName => "mock-sink-connector";

    [TestMethod]
    public async Task CreateSinkConnectorAsync()
    {
      //Arrange
      var connectorConfig = new Dictionary<string, string> {
        { "connector.class", "org.apache.kafka.connect.tools.MockSinkConnector" },
        { "topics.regex", "mock-sink*"},
      };

      //Act
      var httpResponseMessage = await restApiClient.CreateSinkConnectorAsync(connectorConfig, SinkConnectorName);

      //Assert
      httpResponseMessage.IsSuccessStatusCode.Should().BeTrue();

      //Cleanup
      httpResponseMessage = await restApiClient.DropConnectorAsync($"`{SinkConnectorName}`");
    }

    private string SourceConnectorName => "mock-source-connector";

    [TestMethod]
    public async Task CreateSourceConnectorAsync_And_GetConnectorAsync_And_DropConnectorIfExistsAsync()
    {
      //Arrange
      var connectorConfig = new Dictionary<string, string> {
        { "connector.class", "org.apache.kafka.connect.tools.MockSourceConnector" },
      };

      //Act
      var httpResponseMessage = await restApiClient.CreateSourceConnectorAsync(connectorConfig, SourceConnectorName);

      var connectorsResponse = await restApiClient.GetConnectorsAsync();

      //Assert
      connectorsResponse[0].Connectors.Should().NotBeNull();
      connectorsResponse[0].Connectors.Any(c => c.Name == SourceConnectorName).Should().BeTrue();
      connectorsResponse[0].Type.Should().Be("connector_list");

      httpResponseMessage = await restApiClient.DropConnectorAsync($"`{SourceConnectorName}`");

      httpResponseMessage = await restApiClient.DropConnectorAsync("UnknownConnector");
      var content1 = await httpResponseMessage.ToStatementResponsesAsync();

      httpResponseMessage = await restApiClient.DropConnectorIfExistsAsync("UnknownConnector");
      var content2 = await httpResponseMessage.ToStatementResponsesAsync();

      connectorsResponse = await restApiClient.GetConnectorsAsync();
      connectorsResponse[0].Connectors.Any(c => c.Name == SourceConnectorName).Should().BeFalse();
    }

    #endregion

    [TestMethod]
    public async Task GetStreamsAsync()
    {
      //Arrange

      //Act
      var streamResponses = await restApiClient.GetStreamsAsync();

      //Assert
      streamResponses[0].StatementText.Should().Be(StatementTemplates.ShowStreams);
      streamResponses[0].Streams.Select(c => c.Name).Contains(nameof(MyMoviesStreamTest).ToUpper() + "S").Should().BeTrue();
    }

    [TestMethod]
    public async Task GetTablesAsync()
    {
      //Arrange

      //Act
      var tablesResponses = await restApiClient.GetTablesAsync();

      //Assert
      tablesResponses[0].StatementText.Should().Be(StatementTemplates.ShowTables);

      tablesResponses[0].Tables.Select(c => c.Name).Contains(nameof(MyMoviesTable).ToUpper() + "S").Should().BeTrue();
    }

    [TestMethod]
    public async Task GetAllTopicsAsync()
    {
      //Arrange

      //Act
      var topicsResponses = await restApiClient.GetAllTopicsAsync();

      //Assert
      topicsResponses[0].StatementText.Should().Be(StatementTemplates.ShowAllTopics);
      topicsResponses[0].Topics.Select(c => c.Name).Contains(nameof(MyMoviesTable)).Should().BeTrue();
    }

    [TestMethod]
    public async Task TerminatePersistentQueryAsync()
    {
      //Arrange
      string topicName = "testTableAsSelect";

      var contextOptions = new KSqlDBContextOptions(@"http:\\localhost:8088")
      {
        ShouldPluralizeFromItemName = false
      };
      await using var context = new KSqlDBContext(contextOptions);

      string createTableAsSelectStatement = context.CreateOrReplaceTableStatement("TestTableAsSelect")
        .With(new CreationMetadata
        {
          KafkaTopic = topicName,
          KeyFormat = SerializationFormats.Json,
          ValueFormat = SerializationFormats.Json
        })
        .As<object>("TestTable")
        .ToStatementString();

      var statement = new KSqlDbStatement(createTableAsSelectStatement);

      var response = await restApiClient.ExecuteStatementAsync(statement);
      var statementResponse = await response.ToStatementResponsesAsync();

      var queries = await restApiClient.GetQueriesAsync();

      var query = queries.SelectMany(c => c.Queries).FirstOrDefault(c => c.SinkKafkaTopics.Contains(topicName));

      //Act
      statementResponse = await restApiClient.TerminatePersistentQueryAsync(query.Id);

      //Assert
      query.QueryType.Should().Be("PERSISTENT");
      query.SinkKafkaTopics.Should().Contain(topicName);

      response.IsSuccessStatusCode.Should().BeTrue();

      statementResponse[0].CommandStatus.Status.Should().Be(SuccessStatus);
      statementResponse[0].CommandStatus.Message.Should().Be("Query terminated.");
    }

    [TestMethod]
    public async Task TerminatePushQueryAsync()
    {
      //Arrange
      var contextOptions = new KSqlDBContextOptions(@"http:\\localhost:8088");
      await using var context = new KSqlDBContext(contextOptions);

      var queryId = await context.CreateQueryStream<MyMoviesTable>().SubscribeAsync(_ => {}, e => { }, () => { });

      //Act
      var response = await restApiClient.TerminatePushQueryAsync(queryId);

      //Assert
      
      //https://github.com/confluentinc/ksql/issues/7559
      //"{"@type":"generic_error","error_code":50000,"message":"On wrong context or worker"}"
      response.IsSuccessStatusCode.Should().BeFalse();
      //response.IsSuccessStatusCode.Should().BeTrue();
    }

    [TestMethod]
    public async Task GetQueriesAsync()
    {
      //Arrange

      //Act
      var queriesResponses = await restApiClient.GetQueriesAsync();

      //Assert
      Console.WriteLine(string.Join(',', queriesResponses[0].Queries.Select(c => c.Id)));
      queriesResponses[0].StatementText.Should().Be(StatementTemplates.ShowQueries);
    }

    [TestMethod]
    public async Task DropConnectorIfExistsAsync_DoesNotExist_WarningResponse()
    {
      //Arrange

      //Act
      var httpResponseMessage = await restApiClient.DropConnectorIfExistsAsync("UnknownConnector");
      var content = await httpResponseMessage.ToStatementResponsesAsync();

      //Assert
      content[0].Type.Should().Be("warning_entity");
    }

    [TestMethod]
    public async Task DropConnectorAsync_DoesNotExist_ErrorResponse()
    {
      //Arrange

      //Act
      var httpResponseMessage = await restApiClient.DropConnectorAsync("UnknownConnector");
      var content = await httpResponseMessage.ToStatementResponsesAsync();

      //Assert
      content[0].Type.Should().Be("error_entity");
    }

    internal record MyMoviesTable : MyMoviesStreamTest
    {

    }

    internal record MyMoviesStreamTest
    {
      [ksqlDB.KSql.RestApi.Statements.Annotations.Key]
      public int Id { get; set; }

      public string Title { get; set; }

      public string Timestamp { get; set; }

      public int Release_Year { get; set; }

      public int[] NumberOfDays { get; set; }

      public IDictionary<string, int> Dictionary { get; set; }
      public Dictionary<string, int> Dictionary2 { get; set; }

      public int DontFindMe;

      public int DontFindMe2 { get; }
    }
  }
}
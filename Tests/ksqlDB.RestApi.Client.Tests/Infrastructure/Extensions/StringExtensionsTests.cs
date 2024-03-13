using FluentAssertions;
using ksqlDB.RestApi.Client.Infrastructure.Extensions;
using NUnit.Framework;

namespace ksqlDb.RestApi.Client.Tests.Infrastructure.Extensions;

public class StringExtensionsTests
{
  [Test]
  public void ToKSqlFunctionName()
  {
    //Arrange
    string functionName = "ExtractJsonField";

    //Act
    var ksqlFunctionName = functionName.ToKSqlFunctionName();

    //Assert
    ksqlFunctionName.Should().Be("EXTRACT_JSON_FIELD");
  }

  [Test]
  public void IsNotNullOrEmpty()
  {
    //Arrange

    //Act
    var isNotNullOrEmpty = "".IsNotNullOrEmpty();

    //Assert
    isNotNullOrEmpty.Should().BeFalse();
  }

  [Test]
  public void IsNotNullOrEmpty_Null_ReturnsTrue()
  {
    //Arrange

    //Act
    var isNotNullOrEmpty = (null as string).IsNotNullOrEmpty();

    //Assert
    isNotNullOrEmpty.Should().BeFalse();
  }

  [Test]
  public void IsNotNullOrEmpty_TextString_ReturnsTrue()
  {
    //Arrange

    //Act
    var isNotNullOrEmpty = "ksql".IsNotNullOrEmpty();

    //Assert
    isNotNullOrEmpty.Should().BeTrue();
  }
}

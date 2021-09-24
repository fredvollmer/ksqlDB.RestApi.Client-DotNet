﻿using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Kafka.DotNet.ksqlDB.KSql.Query.Visitors
{
  internal class LambdaVisitor : KSqlVisitor
  {
    public LambdaVisitor(StringBuilder stringBuilder)
      : base(stringBuilder, useTableAlias: false)
    {
    }

    public override Expression? Visit(Expression? expression)
    {
      if (expression == null)
        return null;

      switch (expression.NodeType)
      {
        case ExpressionType.Lambda:
          base.Visit(expression);
          break;
        
        case ExpressionType.Parameter:
          VisitParameter((ParameterExpression)expression);
          break;
        default:
          base.Visit(expression);
          break;
      }

      return expression;
    }

    private readonly IList<ParameterExpression> processedExpressions = new List<ParameterExpression>();

    protected override Expression VisitParameter(ParameterExpression node)
    {
      if (processedExpressions.Contains(node))
        return node;

      Append(node.Name);

      processedExpressions.Add(node);

      return base.VisitParameter(node);
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
      Append(node.Parameters[0].Name);

      Append(" => ");

      base.VisitLambda(node);

      return node;
    }
  }
}
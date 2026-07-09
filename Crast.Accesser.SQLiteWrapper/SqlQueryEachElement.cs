using System;
using System.Collections.Generic;
using System.Text;

namespace Crast.Accesser.SqlWrapper{



    /// <summary>
    /// 空白文字型。子要素を持たない。
    /// </summary>
    public abstract record SqlQueryTriviaElement : SqlQueryElement { }
    /// <summary>
    /// 実文字要素。前後にコメント含む空白文字を持てる。
    /// </summary>
    public abstract record SqlQueryStructuralElement : SqlQueryElement{
        public List<SqlQueryTriviaElement> LeadingTrivia { get; init; } = [];
        public List<SqlQueryTriviaElement> TrailingTrivia { get; init; } = [];
    }
    /// <summary>
    /// 子要素を持たない、分割不可の文字列要素。キーワードや識別子、リテラル値など。
    /// </summary>
    public abstract record SqlQueryTokenElement : SqlQueryStructuralElement { }
    /// <summary>
    /// SQLクエリ構造の一般的な要素。文、句、式など。基本的に子要素を持つ。
    /// </summary>
    public abstract record SqlQueryNode : SqlQueryStructuralElement {}
    /// <summary>
    /// 実行可能なSQLクエリ構造の基底。
    /// </summary>
    public abstract record SqlQueryStatement : SqlQueryNode { }
    /// <summary>
    /// 実行不能なSQLクエリ構造の基底。
    /// </summary>
    public abstract record SqlQueryFragment : SqlQueryNode { }


    /// <summary>
    /// SQLクエリの「句」に対応するデータクラスの基底。
    /// </summary>
    interface ISqlClause : ISqlQueryFragment{
        // 例えば、WHERE句やORDER BY句などのSQLクエリの一部を表すインターフェース。
    }
    /// <summary>
    /// SQLクエリの「式」に対応するデータクラスの基底。
    /// </summary>
    interface ISqlExpression : ISqlQueryFragment{
        // 例えば、条件式や算術式などのSQLクエリの一部を表すインターフェース。
    }
    interface ISqlValue : ISqlExpression{
        // 例えば、リテラル値や列名などのSQLクエリの一部を表すインターフェース。
    }

    enum SqlOperatorEnum{
        Equal, NotEqual, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual,
        And, Or, Not,
        Like, In, Between,
        IsNull, IsNotNull
    }

    static class SqlQueryExtensions{
        public static string ToSqlOperator(this SqlOperatorEnum op){
            return op switch{
                SqlOperatorEnum.Equal => "=",
                SqlOperatorEnum.NotEqual => "<>",
                SqlOperatorEnum.GreaterThan => ">",
                SqlOperatorEnum.LessThan => "<",
                SqlOperatorEnum.GreaterThanOrEqual => ">=",
                SqlOperatorEnum.LessThanOrEqual => "<=",
                SqlOperatorEnum.And => "AND",
                SqlOperatorEnum.Or => "OR",
                SqlOperatorEnum.Not => "NOT",
                SqlOperatorEnum.Like => "LIKE",
                SqlOperatorEnum.In => "IN",
                SqlOperatorEnum.Between => "BETWEEN",
                SqlOperatorEnum.IsNull => "IS NULL",
                SqlOperatorEnum.IsNotNull => "IS NOT NULL",
                _ => throw new ArgumentOutOfRangeException(nameof(op), $"Unsupported SQL operator: {op}")
            };
        }
    }



}

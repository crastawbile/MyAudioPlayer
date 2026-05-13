using System;
using System.Collections.Generic;
using System.Text;

namespace Crast.Accesser.SqlWrapper{


    /// <summary>
    /// 実行可能なSQLクエリ文に対応するデータクラスの基底。
    /// </summary>
    interface ISqlQueryStatement : ISqlQueryElement{
        // 例えば、SELECT文やINSERT文などのSQLクエリ全体を表すインターフェース。
    }
    /// <summary>
    /// 単独で実行できないSQLクエリ文の一部に対応するデータクラスの基底。
    /// </summary>
    interface ISqlQueryFragment : ISqlQueryElement{
    }
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

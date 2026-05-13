namespace Crast.Accesser.SqlWrapper
{
    public class QueryBuilder
    {

    }
    /// <summary>
    /// SQLクエリの種類を表すフラグ列挙型。
    /// </summary>
    /// <remarks>
    /// 現状SQLiteのみだが、後の拡張のために枠は用意してある。
    /// </remarks>
    [Flags]
    public enum SqlType{
        Generic = 0,
        SQLite = 1 << 0,
        PostgreSQL = 1 << 1, // 将来的な拡張性
                             // 特定の機能フラグ
        SupportsUpsert = 1 << 8,
        SupportsCte = 1 << 9
    }

    /// <summary>
    /// ISqlQueryStatementから最終的に出力されるSQLクエリ文字列とパラメータのセットを表すレコードクラス。
    /// </summary>
    public record SqlBuiltQuery(
        string Sql,
        IReadOnlyDictionary<string, object> Parameters,
        SqlType Type
    );
    /// <summary>
    /// SqlBuiltQueryを出力する過程で、プレースホルダーを管理するためのクラス。
    /// </summary>
    public class SqlBuildContext{
        private readonly Dictionary<string, object> _parameters = [];

        // 値を預かり、プレースホルダー名（@p0...）を返す
        public string GetPlaceHolder(object value){
            string name = $"@p{_parameters.Count}";
            _parameters[name] = value;
            return name;
        }

        // 最終的な SqlBuiltQuery を生成する
        public SqlBuiltQuery BuildQuery(SqlFragment sql, SqlType type) => new(sql.Value, _parameters, type);
    }
    /// <summary>
    /// ISqlQueryElement.Buildから出力される加工途中の文字列を担当するクラス。
    /// </summary>
    /// <param name="Value"></param>
    public readonly record struct SqlFragment(string Value);

    /// <summary>
    /// ISqlQueryStatementからログ用に出力される文字列を表すレコードクラス。
    /// </summary>
    public record SqlDebugBuiltQuery(
        string Value,
        IReadOnlyList<BuildNotice> Notices,
        SqlType Type
    );
    // 構築中の通知
    public record BuildNotice(
        NoticeLevel Level, // Info, Warning, Critical
        string Message,
        ISqlQueryElement Origin // どの要素が発信したか
    );
    public enum NoticeLevel{
        Info,
        Warning,
        Critical
    }

    /// <summary>
    /// SqlDebugBuiltQueryを出力する過程で、途中の情報を管理するためのクラス。
    /// </summary>
    public class SqlDebugBuildContext{
        public int IndentLevel { get; private set; } = 0;
        //バイナリデータや巨大なオブジェクトに対して、一貫した「表示用ラベル（[BIN: image_data]等）」を割り当てるためのレジストリ。
        private readonly Dictionary<object, string> LabelRegistry = [];
        //テーブルエイリアス（AS t1 等）が適切に解決されているかを追跡し、重複や未定義の参照をデバッグ時に指摘するためのマップ。
        private readonly Dictionary<string, string> AliasMap = [];
        //構築中に発見された警告やメモを蓄積するコレクション。
        private readonly List<string> DiagnosticSink = [];
        public void AddDiagnostic(string message){
            DiagnosticSink.Add(message);
        }
    }



}

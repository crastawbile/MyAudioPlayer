namespace Crast.Accessor.SqlWrapper{
    /// <summary>
    /// SQLクエリの種類を表すフラグ列挙型。
    /// </summary>
    /// <remarks>
    /// 現状SQLiteのみだが、後の拡張のために枠は用意してある。
    /// </remarks>
    [Flags]
    public enum SqlType{
        None = 0,
        Generic = 1 << 0,
        SQLite = 1 << 1,
        PostgreSQL = 1 << 2, // 将来的な拡張性
        MySQL = 1 << 3,

        All = (1 << 10) - 1,
                             // 特定の機能フラグ
        SupportsUpsert = 1 << 11,
        SupportsCte = 1 << 12
    }
    public sealed record SqlDialect {
        public SqlType Type { get; }
        public string Prefix { get; }
        public (char Open, char Close) QuoteChars { get; }
        public (string Open, string Close) BlockCommentChars { get; } = ("/*", "*/");
        public string LineCommentPrefix { get; } = "-- ";
        public SqlDialect(SqlType dialect){
            //単一の方言が指定されていることを確認
            if(dialect == SqlType.None || (dialect & (dialect - 1)) != 0){
                throw new ArgumentException("SqlDialect must be initialized with a single SqlType flag.", nameof(dialect));
            }

            Type = dialect;
            //方言ごとのプレースホルダープレフィックスとクォート文字を設定
            if (dialect.HasFlag(SqlType.SQLite)){
                Prefix = "@p";
                QuoteChars = ('"', '"');
            } else if (dialect.HasFlag(SqlType.PostgreSQL)){
                Prefix = "@p";
                QuoteChars = ('"', '"');
            } else if (dialect.HasFlag(SqlType.MySQL)){
                Prefix = "@p";
                QuoteChars = ('`', '`');
            } else {
                Prefix = "@p";
                QuoteChars = ('[', ']');
            }
        }

    }

    [Flags]
    public enum ModeOfShowingTrivia{
        None = 0,
        Comment = 1,
        Newline = 2,
        Space = 4,
        All = Comment | Newline | Space
    }
    public enum AlertPolicy{
        AllowAndConvert,   // 自動変換して続行（Debug時はInfo/Warning通知）
        AlertAndConvert,   // 警告（Notice）を追加した上で変換して続行
        ThrowException     // 例外を投げる（Build中断）
    }
    public record SqlQueryBuildOptionBase {
        //SQL方言はこっちには含まない。
        //デフォルト値での解決が許されない基軸であること、
        //ツリー変換においてSQL方言のみを変えること、辺りが理由。

        public ModeOfShowingTrivia ShowTrivia { get; init; } = ModeOfShowingTrivia.None; //トリビア要素の表示方法
        public AlertPolicy MissingNewlines { get; init; } = AlertPolicy.AlertAndConvert; //行コメント直後など、改行必須位置に改行が無い場合にデフォルト改行を挿入する
        public AlertPolicy MissingSpaces { get; init; } = AlertPolicy.AlertAndConvert; //スペースが必要な位置にスペースが無い場合にデフォルトスペースを挿入する
        public AlertPolicy UnsupportedHashComments { get; init; } = AlertPolicy.AlertAndConvert; //ハッシュコメント非対応方言で行コメントに変換する

        //単語例
        //unsupportedDialect （非対応の方言である場合）
        //unsupportedSyntax （構文自体は正規かもしれないが、このビルダー/DBでは非対応の構文である場合）
        //invalidSyntax （構文エラー、ルールに沿っていない）
        //malformedQuery （形が崩れている、パースすらできない状態）


    }
    public sealed record SqlQueryBuildOptions : SqlQueryBuildOptionBase{
        public char NewlineChar { get; init; } = '\n';
        public string DefaultSpace { get; init; } = " ";
        public string DefaultTab { get; init; } = "\t";
        public string DefaultNewline { get; init; } = "\n";
    }
    public sealed record SqlQueryDebugBuildOptions : SqlQueryBuildOptionBase{
        public bool ShowIds { get; init; } = false; //要素のIDを表示するか
        public bool ShowRole { get; init; } = false;//要素の親との関係を表示するか
    }

    public abstract class SqlBuildContextBase{
        public SqlDialect Dialect { get; }
        public virtual SqlQueryBuildOptionBase Options { get; }
        protected SqlBuildContextBase(SqlType dialect, SqlQueryBuildOptionBase options){
            Dialect = new SqlDialect(dialect);
            Options = options;
        }
        //構築中に発見された警告やメモを蓄積するコレクション。
        private readonly Dictionary<NoticeLevel, Dictionary<string, List<SqlQuerySlotId>>> _diagnosticSink = [];
        //エレメントで検出され、スロットでIDを代入される前の警告やメモを蓄積するコレクション。
        private readonly Dictionary<string, NoticeLevel> _temporaryDiagnostics = [];
        public IReadOnlyDictionary<NoticeLevel, Dictionary<string, List<SqlQuerySlotId>>> Diagnostics => _diagnosticSink;
        /// <summary>
        /// エレメント内で、検知した通知を一時蓄積に入れる処理
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        /// <param name="id"></param>
        public void AddDiagnostic(NoticeLevel level, string message){
            if (level == NoticeLevel.None) return;
            _temporaryDiagnostics[message] = level;
            //単独のエレメントが同時に同じ通知を複数のレベルで出すことは無いものとする。
        }
        /// <summary>
        /// 一時蓄積の通知にIDを付与して正規の通知シンクに移す処理
        /// </summary>
        /// <param name="id"></param>
        public void AdaptDiagnostics(SqlQuerySlotId id) {
            foreach (var (message, level) in _temporaryDiagnostics) {
                if (level == NoticeLevel.None) continue;
                if (!_diagnosticSink.TryGetValue(level, out var messages)){
                    messages = [];
                    _diagnosticSink[level] = messages;
                }
                if (!messages.TryGetValue(message, out var ids)){
                    ids = [];
                    messages[message] = ids;
                }
                ids.Add(id);
            }
            _temporaryDiagnostics.Clear();
        }
    }
    /// <summary>
    /// SqlBuiltQueryを出力する過程で、プレースホルダーを管理するためのクラス。
    /// </summary>
    public sealed class SqlBuildContext : SqlBuildContextBase{
        public override SqlQueryBuildOptions Options { get; }
        #region Build中に累積されていく情報
        private readonly Dictionary<string, object> _parameters = [];
        #endregion

        public SqlBuildContext(SqlType dialect, SqlQueryBuildOptions options)
            : base(dialect, options)
        {
            Options = options;
        }

        // 値を預かり、プレースホルダー名（@p0...）を返す
        public string GetPlaceHolder(object value){
            string name = $"{Dialect.Prefix}{_parameters.Count}";
            _parameters[name] = value;
            return name;
        }




        // 最終的な SqlBuiltQuery を生成する
        public SqlBuiltQuery BuildQuery(SqlBuiltQueryFragment sql) => new(sql.Value, _parameters, Dialect.Type);
    }
    /// <summary>
    /// SqlDebugBuiltQueryを出力する過程で、途中の情報を管理するためのクラス。
    /// </summary>
    public class SqlDebugBuildContext : SqlBuildContextBase{
        public override SqlQueryDebugBuildOptions Options { get; }

        public SqlDebugBuildContext(SqlType dialect, SqlQueryDebugBuildOptions options)
            :base(dialect, options)
        {
            Options = options;
        }

        public int IndentLevel { get; private set; } = 0;
        //バイナリデータや巨大なオブジェクトに対して、一貫した「表示用ラベル（[BIN: image_data]等）」を割り当てるためのレジストリ。
        private readonly Dictionary<object, string> LabelRegistry = [];
        //テーブルエイリアス（AS t1 等）が適切に解決されているかを追跡し、重複や未定義の参照をデバッグ時に指摘するためのマップ。
        private readonly Dictionary<string, string> AliasMap = [];



    }
    /// <summary>
    /// ISqlQueryStatementから最終的に出力されるSQLクエリ文字列とパラメータのセットを表すレコードクラス。
    /// </summary>
    public sealed record SqlBuiltQuery(
        string Sql,
        IReadOnlyDictionary<string, object> Parameters,
        SqlType Type
    );

    /// <summary>
    /// SqlQueryElement.Buildから出力される加工途中の文字列を担当するクラス。
    /// </summary>
    /// <remarks>
    /// 文字列以外にも、瞬間的に必要な情報はContextよりこちらで保持する。
    /// </remarks>
    /// <param name="Value"></param>
    public readonly record struct SqlBuiltQueryFragment(
        string Value,
        int? Precedence = null,//式型における、演算の優先順位。括弧の有無に影響。
        bool NeedSpaceBefore = false, //前にスペースが必要か
        bool NeedSpaceAfter = false, //後ろにスペースが必要か
        bool NeedNewlineAfter = false //後ろに改行が必要か

        );



    /// <summary>
    /// ISqlQueryStatementからログ用に出力される文字列を表すレコードクラス。
    /// </summary>
    /// <remarks>
    /// 部分出力もあり得るデバッグビルドの特性上、フラグメント型を個別に持たない。
    /// </remarks>
    public readonly record struct SqlDebugBuiltQuery(
        string[] Values,
        int? Precedence = null,//式型における、演算の優先順位。括弧の有無に影響。
        bool NeedSpaceBefore = false, //前にスペースが必要か
        bool NeedSpaceAfter = false, //後ろにスペースが必要か
        bool NeedNewlineAfter = false //後ろに改行が必要か
        );
    // 構築中の通知
    public readonly record struct BuildNotice(
        NoticeLevel Level, // Info, Warning, Critical
        string Message,
        SqlQueryElement Origin // どの要素が発信したか
    );
    public enum NoticeLevel{
        None,       // 無通知
        Info,       // ℹ\u2139　無修正でもビルド自体は問題なく行えるが、注意を促すべき状況（例: 非推奨の構文の使用など）
        Warning,    // ⚠️\u26A0　Context設定によってビルドは問題なく行われるが、修正すべきであることに違いはない状況。
        Error,   // ⛔\u26D4　Context設定によっては修正可能だが、今回は修正せずに通知する設定である場合。
        Critical       // ❌\u274C　Context設定で解決できない、ビルド自体が失敗する状況。
    }






}

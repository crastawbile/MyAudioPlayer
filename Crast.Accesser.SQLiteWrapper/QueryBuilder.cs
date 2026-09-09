using System.Collections.Immutable;

namespace Crast.Accessor.SqlWrapper {
    ///クエリビルダー関連ブロックの内、別ファイルに分けるほどではなさそうなクラス群。
    ///ビルド・デバッグビルドの際に利用するクラス群が主。


    /// <summary>
    /// SQL方言を表すenum。
    /// </summary>
    /// <remarks>
    /// 現状SQLiteのみだが、後の拡張のために枠は用意してある。
    /// </remarks>
    [Flags]
    public enum SqlType {
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
    /// <summary>
    /// 個々のSQL方言を表すクラス。
    /// </summary>
    public sealed record SqlDialect {
        public SqlType Type { get; }
        public string Prefix { get; }
        public (char Open, char Close) QuoteChars { get; }
        public (string Open, string Close) BlockCommentChars { get; } = ("/*", "*/");
        public string LineCommentPrefix { get; } = "-- ";
        public SqlDialect(SqlType dialect) {
            //単一の方言が指定されていることを確認
            if (dialect == SqlType.None || (dialect & (dialect - 1)) != 0) {
                throw new ArgumentException("SqlDialect must be initialized with a single SqlType flag.", nameof(dialect));
            }
            #region SQL方言に応じたPrefixとQuoteCharsの設定
            Type = dialect;
            //方言ごとのプレースホルダープレフィックスとクォート文字を設定
            if (dialect.HasFlag(SqlType.SQLite)) {
                Prefix = "@p";
                QuoteChars = ('"', '"');
            } else if (dialect.HasFlag(SqlType.PostgreSQL)) {
                Prefix = "@p";
                QuoteChars = ('"', '"');
            } else if (dialect.HasFlag(SqlType.MySQL)) {
                Prefix = "@p";
                QuoteChars = ('`', '`');
            } else {
                Prefix = "@p";
                QuoteChars = ('[', ']');
            }
            #endregion
        }

    }

    #region BuildOptionのプロパティ用のenum
    /// <summary>
    /// 空白文字型の種類を表すenum。
    /// </summary>
    [Flags]
    public enum TriviaType {
        None = 0,
        Comment = 1,
        Newline = 2,
        Space = 4,
        All = Comment | Newline | Space
    }
    /// <summary>
    /// ビルド中に発生した警告や通知の扱い方を表すenum。
    /// </summary>
    public enum AlertPolicy {
        AllowAndConvert,   // 自動変換して続行（Debug時はInfo/Warning通知）
        AlertAndConvert,   // 警告（Notice）を追加した上で変換して続行
        ThrowException     // 例外を投げる（Build中断）
    }
    #endregion
    public record SqlQueryBuildOptionBase {
        //SQL方言はこっちには含まない。
        //デフォルト値での解決が許されない基軸であること、
        //ツリー変換においてSQL方言のみを変えること、辺りが理由。

        public TriviaType ShowTrivia { get; init; } = TriviaType.None; //トリビア要素の表示方法

        #region 警告や通知の扱い方
        public AlertPolicy MissingNewlines { get; init; } = AlertPolicy.AlertAndConvert; //行コメント直後など、改行必須位置に改行が無い場合にデフォルト改行を挿入する
        public AlertPolicy MissingSpaces { get; init; } = AlertPolicy.AlertAndConvert; //スペースが必要な位置にスペースが無い場合にデフォルトスペースを挿入する
        public AlertPolicy InvalidComments { get; init; } = AlertPolicy.AlertAndConvert; //ハッシュコメント非対応方言で行コメントに変換する
        public AlertPolicy InvalidDialect { get; init; } = AlertPolicy.ThrowException; //クエリツリーの想定方言とビルド設定方言が一致しない場合に例外を投げる

        //基本的に例外投げて停まるしかないタイプ
        public AlertPolicy InvalidSyntax { get; } = AlertPolicy.ThrowException; //特に対応処理のない構文エラー。
        public AlertPolicy UnsupportedDialect { get; } = AlertPolicy.ThrowException; //このコードが対応していない方言。
        public AlertPolicy UnsupportedSyntax { get; } = AlertPolicy.ThrowException; //このコードが対応していない構文。

        //単語例
        //invalid ルール上不可能
        //unsupported ルール上は可能だがこのコードの機能上は不可能
        //malformedQuery （形が崩れている、パースすらできない状態）
        #endregion

    }
    public sealed record SqlQueryBuildOptions : SqlQueryBuildOptionBase {
        #region ビルド時のデフォルト設定
        public char NewlineChar { get; init; } = '\n';
        public string DefaultSpace { get; init; } = " ";
        public string DefaultTab { get; init; } = "\t";
        public string DefaultNewline { get; init; } = "\n";
        #endregion
    }
    public sealed record SqlQueryDebugBuildOptions : SqlQueryBuildOptionBase {
        #region デバッグビルド時の表示設定
        public bool ShowIds { get; init; } = false; //要素のIDを表示するか
        public bool ShowRole { get; init; } = false;//要素の親との関係を表示するか
        #endregion
    }

    public abstract class SqlBuildContextBase {
        public SqlDialect Dialect { get; }
        public virtual SqlQueryBuildOptionBase Options { get; }
        //コンストラクタ
        protected SqlBuildContextBase(SqlType dialect, SqlQueryBuildOptionBase options) {
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
        public void AddDiagnostic(NoticeLevel level, string message) {
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
                if (!_diagnosticSink.TryGetValue(level, out var messages)) {
                    messages = [];
                    _diagnosticSink[level] = messages;
                }
                if (!messages.TryGetValue(message, out var ids)) {
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
    public sealed class SqlBuildContext : SqlBuildContextBase {
        public override SqlQueryBuildOptions Options { get; }
        #region Build中に累積されていく情報
        private readonly Dictionary<string, object> _parameters = [];
        #endregion

        //コンストラクタ
        public SqlBuildContext(SqlType dialect, SqlQueryBuildOptions options)
            : base(dialect, options)
        {
            Options = options;
        }

        // 値を預かり、プレースホルダー名（@p0...）を返す
        public string GetPlaceHolder(object value) {
            string name = $"{Dialect.Prefix}{_parameters.Count}";
            _parameters[name] = value;
            return name;
        }




        // 最終的な SqlBuiltQuery を生成する
        public SqlBuiltQuery BuildQuery(SqlBuiltQueryFragment sql) => new(sql.Main, _parameters, Dialect.Type);
    }
    /// <summary>
    /// SqlDebugBuiltQueryを出力する過程で、途中の情報を管理するためのクラス。
    /// </summary>
    public class SqlDebugBuildContext : SqlBuildContextBase {
        public override SqlQueryDebugBuildOptions Options { get; }

        //コンストラクタ
        public SqlDebugBuildContext(SqlType dialect, SqlQueryDebugBuildOptions options)
            : base(dialect, options)
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
    /// 必須である可能性があるが禁止されることは無いある状態が満たされているかどうかを表すenum。
    /// </summary>
    /// <remarks>
    /// InProgressのような詳細分岐が必要な場合は、ProgressStatusを別途使用する。
    /// <br></br>初期値の0はUnsatisfiedとする。
    /// </remarks>
    public enum RequiredStatus {
        /// <summary>
        /// 条件を満たしていないが、満たす必要がある
        /// </summary>
        Unsatisfied,
        /// <summary>
        /// 条件を満たしていないが、満たす必要はない
        /// </summary>
        Exempt,
        /// <summary>
        /// 条件を既に満たしている
        /// </summary>
        Satisfied,
    }
    /// <summary>
    /// 処理の進行状態を表す汎用enum。
    /// </summary>
    /// <remarks>
    /// .NETのTaskStatusの名前をそのまま使用している。<br></br>
    /// TaskStatusはTask型＝非同期処理の進行状態を表すenumであるが、こっちは汎用。
    /// </remarks>
    public enum ProgressStatus {
        Created,                  // 作成されたが未開始
        WaitingForActivation,     // 実行開始待ち
        WaitingToRun,             // スケジュール済み（実行直前）
        Running,                  // 処理進行中
        WaitingForChildrenToComplete, // 子タスクの完了待ち
        RanToCompletion,          // 正常完了
        Canceled,                 // キャンセル
        Faulted                   // エラー失敗
    }
    /// <summary>
    /// SqlQueryFragmentの前後の制約状態を表すデータクラス。
    /// </summary>
    /// <remarks>
    /// 現状、空白文字が必要か、改行が必要か、の二点のみ。
    /// </remarks>
    /// <param name="Space"></param>
    /// <param name="Newline"></param>
    public record SqlQueryFragmentConditions(
            RequiredStatus Space = RequiredStatus.Exempt, //空白文字が必要か
            RequiredStatus Newline = RequiredStatus.Exempt //改行が必要か
        );

    //ビルド時の構築を考えると、上のエレメントで本体を加工した後に、前後のトリビアを合成する必要がある。
    //だから、トリビアだけを合成して一つのフラグメントにする部分と、
    //子の合成後トリビアと子の加工後本体の配列を合成して一つのフラグメントにする処理と、
    //が必要になる。
    //処理自体はElement側が持つべき？
    //処理結果を表すフラグメント型二種はこちらで定義する必要がある。
    //だったら、その型のコンストラクタに合成処理を持たせるのが自然か。
    //あーいや、BuiltFragment同士の接続処理は、全部同じか。
    //前方制約と後方制約、後方接続処理、通知がある場合は、合成を行う側のID利用か？
    //いや、Element自体はIDを持たないから、どうせ通知一時シンクに突っ込むだけか。
    //TriviaのみのFragmentかどうかを区別する型安全上、別個に宣言はするが、
    //メソッド自体は共通でいいか。
    //いや、生成処理自体をFragmentが持つ以上、Fragmentを最終型にすべきか。
    //Triviaのみかはプロパティで判別でも特に問題は無いか。




    /// <summary>
    /// SqlQueryElement.Buildから出力される加工途中の文字列を担当するクラス。
    /// </summary>
    /// <remarks>
    /// 文字列以外にも、瞬間的に必要な情報はContextよりこちらで保持する。
    /// <br></br>
    /// 預かるデータ自体はプレースホルダー化されるため、こっちの文字列はstructでいい。
    /// </remarks>
    /// <param name="Main"></param>
    public readonly record struct SqlBuiltQueryFragment(
        string Main,
        SqlQueryFragmentConditions Before, //前方制約
        SqlQueryFragmentConditions After, //後方制約
        bool TriviaOnly = false, //Triviaのみのフラグメントか否か。
        int? Precedence = null//式型における、演算の優先順位。括弧の有無に影響。
        ) {
        public static SqlBuiltQueryFragment EmptyTrivia => new("", new SqlQueryFragmentConditions(), new SqlQueryFragmentConditions(), TriviaOnly: true);
        public static SqlBuiltQueryFragment EmptyStructure => new("", new SqlQueryFragmentConditions(), new SqlQueryFragmentConditions(), TriviaOnly: false);
        /// <summary>
        /// 別のFragmentを後ろに接続する。
        /// </summary>
        /// <param name="after"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public SqlBuiltQueryFragment Join(SqlBuiltQueryFragment after, SqlBuildContext context) {
            string beforeText = this.Main;
            if (beforeText == "") { return after; }
            string afterText = after.Main;
            if (afterText == "") { return this; }

            //接続部分の制約チェック
            //現状、制約は空白文字と改行のみなので、改行を追加すれば空白文字は判定しなくていい。
            if (this.After.Newline == RequiredStatus.Unsatisfied && after.Before.Newline != RequiredStatus.Satisfied ||
                this.After.Newline != RequiredStatus.Satisfied && after.Before.Newline == RequiredStatus.Unsatisfied
                ) {//改行の挿入が必要な場合
                switch (context.Options.MissingNewlines) {
                    case AlertPolicy.ThrowException:
                        context.AddDiagnostic(NoticeLevel.Error, "必要な改行が抜けている。");
                        throw new ArgumentException("必要な改行が抜けている。");
                    case AlertPolicy.AlertAndConvert:
                        context.AddDiagnostic(NoticeLevel.Warning, "必要な改行が抜けている。");
                        beforeText += context.Options.DefaultNewline;
                        break;
                    case AlertPolicy.AllowAndConvert:
                        beforeText += context.Options.DefaultNewline;
                        break;
                    default:
                        throw new InvalidOperationException($"想定外のAlertPolicy");
                }
            } else if (
                this.After.Space == RequiredStatus.Unsatisfied && after.Before.Space != RequiredStatus.Satisfied ||
                this.After.Space != RequiredStatus.Satisfied && after.Before.Space == RequiredStatus.Unsatisfied
                ) {//空白文字の挿入が必要な場合
                switch (context.Options.MissingSpaces) {
                    case AlertPolicy.ThrowException:
                        context.AddDiagnostic(NoticeLevel.Error, "必要な空白文字が抜けている。");
                        throw new ArgumentException("必要な空白文字が抜けている。");
                    case AlertPolicy.AlertAndConvert:
                        context.AddDiagnostic(NoticeLevel.Warning, "必要な空白文字が抜けている。");
                        beforeText += context.Options.DefaultSpace;
                        break;
                    case AlertPolicy.AllowAndConvert:
                        beforeText += context.Options.DefaultSpace;
                        break;
                    default:
                        throw new InvalidOperationException($"想定外のAlertPolicy");
                }
            }
            return new SqlBuiltQueryFragment(
                beforeText + afterText,
                this.Before,
                after.After,
                TriviaOnly: this.TriviaOnly && after.TriviaOnly,
                Precedence: this.Precedence ?? after.Precedence
            );

        }
        /// <summary>
        /// FragmentNodeの三つのFragmentをまとめて後ろに接続する。
        /// </summary>
        /// <param name="node"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public SqlBuiltQueryFragment JoinNode(SqlBuiltQueryFragmentNode node, SqlBuildContext context) {
            var result = this.Join(node.Leading, context);
            result = result.Join(node.Main, context);
            result = result.Join(node.Trailing, context);
            return result;
        }
    };



    /// <summary>
    /// ISqlQueryStatementからログ用に出力される文字列を表すレコードクラス。
    /// </summary>
    /// <remarks>
    /// 部分出力もあり得るデバッグビルドの特性上、フラグメント型を個別に持たない。
    /// </remarks>
    public readonly record struct SqlDebugBuiltQuery(
        //実行用Buildとは違って、罫線追加済みの文字列を、行ごとに分けた文字列配列として保持する。
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

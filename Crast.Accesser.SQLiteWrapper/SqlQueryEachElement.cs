using Crast.Utilities.ExtensionMethods;
using System.Collections.Immutable;
using System.Text;
using System.Linq;


namespace Crast.Accessor.SqlWrapper{


    #region 抽象型の定義の列挙

    /// <summary>
    /// 空白文字型。子要素を持たない。
    /// </summary>
    /// <remarks>
    /// 空白文字、改行文字、コメントを総称する親クラス。
    /// </remarks>
    public abstract record SqlQueryTriviaElement : SqlQueryElement{
        //子要素を持たないことで処理が確定するメソッドをsealedにして、派生クラスでのオーバーライドを禁止する。
        public sealed override bool HasChild => false;
        public sealed override bool HasChildNode => false;
        public abstract ModeOfShowingTrivia RequiredMode { get; }
        public sealed override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotField Slot)> IterateChildren() { yield break; }
        internal sealed override SqlQueryElement CloneNode(SqlQueryDraft draft) => this;

        internal sealed override SqlQueryElement? ReplaceChildByRole(SqlQueryDraft draft, SqlQueryElementRole role, SqlQuerySlotState newNode) => throw new NotSupportedException("空白文字型ノードは子要素を持たない");
        internal sealed override SqlQueryElement AddChildren(SqlQueryElementRole role, SqlQuerySlotState[] newNodes) => throw new NotSupportedException("空白文字型ノードは子要素を持たない");
        internal sealed override SqlQueryElement RemoveChildren(SqlQueryElementRole role, Byte count = 0) => throw new NotSupportedException("空白文字型ノードは子要素を持たない");
    }
    /// <summary>
    /// 実文字要素。前後にコメント含む空白文字を持てる。
    /// </summary>
    public abstract record SqlQueryStructuralElement : SqlQueryElement{
        public sealed override bool HasChild => true;
        public SqlQuerySlotField<SqlQueryTriviaList> LeadingTrivia { get; }
        public SqlQuerySlotField<SqlQueryTriviaList> TrailingTrivia { get; }
        public SqlQueryStructuralElement(
            SqlQuerySlotField<SqlQueryTriviaList> leadingTrivia,
            SqlQuerySlotField<SqlQueryTriviaList> trailingTrivia
            ){
            LeadingTrivia = leadingTrivia;
            TrailingTrivia = trailingTrivia;
        }
    }
    /// <summary>
    /// 実文字子要素を持たない、分割不可の文字列要素。キーワードや識別子、リテラル値など。
    /// </summary>
    public abstract record SqlQueryTokenElement : SqlQueryStructuralElement{
        public sealed override bool HasChildNode => false;
        public SqlQueryTokenElement(
            SqlQuerySlotField<SqlQueryTriviaList> leadingTrivia,
            SqlQuerySlotField<SqlQueryTriviaList> trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }
    }
    /// <summary>
    /// SQLクエリ構造の一般的な要素。文、句、式など。基本的に子要素を持つ。
    /// </summary>
    public abstract record SqlQueryNode : SqlQueryStructuralElement{
        public SqlQueryNode(
            SqlQuerySlotField<SqlQueryTriviaList> leadingTrivia,
            SqlQuerySlotField<SqlQueryTriviaList> trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }
    }
    /// <summary>
    /// 実行可能なSQLクエリ構造の基底。
    /// </summary>
    public abstract record SqlQueryStatement : SqlQueryNode{
        public SqlQueryStatement(
            SqlQuerySlotField<SqlQueryTriviaList> leadingTrivia,
            SqlQuerySlotField<SqlQueryTriviaList> trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }
    }
    /// <summary>
    /// 実行不能なSQLクエリ構造の基底。
    /// </summary>
    public abstract record SqlQueryFragment : SqlQueryNode{
        public SqlQueryFragment(
            SqlQuerySlotField<SqlQueryTriviaList> leadingTrivia,
            SqlQuerySlotField<SqlQueryTriviaList> trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }
    }

    #endregion

    #region Trivia要素の定義

    /// <summary>
    /// 空白文字のうち、水平空白文字（スペースやタブ）を表す抽象クラス。
    /// </summary>
    public abstract record SqlQueryHorizontalWhitespace : SqlQueryTriviaElement{
    }
    /// <summary>
    /// 空白文字のうち、半角スペースを表す具象クラス。
    /// </summary>
    /// <remarks>
    /// count=0の場合は、ビルド時にcontext.Options.DefaultSpaceを使用する。
    /// </remarks>
    /// <param name="Count"></param>
    public sealed record SqlQuerySpaceElement(byte Count = 0) : SqlQueryHorizontalWhitespace{
        public override ModeOfShowingTrivia RequiredMode { get; } = ModeOfShowingTrivia.Space;
        public override SqlBuiltQueryFragment Build(SqlBuildContext context){
            return new SqlBuiltQueryFragment(Count == 0 ? context.Options.DefaultSpace : new string(' ', Count));
        }
        public override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            var valueStr = Count == 0 ? "[スペース:デフォルト]" : $"[スペース:×{Count}]";
            return new([valueStr]);
        }
    }
    /// <summary>
    /// 空白文字のうち、タブを表す具象クラス。
    /// </summary>
    /// <remarks>
    /// count=0の場合は、ビルド時にcontext.Options.DefaultTabを使用する。
    /// </remarks>
    /// <param name="Count"></param>
    public sealed record SqlQueryTabElement(byte Count = 0) : SqlQueryHorizontalWhitespace{
        public override ModeOfShowingTrivia RequiredMode { get; } = ModeOfShowingTrivia.Space;
        public override SqlBuiltQueryFragment Build(SqlBuildContext context){
            return new SqlBuiltQueryFragment(Count == 0 ? context.Options.DefaultTab : new string('\t', Count));
        }
        public override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            var valueStr = Count == 0 ? "[タブ:デフォルト]" : $"[タブ:×{Count}]";
            return new([valueStr]);
        }
    }
    /// <summary>
    /// 空白文字のうち、改行文字を表す具象クラス。
    /// </summary>
    /// <remarks>
    /// count=0の場合は、ビルド時にcontext.Options.DefaultNewlineを使用する。
    /// </remarks>
    /// <param name="Count"></param>
    public sealed record SqlQueryNewlineElement(byte Count = 0) : SqlQueryTriviaElement{
        public override ModeOfShowingTrivia RequiredMode { get; } = ModeOfShowingTrivia.Newline;
        public override SqlBuiltQueryFragment Build(SqlBuildContext context){
            return new SqlBuiltQueryFragment(Count == 0 ? context.Options.DefaultNewline : new string(context.Options.NewlineChar, Count));
        }
        public override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            var valueStr = Count == 0 ? "[改行:デフォルト]" : $"[改行:×{Count}]";
            return new([valueStr]);
        }
    }
    /// <summary>
    /// 空白文字のうち、コメントを表す抽象クラス。
    /// </summary>
    /// <param name="Value"></param>
    public abstract record SqlQueryCommentElement(string Value) : SqlQueryTriviaElement { }
    /// <summary>
    /// 空白文字のうち、ブロックコメントを表す具象クラス。
    /// </summary>
    public sealed record SqlQueryBlockCommentElement : SqlQueryCommentElement{
        public override ModeOfShowingTrivia RequiredMode { get; } = ModeOfShowingTrivia.Comment;
        public SqlQueryBlockCommentElement(string value) : base(value){ }
        public override SqlBuiltQueryFragment Build(SqlBuildContext context){
            return new SqlBuiltQueryFragment($"/*{Value}*/");
        }
        public override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            return new([$"[コメント] : {Value}"]);
        }
    }
    /// <summary>
    /// 空白文字のうち、行コメントを表す抽象クラス。
    /// </summary>
    public abstract record SqlQueryLineCommentElement : SqlQueryCommentElement{
        public SqlQueryLineCommentElement(string value) : base(value) { }
    }
    /// <summary>
    /// 空白文字のうち、通常の行コメントを表す具象クラス。`--`で始まる形式。
    /// </summary>
    public sealed record SqlQueryDoubleDashCommentElement : SqlQueryLineCommentElement{
        public override ModeOfShowingTrivia RequiredMode { get; } = ModeOfShowingTrivia.Comment;
        public SqlQueryDoubleDashCommentElement(string value) : base(value) { }
        public override SqlBuiltQueryFragment Build(SqlBuildContext context){
            return new SqlBuiltQueryFragment($"--{Value}", NeedNewlineAfter: true);
        }
        public override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            return new([$"[行コメント] : {Value}"]);
        }
    }
    /// <summary>
    /// 空白文字のうち、ハッシュコメントを表す具象クラス。`#`で始まる形式。MySQLなど一部の方言でのみ有効。
    /// </summary>
    public sealed record SqlQueryHashCommentElement : SqlQueryLineCommentElement{
        public override SqlType SafeType { get; } = SqlType.MySQL;//Allでないエレメントでのみ上書きする。
        public override ModeOfShowingTrivia RequiredMode { get; } = ModeOfShowingTrivia.Comment;

        public SqlQueryHashCommentElement(string value) : base(value) { }
        public override SqlBuiltQueryFragment Build(SqlBuildContext context){
            if (context.Dialect.Type.InFlag(SafeType)){
                //ハッシュコメントをそのまま出力可能な場合
                return new SqlBuiltQueryFragment($"#{Value}", NeedNewlineAfter: true);
            } else {
                //ハッシュコメント非対応の方言の場合、AlertPolicyに従って処理する
                switch (context.Options.UnsupportedHashComments){
                    case AlertPolicy.AllowAndConvert:
                        return AdaptTo(context.Dialect.Type).Build(context);
                    case AlertPolicy.AlertAndConvert:
                        context.AddDiagnostic(NoticeLevel.Warning, "ハッシュコメント非対応の方言");
                        return AdaptTo(context.Dialect.Type).Build(context);
                    case AlertPolicy.ThrowException:
                        throw new InvalidOperationException("ハッシュコメント非対応の方言");
                    default:
                        throw new InvalidOperationException("AlertPolicyの新たな選択肢に対する分岐が無い。");
                }
            }
        }
        public override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            string[] fragment = [$"[ハッシュコメント] : {Value}"];
            if (!context.Dialect.Type.InFlag(SafeType)){
                switch (context.Options.UnsupportedHashComments){
                    case AlertPolicy.AllowAndConvert:
                        //fragment = new[] {$"[行コメント] : {Values}"};
                        break;
                    case AlertPolicy.AlertAndConvert:
                        context.AddDiagnostic(NoticeLevel.Warning, "ハッシュコメント非対応の方言");
                        fragment = fragment.Append($"  \u26A0 [Warning] : ハッシュコメント非対応の方言").ToArray();
                        break;
                    case AlertPolicy.ThrowException:
                        context.AddDiagnostic(NoticeLevel.Error, "ハッシュコメント非対応の方言");
                        fragment = fragment.Append($"  \u26D4 [Error] : ハッシュコメント非対応の方言").ToArray();
                        break;
                    default:
                        throw new InvalidOperationException("AlertPolicyの新たな選択肢に対する分岐が無い。");
                }
            }
            return new(fragment, NeedNewlineAfter : true);
        }
        internal override SqlQueryLineCommentElement AdaptTo(SqlType targetDialect) => targetDialect.InFlag(SafeType) ? this : new SqlQueryDoubleDashCommentElement(Value);
    }

    public sealed record SqlQueryTriviaList : SqlQueryElement{
        //リスト型の不定数子要素を受ける配列型フィールド
        private readonly ImmutableArray<SqlQuerySlotField<SqlQueryTriviaElement>> _values;
        public override bool HasChild => _values.Length > 0;
        public sealed override bool HasChildNode => false;
        //プライベートコンストラクタ
        private SqlQueryTriviaList(SqlQuerySlotId[] ids, IReadOnlyList<SqlQuerySlotState<SqlQueryTriviaElement>> children){
            var length = children.Count();
            List<SqlQuerySlotField<SqlQueryTriviaElement>> values = [];
            for (var i = 0; i < length; i++){
                var slot =new SqlQuerySlotField<SqlQueryTriviaElement>(
                        ids[i],
                        SqlQuerySlotCapabilities.None,//空白文字リストにデフォルトや未定義は不要なので、CapabilitiesはNoneで良い。
                        children[i]
                );
                values.Add(slot);
            }
            _values = [.. values];
        }
        #region ファクトリメソッド
        /// <summary>
        /// 新しいIDと子要素で新たなクエリノードを作成する処理。
        /// </summary>
        /// <param name="draft"></param>
        /// <param name="children"></param>
        /// <returns></returns>
        public static SqlQueryTriviaList Create(SqlQueryDraft draft, IReadOnlyList<SqlQuerySlotState<SqlQueryTriviaElement>> children){
            //ちゃんとElementまで存在している物のみ子要素として扱う。
            //Build時のShowTriviaでModeに確定でアクセスできるために必要。
            List<SqlQuerySlotState<SqlQueryTriviaElement>> values = [];
            foreach (var child in children){
                if (!child.IsNormal) continue;
                values.Add(child);
            }
            var ids = new SqlQuerySlotId[values.Count];
            for (var i = 0; i < ids.Length; i++){
                ids[i] = SqlQuerySlotId.GetNextId(draft);
            }
            return new SqlQueryTriviaList(ids, values);
        }
        /// <summary>
        /// 既存ノードのスロットIDを保持したまま、子要素を更新した新しいクエリノードを作成する処理。
        /// </summary>
        /// <param name="old"></param>
        /// <param name="children"></param>
        /// <returns></returns>
        public static SqlQueryTriviaList Reload(SqlQueryTriviaList old, IReadOnlyList<SqlQuerySlotState<SqlQueryTriviaElement>> children){
            //ちゃんとElementまで存在している物のみ子要素として扱う。
            //Build時のShowTriviaでModeに確定でアクセスできるために必要。
            List<SqlQuerySlotState<SqlQueryTriviaElement>> values = [];
            foreach (var child in children){
                if (!child.IsNormal) continue;
                values.Add(child);
            }
            var ids = new SqlQuerySlotId[values.Count];
            for (var i = 0; i < ids.Length; i++){
                ids[i] = old._values[i].Id;
            }
            return new SqlQueryTriviaList(ids, values);
        }
        #endregion

        public override SqlBuiltQueryFragment Build(SqlBuildContext context){
            StringBuilder sb = new();
            bool needNewLine = false;
            foreach (var trivia in _values){
                //MissingNewlines : 行コメントの後に改行がない場合の処理
                if (needNewLine && trivia.State.Element is not SqlQueryNewlineElement){
                    switch (context.Options.MissingNewlines){
                        case AlertPolicy.AllowAndConvert:
                            sb.Append(context.Options.DefaultNewline);
                            break;
                        case AlertPolicy.AlertAndConvert:
                            context.AddDiagnostic(NoticeLevel.Warning, "行コメント直後に改行がない。");
                            sb.Append(context.Options.DefaultNewline);
                            break;
                        case AlertPolicy.ThrowException:
                            throw new InvalidOperationException("行コメント直後に改行がない。");
                        default:
                            throw new InvalidOperationException("AlertPolicyの新たな選択肢に対する分岐が無い。");
                    }
                }
                //ShowTrivia : 表示対象外のトリビアは無視する
                if (!context.Options.ShowTrivia.HasFlag(trivia.State.Element!.RequiredMode)) continue;

                //子要素のビルド結果の回収
                var fragment = trivia.Build(context);
                sb.Append(fragment.Value);
                needNewLine = fragment.NeedNewlineAfter;
            }
            return new SqlBuiltQueryFragment(sb.ToString(), NeedNewlineAfter: needNewLine);
        }
        public override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            List<string> outputs = [""];//先頭行は、後から入るIDとRole以外に必要な情報が無いので空文字。
            bool needNewLine = false;
            foreach (var trivia in _values){
                //MissingNewlines : 行コメントの後に改行がない場合の処理
                if (needNewLine && trivia.State.Element is not SqlQueryNewlineElement){
                    switch (context.Options.MissingNewlines){
                        case AlertPolicy.AllowAndConvert:
                            break;
                        case AlertPolicy.AlertAndConvert:
                            context.AddDiagnostic(NoticeLevel.Warning, "行コメント直後に改行がない。");
                            outputs.Add($"  \u26A0 [Warning] : 行コメント直後に改行がない。");
                            break;
                        case AlertPolicy.ThrowException:
                            context.AddDiagnostic(NoticeLevel.Error, "行コメント直後に改行がない。");
                            outputs.Add($"  \u26D4 [Error] : 行コメント直後に改行がない。");
                            break;
                        default:
                            throw new InvalidOperationException("AlertPolicyの新たな選択肢に対する分岐が無い。");
                    }
                }
                //ShowTrivia : 表示対象外のトリビアは無視する
                if (!context.Options.ShowTrivia.HasFlag(trivia.State.Element!.RequiredMode)) continue;

                //子要素のビルド結果の回収
                var fragment = trivia.DebugBuild(context);

                //罫線追加処理
                var framedFragment = fragment.Values;
                framedFragment[0] = $"├{framedFragment[0]}";
                if (framedFragment.Length > 1){
                    for (int i = 1; i < framedFragment.Length; i++){
                        framedFragment[i] = $"│{framedFragment[i]}";
                    }
                }

                outputs.AddRange(framedFragment);
                needNewLine = fragment.NeedNewlineAfter;
            }
            //末尾の罫線を変える処理
            for (var i = outputs.Count - 1; i >= 0; i--){
                if (outputs[i].StartsWith("├")){
                    outputs[i] = $"└{outputs[i].Substring(1)}";
                    break;
                } else if (outputs[i].StartsWith("│")){
                    outputs[i] = $"　{outputs[i].Substring(1)}";
                }
            }

            return new SqlDebugBuiltQuery([.. outputs], NeedNewlineAfter: needNewLine);
        }
        public override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotField Slot)> IterateChildren(){
            for (var i = 0; i < _values.Length; i++){
                yield return (new SqlQueryElementRole(SqlQueryElementRoleEnum.Trivia, i), _values[i]);
            }
        }
        internal override SqlQueryTriviaList CloneNode(SqlQueryDraft draft){
            var clonedChildren = new List<SqlQuerySlotState<SqlQueryTriviaElement>>();
            foreach (var (role, slot) in IterateChildren()){
                if (slot.State is not SqlQuerySlotState<SqlQueryTriviaElement> s)
                    throw new InvalidOperationException("SqlQueryTriviaListの子要素がSqlQueryTriviaElementでない、想定外の状況。");
                clonedChildren.Add(s.CloneNode(draft));
            }
            return Create(draft,clonedChildren);
        }
        /// <summary>
        /// SqlQueryTriviaListの子要素を、指定されたロールに基づいて置換する。
        /// </summary>
        /// <remarks>
        /// 子要素はroleによって異なる型になり得るので、非ジェネリックのこのメソッドはAlterではなく本メソッド。
        /// roleの該当無しはnull、newNodeの型違いは例外を投げる。
        /// </remarks>
        /// <param name="draft"></param>
        /// <param name="role"></param>
        /// <param name="newNode"></param>
        /// <returns></returns>
        internal override SqlQueryTriviaList? ReplaceChildByRole(SqlQueryDraft draft, SqlQueryElementRole role, SqlQuerySlotState newNode){
            //roleが非適正ならnull
            if (role.Name != SqlQueryElementRoleEnum.Trivia || role.Index < 0 || role.Index >= _values.Length) return null;
            //newNodeがSqlQueryTriviaElementでないなら例外
            if (newNode is not SqlQuerySlotState<SqlQueryTriviaElement> newTriviaState)
                throw new ArgumentException("newNodeの型がSqlQueryTriviaElementでない。", nameof(newNode));

            List<SqlQuerySlotState<SqlQueryTriviaElement>> newChildren = _values.Select(slot => slot.State).ToList();
            newChildren[role.Index] = newTriviaState;
            return Reload(this, newChildren);
        }

        internal override SqlQueryElement? AddChildren(SqlQueryElementRole role, SqlQuerySlotState[] newNodes) {
            //roleが非適正ならnull
            if (role.Name != SqlQueryElementRoleEnum.Trivia || role.Index < 0 || role.Index >= _values.Length) return null;
            //newNodeがSqlQueryTriviaElementでないなら例外
            if (newNodes is not SqlQuerySlotState<SqlQueryTriviaElement>[] newTriviaStates)
                throw new ArgumentException("newNodeの型がSqlQueryTriviaElementでない。", nameof(newNodes));

            List<SqlQuerySlotState<SqlQueryTriviaElement>> newChildren = _values.Select(slot => slot.State).ToList();
            //新しい子要素群を指定された位置に挿入する
            newChildren.InsertRange(role.Index, newTriviaStates);
            return Reload(this, newChildren);
        }
        internal override SqlQueryElement? RemoveChildren(SqlQueryElementRole role, Byte count = 0) {
            //roleが非適正ならnull
            if (role.Name != SqlQueryElementRoleEnum.Trivia || role.Index < 0 || role.Index >= _values.Length) return null;
            int size = count;
            if (count == 0 || count > _values.Length - role.Index) { size = _values.Length - role.Index; }

            List<SqlQuerySlotState<SqlQueryTriviaElement>> newChildren = _values.Select(slot => slot.State).ToList();
            //指定された位置から指定された数だけ子要素を削除する
            newChildren.RemoveRange(role.Index, size);
            return Reload(this, newChildren);
        }

        internal override SqlQueryTriviaList AdaptTo(SqlType targetDialect) {
            //ハッシュコメント非対応方言かつ、ハッシュコメントを含んでいるときのみ、再構成が必要になる。
            bool isSafe = true;
            foreach (var slot in _values) {
                isSafe &= slot.State.Element!.SafeType.HasFlag(targetDialect);
            }
            if (isSafe) return this;

            List<SqlQuerySlotState<SqlQueryTriviaElement>> adaptedChildren = [];
            foreach (var slot in _values){
                if (slot.State.Element!.AdaptTo(targetDialect) is not SqlQueryTriviaElement adaptedElement) throw new InvalidOperationException("空白文字型が空白文字型に変換されない異常事態。");
                adaptedChildren.Add(new SqlQuerySlotState<SqlQueryTriviaElement>(SqlQuerySlotStateEnum.Normal, adaptedElement));
            }
            return Reload(this, adaptedChildren);

        }


    }

    #endregion

    #region Token要素の定義

    public abstract record SqlQueryKeywordElement : SqlQueryTokenElement{
        public abstract string String { get; }
    public SqlQueryKeywordElement(SqlQuerySlotField<SqlQueryTriviaList> leadingTrivia, SqlQuerySlotField<SqlQueryTriviaList> trailingTrivia) : base(leadingTrivia, trailingTrivia)
    {
    }
    public override SqlBuiltQueryFragment Build(SqlBuildContext context)
    {
        var leading = LeadingTrivia.Build(context);
        var trailing = TrailingTrivia.Build(context);
        return new SqlBuiltQueryFragment($"{leading.Value}{String}{trailing.Value}", NeedNewlineAfter: trailing.NeedNewlineAfter);
    }
    public override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context)
    {
        var leading = LeadingTrivia.DebugBuild(context);
        var trailing = TrailingTrivia.DebugBuild(context);
        List<string> outputs = [];
        outputs.AddRange(leading.Values.Select(line => $"├{line}"));
        outputs.Add($"├[キーワード] : {String}");
        outputs.AddRange(trailing.Values.Select(line => $"└{line}"));
        return new SqlDebugBuiltQuery([.. outputs], NeedNewlineAfter: trailing.NeedNewlineAfter);
    }
}





        #endregion



//ここから下は未整理

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

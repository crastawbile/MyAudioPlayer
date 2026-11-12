using Crast.Utilities.ExtensionMethods;
using System.Collections.Immutable;
using System.Text;


namespace Crast.Accessor.SqlWrapper{
    ///各エレメントの具象型及び比較的細かい抽象型をまとめたファイル。

    //SqlQueryElement                                全ての基底
    //├SqlQueryStringElement                        裸文字要素
    //├SqlQueryTriviaList                           周辺要素リスト
    //├SqlQueryTriviaElement                        周辺要素
    //│├SqlQueryWhiteSpaceElement                  空白文字要素
    //│└SqlQueryCommentElement                     コメント要素
    //└SqlQueryStructuralElement                    機構要素
    //　├SqlQueryStructuralList                     機構要素リストの非ジェネリック抽象
    //　│└SqlQueryStructuralList<TElement>         機構要素リストのジェネリック具象
    //　└SqlQueryStructuralSingleElement            単独機構要素
    //　　├SqlQuerySubElement                       非実体要素の非ジェネリック抽象
    //　　│└SqlQuerySubElement<TElement>           非実体要素のジェネリック具象
    //　　└SqlQueryRealElement                      SQL実体要素
    //　　　├SqlQueryTokenElement                   実文字トークン
    //　　　└SqlQueryNode                           実構造要素
    //


    internal static class ElementExtender {


        #region ElementをStateに変換する、メソッドチェイン用の逆順処理
        //継承過程のSubElementだけ処理が変わるため、細かく拡張メソッドを定義する。

        /// <summary>
        /// ElementをStateに変換する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <returns></returns>
        internal static SqlQueryStructuralSlotState<T> WrapToEmptyState<T>(this SqlQuerySubElement<T> element)
            where T : SqlQuerySubElement
        {
            return new(SqlQuerySlotStateEnum.Empty,null,element);
        }
        /// <summary>
        /// ElementをStateに変換する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <returns></returns>
        internal static SqlQueryStructuralSlotState<T> WrapToUndefinedState<T>(this SqlQuerySubElement<T> element)
            where T : SqlQuerySubElement
        {
            return new(SqlQuerySlotStateEnum.Undefined, null, element);
        }
        /// <summary>
        /// ElementをStateに変換する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <returns></returns>
        internal static SqlQueryStructuralSlotState<T> WrapToDefaultState<T>(this SqlQuerySubElement<T> element)
            where T : SqlQuerySubElement
        {
            return new(SqlQuerySlotStateEnum.Default, null, element);
        }
        /// <summary>
        /// ElementをStateに変換する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <returns></returns>
        internal static SqlQueryStructuralSlotState<T> WrapToNormalState<T,TElement>(this T element)
            where T : SqlQueryStructuralList<TElement>
            where TElement : SqlQueryStructuralElement
        {
            return new(SqlQuerySlotStateEnum.Normal, element,null);
        }
        /// <summary>
        /// ElementをStateに変換する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <returns></returns>
        internal static SqlQueryStructuralSlotState<T> WrapToNormalState<T>(this T element)
            where T : SqlQueryRealElement
        {
            return new(SqlQuerySlotStateEnum.Normal, element, null);
        }
        /// <summary>
        /// ElementをStateに変換する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <returns></returns>
        internal static SqlQueryTriviaListSlotState WrapToNormalState(this SqlQueryTriviaList element){
            return new(element);
        }
        /// <summary>
        /// ElementをStateに変換する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <returns></returns>
        internal static SqlQueryTriviaSlotState WrapToNormalState(this SqlQueryTriviaElement element){
            return new(element);
        }
        /// <summary>
        /// ElementをStateに変換する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <returns></returns>
        internal static SqlQueryStringSlotState WrapToNormalState(this SqlQueryStringElement element){
            return new(element);
        }
        #endregion

    }



    #region 抽象型の定義の列挙

    /// <summary>
    /// 周辺要素型。空白、改行、コメントを総称する親クラス。
    /// </summary>
    internal abstract record SqlQueryTriviaElement : SqlQueryElement{
        //子要素を持たないことで処理が確定するメソッドをsealedにして、派生クラスでのオーバーライドを禁止する。
        //→コメント要素は裸文字要素を子要素に持つことになったので、空白文字要素に移動。
        internal sealed override bool HasChildNode => false;
        internal abstract TriviaType TriviaType { get; }
        internal abstract override SqlBuiltQueryFragmentText Build(SqlBuildContext context);
        internal override SqlQueryTriviaElement CloneNode(SqlQueryDraft draft){
            throw new NotImplementedException();
        }
        internal override SqlQueryTriviaElement AdaptTo(SqlType targetDialect) => this;

        #region 子要素取得
        internal override SqlQueryStringSlotField? GetChildByRole(SqlQueryElementRole role) => null;
        internal override SqlQueryStringSlotField? GetChildById(SqlQuerySlotId id) => null;
        #endregion

        #region 自身と子スロットのID固定で子スロット内のStateを変更するRePlace処理
        internal override SqlQueryTriviaElement? ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states) => null;
        internal override SqlQueryTriviaElement? ReplaceChildrenById(IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states) => null;
        internal override SqlQueryTriviaElement? ReplaceChildByRole(SqlQueryElementRole role, SqlQuerySlotStateAbstract state) => null;
        internal override SqlQueryTriviaElement? ReplaceChildById(SqlQuerySlotId id, SqlQuerySlotStateAbstract state) => null;
        #endregion
    }
    /// <summary>
    /// 周辺要素のうち、空白文字を表す抽象クラス。
    /// </summary>
    internal abstract record SqlQueryWhiteSpaceElement : SqlQueryTriviaElement{
        internal sealed override bool HasChild => false;
        internal sealed override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotFieldAbstract Slot)> IterateChildren() { yield break; }
        internal sealed override SqlQueryWhiteSpaceElement CloneNode(SqlQueryDraft draft) => this;
        internal sealed override SqlQueryWhiteSpaceElement? ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states) => throw new NotSupportedException("空白文字型ノードは子要素を持たない");
        internal sealed override SqlQueryWhiteSpaceElement? ReplaceChildrenById(IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states) => throw new NotSupportedException("空白文字型ノードは子要素を持たない");
        internal override SqlQueryWhiteSpaceElement AdaptTo(SqlType targetDialect) => this;
    }
    /// <summary>
    /// 周辺要素のうち、コメントを表す抽象クラス。
    /// </summary>
    /// <param name="Value"></param>
    internal abstract record SqlQueryCommentElement : SqlQueryTriviaElement {
        internal sealed override TriviaType TriviaType { get; } = TriviaType.Comment;
        internal override bool HasChild => true;//空のブロックコメントは子要素の裸文字列を持たないためfalseとなる。
        internal override SqlQueryCommentElement AdaptTo(SqlType targetDialect) => this;

    }
    /// <summary>
    /// 実文字要素。前後にコメント含む空白文字を持てる。
    /// </summary>
    internal abstract record SqlQueryStructuralElement : SqlQueryElement{
        internal sealed override bool HasChild => true;//前後の空白文字要素があるため、確定で子要素はある
        internal SqlQueryTriviaListSlotField LeadingTrivia { get; }
        internal SqlQueryTriviaListSlotField TrailingTrivia { get; }
        //コンストラクタ
        internal SqlQueryStructuralElement(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            ){
            LeadingTrivia = leadingTrivia;
            TrailingTrivia = trailingTrivia;
        }

        //Fieldの置き換えは具象型のコンストラクタを呼ぶしかない
        internal abstract SqlQueryStructuralElement ReplaceTriviaByField(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            );
        
        internal virtual SqlQueryStructuralElement ReplaceTriviaByState(SqlQueryTriviaListSlotState leadingTrivia, SqlQueryTriviaListSlotState trailingTrivia) {
            var result = ReplaceChildrenByRole(
                [
                (SqlQueryElementRole.Trivia(0), leadingTrivia),
                (SqlQueryElementRole.Trivia(1), trailingTrivia)
                ]
                );
            //空白文字スロットは絶対にあるからnullが返ることは無い。
            if (result is not SqlQueryStructuralElement se) throw new NotImplementedException("StructuralElementのReplaceメソッドからStructuralElementではない型が返る異常事態。");
            return se;
        }
        internal virtual SqlQueryStructuralElement ReplaceTriviaByElement(SqlQueryTriviaList leadingTrivia, SqlQueryTriviaList trailingTrivia){
            return ReplaceTriviaByState(
                SqlQueryTriviaListSlotState.CreateNormal(leadingTrivia),
                SqlQueryTriviaListSlotState.CreateNormal(trailingTrivia)
                );
        }

    }
    /// <summary>
    /// 単独実文字要素。実文字リスト型を除いた実文字要素全ての基底。
    /// </summary>
    /// <remarks>
    /// Buildの返り値型がSqlBuiltQueryFragmentNodeに確定する世代
    /// </remarks>
    internal abstract record SqlQueryStructuralSingleElement: SqlQueryStructuralElement{
        //コンストラクタ
        internal SqlQueryStructuralSingleElement(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            )
            : base(leadingTrivia,trailingTrivia)
        {
        }
        internal abstract override SqlBuiltQueryFragmentNode Build(SqlBuildContext context);
        internal abstract override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context);
}
    /// <summary>
    /// SQL実体要素。空白文字・リスト層・代用Elementのようなツリーの形式上必要なものではなく、SQLクエリの内容を決定するのに関係する要素。
    /// </summary>
    internal abstract record SqlQueryRealElement : SqlQueryStructuralElement {
        internal SqlQueryRealElement(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }
    }
    /// <summary>
    /// 実文字子要素を持たない、分割不可の文字列要素。キーワードや識別子、リテラル値など。
    /// </summary>
    internal abstract record SqlQueryTokenElement : SqlQueryRealElement{
        internal sealed override bool HasChildNode => false;
        internal SqlQueryTokenElement(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }
    }
    /// <summary>
    /// SQLクエリ構造の一般的な非末端要素。文、句、式など。基本的に子実文字要素を持つ。
    /// </summary>
    internal abstract record SqlQueryNode : SqlQueryRealElement{
        internal SqlQueryNode(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }
    }
    /// <summary>
    /// 実行可能なSQLクエリ構造の基底。
    /// </summary>
    internal abstract record SqlQueryStatement : SqlQueryNode{
        internal SqlQueryStatement(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }
    }
    /// <summary>
    /// 実行不能なSQLクエリ構造の基底。
    /// </summary>
    internal abstract record SqlQueryFragment : SqlQueryNode{
        internal SqlQueryFragment(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }
    }

    #endregion

    #region 代用Element

    internal abstract record SqlQuerySubElement : SqlQueryStructuralSingleElement{
        internal sealed override bool HasChildNode => false;
        //コンストラクタ
        internal SqlQuerySubElement(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            )
            :base(leadingTrivia, trailingTrivia)
        {
        }

        #region Build関連
        /// <summary>
        /// 実行用のクエリを構築する処理。
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        internal override SqlBuiltQueryFragmentNode Build(SqlBuildContext context){
            if (LeadingTrivia.Build(context) is not SqlBuiltQueryFragmentText lt) throw new NotSupportedException("空白文字リストのBuild結果がSqlBuiltQueryFragmentTextではない異常事態。");
            if (TrailingTrivia.Build(context) is not SqlBuiltQueryFragmentText tt) throw new NotSupportedException("空白文字リストのBuild結果がSqlBuiltQueryFragmentTextではない異常事態。");
            return new SqlBuiltQueryFragmentNode(lt,null,tt);
        }
        /// <summary>
        /// 人間用のクエリ構造図を構築する処理。
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            throw new NotSupportedException();
        }
        #endregion

        internal override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotFieldAbstract Slot)> IterateChildren(){
            yield return (new SqlQueryElementRole(SqlQueryElementRoleEnum.Trivia, 0), LeadingTrivia);
            yield return (new SqlQueryElementRole(SqlQueryElementRoleEnum.Trivia, 1), TrailingTrivia);
        }

    }
    internal sealed record SqlQuerySubElement<TElement> : SqlQuerySubElement where TElement : SqlQueryElement{
        //コンストラクタ
        internal SqlQuerySubElement(
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            ) : base(leadingTrivia, trailingTrivia)
        {
        }

        #region ファクトリ
        internal static SqlQuerySubElement<TElement> CreateNull(SqlQueryDraft draft){
            return new SqlQuerySubElement<TElement>(
                new SqlQueryTriviaListSlotField(
                    SqlQuerySlotId.GetNextId(draft),
                    SqlQuerySlotCapabilities.OnlyNormal,
                    SqlQueryTriviaListSlotState.CreateNormal(SqlQueryTriviaList.CreateFrom0())
                    ),
                new SqlQueryTriviaListSlotField(
                    SqlQuerySlotId.GetNextId(draft), 
                    SqlQuerySlotCapabilities.OnlyNormal,
                    SqlQueryTriviaListSlotState.CreateNormal(SqlQueryTriviaList.CreateFrom0())
                    )
            );
        }
        #endregion

        internal override SqlQuerySubElement<TElement> CloneNode(SqlQueryDraft draft){
            return new SqlQuerySubElement<TElement>(
                new SqlQueryTriviaListSlotField(SqlQuerySlotId.GetNextId(draft), SqlQuerySlotCapabilities.OnlyNormal, LeadingTrivia.State.CloneNode(draft)),
                new SqlQueryTriviaListSlotField(SqlQuerySlotId.GetNextId(draft), SqlQuerySlotCapabilities.OnlyNormal, TrailingTrivia.State.CloneNode(draft))
            );
        }

        #region 自身と子スロットのID固定で子スロット内のStateを変更するRePlace処理
        internal override SqlQuerySubElement<TElement>? ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states){
            var leadingTrivia = LeadingTrivia;
            var trailingTrivia = TrailingTrivia;
            foreach (var (role, state) in states) {
                if (role.Name != SqlQueryElementRoleEnum.Trivia) return null;
                if (state is not SqlQueryTriviaListSlotState tl) return null;
                switch (role.Index) {
                    case 0:
                        leadingTrivia = leadingTrivia.ReplaceState(tl);
                        break;
                    case 1:
                        trailingTrivia = trailingTrivia.ReplaceState(tl);
                        break;
                    default:
                        return null;
                }
            }
            return new SqlQuerySubElement<TElement>(leadingTrivia, trailingTrivia);
        }
        internal override SqlQuerySubElement<TElement>? ReplaceChildrenById(IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states){
            var leadingTrivia = LeadingTrivia;
            var trailingTrivia = TrailingTrivia;
            foreach (var (id, state) in states){
                if (state is not SqlQueryTriviaListSlotState tl) return null;
                
                if (id == LeadingTrivia.Id) leadingTrivia = leadingTrivia.ReplaceState(tl);
                else if (id == TrailingTrivia.Id) trailingTrivia = trailingTrivia.ReplaceState(tl);
                else return null;
            }
            return new SqlQuerySubElement<TElement>(leadingTrivia, trailingTrivia);
        }
        internal override SqlQuerySubElement<TElement> ReplaceTriviaByField(SqlQueryTriviaListSlotField leadingTrivia, SqlQueryTriviaListSlotField trailingTrivia){
            return new SqlQuerySubElement<TElement>(leadingTrivia, trailingTrivia);
        }
        internal override SqlQuerySubElement<TElement> ReplaceTriviaByState(SqlQueryTriviaListSlotState leadingTrivia, SqlQueryTriviaListSlotState trailingTrivia){
            var result = ReplaceChildrenByRole(
                [
                (SqlQueryElementRole.Trivia(0), leadingTrivia),
                (SqlQueryElementRole.Trivia(1), trailingTrivia)
                ]
                )!;
            return result;
        }
        internal override SqlQuerySubElement<TElement> ReplaceTriviaByElement(SqlQueryTriviaList leadingTrivia, SqlQueryTriviaList trailingTrivia){
            return ReplaceTriviaByState(
                SqlQueryTriviaListSlotState.CreateNormal(leadingTrivia),
                SqlQueryTriviaListSlotState.CreateNormal(trailingTrivia)
                );
        }
        #endregion
    }
    #endregion


    #region Trivia要素の定義

    /// <summary>
    /// 空白文字のうち、水平空白文字（スペースやタブ）を表す抽象クラス。
    /// </summary>
    internal abstract record SqlQueryHorizontalWhitespace : SqlQueryWhiteSpaceElement{
    }
    /// <summary>
    /// 空白文字のうち、半角スペースを表す具象クラス。
    /// </summary>
    /// <remarks>
    /// count=0の場合は、ビルド時にcontext.Options.DefaultSpaceを使用する。
    /// </remarks>
    /// <param name="Count"></param>
    internal sealed record SqlQuerySpaceElement(byte Count = 0) : SqlQueryHorizontalWhitespace{
        internal override TriviaType TriviaType { get; } = TriviaType.Space;
        internal static SqlQuerySpaceElement Create(byte count = 0){
            return new SqlQuerySpaceElement(count);
        }
        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            return new SqlBuiltQueryFragmentText(
                main : Count == 0 ? context.Options.DefaultSpace : new string(' ', Count),
                before : new SqlQueryFragmentConditions(Space : RequiredStatus.Satisfied),
                after : new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                TriviaOnly : true
                );
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
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
    internal sealed record SqlQueryTabElement(byte Count = 0) : SqlQueryHorizontalWhitespace{
        internal override TriviaType TriviaType { get; } = TriviaType.Space;
        internal static SqlQueryTabElement Create(byte count = 0){
            return new SqlQueryTabElement(count);
        }
        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            return new SqlBuiltQueryFragmentText(
                main : Count == 0 ? context.Options.DefaultTab : new string('\t', Count),
                before: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                after: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                TriviaOnly: true
            );
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
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
    internal sealed record SqlQueryNewlineElement(byte Count = 0) : SqlQueryWhiteSpaceElement{
        internal override TriviaType TriviaType { get; } = TriviaType.Newline;
        internal static SqlQueryNewlineElement Create(byte count = 0){
            return new SqlQueryNewlineElement(count);
        }
        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            return new SqlBuiltQueryFragmentText(
                Count == 0 ? context.Options.DefaultNewline : new string(context.Options.NewlineChar, Count),
                new SqlQueryFragmentConditions(
                    Newline: RequiredStatus.Satisfied,
                    Space: RequiredStatus.Satisfied
                    ),
                new SqlQueryFragmentConditions(
                    Newline: RequiredStatus.Satisfied,
                    Space: RequiredStatus.Satisfied
                    ),
                TriviaOnly: true
            );
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            var valueStr = Count == 0 ? "[改行:デフォルト]" : $"[改行:×{Count}]";
            return new([valueStr]);
        }
    }

    /// <summary>
    /// コメントのうち、ブロックコメントを表す具象クラス。
    /// </summary>
    /// <remarks>
    /// リスト型的な要素もあるが、コメント要素型であることが必須であるため、リスト型は継承していない。
    /// また、子要素のRoleはSqlQueryElementRoleEnum.Stringである。
    /// </remarks>
    internal sealed record SqlQueryBlockCommentElement : SqlQueryCommentElement{
        private SqlQueryStringSlotField[] Values { get; }
        internal int Length => Values.Length;
        internal override bool HasChild => Values.Length > 0;
        //コンストラクタ
        private SqlQueryBlockCommentElement(IEnumerable<SqlQueryStringSlotField> values){
            Values = [..values];
        }
        #region ファクトリ
        internal static SqlQueryBlockCommentElement CreateFromDraftAndStates(SqlQueryDraft draft, IEnumerable<SqlQueryStringSlotState> values){
            return new SqlQueryBlockCommentElement(values.Select(v => SqlQueryStringSlotField.CreateNormal(draft, v.Element)).ToArray());
        }
        internal static SqlQueryBlockCommentElement CreateFromIdAndStates(IEnumerable<(SqlQuerySlotId id, SqlQueryStringSlotState value)> values){
            return new SqlQueryBlockCommentElement(values.Select(v => new SqlQueryStringSlotField(v.id, SqlQuerySlotCapabilities.OnlyNormal, v.value)).ToArray());
        }
        internal static SqlQueryBlockCommentElement CreateFromDraftAndElements(SqlQueryDraft draft, IEnumerable<SqlQueryStringElement> values){
            return new SqlQueryBlockCommentElement(values.Select(v => SqlQueryStringSlotField.CreateNormal(draft, v)).ToArray());
        }
        internal static SqlQueryBlockCommentElement CreateFromIdAndElements(IEnumerable<(SqlQuerySlotId id, SqlQueryStringElement value)> values){
            return new SqlQueryBlockCommentElement(values.Select(v => new SqlQueryStringSlotField(v.id, SqlQuerySlotCapabilities.OnlyNormal, v.value.WrapToNormalState())).ToArray());
        }
        internal static SqlQueryBlockCommentElement CreateFromDraftAndStrings(SqlQueryDraft draft, IEnumerable<string> values){
            return new SqlQueryBlockCommentElement(values.Select(v => SqlQueryStringSlotField.CreateNormal(draft, SqlQueryStringElement.Create(v))).ToArray());
        }
        internal static SqlQueryBlockCommentElement CreateFromIdAndStrings(IEnumerable<(SqlQuerySlotId id, string value)> values){
            return new SqlQueryBlockCommentElement(values.Select(v => new SqlQueryStringSlotField(v.id, SqlQuerySlotCapabilities.OnlyNormal, SqlQueryStringElement.Create(v.value).WrapToNormalState())).ToArray());
        }

        #endregion
        internal override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotFieldAbstract Slot)> IterateChildren(){
            for(ushort i = 0; i < Values.Length; i++){
                yield return (new SqlQueryElementRole(SqlQueryElementRoleEnum.String, i), Values[i]);
            }
        }

        #region 再帰処理
        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            var valueStr = string.Join(context.Options.NewlineChar, Values.Select(v => v.State.Element.Value));
            return new SqlBuiltQueryFragmentText(
                $"/*{valueStr}*/",
                before: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                after: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                TriviaOnly: true
                );
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            var valueStr = string.Join(context.Options.NewlineChar, Values.Select(v => v.State.Element.Value));
            return new([$"[コメント] : {context.Options.NewlineChar}{valueStr}"]);
        }
        internal override SqlQueryBlockCommentElement CloneNode(SqlQueryDraft draft){
            return CreateFromDraftAndStates(draft, Values.Select(v => v.State));
        }
        internal override SqlQueryBlockCommentElement AdaptTo(SqlType targetDialect) => this;
        #endregion

        #region 子要素取得
        internal override bool HasRole(SqlQueryElementRole role){
            if (role.Name != SqlQueryElementRoleEnum.String) return false;
            if (role.Index >= Values.Length) return false;
            return true;
        }
        internal override SqlQueryStringSlotField? GetChildByRole(SqlQueryElementRole role){
            if (!HasRole(role)) return null;
            return Values[role.Index];
        }
        internal override SqlQueryStringSlotField? GetChildById(SqlQuerySlotId id){
            foreach (var field in Values){
                if (field.Id == id) return field;
            }
            return null;
        }


        #endregion

        #region 自身と子スロットのID固定で子スロット内のStateを変更するRePlace処理

        internal override SqlQueryBlockCommentElement? ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states){
            var resultStates = Values.Select(v => v.State).ToArray();
            foreach (var (role, state) in states){
                if (role.Name != SqlQueryElementRoleEnum.String) return null;
                if (state is not SqlQueryStringSlotState s) return null;
                if (role.Index >= resultStates.Length) return null;
                resultStates[role.Index] = s;
            }
            return CreateFromIdAndStates(Values.Select((v, i) => (v.Id, resultStates[i])));
        }
        internal override SqlQueryBlockCommentElement? ReplaceChildrenById(IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states){
            var resultStates = Values.Select(v => v.State).ToArray();
            foreach (var (id, state) in states){
                if (state is not SqlQueryStringSlotState s) return null;

                var index = Array.FindIndex(Values, v => v.Id == id);
                if (index < 0) return null;
                resultStates[index] = s;
            }
            return CreateFromIdAndStates(Values.Select((v, i) => (v.Id, resultStates[i])));
        }
        internal override SqlQueryBlockCommentElement? ReplaceChildByRole(SqlQueryElementRole role, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenByRole([(role, state)]);
        }
        internal override SqlQueryBlockCommentElement? ReplaceChildById(SqlQuerySlotId id, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenById([(id, state)]);
        }
        #endregion

        #region リスト型特有の挿入・削除処理
        internal SqlQueryBlockCommentElement AddChildren(SqlQueryDraft draft, ushort index, IEnumerable<SqlQueryStringSlotState> states) {
            if (index > Length) throw new ArgumentOutOfRangeException($"{Length}個のリストの{index}番目に挿入しようとしている");
            return new SqlQueryBlockCommentElement(
                states
                .Select(state => SqlQueryStringSlotField.Create(draft, state))
                .LINQEmbed(index, Values)
                );
        }
        internal SqlQueryBlockCommentElement RemoveChildren(ushort index, ushort count){
            if (index + count > Length) throw new ArgumentOutOfRangeException($"{Length}個のリストの{index}番目から{count}個削除しようとしている");
            return new SqlQueryBlockCommentElement(
                Values
                .LINQWithout(index, count)
                );
        }
        #endregion
    }
    /// <summary>
    /// 空白文字のうち、行コメントを表す抽象クラス。
    /// </summary>
    internal abstract record SqlQueryLineCommentElement : SqlQueryCommentElement{
        internal SqlQueryStringSlotField Value { get; }
        internal SqlQueryLineCommentElement(SqlQueryStringSlotField value){
            Value = value;
        }
        internal override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotFieldAbstract Slot)> IterateChildren() {
            yield return (new SqlQueryElementRole(SqlQueryElementRoleEnum.String, 0), Value);
        }
    }
    /// <summary>
    /// 空白文字のうち、通常の行コメントを表す具象クラス。`--`で始まる形式。
    /// </summary>
    internal sealed record SqlQueryDoubleDashCommentElement : SqlQueryLineCommentElement{
        //コンストラクタ
        internal SqlQueryDoubleDashCommentElement(SqlQueryStringSlotField value) : base(value) { }

        #region ファクトリ
        internal static SqlQueryDoubleDashCommentElement CreateFromDraftAndState(SqlQueryDraft draft, SqlQueryStringSlotState value){
            return new SqlQueryDoubleDashCommentElement(SqlQueryStringSlotField.CreateNormal(draft, value.Element));
        }
        internal static SqlQueryDoubleDashCommentElement CreateFromIdAndState(SqlQuerySlotId id, SqlQueryStringSlotState value){
            return new SqlQueryDoubleDashCommentElement(new SqlQueryStringSlotField(id, SqlQuerySlotCapabilities.OnlyNormal, value));
        }
        internal static SqlQueryDoubleDashCommentElement CreateFromDraftAndElement(SqlQueryDraft draft, SqlQueryStringElement value){
            return new SqlQueryDoubleDashCommentElement(SqlQueryStringSlotField.CreateNormal(draft, value));
        }
        internal static SqlQueryDoubleDashCommentElement CreateFromIdAndElement(SqlQuerySlotId id, SqlQueryStringElement value){
            return new SqlQueryDoubleDashCommentElement(new SqlQueryStringSlotField(id, SqlQuerySlotCapabilities.OnlyNormal, value.WrapToNormalState()));
        }
        internal static SqlQueryDoubleDashCommentElement CreateFromDraftAndString(SqlQueryDraft draft, string value){
            return new SqlQueryDoubleDashCommentElement(SqlQueryStringSlotField.CreateNormal(draft, SqlQueryStringElement.Create(value)));
        }
        internal static SqlQueryDoubleDashCommentElement CreateFromIdAndString(SqlQuerySlotId id, string value){
            return new SqlQueryDoubleDashCommentElement(new SqlQueryStringSlotField(id, SqlQuerySlotCapabilities.OnlyNormal, SqlQueryStringElement.Create(value).WrapToNormalState()));
        }
        #endregion

        #region 再帰処理

        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            return new SqlBuiltQueryFragmentText(
                $"--{Value}",
                before: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                after: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied, Newline: RequiredStatus.Unsatisfied),
                TriviaOnly: true
                );
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            return new([$"[行コメント] : {Value}"]);
        }
        internal override SqlQueryDoubleDashCommentElement CloneNode(SqlQueryDraft draft){
            return CreateFromDraftAndState(draft, Value.State);
        }
        internal override SqlQueryLineCommentElement AdaptTo(SqlType targetDialect) => this;
        #endregion

        #region 自身と子スロットのID固定で子スロット内のStateを変更するRePlace処理
        internal override SqlQueryDoubleDashCommentElement? ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states){
            var resultState = Value.State;
            foreach (var (role, state) in states){
                if (role.Name != SqlQueryElementRoleEnum.String) return null;
                if (state is not SqlQueryStringSlotState s) return null;
                switch (role.Index){
                    case 0:
                        resultState = s;
                        break;
                    default:
                        return null;
                }
            }
            return CreateFromIdAndState(Value.Id, resultState);
        }
        internal override SqlQueryDoubleDashCommentElement? ReplaceChildrenById(IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states){
            var resultState = Value.State;
            foreach (var (id, state) in states){
                if (state is not SqlQueryStringSlotState s) return null;
                if (id == Value.Id) resultState = s;
                else return null;
            }
            return CreateFromIdAndState(Value.Id, resultState);
        }
        internal override SqlQueryDoubleDashCommentElement? ReplaceChildByRole(SqlQueryElementRole role, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenByRole([(role, state)]);
        }
        internal override SqlQueryDoubleDashCommentElement? ReplaceChildById(SqlQuerySlotId id, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenById([(id, state)]);
        }
        #endregion
    }
    /// <summary>
    /// 空白文字のうち、ハッシュコメントを表す具象クラス。`#`で始まる形式。MySQLなど一部の方言でのみ有効。
    /// </summary>
    internal sealed record SqlQueryHashCommentElement : SqlQueryLineCommentElement{
        internal override SqlType EnableType { get; } = SqlType.MySQL;//Allでないエレメントでのみ上書きする。

        //コンストラクタ
        internal SqlQueryHashCommentElement(SqlQueryStringSlotField value) : base(value) { }

        #region ファクトリ
        internal static SqlQueryHashCommentElement CreateFromDraftAndState(SqlQueryDraft draft, SqlQueryStringSlotState value){
            return new SqlQueryHashCommentElement(SqlQueryStringSlotField.CreateNormal(draft, value.Element));
        }
        internal static SqlQueryHashCommentElement CreateFromIdAndState(SqlQuerySlotId id, SqlQueryStringSlotState value){
            return new SqlQueryHashCommentElement(new SqlQueryStringSlotField(id, SqlQuerySlotCapabilities.OnlyNormal, value));
        }
        internal static SqlQueryHashCommentElement CreateFromDraftAndElement(SqlQueryDraft draft, SqlQueryStringElement value){
            return new SqlQueryHashCommentElement(SqlQueryStringSlotField.CreateNormal(draft, value));
        }
        internal static SqlQueryHashCommentElement CreateFromIdAndElement(SqlQuerySlotId id, SqlQueryStringElement value){
            return new SqlQueryHashCommentElement(new SqlQueryStringSlotField(id, SqlQuerySlotCapabilities.OnlyNormal, value.WrapToNormalState()));
        }
        internal static SqlQueryHashCommentElement CreateFromDraftAndString(SqlQueryDraft draft, string value){
            return new SqlQueryHashCommentElement(SqlQueryStringSlotField.CreateNormal(draft, SqlQueryStringElement.Create(value)));
        }
        internal static SqlQueryHashCommentElement CreateFromIdAndString(SqlQuerySlotId id, string value){
            return new SqlQueryHashCommentElement(new SqlQueryStringSlotField(id, SqlQuerySlotCapabilities.OnlyNormal, SqlQueryStringElement.Create(value).WrapToNormalState()));
        }
        #endregion

        #region 再帰処理
        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            if (context.Dialect.Type.InFlag(EnableType)){
                //ハッシュコメントをそのまま出力可能な場合
                return new SqlBuiltQueryFragmentText(
                    $"#{Value}",
                    before: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                    after: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied, Newline: RequiredStatus.Unsatisfied),
                    TriviaOnly: true
                    );
            }else{
                //ハッシュコメント非対応の方言の場合、AlertPolicyに従って処理する
                switch (context.Options.InvalidComments){
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
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            string[] fragment = [$"[ハッシュコメント] : {Value}"];
            if (!context.Dialect.Type.InFlag(EnableType)){
                switch (context.Options.InvalidComments){
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
            return new(fragment, NeedNewlineAfter: true);
        }
        internal override SqlQueryHashCommentElement CloneNode(SqlQueryDraft draft) => CreateFromDraftAndState(draft, Value.State);
        internal override SqlQueryLineCommentElement AdaptTo(SqlType targetDialect) => targetDialect.InFlag(EnableType) ? this : new SqlQueryDoubleDashCommentElement(Value);
        #endregion

        #region 自身と子スロットのID固定で子スロット内のStateを変更するRePlace処理
        internal override SqlQueryHashCommentElement? ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states){
            var resultState = Value.State;
            foreach (var (role, state) in states){
                if (role.Name != SqlQueryElementRoleEnum.String) return null;
                if (state is not SqlQueryStringSlotState s) return null;
                switch (role.Index){
                    case 0:
                        resultState = s;
                        break;
                    default:
                        return null;
                }
            }
            return CreateFromIdAndState(Value.Id, resultState);
        }
        internal override SqlQueryHashCommentElement? ReplaceChildrenById(IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states){
            var resultState = Value.State;
            foreach (var (id, state) in states){
                if (state is not SqlQueryStringSlotState s) return null;
                if (id == Value.Id) resultState = s;
                else return null;
            }
            return CreateFromIdAndState(Value.Id, resultState);
        }
        internal override SqlQueryHashCommentElement? ReplaceChildByRole(SqlQueryElementRole role, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenByRole([(role, state)]);
        }
        internal override SqlQueryHashCommentElement? ReplaceChildById(SqlQuerySlotId id, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenById([(id, state)]);
        }
        #endregion
    }
    /// <summary>
    /// コメント内部の改行を除く文字列そのものを表す具象クラス。各コメント型の子要素となる。
    /// </summary>
    /// <remarks>
    /// Build・DebugBuildは実際には使わない。Value読むだけだからな。
    /// </remarks>
    internal sealed record SqlQueryStringElement : SqlQueryElement {
        //他の要素とまとめる理由が皆無なため、Element直下の型。


        internal string Value { get; }
        internal override bool HasChild => false;
        internal override bool HasChildNode => false;
        //コンストラクタ
        internal SqlQueryStringElement(string value) {
            Value = value.Replace("\n", "").Replace("\r", "");
        }
        internal static SqlQueryStringElement Create(string value){
            return new SqlQueryStringElement(value);
        }
        internal static SqlQueryStringElement CreateNull() => new (string.Empty);
        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            return new SqlBuiltQueryFragmentText(
                Value,
                before : new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                after: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                TriviaOnly: true
                );
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            return new([$"{Value}"]);
        }
        internal override SqlQueryElement CloneNode(SqlQueryDraft draft) => this;
        internal override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotFieldAbstract Slot)> IterateChildren() { yield break; }
        internal override SqlQueryElement? ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states) => throw new NotSupportedException("文字列型ノードは子要素を持たない");
        internal override SqlQueryElement? ReplaceChildrenById(IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states) => throw new NotSupportedException("文字列型ノードは子要素を持たない");

    }



    #endregion

    #region リスト要素の定義

    internal abstract record SqlQueryStructuralList<TElement> : SqlQueryStructuralElement
        where TElement : SqlQueryStructuralElement
    {
        //リスト型の不定数子要素を受ける配列型フィールド
        protected readonly ImmutableArray<SqlQueryStructuralSlotField<TElement>> _values;
        internal int Length => _values.Length;
        internal override bool HasChildNode => _values.Length > 0;
        internal int LeastCount { get; }

        //プライベートコンストラクタ
        protected SqlQueryStructuralList(
            IEnumerable<SqlQueryStructuralSlotField<TElement>> values,
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia,
            int leastCount
            )
            :base(leadingTrivia,trailingTrivia)
        {
            LeastCount = leastCount switch{
                0 or 1 or 2 => leastCount,
                _ => throw new ArgumentException("リスト要素の最低要素数は0か1か2のみ。")
            };
            _values = values
                .Where(f=>f.IsNormal)
                .ToImmutableArray();
            if (Length < LeastCount) throw new ArgumentException($"List must contain at least {LeastCount} element.", nameof(values));
        }
        #region ファクトリメソッド
        //最終的な出力は各型で行うが、直前のSlotFields三つ組生成までは共通化できる。

        private static SqlQueryTriviaListSlotField CreateTriviaSlotFromState(SqlQuerySlotId id, SqlQueryTriviaListSlotState state) {
            return new SqlQueryTriviaListSlotField(id, SqlQuerySlotCapabilities.OnlyNormal, state);
        }
        private static SqlQueryTriviaListSlotField CreateTriviaSlotFromElement(SqlQuerySlotId id, SqlQueryTriviaList element){
            return new SqlQueryTriviaListSlotField(id, SqlQuerySlotCapabilities.OnlyNormal, SqlQueryTriviaListSlotState.CreateNormal(element));
        }


        public static (
            IEnumerable<SqlQueryStructuralSlotField<TElement>> Values,
            SqlQueryTriviaListSlotField LeadingTrivia,
            SqlQueryTriviaListSlotField TrailingTrivia
            )CreateFieldsFromIdAndState(
                IEnumerable<(SqlQuerySlotId Id, SqlQueryStructuralSlotState<TElement> State)> values,
                (SqlQuerySlotId Id, SqlQueryTriviaListSlotState State) leadingTrivia,
                (SqlQuerySlotId Id, SqlQueryTriviaListSlotState State) trailingTrivia
            )
        {
            return (Values : values
                .Where(v => v.State.IsNormal)
                .Select(v => new SqlQueryStructuralSlotField<TElement>(v.Id, SqlQuerySlotCapabilities.OnlyNormal, v.State)),
                LeadingTrivia : CreateTriviaSlotFromState(leadingTrivia.Id, leadingTrivia.State),
                TrailingTrivia : CreateTriviaSlotFromState(trailingTrivia.Id, trailingTrivia.State)
                );
        }
        public static (
            IEnumerable<SqlQueryStructuralSlotField<TElement>> Values,
            SqlQueryTriviaListSlotField LeadingTrivia,
            SqlQueryTriviaListSlotField TrailingTrivia
            ) CreateFieldsFromIdAndElement(
                IEnumerable<(SqlQuerySlotId Id, TElement Element)> values,
                (SqlQuerySlotId Id, SqlQueryTriviaList Element) leadingTrivia,
                (SqlQuerySlotId Id, SqlQueryTriviaList Element) trailingTrivia
            )
        {
            return (Values : values.Select(
                    v => new SqlQueryStructuralSlotField<TElement>(
                        v.Id,
                        SqlQuerySlotCapabilities.OnlyNormal,
                        SqlQueryStructuralSlotState<TElement>.CreateNormal(v.Element)
                    )
                ),
                LeadingTrivia: CreateTriviaSlotFromElement(leadingTrivia.Id, leadingTrivia.Element),
                TrailingTrivia: CreateTriviaSlotFromElement(trailingTrivia.Id, trailingTrivia.Element)
            );
        }
        public static (
            IEnumerable<SqlQueryStructuralSlotField<TElement>> Values,
            SqlQueryTriviaListSlotField LeadingTrivia,
            SqlQueryTriviaListSlotField TrailingTrivia
            ) CreateFieldsFromDraftAndState(
                SqlQueryDraft draft,
                IEnumerable<SqlQueryStructuralSlotState<TElement>> values,
                SqlQueryTriviaListSlotState leadingTrivia,
                SqlQueryTriviaListSlotState trailingTrivia
            )
        {
            return (Values : values
                .Where(state => state.IsNormal)
                .Select(
                    state => new SqlQueryStructuralSlotField<TElement>(
                        SqlQuerySlotId.GetNextId(draft),
                        SqlQuerySlotCapabilities.OnlyNormal,
                        state
                    )
                ),
                LeadingTrivia: CreateTriviaSlotFromState(SqlQuerySlotId.GetNextId(draft), leadingTrivia),
                TrailingTrivia: CreateTriviaSlotFromState(SqlQuerySlotId.GetNextId(draft), trailingTrivia)
                );
        }
        public static (
            IEnumerable<SqlQueryStructuralSlotField<TElement>> Values,
            SqlQueryTriviaListSlotField LeadingTrivia,
            SqlQueryTriviaListSlotField TrailingTrivia
            ) CreateFieldsFromDraftAndElement(
                SqlQueryDraft draft,
                IEnumerable<TElement> values,
                SqlQueryTriviaList leadingTrivia,
                SqlQueryTriviaList trailingTrivia
            )
        {
            return (Values : values.Select(
                element => new SqlQueryStructuralSlotField<TElement>(
                    SqlQuerySlotId.GetNextId(draft),
                    SqlQuerySlotCapabilities.OnlyNormal,
                    SqlQueryStructuralSlotState<TElement>.CreateNormal(element)
                )
            ),
                LeadingTrivia: CreateTriviaSlotFromElement(SqlQuerySlotId.GetNextId(draft), leadingTrivia),
                TrailingTrivia: CreateTriviaSlotFromElement(SqlQuerySlotId.GetNextId(draft), trailingTrivia)
            );
        }
        #endregion

        internal sealed override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotFieldAbstract Slot)> IterateChildren() {
            yield return (SqlQueryElementRole.Trivia(0), LeadingTrivia);
            var ln = _values.Length;
            for (var i = 0; i<ln; i++) {
                yield return (SqlQueryElementRole.Listed(i), _values[i]);
            }
            yield return (SqlQueryElementRole.Trivia(1), TrailingTrivia);
        }

        //リスト型はRole.Indexがバグらないようにするための二段化で、文法機能が薄いため、リスト状態のFragmentのまま上に回して構成してもらう方がいい。
        //つまり、FragmentListという別個の返り値型が要る。
        #region 再帰処理
        internal override SqlBuiltQueryFragmentNode Build(SqlBuildContext context){
            throw new NotImplementedException();
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context)
        {
            throw new NotImplementedException();
        }
        internal override SqlQueryElement CloneNode(SqlQueryDraft draft)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region 子要素取得
        //IterateChildrenするより楽っぽいのは個別実装。


        internal override bool HasRole(SqlQueryElementRole role){
            if (role.Name == SqlQueryElementRoleEnum.Trivia){
                if (role.Index <= 1) return true;
            } else if (role.Name == SqlQueryElementRoleEnum.Listed){
                if (role.Index < Length) return true;
            }
            return false;
        }
        internal override SqlQuerySlotFieldAbstract? GetChildByRole(SqlQueryElementRole role){
            if (role.Name == SqlQueryElementRoleEnum.Trivia){
                if (role.Index == 0) return LeadingTrivia;
                else if (role.Index == 1) return TrailingTrivia;
            } else if (role.Name == SqlQueryElementRoleEnum.Listed){
                if (role.Index < Length) return _values[role.Index];
            }
            return null;
        }

        #endregion

        #region 自身と子スロットのID固定で子スロット内のStateを変更するRePlace処理
        //最終的な出力は各具象型でしかできないが、その直前のvaluesの生成までは基底で共通処理にできる。


        protected (
            IEnumerable<SqlQueryStructuralSlotField<TElement>> Values,
            SqlQueryTriviaListSlotField LeadingTrivia,
            SqlQueryTriviaListSlotField TrailingTrivia
            ) ReloadFieldsById(IEnumerable<(SqlQuerySlotId, SqlQuerySlotStateAbstract)> states) {
            var newValues = _values.ToList();
            var newLeadingTrivia = LeadingTrivia;
            var newTrailingTrivia = TrailingTrivia;
            foreach (var (id,state) in states) {
                if (!state.IsNormal) throw new ArgumentException("リストの子要素に非実要素は入らない");

                bool idExist = false;
                if (state is SqlQueryTriviaListSlotState l){
                    if (newLeadingTrivia.Id == id){
                        newLeadingTrivia.ReplaceState(l);
                        idExist = true;
                    } else if (newTrailingTrivia.Id == id){
                        newTrailingTrivia.ReplaceState(l);
                        idExist = true;
                    }
                } else if (state is SqlQueryStructuralSlotState<TElement> e){
                    for (var i2 = 0; i2 < newValues.Count; i2++){
                        if (newValues[i2].Id == id){
                            newValues[i2] = newValues[i2].ReplaceState(e);
                            idExist = true;
                            break;
                        }
                    }
                } else {
                    throw new ArgumentException("このリストの子要素でも前後の空白文字リストでもない型の要素が与えられている");
                }

                if (!idExist) throw new ArgumentException($"指定のIDのスロットが直下に存在しない：{id.Value}");
            }
            return (Values : newValues, LeadingTrivia : newLeadingTrivia, TrailingTrivia : newTrailingTrivia);
        }
        protected (
            IEnumerable<SqlQueryStructuralSlotField<TElement>> Values,
            SqlQueryTriviaListSlotField LeadingTrivia,
            SqlQueryTriviaListSlotField TrailingTrivia
            ) ReloadFieldsByRole(IEnumerable<(SqlQueryElementRole, SqlQuerySlotStateAbstract)> states){
            var newValues = _values.ToList();
            var newLeadingTrivia = LeadingTrivia;
            var newTrailingTrivia = TrailingTrivia;
            foreach (var (role, state) in states){
                if (!state.IsNormal) throw new ArgumentException("リストの子要素に非実要素は入らない");

                if (role.Name == SqlQueryElementRoleEnum.Trivia) {
                    if (state is not SqlQueryTriviaListSlotState l) throw new ArgumentException("空白文字リストスロットに他の型が指定されている。");
                    if (role.Index == 0) {
                        newLeadingTrivia = newLeadingTrivia.ReplaceState(l);
                        continue;
                    } else if (role.Index == 1) {
                        newTrailingTrivia = newTrailingTrivia.ReplaceState(l);
                        continue;
                    } else {
                        throw new ArgumentOutOfRangeException($"空白文字リスト要素は前後二つしか存在しない。Index{role.Index}は指定できない。");
                    }
                } else if (role.Name == SqlQueryElementRoleEnum.Listed) {
                    if (state is not SqlQueryStructuralSlotState<TElement> c) throw new ArgumentException($"{typeof(TElement)}型のリストに{state.GenericType}型の要素が渡されている。");
                    if (role.Index >= newValues.Count) throw new ArgumentOutOfRangeException($"{newValues.Count}個のリストの{role.Index}番目に上書きしようとしている。");
                    newValues[role.Index] = newValues[role.Index].ReplaceState(c);
                    continue;
                } else {
                    throw new ArgumentException($"リストの子要素でも空白文字リストでも無い型が指定されている。");
                }
            }
            return (Values : newValues, LeadingTrivia : newLeadingTrivia, TrailingTrivia : newTrailingTrivia);
        }
        #endregion

        #region リスト型特有の挿入・削除処理

        protected IEnumerable<SqlQueryStructuralSlotField<TElement>> ReloadChildrenWithAdded(SqlQueryDraft draft, ushort index, SqlQueryStructuralSlotState<TElement>[] states) {
            if (index > Length) throw new ArgumentOutOfRangeException($"{Length}個のリストの{index}番目に挿入しようとしている");
            return states
                .Select(state => SqlQueryStructuralSlotField<TElement>.Create(draft,SqlQuerySlotCapabilities.OnlyNormal,state))
                .LINQEmbed(index, _values);
        }
        protected IEnumerable<SqlQueryStructuralSlotField<TElement>> ReloadChildrenWithRemoved(ushort index, ushort count){
            if (index >= Length) throw new ArgumentOutOfRangeException($"{Length}個のリストの{index}番目を削除しようとしている");
            return _values.LINQWithout(index, count);
        }

        #endregion
    }

    internal sealed record SqlQueryStructuralListAllow0<TElement> : SqlQueryStructuralList<TElement>
        where TElement : SqlQueryStructuralElement
    {
        //コンストラクタ
        private SqlQueryStructuralListAllow0(
            IEnumerable<SqlQueryStructuralSlotField<TElement>> values,
                SqlQueryTriviaListSlotField leadingTrivia,
                SqlQueryTriviaListSlotField trailingTrivia
            ) : base(values,leadingTrivia,trailingTrivia,0) { }

        #region ファクトリメソッド
        internal static SqlQueryStructuralListAllow0<TElement> CreateFromIdAndState(
                IEnumerable<(SqlQuerySlotId Id, SqlQueryStructuralSlotState<TElement> State)> values,
                (SqlQuerySlotId Id, SqlQueryTriviaListSlotState State) leadingTrivia,
                (SqlQuerySlotId Id, SqlQueryTriviaListSlotState State) trailingTrivia
            )
        {
            var fields = CreateFieldsFromIdAndState(values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListAllow0<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal static SqlQueryStructuralListAllow0<TElement> CreateFromIdAndElement(
                IEnumerable<(SqlQuerySlotId Id, TElement Element)> values,
                (SqlQuerySlotId Id, SqlQueryTriviaList Element) leadingTrivia,
                (SqlQuerySlotId Id, SqlQueryTriviaList Element) trailingTrivia
            )
        {
            var fields = CreateFieldsFromIdAndElement(values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListAllow0<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal static SqlQueryStructuralListAllow0<TElement> CreateFromDraftAndState(
                SqlQueryDraft draft,
                IEnumerable<SqlQueryStructuralSlotState<TElement>> values,
                SqlQueryTriviaListSlotState leadingTrivia,
                SqlQueryTriviaListSlotState trailingTrivia
            )
        {
            var fields = CreateFieldsFromDraftAndState(draft,values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListAllow0<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal static SqlQueryStructuralListAllow0<TElement> CreateFromDraftAndElement(
                SqlQueryDraft draft,
                IEnumerable<TElement> values,
                SqlQueryTriviaList leadingTrivia,
                SqlQueryTriviaList trailingTrivia
            )
        {
            var fields = CreateFieldsFromDraftAndElement(draft, values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListAllow0<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        #endregion
        #region 自身と子スロットのID固定で子スロット内のStateを変更するReplace処理
        internal override SqlQueryStructuralListAllow0<TElement> ReplaceChildrenById(IEnumerable<(SqlQuerySlotId Id, SqlQuerySlotStateAbstract State)> states){
            var fields = ReloadFieldsById(states);
            return new(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal override SqlQueryStructuralListAllow0<TElement> ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotStateAbstract State)> states){
            var fields = ReloadFieldsByRole(states);
            return new(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal override SqlQueryStructuralListAllow0<TElement> ReplaceChildById(SqlQuerySlotId id, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenById([(id, state)]);
        }
        internal override SqlQueryStructuralListAllow0<TElement> ReplaceChildByRole(SqlQueryElementRole Role, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenByRole([(Role, state)]);
        }
        internal override SqlQueryStructuralListAllow0<TElement> ReplaceTriviaByField(SqlQueryTriviaListSlotField leadingTrivia, SqlQueryTriviaListSlotField trailingTrivia) {
            return new SqlQueryStructuralListAllow0<TElement>(_values, leadingTrivia, trailingTrivia);
        }
        #endregion
        #region リスト型特有の挿入・削除処理
        internal SqlQueryStructuralListAllow0<TElement> PushChildren(SqlQueryDraft draft, SqlQueryStructuralSlotState<TElement>[] states) => AddChildren(draft, (ushort)Length, states);
        internal SqlQueryStructuralListAllow0<TElement> AddChildren(SqlQueryDraft draft, ushort index, SqlQueryStructuralSlotState<TElement>[] states){
            return new(
                ReloadChildrenWithAdded(draft, index, states),
                LeadingTrivia,
                TrailingTrivia
                );
        }
        internal SqlQueryStructuralListAllow0<TElement> RemoveChildren(ushort index, ushort count = 0) {
            return new(
                ReloadChildrenWithRemoved(index, count),
                LeadingTrivia,
                TrailingTrivia
                );
        }
        #endregion

        
    }
    internal sealed record SqlQueryStructuralListUpper1<TElement> : SqlQueryStructuralList<TElement>
        where TElement : SqlQueryStructuralElement
    {
        //コンストラクタ
        private SqlQueryStructuralListUpper1(
            IEnumerable<SqlQueryStructuralSlotField<TElement>> values,
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            )
            : base(values,leadingTrivia,trailingTrivia,1) {
            if(Length < LeastCount) throw new ArgumentException("List must contain at least one element.", nameof(values));
        }
        #region ファクトリメソッド
        internal static SqlQueryStructuralListUpper1<TElement> CreateFromIdAndState(
                IEnumerable<(SqlQuerySlotId Id, SqlQueryStructuralSlotState<TElement> State)> values,
                (SqlQuerySlotId Id, SqlQueryTriviaListSlotState State) leadingTrivia,
                (SqlQuerySlotId Id, SqlQueryTriviaListSlotState State) trailingTrivia
            )
        {
            var fields = CreateFieldsFromIdAndState(values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListUpper1<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal static SqlQueryStructuralListUpper1<TElement> CreateFromIdAndElement(
                IEnumerable<(SqlQuerySlotId Id, TElement Element)> values,
                (SqlQuerySlotId Id, SqlQueryTriviaList Element) leadingTrivia,
                (SqlQuerySlotId Id, SqlQueryTriviaList Element) trailingTrivia
            )
        {
            var fields = CreateFieldsFromIdAndElement(values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListUpper1<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal static SqlQueryStructuralListUpper1<TElement> CreateFromDraftAndState(
                SqlQueryDraft draft,
                IEnumerable<SqlQueryStructuralSlotState<TElement>> values,
                SqlQueryTriviaListSlotState leadingTrivia,
                SqlQueryTriviaListSlotState trailingTrivia
            )
        {
            var fields = CreateFieldsFromDraftAndState(draft, values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListUpper1<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal static SqlQueryStructuralListUpper1<TElement> CreateFromDraftAndElement(
                SqlQueryDraft draft,
                IEnumerable<TElement> values,
                SqlQueryTriviaList leadingTrivia,
                SqlQueryTriviaList trailingTrivia
            )
        {
            var fields = CreateFieldsFromDraftAndElement(draft, values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListUpper1<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        #endregion
        #region 自身と子スロットのID固定で子スロット内のStateを変更するReplace処理
        internal override SqlQueryStructuralListUpper1<TElement> ReplaceChildrenById(IEnumerable<(SqlQuerySlotId Id, SqlQuerySlotStateAbstract State)> states){
            var fields = ReloadFieldsById(states);
            return new(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal override SqlQueryStructuralListUpper1<TElement> ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotStateAbstract State)> states){
            var fields = ReloadFieldsByRole(states);
            return new(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal override SqlQueryStructuralListUpper1<TElement> ReplaceChildById(SqlQuerySlotId id, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenById([(id, state)]);
        }
        internal override SqlQueryStructuralListUpper1<TElement> ReplaceChildByRole(SqlQueryElementRole Role, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenByRole([(Role, state)]);
        }
        internal override SqlQueryStructuralListUpper1<TElement> ReplaceTriviaByField(SqlQueryTriviaListSlotField leadingTrivia, SqlQueryTriviaListSlotField trailingTrivia){
            return new SqlQueryStructuralListUpper1<TElement>(_values, leadingTrivia, trailingTrivia);
        }
        #endregion
        #region リスト型特有の挿入・削除処理
        internal SqlQueryStructuralListUpper1<TElement> PushChildren(SqlQueryDraft draft, SqlQueryStructuralSlotState<TElement>[] states) => AddChildren(draft, (ushort)Length, states);
        internal SqlQueryStructuralListUpper1<TElement> AddChildren(SqlQueryDraft draft, ushort index, SqlQueryStructuralSlotState<TElement>[] states){
            return new(
                ReloadChildrenWithAdded(draft, index, states),
                LeadingTrivia,
                TrailingTrivia
                );
        }
        internal SqlQueryStructuralListUpper1<TElement> RemoveChildren(ushort index, ushort count = 0){
            return new(
                ReloadChildrenWithRemoved(index, count),
                LeadingTrivia,
                TrailingTrivia
                );
        }
        #endregion
    }
    internal sealed record SqlQueryStructuralListUpper2<TElement> : SqlQueryStructuralList<TElement>
        where TElement : SqlQueryStructuralElement
    {
        //コンストラクタ
        private SqlQueryStructuralListUpper2(
            IEnumerable<SqlQueryStructuralSlotField<TElement>> values,
            SqlQueryTriviaListSlotField leadingTrivia,
            SqlQueryTriviaListSlotField trailingTrivia
            )
            : base(values,leadingTrivia,trailingTrivia,2)
        {
            if (Length < LeastCount) throw new ArgumentException("List must contain at least one element.", nameof(values));
        }
        #region ファクトリメソッド
        public static SqlQueryStructuralListUpper2<TElement> CreateFromIdAndState(
                IEnumerable<(SqlQuerySlotId Id, SqlQueryStructuralSlotState<TElement> State)> values,
                (SqlQuerySlotId Id, SqlQueryTriviaListSlotState State) leadingTrivia,
                (SqlQuerySlotId Id, SqlQueryTriviaListSlotState State) trailingTrivia
            )
        {
            var fields = CreateFieldsFromIdAndState(values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListUpper2<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        public static SqlQueryStructuralListUpper2<TElement> CreateFromIdAndElement(
                IEnumerable<(SqlQuerySlotId Id, TElement Element)> values,
                (SqlQuerySlotId Id, SqlQueryTriviaList Element) leadingTrivia,
                (SqlQuerySlotId Id, SqlQueryTriviaList Element) trailingTrivia
            )
        {
            var fields = CreateFieldsFromIdAndElement(values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListUpper2<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        public static SqlQueryStructuralListUpper2<TElement> CreateFromDraftAndState(
                SqlQueryDraft draft,
                IEnumerable<SqlQueryStructuralSlotState<TElement>> values,
                SqlQueryTriviaListSlotState leadingTrivia,
                SqlQueryTriviaListSlotState trailingTrivia
            )
        {
            var fields = CreateFieldsFromDraftAndState(draft, values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListUpper2<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        public static SqlQueryStructuralListUpper2<TElement> CreateFromDraftAndElement(
                SqlQueryDraft draft,
                IEnumerable<TElement> values,
                SqlQueryTriviaList leadingTrivia,
                SqlQueryTriviaList trailingTrivia
            )
        {
            var fields = CreateFieldsFromDraftAndElement(draft, values, leadingTrivia, trailingTrivia);
            return new SqlQueryStructuralListUpper2<TElement>(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        #endregion

        #region 自身と子スロットのID固定で子スロット内のStateを変更するReplace処理
        internal override SqlQueryStructuralListUpper2<TElement> ReplaceChildrenById(IEnumerable<(SqlQuerySlotId Id, SqlQuerySlotStateAbstract State)> states){
            var fields = ReloadFieldsById(states);
            return new(fields.Values,fields.LeadingTrivia,fields.TrailingTrivia);
        }
        internal override SqlQueryStructuralListUpper2<TElement> ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotStateAbstract State)> states){
            var fields = ReloadFieldsByRole(states);
            return new(fields.Values, fields.LeadingTrivia, fields.TrailingTrivia);
        }
        internal override SqlQueryStructuralListUpper2<TElement> ReplaceChildById(SqlQuerySlotId id, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenById([(id,state)]);
        }
        internal override SqlQueryStructuralListUpper2<TElement> ReplaceChildByRole(SqlQueryElementRole Role, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenByRole([(Role, state)]);
        }
        internal override SqlQueryStructuralListUpper2<TElement> ReplaceTriviaByField(SqlQueryTriviaListSlotField leadingTrivia, SqlQueryTriviaListSlotField trailingTrivia){
            return new SqlQueryStructuralListUpper2<TElement>(_values, leadingTrivia, trailingTrivia);
        }

        #endregion
        #region リスト型特有の挿入・削除処理
        internal SqlQueryStructuralListUpper2<TElement> PushChildren(SqlQueryDraft draft, SqlQueryStructuralSlotState<TElement>[] states) => AddChildren(draft, (ushort)Length, states);
        internal SqlQueryStructuralListUpper2<TElement> AddChildren(SqlQueryDraft draft, ushort index, SqlQueryStructuralSlotState<TElement>[] states){
            return new(
                ReloadChildrenWithAdded(draft, index, states),
                LeadingTrivia,
                TrailingTrivia
                );
        }
        internal SqlQueryStructuralListUpper2<TElement> RemoveChildren(ushort index, ushort count = 0){
            return new(
                ReloadChildrenWithRemoved(index, count),
                LeadingTrivia,
                TrailingTrivia
                );
        }
        #endregion
    }

    internal sealed record SqlQueryTriviaList : SqlQueryElement{
        //リスト型の不定数子要素を受ける配列型フィールド
        private readonly ImmutableArray<SqlQueryTriviaSlotField> _values;
        internal int Length => _values.Length;
        internal override bool HasChild => _values.Length > 0;
        internal sealed override bool HasChildNode => false;
        //プライベートコンストラクタ
        internal SqlQueryTriviaList(IEnumerable<SqlQueryTriviaSlotField> values){
            _values = [..values];
        }
        #region ファクトリメソッド
        internal static SqlQueryTriviaList CreateFromIdAndState(
                IEnumerable<(SqlQuerySlotId Id, SqlQueryTriviaSlotState State)> values
            )
        {
            var slotFields = values
                .Where(v => v.State.IsNormal)
                .Select(v => new SqlQueryTriviaSlotField(v.Id, SqlQuerySlotCapabilities.OnlyNormal, v.State))
                ;
            return new SqlQueryTriviaList(slotFields);
        }
        internal static SqlQueryTriviaList CreateFromIdAndElement(
                IEnumerable<(SqlQuerySlotId Id, SqlQueryTriviaElement Element)> values
            )
        {
            var slotFields = values.Select(
                v => new SqlQueryTriviaSlotField(
                    v.Id,
                    SqlQuerySlotCapabilities.OnlyNormal,
                    SqlQueryTriviaSlotState.CreateNormal(v.Element)
                )
            );
            return new SqlQueryTriviaList(slotFields);
        }
        internal static SqlQueryTriviaList CreateFromDraftAndState(
                SqlQueryDraft draft,
                IEnumerable<SqlQueryTriviaSlotState> values
            )
        {
            var slotFields = values
                .Where(state => state.IsNormal)
                .Select(
                    state => new SqlQueryTriviaSlotField(
                        SqlQuerySlotId.GetNextId(draft),
                        SqlQuerySlotCapabilities.OnlyNormal,
                        state
                    )
                ).ToList();
            return new SqlQueryTriviaList(slotFields);
        }
        internal static SqlQueryTriviaList CreateFromDraftAndElement(
                SqlQueryDraft draft,
                IEnumerable<SqlQueryTriviaElement> values
            )
        {
            var slotFields = values.Select(
                element => new SqlQueryTriviaSlotField(
                    SqlQuerySlotId.GetNextId(draft),
                    SqlQuerySlotCapabilities.OnlyNormal,
                    SqlQueryTriviaSlotState.CreateNormal(element)
                )
            );
            return new SqlQueryTriviaList(slotFields);
        }
        /// <summary>
        /// 0許容リスト型なので、子要素が0の場合にドラフト参照無しに作成できるファクトリが有用。
        /// </summary>
        /// <returns></returns>
        internal static SqlQueryTriviaList CreateFrom0() => new ([]);
        #endregion

        internal override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotFieldAbstract Slot)> IterateChildren(){
            for (var i = 0; i < _values.Length; i++){
                yield return (new SqlQueryElementRole(SqlQueryElementRoleEnum.Trivia, (ushort)i), _values[i]);
            }
        }

        #region 再帰処理
        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            StringBuilder sb = new();
            bool needNewLine = false;
            foreach (var trivia in _values){
                if (trivia.State.Element is not SqlQueryTriviaElement te) throw new NotSupportedException("空白文字リストの子は全て空白文字要素のはず。");
                //MissingNewlines : 行コメントの後に改行がない場合の処理
                if (needNewLine && te is not SqlQueryNewlineElement){
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
                if (!context.Options.ShowTrivia.HasFlag(te.TriviaType)) continue;

                //子要素のビルド結果の回収
                var fragment = trivia.Build(context);
                if (fragment is not SqlBuiltQueryFragmentText f) throw new InvalidOperationException("空白文字要素のBuild結果がSqlBuiltQueryFragmentTextと異なる可能性は無いはず。");
                sb.Append(f.Main);
                needNewLine = f.After.Newline == RequiredStatus.Unsatisfied;
            }
            return new SqlBuiltQueryFragmentText(
                sb.ToString(),
                before : new SqlQueryFragmentConditions(Space : RequiredStatus.Satisfied),
                after: new SqlQueryFragmentConditions(Space: RequiredStatus.Satisfied),
                TriviaOnly : true
                );
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            List<string> outputs = [""];//先頭行は、後から入るIDとRole以外に必要な情報が無いので空文字。
            bool needNewLine = false;
            foreach (var trivia in _values){
                if (trivia.State.Element is not SqlQueryTriviaElement te) throw new NotSupportedException("空白文字リストの子は全て空白文字要素のはず。");
                //MissingNewlines : 行コメントの後に改行がない場合の処理
                if (needNewLine && te is not SqlQueryNewlineElement){
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
                if (!context.Options.ShowTrivia.HasFlag(te.TriviaType)) continue;

                //子要素のビルド結果の回収
                var fragment = trivia.DebugBuild(context);

                //罫線追加処理
                var framedFragment = fragment.Values;
                framedFragment[0] = $"├{framedFragment[0]}";
                if (framedFragment.Length > 1)
                {
                    for (int i = 1; i < framedFragment.Length; i++)
                    {
                        framedFragment[i] = $"│{framedFragment[i]}";
                    }
                }

                outputs.AddRange(framedFragment);
                needNewLine = fragment.NeedNewlineAfter;
            }
            //末尾の罫線を変える処理
            for (var i = outputs.Count - 1; i >= 0; i--)
            {
                if (outputs[i].StartsWith("├"))
                {
                    outputs[i] = $"└{outputs[i].Substring(1)}";
                    break;
                }
                else if (outputs[i].StartsWith("│"))
                {
                    outputs[i] = $"　{outputs[i].Substring(1)}";
                }
            }

            return new SqlDebugBuiltQuery([.. outputs], NeedNewlineAfter: needNewLine);
        }
        internal override SqlQueryTriviaList CloneNode(SqlQueryDraft draft){
            var clonedChildren = new List<SqlQueryTriviaSlotState>();
            foreach (var (role, slot) in IterateChildren()){
                if (slot.State is not SqlQueryTriviaSlotState s) throw new InvalidOperationException("SqlQueryTriviaListの子要素がSqlQueryTriviaElementでない、想定外の状況。");
                clonedChildren.Add(s.CloneNode(draft));
            }
            return CreateFromDraftAndState(draft, clonedChildren);
        }
        #endregion



        #region 子要素取得
        //IterateChildrenするより楽っぽいのは個別実装。


        internal override bool HasRole(SqlQueryElementRole role){
            if (role.Name == SqlQueryElementRoleEnum.Listed){
                if (role.Index < Length) return true;
            }
            return false;
        }
        internal override SqlQueryTriviaSlotField? GetChildByRole(SqlQueryElementRole role){
            if (role.Name == SqlQueryElementRoleEnum.Listed){
                if (role.Index < Length) return _values[role.Index];
            }
            return null;
        }
        internal override SqlQueryTriviaSlotField? GetChildById(SqlQuerySlotId id) {
            foreach (var triviaField in _values) {
                if (triviaField.Id == id) return triviaField;
            }
            return null;
        }

        #endregion

        #region 自身と子スロットのID固定で子スロット内のStateを変更するRePlace処理

        private List<SqlQueryTriviaSlotField> ReloadFieldsById(IEnumerable<(SqlQuerySlotId, SqlQueryTriviaSlotState)> states){
            var newValues = _values.ToList();
            foreach (var (id, state) in states){
                if (!state.IsNormal) throw new ArgumentException("リストの子要素に非実要素は入らない");

                bool idExist = false;
                for (var i = 0; i < newValues.Count; i++){
                    if (newValues[i].Id == id){
                        newValues[i] = newValues[i].ReplaceState(state);
                        idExist = true;
                        break;
                    }
                }

                if (!idExist) throw new ArgumentException($"指定のIDのスロットが直下に存在しない：{id.Value}");
            }
            return newValues;
        }
        private List<SqlQueryTriviaSlotField> ReloadFieldsByRole(IEnumerable<(SqlQueryElementRole, SqlQueryTriviaSlotState)> states){
            var newValues = _values.ToList();
            foreach (var (role, state) in states){
                if (!state.IsNormal) throw new ArgumentException("リストの子要素に非実要素は入らない");

                if (role.Name == SqlQueryElementRoleEnum.Listed){
                    if (role.Index >= newValues.Count) throw new ArgumentOutOfRangeException($"{newValues.Count}個のリストの{role.Index}番目に上書きしようとしている。");
                    newValues[role.Index] = newValues[role.Index].ReplaceState(state);
                    continue;
                }
            }
            return newValues;
        }
        internal override SqlQueryTriviaList ReplaceChildrenById(IEnumerable<(SqlQuerySlotId Id, SqlQuerySlotStateAbstract State)> states){
            List<(SqlQuerySlotId, SqlQueryTriviaSlotState)> castedStates = [];
            foreach (var (id, state) in states) {
                if (state is SqlQueryTriviaSlotState t) castedStates.Add((id, t));
            }
            return new(ReloadFieldsById(castedStates));
        }
        internal override SqlQueryTriviaList ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotStateAbstract State)> states){
            List<(SqlQueryElementRole, SqlQueryTriviaSlotState)> castedStates = [];
            foreach (var (role, state) in states){
                if (state is SqlQueryTriviaSlotState t) castedStates.Add((role, t));
            }
            return new(ReloadFieldsByRole(castedStates));
        }
        internal override SqlQueryTriviaList ReplaceChildById(SqlQuerySlotId id, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenById([(id, state)]);
        }
        internal override SqlQueryTriviaList ReplaceChildByRole(SqlQueryElementRole Role, SqlQuerySlotStateAbstract state){
            return ReplaceChildrenByRole([(Role, state)]);
        }


        #endregion

        #region リスト型特有の挿入・削除処理

        private IEnumerable<SqlQueryTriviaSlotField> ReloadChildrenWithAdded(SqlQueryDraft draft, ushort index, IEnumerable<SqlQueryTriviaSlotState> states){
            if (index > Length) throw new ArgumentOutOfRangeException($"{Length}個のリストの{index}番目に挿入しようとしている");
            return states
                .Select(state => SqlQueryTriviaSlotField.Create(draft, state))
                .LINQEmbed(index, _values);
        }
        private IEnumerable<SqlQueryTriviaSlotField> ReloadChildrenWithRemoved(ushort index, ushort count){
            if (index >= Length) throw new ArgumentOutOfRangeException($"{Length}個のリストの{index}番目を削除しようとしている");
            return _values
                .Take(index)
                .Concat(_values.Skip(index + count))
                ;
        }
        internal SqlQueryTriviaList PushChildren(SqlQueryDraft draft, IEnumerable<SqlQueryTriviaSlotState> states) => AddChildren(draft, (ushort)Length, states);
        internal SqlQueryTriviaList AddChildren(SqlQueryDraft draft, ushort index, IEnumerable<SqlQueryTriviaSlotState> states){
            return new(ReloadChildrenWithAdded(draft, index, states));
        }
        internal SqlQueryTriviaList AddChildrenByElement(SqlQueryDraft draft, ushort index, SqlQueryTriviaElement[] elements){
            return new(ReloadChildrenWithAdded(draft, index, elements.Select(e => SqlQueryTriviaSlotState.CreateNormal(e))));
        }
        internal SqlQueryTriviaList RemoveChildren(ushort index, ushort count = 0){
            return new(ReloadChildrenWithRemoved(index, count));
        }

        #endregion


        internal override SqlQueryTriviaList AdaptTo(SqlType targetDialect){
            //ハッシュコメント非対応方言かつ、ハッシュコメントを含んでいるときのみ、再構成が必要になる。
            //IDとStateのセットで対象要素を取り出して、Element.AdaptTo(targetDialect)で再構成したものでReplaceする方針。
            if (EnableType.HasFlag(targetDialect)) return this;
            var adaptedChildren = IterateChildren()
                                .Where(child => !child.Slot.State.Element.EnableType.HasFlag(targetDialect))
                                .Select(child => (child.Role,
                                    child
                                    .Slot
                                    .State
                                    .Element
                                    .ChainCastTo<SqlQueryTriviaElement>()
                                    .AdaptTo(targetDialect)
                                    .WrapToNormalState()
                                    .ChainCastTo<SqlQuerySlotStateAbstract>()
                                    )
                                )
                                ;
            return ReplaceChildrenByRole(adaptedChildren);
        }


    }

    #endregion


    //未達成項目：
    //StructualElementの時点で、Build内で空白文字を接続する処理を定義しておくべき。
    //それに伴って、いくつかBuildOptionの項目も追加する必要はある。
    //Triviaと本体とを分けた状態で親に回して、親の方で本体を加工したのちにTriviaを接続する方が良さげ。
    //定置キーワードの指定や空白文字の必須性など、一段上で処理した方がいい処理は多そう。
    //→代用ElementによるTrivia配置構成の更新は一通り終わったので、Build処理とDebugBuild処理の整備に入る。

    //裸文字要素の定義を完了。

    #region Token要素の定義

    internal abstract record SqlQueryKeywordElement : SqlQueryTokenElement{
        public abstract string String { get; }
        public SqlQueryKeywordElement(SqlQueryTriviaListSlotField leadingTrivia, SqlQueryTriviaListSlotField trailingTrivia)
            : base(leadingTrivia, trailingTrivia)
        {
        }
    }
    /// <summary>
    /// 定置固定キーワード要素。DefaultOnlyでのみ作られるため、実際にインスタンスが作られることは無く、型パラメータとしてのみ使われる。
    /// </summary>
    internal sealed record SqlQueryFixedKeywordElement : SqlQueryKeywordElement{
        public override string String { get; }
        public SqlQueryFixedKeywordElement(string word, SqlQueryTriviaListSlotField leadingTrivia, SqlQueryTriviaListSlotField trailingTrivia)
            : base(leadingTrivia, trailingTrivia)
        {
            String = word;
        }

        internal override IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotFieldAbstract Slot)> IterateChildren() {
            yield return (SqlQueryElementRole.Trivia(0),LeadingTrivia);
            yield return (SqlQueryElementRole.Trivia(1), TrailingTrivia);
        }

        #region 再帰処理
        /// <summary>
        /// 実行用のクエリを構築する処理。
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        internal override SqlBuiltQueryFragmentNode Build(SqlBuildContext context) {
            return new SqlBuiltQueryFragmentNode(
                LeadingTrivia.Build(context),
                new SqlBuiltQueryFragmentText(String, new SqlQueryFragmentConditions(), new SqlQueryFragmentConditions()),
                TrailingTrivia.Build(context)
                );
        }
        /// <summary>
        /// 人間用のクエリ構造図を構築する処理。
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        internal abstract SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context);
        //再帰的にクローンする必要があるので、この処理ではまずい。
        //internal SqlQueryElement CloneNode() => this with { Id = SqlQueryDraft.GetNewId() };
        /// <summary>
        /// 構造を保ったまま、IDのみ再帰的に振り直したクローンを生成する。
        /// </summary>
        /// <param name="draft"></param>
        /// <returns></returns>
        internal abstract SqlQueryElement CloneNode(SqlQueryDraft draft);
        internal virtual SqlQueryElement AdaptTo(SqlType targetDialect) => this;
        #endregion

    }




    #endregion



    //ここから下は未整理

    /// <summary>
    /// SQLクエリの「句」に対応するデータクラスの基底。
    /// </summary>
    internal interface ISqlClause : SqlQueryNode{
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

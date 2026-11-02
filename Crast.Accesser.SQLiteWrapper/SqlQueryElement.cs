using System.Collections.Immutable;
using System.Data;

namespace Crast.Accessor.SqlWrapper{

    //枝単位での更新処理やクローン時のID変更などは、上に上がることが可能なDraftとPathが担う。
    //ElementとSlotは、直下と自身だけを見て自分の処理を行う。
    //ただし、Build・DebugBuild・Clone及びAdaptToに関しては、Element自体が再帰メソッドを持つのが必須なため
    //再帰メソッド自体はElementに実装するが、呼び出しはPath(AdaptToはDraft)から行う。

    //共変と不変を両方対応するのはほぼ不可能であるため、
    //不変部分のみジェネリック型の型パラメータで対応し、共変部分は読み取り側で型キャストして対処する。
    //メソッドの所属型と引数型と返り値型がジェネリックで共通化されていることを保証することが第一義であるため、
    //ジェネリックで解決できないならパターンマッチングで解決するしかない。

    #region 引数用のデータクラスや列挙型

    /// <summary>
    /// ルートからあるElementまでのRole列を表すデータクラス。
    /// </summary>
    /// <remarks>
    /// 人の感覚的にはこの方が構造をとらえやすい気はする。
    /// 
    /// </remarks>
    /// <param name="Roles"></param>
    public record class SqlQuerySelector(SqlQuerySlotId RootId, ImmutableArray<SqlQueryElementRole> Roles){
        public int Length => Roles.Length;


    }
    /// <summary>
    /// 各Elementの子要素を識別する為のデータ。
    /// </summary>
    /// <param name="Name">子要素の役割を識別する列挙値。</param>
    /// <param name="Index">同じ役割を持つ子要素のインデックス。</param>
    public readonly record struct SqlQueryElementRole(SqlQueryElementRoleEnum Name, UInt16 Index) {
        #region ファクトリ
        public static SqlQueryElementRole Trivia(int index = 0) => new(SqlQueryElementRoleEnum.Trivia, (UInt16)index);
        public static SqlQueryElementRole Listed(int index = 0) => new(SqlQueryElementRoleEnum.Listed, (UInt16)index);
        public static SqlQueryElementRole Statement(int index = 0) => new(SqlQueryElementRoleEnum.Statement, (UInt16)index);
        public static SqlQueryElementRole Relation(int index = 0) => new(SqlQueryElementRoleEnum.Relation, (UInt16)index);
        public static SqlQueryElementRole Selection(int index = 0) => new(SqlQueryElementRoleEnum.Selection, (UInt16)index);
        public static SqlQueryElementRole Condition(int index = 0) => new(SqlQueryElementRoleEnum.Condition, (UInt16)index);
        public static SqlQueryElementRole Argument(int index = 0) => new(SqlQueryElementRoleEnum.Argument, (UInt16)index);
        public static SqlQueryElementRole Ordering(int index = 0) => new(SqlQueryElementRoleEnum.Ordering, (UInt16)index);
        public static SqlQueryElementRole Naming(int index = 0) => new(SqlQueryElementRoleEnum.Naming, (UInt16)index);
        #endregion
    };
    public enum SqlQueryElementRoleEnum{
        Statement,//実行可能な命令の実体:ISqlStatement:単数
        Relation,//データの供給源（表など）:ITableExpression:複数
        Selection,//出力・対象の指定（列）:IScalarExpression:複数
        Condition,//真偽判定のロジック:IPredicate:単数
        Argument,//演算や関数の入力値:IScalarExpression:複数
        Ordering,//並び替えや集約の基準:ISqlElement (Sort):複数
        Naming,//名前定義（別名など）:SqlIdentifier:単数
        Trivia,//空白文字やコメントなどの構造に影響しない要素:SqlQueryTriviaElement:複数
        Listed//リスト型の直下。直上のElementに対してはリストの要素という以外の意味が無い。
    }

    /// <summary>
    /// Slotが実際にElementを持つか、他の状態であるかを表す列挙型。
    /// </summary>
    public enum SqlQuerySlotStateEnum{
        /// <summary>
        /// 通常（実体Elementあり）
        /// </summary>
        Normal,
        /// <summary>
        /// 空（親の構造上スロットはあるが空でもBuild可能）
        /// </summary>
        Empty,
        /// <summary>
        /// 未定義（クエリ構築途中など、具体的な実体が指定されていない状態）
        /// </summary>
        Undefined,
        /// <summary>
        /// デフォルト（親のデフォルト値を使用するため、具体的な実体が指定されていない状態）
        /// </summary>
        Default
    }
    /// <summary>
    /// Slotが実際にElementを持つ必要があるか、他の状態でも成立するかを表す列挙型。
    /// </summary>
    [Flags]
    public enum SqlQuerySlotCapabilities{
        /// <summary>
        /// Flag用の何も指定されていない0。
        /// </summary>
        None = 0,
        /// <summary>
        /// 実際にElementを持つ必要があるスロット。
        /// </summary>
        OnlyNormal = 1 << 0,
        /// <summary>
        /// 中身が無くても成立するスロット。
        /// </summary>
        OnlyEmpty = 1 << 1,
        /// <summary>
        /// 中身が未定義の状態で仮置き可能なスロット。
        /// </summary>
        OnlyUndefined = 1 << 2,
        /// <summary>
        /// 親Elementのデフォルト値が使用可能なスロット。
        /// </summary>
        OnlyDefault = 1 << 3,
        AllowEmpty = OnlyNormal | OnlyEmpty,
        AllowUndefined = OnlyNormal | OnlyUndefined,
        AllowDefault = OnlyNormal | OnlyDefault,
        ExceptEmpty = OnlyNormal | OnlyUndefined | OnlyDefault,
        ExceptUndefined = OnlyNormal | OnlyEmpty | OnlyDefault,
        ExceptDefault = OnlyNormal | OnlyEmpty | OnlyUndefined,
        All = OnlyNormal | OnlyEmpty | OnlyUndefined | OnlyDefault
    }
    /// <summary>
    /// Stateの状態遷移時にTriviaをどう更新するかを指定するEnum
    /// </summary>
    public enum SqlQueryTriviaMergeMode {
        KeepOld,
        OverWrite,
        PrependNew,
        AppendNew,
        NestInner,
        NestOuter
    }

    #endregion


    #region SlotField

    /// <summary>
    /// Elementのプロパティ型としてのスロットの非ジェネリック基底。
    /// </summary>
    /// <remarks>
    /// Elementの不変かつ固定の一部として、IDと取りうる状態を保持する。
    /// PointerはこのFieldを指し示し、この中身を操作する。
    /// </remarks>
    internal abstract record class SqlQuerySlotFieldAbstract{
        /// <summary>
        /// このスロットの中身を指すID。
        /// </summary>
        internal SqlQuerySlotId Id { get; }
        internal string IdString => $"#0x{Id.Value:x8} ";
        internal SqlQuerySlotCapabilities Capabilities { get; }
        internal virtual SqlQuerySlotStateAbstract State { get; }
        //コンストラクタ
        internal SqlQuerySlotFieldAbstract(SqlQuerySlotId id, SqlQuerySlotCapabilities caps, SqlQuerySlotStateAbstract state){
            Id = id;
            Capabilities = caps;
            State = ValidateState(state);
        }
        /// <summary>
        /// StateがCapabilityに適合するかどうかをチェックして例外を出す。
        /// </summary>
        /// <param name="state"></param>
        protected SqlQuerySlotStateAbstract ValidateState(SqlQuerySlotStateAbstract state){
            if (state.IsEmpty && !Capabilities.HasFlag(SqlQuerySlotCapabilities.OnlyEmpty)) throw new ArgumentException("Empty slot is not allowed.");
            if (state.IsUndefined && !Capabilities.HasFlag(SqlQuerySlotCapabilities.OnlyUndefined)) throw new ArgumentException("Undefined slot is not allowed.");
            if (state.IsDefault && !Capabilities.HasFlag(SqlQuerySlotCapabilities.OnlyDefault)) throw new ArgumentException("Default slot is not allowed.");
            return state;
        }
        
        #region 短絡プロパティ
        public bool IsNormal => State.IsNormal;
        public bool IsDefault => State.IsDefault;
        public bool IsEmpty => State.IsEmpty;
        public bool IsUndefined => State.IsUndefined;
        public bool CanNormal => Capabilities.HasFlag(SqlQuerySlotCapabilities.OnlyNormal);
        public bool CanEmpty => Capabilities.HasFlag(SqlQuerySlotCapabilities.OnlyEmpty);
        public bool CanUndefined => Capabilities.HasFlag(SqlQuerySlotCapabilities.OnlyUndefined);
        public bool CanDefault => Capabilities.HasFlag(SqlQuerySlotCapabilities.OnlyDefault);
        public bool HasChild => State.HasChild;
        public bool HasUndefined => State.HasUndefined;
        public SqlType EnableType => State.EnableType;
        #endregion

        #region Buildの通知シンクにIDを追加する処理
        internal virtual SqlBuiltQueryFragment Build(SqlBuildContext context) {
            var fragment = State.Element.Build(context);
            context.AdaptDiagnostics(Id);
            return fragment;
        }
        internal virtual SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context) {
            var fragment = State.Element.DebugBuild(context);
            return fragment;
        }
        #endregion

        #region Elementのメソッドを呼び出す処理
        /// <summary>
        /// 子世代のSlotFieldを、Roleで取得する。
        /// </summary>
        internal abstract SqlQuerySlotFieldAbstract? GetChildByRole(SqlQueryElementRole role);
        /// <summary>
        /// 子世代のSlotFieldを、IDで取得する。
        /// </summary>
        internal abstract SqlQuerySlotFieldAbstract? GetChildById(SqlQuerySlotId id);
        /// <summary>
        /// IDで指定された子世代のSlotFieldのRoleを取得する。
        /// </summary>
        internal abstract SqlQueryElementRole? GetRoleById(SqlQuerySlotId childId, out SqlQuerySlotFieldAbstract? targetSlot);

        #endregion

        #region IDを辿る処理
        /// <summary>
        /// 与えられたパスがこのノードを起点として有効かどうかを判定する。無効な場合はInvalid、パスが自身を指している場合はSelf、子要素を指している場合はDescendを返す。
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        protected SqlQueryPathResolve ResolvePath(SqlQueryPath path){
            if (path.RootId != this.Id) return SqlQueryPathResolve.Invalid;
            if (path.IsRoot) return SqlQueryPathResolve.Self;
            if (HasChild) return SqlQueryPathResolve.Descend;
            else return SqlQueryPathResolve.Invalid;
        }
        /// <summary>
        /// Pathを基準にツリー内のスロットを取得する。該当のスロットが存在しなければnull。
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal SqlQuerySlotFieldAbstract? GetSlotField(SqlQueryPath path){
            switch (ResolvePath(path)){
                case SqlQueryPathResolve.Self:
                    return this;

                case SqlQueryPathResolve.Descend:
                    var current = this;
                    foreach (var id in path.Ids){
                        if (current.IsNormal && current.GetChildById(new SqlQuerySlotId(id)) is SqlQuerySlotFieldAbstract next){
                            current = next;
                        }else{
                            return null;
                        }
                    }
                    return current;

                case SqlQueryPathResolve.Invalid:
                default:
                    return null;
            }
        }
        /// <summary>
        /// 与えられたElementがこのノードの子孫に存在する場合、そのElementまでのパスを返す。存在しない場合はnull。
        /// </summary>
        /// <param name="target"></param>
        /// <param name="targetSlot"></param>
        /// <returns></returns>
        internal SqlQueryPath? GetPathByElement(SqlQueryElement target, out SqlQuerySlotFieldAbstract? targetSlot){
            targetSlot = null;
            if (!IsNormal) return null;
            if (object.ReferenceEquals(State.Element, target)){
                targetSlot = this;
                return new(Id, []);
            }
            foreach (var (_, child) in State.Element!.IterateChildren()){
                if (child.GetPathByElement(target, out targetSlot) is SqlQueryPath p) return p.AppendRoot(Id);
            }
            return null;
        }
        /// <summary>
        /// 与えられたIDを持つSlotがこのノードの子孫に存在する場合、そのSlotまでのパスを返す。存在しない場合はnull。
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="targetSlot"></param>
        /// <returns></returns>
        internal SqlQueryPath? GetPathById(SqlQuerySlotId targetId, out SqlQuerySlotFieldAbstract? targetSlot){
            targetSlot = null;
            if (!IsNormal) return null;
            if (this.Id == targetId){
                targetSlot = this;
                return new(Id, []);
            }
            foreach (var (_, child) in State.Element!.IterateChildren()){
                if (child.GetPathById(targetId, out targetSlot) is SqlQueryPath p) return p.AppendRoot(Id);
            }
            return null;
        }
        internal SqlQueryPath? GetPathBySelector(SqlQuerySelector selector, out SqlQuerySlotFieldAbstract? targetSlot){
            targetSlot = null;
            if (!IsNormal) return null;
            if (selector.RootId != Id) return null;
            if (selector.Length == 0){
                targetSlot = this;
                return new(Id, []);
            }

            uint[] ids = new uint[selector.Length];//完走しない限り読み取らないので初期値は気にしない。
            var currentNode = this;
            for (var i = 0; i < selector.Length; i++){
                var role = selector.Roles[i];

                bool found = false;
                foreach (var (childRole, child) in currentNode.State.Element.IterateChildren()){
                    if (role == childRole) continue;

                    currentNode = child;
                    ids[i] = child.Id.Value;
                    found = true;
                    break;
                }
                if (!found) return null;//その役割の子要素が見つからなかった場合はnullを返す。
            }
            //完走できたら出力。
            targetSlot = currentNode;
            return new(Id, [.. ids]);
        }
        internal SqlQuerySelector? GetSelectorByPath(SqlQueryPath path, out SqlQuerySlotFieldAbstract? targetSlot){
            targetSlot = null;
            if (!IsNormal) return null;
            if (path.RootId != Id) return null;
            if (path.Length == 0){
                targetSlot = this;
                return new(Id, []);
            }

            SqlQueryElementRole[] roles = new SqlQueryElementRole[path.Length];//完走しない限り読み取らないので初期値は気にしない。
            var currentNode = this;
            for (var i = 0; i < path.Length; i++){
                var id = path.Ids[i];
                bool found = false;
                foreach (var (childRole, child) in currentNode.State.Element.IterateChildren()){
                    if (id == child.Id.Value) continue;

                    currentNode = child;
                    roles[i] = childRole;
                    found = true;
                    break;
                }
                if (!found) return null;//そのIDの子要素が見つからなかった場合はnullを返す。
            }
            //完走できたら出力。
            targetSlot = currentNode;
            return new(Id, [.. roles]);
        }

        
        #endregion
    }


    /// <summary>
    /// Elementのプロパティ型としてのスロットのジェネリック基底。
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="SqlQuerySlotFieldAbstract"/>
    /// </remarks>
    internal abstract record class SqlQuerySlotField<TElement> : SqlQuerySlotFieldAbstract
        where TElement : SqlQueryElement
    {
        internal virtual Type GenericType => typeof(TElement);
        internal override SqlQuerySlotState<TElement> State => (SqlQuerySlotState<TElement>)base.State;
        //コンストラクタ
        internal SqlQuerySlotField(SqlQuerySlotId id, SqlQuerySlotCapabilities caps, SqlQuerySlotState<TElement> state)
            : base(id, caps, state)
        {
        }

    }

    internal sealed record class SqlQueryTriviaSlotField : SqlQuerySlotField<SqlQueryTriviaElement> {
        internal override Type GenericType => typeof(SqlQueryTriviaElement);
        internal override SqlQueryTriviaSlotState State => (SqlQueryTriviaSlotState)base.State;
        //コンストラクタ
        internal SqlQueryTriviaSlotField(SqlQuerySlotId id, SqlQuerySlotCapabilities caps, SqlQueryTriviaSlotState state)
            : base(id, caps, state)
        {
        }

        #region ファクトリ
        internal static SqlQueryTriviaSlotField Create(
            SqlQueryDraft draft,
            SqlQueryTriviaSlotState state
            )
        {
            return new SqlQueryTriviaSlotField(
                SqlQuerySlotId.GetNextId(draft),
                SqlQuerySlotCapabilities.OnlyNormal,
                state
                );
        }
        internal static SqlQueryTriviaSlotField CreateNormal(
            SqlQueryDraft draft,
            SqlQueryTriviaElement element
            )
        {
            return new SqlQueryTriviaSlotField(
                SqlQuerySlotId.GetNextId(draft),
                SqlQuerySlotCapabilities.OnlyNormal,
                SqlQueryTriviaSlotState.CreateNormal(element)
                );
        }
        #endregion

        #region Buildの通知シンクにIDを追加する処理
        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            var fragment = State.Element.Build(context);
            context.AdaptDiagnostics(Id);
            return fragment;
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            var fragment = State.Element.DebugBuild(context);
            context.AdaptDiagnostics(Id);
            return fragment;
        }
        #endregion

        #region 状態遷移用メソッド

        /// <summary>
        /// スロットの中身を置き換える。
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public SqlQueryTriviaSlotField ReplaceState(SqlQueryTriviaSlotState state){
            return new (Id, Capabilities, state);
        }
        #endregion

        #region Elementのメソッドを呼び出す処理
        /// <summary>
        /// 子世代のSlotFieldを、Roleで取得する。
        /// </summary>
        internal override SqlQueryTriviaSlotField? GetChildByRole(SqlQueryElementRole role) => null;
        /// <summary>
        /// 子世代のSlotFieldを、IDで取得する。
        /// </summary>
        internal override SqlQueryTriviaSlotField? GetChildById(SqlQuerySlotId id) => null;
        /// <summary>
        /// IDで指定された子世代のSlotFieldのRoleを取得する。
        /// </summary>
        internal override SqlQueryElementRole? GetRoleById(SqlQuerySlotId childId, out SqlQuerySlotFieldAbstract? targetSlot){
            targetSlot = null;
            return null;
        }

        #endregion
    }
    internal sealed record class SqlQueryTriviaListSlotField : SqlQuerySlotField<SqlQueryTriviaList>{
        internal override Type GenericType => typeof(SqlQueryTriviaList);
        internal override SqlQueryTriviaListSlotState State => (SqlQueryTriviaListSlotState)base.State;
        //コンストラクタ
        internal SqlQueryTriviaListSlotField(SqlQuerySlotId id, SqlQuerySlotCapabilities caps, SqlQueryTriviaListSlotState state)
            : base(id, caps, state)
        {
        }

        #region ファクトリ
        internal static SqlQueryTriviaListSlotField Create(
            SqlQueryDraft draft,
            SqlQueryTriviaListSlotState state
            )
        {
            return new SqlQueryTriviaListSlotField(
                SqlQuerySlotId.GetNextId(draft),
                SqlQuerySlotCapabilities.OnlyNormal,
                state
                );
        }
        internal static SqlQueryTriviaListSlotField CreateNormal(
            SqlQueryDraft draft,
            SqlQueryTriviaList element
            )
        {
            return new SqlQueryTriviaListSlotField(
                SqlQuerySlotId.GetNextId(draft),
                SqlQuerySlotCapabilities.OnlyNormal,
                SqlQueryTriviaListSlotState.CreateNormal(element)
                );
        }
        #endregion

        #region Buildの通知シンクにIDを追加する処理
        internal override SqlBuiltQueryFragmentText Build(SqlBuildContext context){
            var fragment = State.Element.Build(context);
            context.AdaptDiagnostics(Id);
            return fragment;
        }
        internal override SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            var fragment = State.Element.DebugBuild(context);
            context.AdaptDiagnostics(Id);
            return fragment;
        }
        #endregion

        #region 状態遷移用メソッド
        /// <summary>
        /// スロットの中身を置き換える。
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        internal SqlQueryTriviaListSlotField ReplaceState(SqlQueryTriviaListSlotState state){
            return new(Id, Capabilities, state);
        }
        internal SqlQueryTriviaListSlotField ToNormal(SqlQueryTriviaList element) {
            return new(Id, Capabilities, State.ToNormal(element));
        }
        #endregion

        #region Elementのメソッドを呼び出す処理
        /// <summary>
        /// 子世代のSlotFieldを、Roleで取得する。
        /// </summary>
        internal override SqlQueryTriviaSlotField? GetChildByRole(SqlQueryElementRole role)=> State.Element.GetChildByRole(role);
        /// <summary>
        /// 子世代のSlotFieldを、IDで取得する。
        /// </summary>
        internal override SqlQueryTriviaSlotField? GetChildById(SqlQuerySlotId id) => State.Element.GetChildById(id);
        /// <summary>
        /// IDで指定された子世代のSlotFieldのRoleを取得する。
        /// </summary>
        internal override SqlQueryElementRole? GetRoleById(SqlQuerySlotId childId, out SqlQuerySlotFieldAbstract? targetSlot) => State.Element.GetRoleById(childId, out targetSlot);

        #endregion
    }
    internal record class SqlQueryStructuralSlotField<TElement> : SqlQuerySlotField<TElement>
        where TElement : SqlQueryStructuralElement
    {
        internal override SqlQueryStructuralSlotState<TElement> State => (SqlQueryStructuralSlotState<TElement>)base.State;
        //コンストラクタ
        internal SqlQueryStructuralSlotField(SqlQuerySlotId id, SqlQuerySlotCapabilities caps, SqlQueryStructuralSlotState<TElement> state)
            : base(id, caps, state)
        {
        }

        #region ファクトリ

        /// <summary>
        /// 新規のIDでFieldを生成する。
        /// </summary>
        /// <remarks>
        /// Fieldを、CloneでもReplaceでも無く生成するのは、Elementの初期作成時のみ。
        /// その場合、Capabilitiesの詳細設定は必須なので、SqlQuerySlotId.GetNextId(draft)の分くらいしかファクトリに委託できない。
        /// <br></br>
        /// 空の代用Elementを使う場合は、SqlQuerySubElement<TElement>.CreateNull(draft).WrapToUndefinedState()で。
        /// </remarks>
        internal static SqlQueryStructuralSlotField<TElement> Create(
            SqlQueryDraft draft,
            SqlQuerySlotCapabilities caps,
            SqlQueryStructuralSlotState<TElement> state
            )
        {
            return new SqlQueryStructuralSlotField<TElement>(
                SqlQuerySlotId.GetNextId(draft),
                caps,
                state
                );
        }
        #endregion
        
        #region 状態遷移用メソッド

        /// <summary>
        /// スロットの中身を置き換える。
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        internal SqlQueryStructuralSlotField<TElement> ReplaceState(SqlQueryStructuralSlotState<TElement> state){
            return new SqlQueryStructuralSlotField<TElement>(Id, Capabilities, state);
        }
        /// <summary>
        /// スロットの中身を埋める。既に中身がある場合は何もしない。
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public SqlQueryStructuralSlotField<TElement> Fill(TElement element, SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew){
            if (IsNormal) return this;
            return new SqlQueryStructuralSlotField<TElement>(Id, Capabilities, State.Fill(element,mode));
        }
        /// <summary>
        /// スロットの中身を実体にする。
        /// </summary>
        /// <returns></returns>
        public virtual SqlQueryStructuralSlotField<TElement> ToNormal(
            TElement element,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            return new SqlQueryStructuralSlotField<TElement>(Id, Capabilities, State.ToNormal(element, mode));
        }
        /// <summary>
        /// スロットの中身を空にする。
        /// </summary>
        /// <returns></returns>
        public virtual SqlQueryStructuralSlotField<TElement> ToEmpty(
            SqlQuerySubElement<TElement>? element = null,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            return new SqlQueryStructuralSlotField<TElement>(Id, Capabilities, State.ToEmpty(element,mode));
        }
        /// <summary>
        /// スロットの中身を未定義にする。
        /// </summary>
        /// <returns></returns>
        public virtual SqlQueryStructuralSlotField<TElement> ToUndefined(
            SqlQuerySubElement<TElement>? element = null,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            return new SqlQueryStructuralSlotField<TElement>(Id, Capabilities, State.ToUndefined(element, mode));
        }
        /// <summary>
        /// スロットの中身をデフォルトにする。
        /// </summary>
        /// <returns></returns>
        public virtual SqlQueryStructuralSlotField<TElement> ToDefault(
            SqlQuerySubElement<TElement>? element = null,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            return new SqlQueryStructuralSlotField<TElement>(Id, Capabilities, State.ToDefault(element, mode));
        }
        #endregion

        #region Elementのメソッドを呼び出す処理
        /// <summary>
        /// 子世代のSlotFieldを、Roleで取得する。
        /// </summary>
        internal override SqlQuerySlotFieldAbstract? GetChildByRole(SqlQueryElementRole role){
            foreach (var (childRole, childField) in State.Element.IterateChildren()){
                if (childRole == role) return childField;
            }
            return null;
        }
        /// <summary>
        /// 子世代のSlotFieldを、IDで取得する。
        /// </summary>
        internal override SqlQuerySlotFieldAbstract? GetChildById(SqlQuerySlotId id){
            foreach (var (_, childField) in State.Element.IterateChildren()){
                if (childField.Id == id) return childField;
            }
            return null;
        }
        /// <summary>
        /// IDで指定された子世代のSlotFieldのRoleを取得する。
        /// </summary>
        internal override SqlQueryElementRole? GetRoleById(SqlQuerySlotId childId, out SqlQuerySlotFieldAbstract? targetSlot){
            foreach (var (childRole, childField) in State.Element.IterateChildren()){
                if (childField.Id == childId){
                    targetSlot = childField;
                    return childRole;
                }
            }
            targetSlot = null;
            return null;
        }

        #endregion
    }


    #endregion

    #region SlotState

    /// <summary>
    /// スロットの中身の非ジェネリック基底。
    /// </summary>
    /// <remarks>
    /// ElementもしくはDefault・Empty・Undefinedの状態を表すラッパー。
    /// </remarks>
    internal abstract record class SqlQuerySlotStateAbstract{
        internal SqlQuerySlotStateEnum StateEnum { get; }
        internal virtual SqlQueryElement Element => IsNormal ? MainElement! : SubElement!;// StateEnum == Normal の時のみMainが返る
        protected virtual SqlQueryElement? MainElement { get; }//実体状態のStateが持つElement
        protected virtual SqlQuerySubElement? SubElement { get; }//実文字要素が空白文字を非実体でも保持するための代用Element
        /// <summary>
        /// 不確定型ノードを配下に持つかどうか。
        /// </summary>
        internal bool HasUndefined { get; }
        /// <summary>
        /// 対応可能なSQL方言範囲。
        /// </summary>
        internal SqlType EnableType { get; }
        internal abstract Type GenericType { get; }
        //フルコンストラクタ
        internal SqlQuerySlotStateAbstract(SqlQuerySlotStateEnum stateEnum, SqlQueryElement? mainElement = null, SqlQuerySubElement? subElement = null){
            StateEnum = stateEnum;
            if (mainElement is SqlQueryTriviaElement te) {
                if (stateEnum != SqlQuerySlotStateEnum.Normal) throw new ArgumentException("空文字要素は非実体で生成できない。");
                MainElement = te;
                SubElement = null;
            } else if (mainElement is SqlQueryTriviaList tl) {
                if (stateEnum != SqlQuerySlotStateEnum.Normal) throw new ArgumentException("空文字要素は非実体で生成できない。");
                MainElement = tl;
                SubElement = null;
            } else if (stateEnum == SqlQuerySlotStateEnum.Normal) {
                if (mainElement is null) throw new ArgumentException("実体要素は代用Elementのみからは生成できない。");
                if (subElement is null) throw new ArgumentException("実文字要素は代用Element無しに生成できない。");
                MainElement = mainElement;
                SubElement = subElement;
            } else {
                if (subElement is null) throw new ArgumentException("実文字要素は代用Element無しに生成できない。");
                MainElement = null;
                SubElement = subElement;
            }



            //HasUndefinedとEnableTypeの決定処理。
            //どちらかというとElementの状態ではあるが、
            //Elementの多重継承コンストラクタの処理が終わった後に処理したいため、
            //完成したElementを受け取るStateのコンストラクタに隔離。
            //get;指定により、コンストラクタから分離できない。
            HasUndefined = IsUndefined;
            EnableType = IsNormal ? Element!.EnableType : SqlType.All;
            if (IsNormal){
                foreach (var (_, slot) in mainElement!.IterateChildren()){
                    HasUndefined |= (slot.State!.IsUndefined);
                    EnableType &= slot.State.EnableType;
                }
            }
        }

        #region 短絡プロパティ
        internal bool IsNormal => StateEnum == SqlQuerySlotStateEnum.Normal;
        internal bool IsDefault => StateEnum == SqlQuerySlotStateEnum.Default;
        internal bool IsEmpty => StateEnum == SqlQuerySlotStateEnum.Empty;
        internal bool IsUndefined => StateEnum == SqlQuerySlotStateEnum.Undefined;

        internal bool HasChild => IsNormal && Element!.HasChild;
        internal bool HasChildNode => IsNormal && Element!.HasChildNode;
        #endregion

        #region Elementのメソッドを呼び出すだけの処理
        internal abstract SqlQuerySlotStateAbstract CloneNode(SqlQueryDraft draft);
        internal abstract SqlQuerySlotStateAbstract? ReplaceChildrenByRole(
            IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            );
        internal abstract SqlQuerySlotStateAbstract? ReplaceChildrenById(
            IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            );
        #endregion

    }
    /// <summary>
    /// スロットの中身のジェネリック基底。
    /// </summary>
    /// <remarks>
    /// ElementもしくはDefault・Empty・Undefinedの状態を表すラッパー。
    /// </remarks>
    internal abstract record class SqlQuerySlotState<TElement> : SqlQuerySlotStateAbstract
        where TElement : SqlQueryElement
    {
        internal override Type GenericType => typeof(TElement);
        protected override TElement? MainElement => (TElement?)base.MainElement;
        protected override SqlQuerySubElement<TElement>? SubElement => (SqlQuerySubElement<TElement>?)base.SubElement;
        //コンストラクタ
        internal SqlQuerySlotState(SqlQuerySlotStateEnum stateEnum, TElement? mainElement, SqlQuerySubElement<TElement>? subElement)
            :base(stateEnum, mainElement, subElement)
        {
        }
    }

    internal sealed record class SqlQueryTriviaSlotState : SqlQuerySlotState<SqlQueryTriviaElement> {
        internal override Type GenericType => typeof(SqlQueryTriviaElement);
        internal override SqlQueryTriviaElement Element => base.MainElement!;
        protected override SqlQueryTriviaElement MainElement => base.MainElement!;
        protected override SqlQuerySubElement<SqlQueryTriviaElement>? SubElement => null;
        //コンストラクタ
        internal SqlQueryTriviaSlotState(SqlQueryTriviaElement mainElement)
            : base(SqlQuerySlotStateEnum.Normal, mainElement, null)
        {
        }
        #region ファクトリ
        public static SqlQueryTriviaSlotState CreateNormal(SqlQueryTriviaElement element) => new(element);
        #endregion

        #region 状態遷移用メソッド
        /// <summary>
        /// スロットの中身を置き換える。
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        internal SqlQueryTriviaSlotState ToNormal(SqlQueryTriviaElement element) => new (element);
        #endregion

        /// <summary>
        /// StateをFieldに挿入する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        internal SqlQueryTriviaSlotField SetInField(SqlQueryTriviaSlotField field) => field.ReplaceState(this);

        #region Elementのメソッドを呼び出すだけの処理
        internal override SqlQueryTriviaSlotState CloneNode(SqlQueryDraft draft) => this;
        internal override SqlQueryTriviaSlotState? ReplaceChildrenByRole(
            IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew)
        {
            return null;
        }
        internal override SqlQueryTriviaSlotState? ReplaceChildrenById(
            IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew)
        {
            return null;
        }
        #endregion

    }
    internal sealed record class SqlQueryTriviaListSlotState : SqlQuerySlotState<SqlQueryTriviaList> {
        internal override Type GenericType => typeof(SqlQueryTriviaList);
        internal override SqlQueryTriviaList Element => base.MainElement!;
        protected override SqlQueryTriviaList MainElement => base.MainElement!;
        protected override SqlQuerySubElement<SqlQueryTriviaList>? SubElement => null;
        //コンストラクタ
        internal SqlQueryTriviaListSlotState(SqlQueryTriviaList mainElement)
            : base(SqlQuerySlotStateEnum.Normal, mainElement, null)
        {
        }
        #region ファクトリ
        public static SqlQueryTriviaListSlotState CreateNormal(SqlQueryTriviaList element) => new(element);
        #endregion

        #region 状態遷移用メソッド
        /// <summary>
        /// スロットの中身を置き換える。
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        internal SqlQueryTriviaListSlotState ToNormal(SqlQueryTriviaList element) => new(element);
        #endregion

        /// <summary>
        /// StateをFieldに挿入する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        internal SqlQueryTriviaListSlotField SetInField(SqlQueryTriviaListSlotField field) => field.ReplaceState(this);

        #region Elementのメソッドを呼び出すだけの処理
        internal override SqlQueryTriviaListSlotState CloneNode(SqlQueryDraft draft) => new(MainElement.CloneNode(draft));
        internal override SqlQueryTriviaListSlotState? ReplaceChildrenByRole(
            IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            var newMainElement = MainElement!.ReplaceChildrenByRole(states);
            if (newMainElement is null) return null;
            if (newMainElement is not SqlQueryTriviaList mte) throw new NotSupportedException("ReplaceでElementの型が変わる異常事態。");
            return ToNormal(mte);
        }
        internal override SqlQueryTriviaListSlotState? ReplaceChildrenById(
            IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            var newMainElement = MainElement!.ReplaceChildrenById(states);
            if (newMainElement is null) return null;
            if (newMainElement is not SqlQueryTriviaList mte) throw new NotSupportedException("ReplaceでElementの型が変わる異常事態。");
            return ToNormal(mte);
        }
        #endregion
    }
    internal sealed record class SqlQueryStructuralSlotState<TElement> : SqlQuerySlotState<TElement>
        where TElement : SqlQueryStructuralElement
    {
        //コンストラクタ
        internal SqlQueryStructuralSlotState(SqlQuerySlotStateEnum stateEnum, TElement? mainElement, SqlQuerySubElement<TElement>? subElement)
            : base(stateEnum, mainElement, subElement)
        { }

        #region ファクトリ
        public static SqlQueryStructuralSlotState<TElement> CreateNormal(TElement element){
            return new (SqlQuerySlotStateEnum.Normal, element, null);
        }
        public static SqlQueryStructuralSlotState<TElement> CreateEmpty(SqlQuerySubElement<TElement> element){
            return new (SqlQuerySlotStateEnum.Empty, null, element);
        }
        public static SqlQueryStructuralSlotState<TElement> CreateUndefined(SqlQuerySubElement<TElement> element){
            return new (SqlQuerySlotStateEnum.Undefined, null, element);
        }
        public static SqlQueryStructuralSlotState<TElement> CreateDefault(SqlQuerySubElement<TElement> element){
            return new (SqlQuerySlotStateEnum.Default, null, element);
        }
        #endregion

        #region 状態遷移用メソッド


        /// <summary>
        /// Trivia合成モードに応じて、直下Elementのコンストラクタに渡す空白文字要素の中身を生成する。
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="oldElement"></param>
        /// <param name="newElement"></param>
        /// <returns></returns>
        private static (
            SqlQueryTriviaListSlotState LeadingTrivia,
            SqlQueryTriviaListSlotState TrailingTrivia
            ) ReloadTriviaListState(
            SqlQueryTriviaMergeMode mode,
            SqlQueryStructuralElement oldElement,
            SqlQueryStructuralElement newElement
            )
        {
            var newLeadingFields = newElement.LeadingTrivia
                                .State.Element.IterateChildren()
                                .Select(t => t.Slot)
                                .Cast<SqlQueryTriviaSlotField>();
            var newTrailingFields = newElement.TrailingTrivia
                                .State.Element.IterateChildren()
                                .Select(t => t.Slot)
                                .Cast<SqlQueryTriviaSlotField>();
            var oldLeadingFields = oldElement.LeadingTrivia
                                .State.Element.IterateChildren()
                                .Select(t => t.Slot)
                                .Cast<SqlQueryTriviaSlotField>();
            var oldTrailingFields = oldElement.TrailingTrivia
                                .State.Element.IterateChildren()
                                .Select(t => t.Slot)
                                .Cast<SqlQueryTriviaSlotField>();

            switch (mode){
                case SqlQueryTriviaMergeMode.KeepOld:
                    return (oldElement.LeadingTrivia.State, oldElement.TrailingTrivia.State);
                case SqlQueryTriviaMergeMode.OverWrite:
                    return (newElement.LeadingTrivia.State, newElement.TrailingTrivia.State);
                case SqlQueryTriviaMergeMode.PrependNew:
                    return (
                        new SqlQueryTriviaList([.. newLeadingFields, .. oldLeadingFields]).WrapToNormalState(),
                        new SqlQueryTriviaList([.. newTrailingFields, .. oldTrailingFields]).WrapToNormalState()
                        );
                case SqlQueryTriviaMergeMode.AppendNew:
                    return (
                        new SqlQueryTriviaList([.. oldLeadingFields, .. newLeadingFields]).WrapToNormalState(),
                        new SqlQueryTriviaList([.. oldTrailingFields, .. newTrailingFields]).WrapToNormalState()
                        );
                case SqlQueryTriviaMergeMode.NestInner:
                    return (
                        new SqlQueryTriviaList([.. oldLeadingFields, .. newLeadingFields]).WrapToNormalState(),
                        new SqlQueryTriviaList([.. newTrailingFields, .. oldTrailingFields]).WrapToNormalState()
                        );
                case SqlQueryTriviaMergeMode.NestOuter:
                    return (
                        new SqlQueryTriviaList([.. newLeadingFields, .. oldLeadingFields]).WrapToNormalState(),
                        new SqlQueryTriviaList([.. oldTrailingFields, .. newTrailingFields]).WrapToNormalState()
                        );
                default:
                    throw new NotImplementedException("SqlQueryTriviaMergeModeの未知の分岐。");
            }
        }

        //非実体三種の中身の処理はほぼ同一であるため、単一のメソッドにまとめる。
        private SqlQueryStructuralSlotState<TElement> ReplaceUnreal(
            SqlQuerySlotStateEnum stateEnum,
            SqlQuerySubElement<TElement> newSubElement,
            SqlQueryTriviaMergeMode mode
            )
        {
            if (stateEnum == SqlQuerySlotStateEnum.Normal) throw new ArgumentException("非実体要素への遷移のみ想定するメソッドである。");
            //ここから先は、代用Elementを持つ実文字要素だけ考える。

            var (leadingTriviaState, trailingTriviaState) = ReloadTriviaListState(mode, SubElement!, newSubElement);

            //直下代用Elementの空白文字リストIDは引き継ぐ。
            var outputSubElement = SubElement!.ReplaceChildrenByRole([
                (SqlQueryElementRole.Trivia(0),leadingTriviaState),
                (SqlQueryElementRole.Trivia(1),trailingTriviaState)
                ]);

            return new SqlQueryStructuralSlotState<TElement>(
                stateEnum,
                null,
                outputSubElement
                );
        }

        /// <summary>
        /// スロットの中身を置き換える。
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        internal SqlQueryStructuralSlotState<TElement> ToNormal(
            TElement element,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            var (leadingTriviaState, trailingTriviaState) = ReloadTriviaListState(mode, SubElement!, element);

            //直下代用Elementの空白文字リストIDは引き継ぐ。
            var outputSubElement = SubElement!.ReplaceTriviaByState(leadingTriviaState, trailingTriviaState);

            var outputMainElement = element.ReplaceTriviaByField(
                outputSubElement.LeadingTrivia,
                outputSubElement.TrailingTrivia
                );
            if (outputMainElement is not TElement nte) throw new NotImplementedException("replaceでElementの型が変わってしまう異常事態。");

            return new SqlQueryStructuralSlotState<TElement>(
                SqlQuerySlotStateEnum.Normal,
                nte,
                outputSubElement
                );
        }
        /// <summary>
        /// スロットの中身を埋める。既に中身がある場合は何もしない。
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        internal SqlQueryStructuralSlotState<TElement> Fill(
            TElement element,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            if (IsNormal) return this;
            return ToNormal(element, mode);
        }
        /// <summary>
        /// スロットの中身を空にする。
        /// </summary>
        /// <returns></returns>
        internal SqlQueryStructuralSlotState<TElement> ToEmpty(
            SqlQuerySubElement<TElement>? element,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            if (element == null) return new SqlQueryStructuralSlotState<TElement>(SqlQuerySlotStateEnum.Empty, null, SubElement);
            else return ReplaceUnreal(SqlQuerySlotStateEnum.Empty, element, mode);
        }
        /// <summary>
        /// スロットの中身を未定義にする。
        /// </summary>
        /// <returns></returns>
        internal SqlQueryStructuralSlotState<TElement> ToUndefined(
            SqlQuerySubElement<TElement>? element,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            if (element == null) return new SqlQueryStructuralSlotState<TElement>(SqlQuerySlotStateEnum.Undefined, null, SubElement);
            else return ReplaceUnreal(SqlQuerySlotStateEnum.Undefined, element, mode);
        }
        /// <summary>
        /// スロットの中身をデフォルトにする。
        /// </summary>
        /// <returns></returns>
        internal SqlQueryStructuralSlotState<TElement> ToDefault(
            SqlQuerySubElement<TElement>? element,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            if (element == null) return new SqlQueryStructuralSlotState<TElement>(SqlQuerySlotStateEnum.Default, null, SubElement);
            else return ReplaceUnreal(SqlQuerySlotStateEnum.Default, element, mode);
        }
        #endregion

        /// <summary>
        /// StateをFieldに挿入する、メソッドチェイン用の逆順処理
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        internal SqlQueryStructuralSlotField<TElement> SetInField(SqlQueryStructuralSlotField<TElement> field) => field.ReplaceState(this);

        #region Elementのメソッドを呼び出すだけの処理
        internal override SqlQueryStructuralSlotState<TElement> CloneNode(SqlQueryDraft draft) {
            return new SqlQueryStructuralSlotState<TElement>(StateEnum, (TElement?)MainElement?.CloneNode(draft), SubElement?.CloneNode(draft));
        }
        internal override SqlQueryStructuralSlotState<TElement>? ReplaceChildrenByRole(
            IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            if (IsNormal){
                var newMainElement = MainElement!.ReplaceChildrenByRole(states);
                if (newMainElement is null) return null;
                if (newMainElement is not TElement mte) throw new NotImplementedException("ReplaceでElementの型が変わる異常事態。");
                return ToNormal(mte, mode);
            }else{
                //Normal状態ではないなら、実文字要素であるのは確定するので代用Elementは存在する。
                var newSubElement = SubElement!.ReplaceChildrenByRole(states);
                if (newSubElement is null) return null;
                return ReplaceUnreal(StateEnum, newSubElement, mode);
            }
        }
        internal override SqlQueryStructuralSlotState<TElement>? ReplaceChildrenById(
            IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states,
            SqlQueryTriviaMergeMode mode = SqlQueryTriviaMergeMode.AppendNew
            )
        {
            if (IsNormal){
                var newMainElement = MainElement!.ReplaceChildrenById(states);
                if (newMainElement is null) return null;
                if (newMainElement is not TElement mte) throw new NotImplementedException("ReplaceでElementの型が変わる異常事態。");
                return ToNormal(mte, mode);
            }else{
                //Normal状態ではないなら、実文字要素であるのは確定するので代用Elementは存在する。
                var newSubElement = SubElement!.ReplaceChildrenById(states);
                if (newSubElement is null) return null;
                return ReplaceUnreal(StateEnum, newSubElement, mode);
            }
        }
        #endregion
    }

    #endregion


    /// <summary>
    /// SqlQuerySlotFieldAbstract.Buildから出力される加工途中の文字列そのものを表すクラス。
    /// </summary>
    /// <remarks>
    /// 本体の前後に、前方空白文字・後方空白文字を分けて保持するためのクラス。
    /// <br></br>
    /// 文字列以外にも、瞬間的に必要な情報はContextよりこちらで保持する。
    /// </remarks>
    internal record SqlBuiltQueryFragmentNode : SqlBuiltQueryFragment{
        public SqlBuiltQueryFragmentText Leading { get; init; }
        public SqlBuiltQueryFragmentText? Main { get; init; }
        public SqlBuiltQueryFragmentText Trailing { get; init; }

        //コンストラクタ
        public SqlBuiltQueryFragmentNode(
            SqlBuiltQueryFragmentText leading,
            SqlBuiltQueryFragmentText? main,
            SqlBuiltQueryFragmentText trailing
            )
        {
            if (leading.TriviaOnly == false) { throw new ArgumentException("前方空白文字に空白文字型でないFragmentが含まれている"); }
            if (trailing.TriviaOnly == false) { throw new ArgumentException("後方空白文字に空白文字型でないFragmentが含まれている"); }
            if (main?.TriviaOnly == true) { throw new ArgumentException("本体文字列が空白文字型のFragmentになっている"); }

            Main = main;
            Leading = leading;
            Trailing = trailing;
        }

        /// <summary>
        /// 前方のTriviaを保持したまま、Mainの前に別のFragmentを接続する。
        /// </summary>
        /// <param name="added"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public SqlBuiltQueryFragmentNode MergeMainBefore(SqlBuiltQueryFragmentText added, SqlBuildContext context){
            return new SqlBuiltQueryFragmentNode(
                Leading,
                Main != null ? added.JoinAfter(Main, context) : added,
                Trailing
            );
        }
        internal SqlBuiltQueryFragmentNode ReplaceMain(SqlBuiltQueryFragmentText newMain, SqlBuildContext context) {
            return new SqlBuiltQueryFragmentNode(
                Leading,
                newMain,
                Trailing
            );

        }
        internal override SqlBuiltQueryFragmentText ToText(SqlBuildContext context){
            if (Main is SqlBuiltQueryFragmentText ft) return Leading.JoinAfter(ft, context).JoinAfter(Trailing, context);
            else return Leading.JoinAfter(Trailing, context);

        }
    }
    /// <summary>
    /// リスト型Elementから上のElementに渡すためのリスト型Fragment。
    /// </summary>
    internal record SqlBuiltQueryFragmentListNode : SqlBuiltQueryFragment{
        public SqlBuiltQueryFragmentText Leading { get; init; }
        public SqlBuiltQueryFragmentText[] Main { get; init; }
        public SqlBuiltQueryFragmentText Trailing { get; init; }
        public int? Precedence => null; //式型における、演算の優先順位。括弧の有無に影響。可変数要素に括弧が要るかどうかは上で判断なのでここではnull。

        //コンストラクタ
        public SqlBuiltQueryFragmentListNode(
            SqlBuiltQueryFragmentText leading,
            SqlBuiltQueryFragmentText[] main,
            SqlBuiltQueryFragmentText trailing
            )
        {
            if (leading.TriviaOnly == false) { throw new ArgumentException("前方空白文字に空白文字型でないFragmentが含まれている"); }
            if (trailing.TriviaOnly == false) { throw new ArgumentException("後方空白文字に空白文字型でないFragmentが含まれている"); }
            foreach (var f in main){
                if (f.TriviaOnly == true) { throw new ArgumentException("本体文字列が空白文字型のFragmentになっている"); }
            }

            Main = main;
            Leading = leading;
            Trailing = trailing;
        }
        internal override SqlBuiltQueryFragmentText ToText(SqlBuildContext context){
            var result = Leading;
            foreach (var fragment in Main) {
                result = result.JoinAfter(fragment, context);
            }
            return result.JoinAfter(Trailing, context);

        }
    }


    /// <summary>
    /// SQLクエリ文字列に対応するデータクラスの基底。
    /// </summary>
    internal abstract record SqlQueryElement{
        internal virtual SqlType EnableType { get; } = SqlType.All;//Allでないエレメントでのみ上書きする。
        internal abstract bool HasChild { get; }//子要素を持つかどうか。空文字型以外は全て持つことになる。空の空文字リストもfalseとなる。つまり、Build可能な子要素を持つかどうか。
        internal abstract bool HasChildNode { get; }//実文字型の子要素を持つかどうか。Token型やリテラル、名前などもfalseとなる。空のリストもfalseとなる。つまり、Build可能な子要素を持つかどうか。


        //// 構築前に構造の妥当性をチェックする（例：必須の子要素が不足していないか）
        //public virtual void Validate(){
        //    ValidateSelf();
        //    foreach (var (_, newMainElement) in IterateChildren()) newMainElement.Validate();
        //}
        //protected virtual void ValidateSelf() { }
        /// <summary>
        /// 直接の子要素を反復処理する為のジェネレータ。
        /// </summary>
        /// <returns></returns>
        internal virtual IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotFieldAbstract Slot)> IterateChildren() { yield break; }

        #region 再帰処理
        /// <summary>
        /// 実行用のクエリを構築する処理。
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        internal abstract SqlBuiltQueryFragment Build(SqlBuildContext context);
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

        #region 子要素取得
        internal virtual bool HasRole(SqlQueryElementRole role) {
            foreach (var (eachRole, _) in IterateChildren()){
                if (eachRole == role) return true;
            }
            return false;
        }
        internal virtual SqlQuerySlotFieldAbstract? GetChildByRole(SqlQueryElementRole role){
            foreach (var (eachRole, child) in IterateChildren()){
                if (eachRole == role) return child;
            }
            return null;
        }
        internal virtual SqlQuerySlotFieldAbstract? GetChildById(SqlQuerySlotId id){
            foreach (var (_, child) in IterateChildren()){
                if (child.Id == id) return child;
            }
            return null;
        }
        /// <summary>
        /// IDに該当する直接の子があればその役割と子要素を返す。なければnullを返す。
        /// </summary>
        /// <param name="childId"></param>
        /// <param name="targetSlot"></param>
        /// <returns></returns>
        internal SqlQueryElementRole? GetRoleById(SqlQuerySlotId childId, out SqlQuerySlotFieldAbstract? targetSlot){
            foreach (var (role, child) in IterateChildren()){
                if (child.Id == childId){
                    targetSlot = child;
                    return role;
                }
            }
            targetSlot = null;
            return null;
        }
        #endregion

        #region 子スロットのIDを保ったまま中身を変更するReplace処理
        /// <summary>
        /// 子スロットのIDを保ったまま中身を変更するReplace処理
        /// </summary>
        internal virtual SqlQueryElement? ReplaceChildByRole(SqlQueryElementRole role, SqlQuerySlotStateAbstract state) => ReplaceChildrenByRole([(role,state)]);
        /// <summary>
        /// 子スロットのIDを保ったまま中身を変更するReplace処理
        /// </summary>
        internal virtual SqlQueryElement? ReplaceChildById(SqlQuerySlotId id, SqlQuerySlotStateAbstract state) => ReplaceChildrenById([(id, state)]);
        /// <summary>
        /// 子スロットのIDを保ったまま中身を変更するReplace処理
        /// </summary>
        internal abstract SqlQueryElement? ReplaceChildrenByRole(IEnumerable<(SqlQueryElementRole role, SqlQuerySlotStateAbstract state)> states);
        /// <summary>
        /// 子スロットのIDを保ったまま中身を変更するReplace処理
        /// </summary>
        internal abstract SqlQueryElement? ReplaceChildrenById(IEnumerable<(SqlQuerySlotId id, SqlQuerySlotStateAbstract state)> states);
        #endregion




    }

}
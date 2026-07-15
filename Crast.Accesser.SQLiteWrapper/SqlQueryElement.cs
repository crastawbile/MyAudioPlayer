using System.Collections.Immutable;

namespace Crast.Accessor.SqlWrapper{

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
    public readonly record struct SqlQueryElementRole(SqlQueryElementRoleEnum Name, int Index);
    public enum SqlQueryElementRoleEnum{
        Statement,//実行可能な命令の実体:ISqlStatement:単数
        Relation,//データの供給源（表など）:ITableExpression:複数
        Selection,//出力・対象の指定（列）:IScalarExpression:複数
        Condition,//真偽判定のロジック:IPredicate:単数
        Argument,//演算や関数の入力値:IScalarExpression:複数
        Ordering,//並び替えや集約の基準:ISqlElement (Sort):複数
        Naming,//名前定義（別名など）:SqlIdentifier:単数
        Trivia//空白文字やコメントなどの構造に影響しない要素:SqlQueryTriviaElement:複数
    }

    /// <summary>
    /// Slotが実際にElementを持つか、他の状態であるかを表す列挙型。
    /// </summary>
    public enum SqlQuerySlotStateEnum{
        Normal,    // 通常（実体あり）
        Empty,     // 空（親の構造上スロットはあるが空でもBuild可能）
        Undefined, // 未定義（クエリ構築途中など、具体的な実体が指定されていない状態）
        Default    // デフォルト状態
    }
    [Flags]
    public enum SqlQuerySlotCapabilities{
        None = 0,
        AllowEmpty = 1 << 0,        // 中身が無くても成立するスロット
        AllowUndefined = 1 << 1,    // 常に中身のあるElementで置換しなければならないスロットが存在するなら、それはUndefined不可となるが、存在するか……？
        AllowDefault = 1 << 2       // デフォルト指定が存在するスロット
    }

    #endregion

    /// <summary>
    /// Elementのプロパティ型としてのスロットの非ジェネリック基底。
    /// </summary>
    /// <remarks>
    /// Elementの不変かつ固定の一部として、IDと取りうる状態を保持する。
    /// PointerはこのFieldを指し示し、この中身を操作する。
    /// </remarks>
    public abstract record SqlQuerySlotField {
        public SqlQuerySlotId Id { get;}
        internal string IdString => $"#0x{Id.Value:x8} ";
        public SqlQuerySlotCapabilities Capabilities { get; }
        public virtual SqlQuerySlotState　State { get;}
        public abstract Type GenericType { get; }
        public SqlQuerySlotField (SqlQuerySlotId id, SqlQuerySlotCapabilities caps, SqlQuerySlotState state) {
            ValidateState(state);
            Id = id;
            Capabilities = caps;
            State = state;
        }
        /// <summary>
        /// StateがCapabilityに適合するかどうかと、Elementがジェネリック型に適合するかどうかを、それぞれチェックして例外を出す。
        /// </summary>
        /// <param name="state"></param>
        protected abstract SqlQuerySlotState ValidateState(SqlQuerySlotState state);

        #region 短絡プロパティ
        public bool IsNormal => State.IsNormal;
        public bool IsDefault => State.IsDefault;
        public bool IsEmpty => State.IsEmpty;
        public bool IsUndefined => State.IsUndefined;

        public bool HasChild => IsNormal && State.Element!.HasChild;
        #endregion



        #region Buildの通知シンクにIDを追加する処理
        public SqlBuiltQueryFragment Build(SqlBuildContext context){
            if (!IsNormal) throw new InvalidOperationException("実データのないスロットは親エレメントの債務で構築する");
            var fragment = State.Element!.Build(context);
            context.AdaptDiagnostics(Id);
            return fragment;
        }
        public SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context){
            if (!IsNormal) throw new InvalidOperationException("実データのないスロットは親エレメントの債務で構築する");
            var fragment = State.Element!.DebugBuild(context);
            context.AdaptDiagnostics(Id);

            //出力文字列にも、設定次第でIDを付与する。
            var idStr = context.Options.ShowIds ? IdString : string.Empty;
            fragment.Values[0] = $"{idStr}{fragment.Values[0]}";
            return fragment;
        }
        #endregion

        #region Elementのメソッドを呼び出す処理
        public IEnumerable<(SqlQueryElementRole,SqlQuerySlotField)> IterateChildren() {
            if (!IsNormal) yield break;
            else State.Element!.IterateChildren();
        }
        public SqlQuerySlotField? GetChildByRole(SqlQueryElementRole role) => State.GetChildByRole(role);
        public SqlQuerySlotField? GetChildById(SqlQuerySlotId id) => State.GetChildById(id);
        public SqlQueryElementRole? GetRoleById(SqlQuerySlotId childId, out SqlQuerySlotField? targetSlot) => State.GetRoleById(childId, out targetSlot);
        #endregion

        #region 状態遷移用メソッド
        public abstract SqlQuerySlotField RecreateAlter(SqlQuerySlotState newState);
        public abstract SqlQuerySlotField ReplaceAlter(SqlQueryElement element);
        public abstract SqlQuerySlotField FillAlter(SqlQueryElement element);
        public abstract SqlQuerySlotField ToEmpty();
        public abstract SqlQuerySlotField ToUndefined();
        public abstract SqlQuerySlotField ToDefault();
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
        public SqlQuerySlotField? GetSlotField(SqlQueryPath path){
            switch (ResolvePath(path)){
                case SqlQueryPathResolve.Self:
                    return this;

                case SqlQueryPathResolve.Descend:
                    var current = this;
                    foreach (var id in path.Ids){
                        if (current.IsNormal
                            && current.State.GetChildById(new SqlQuerySlotId(id)) is SqlQuerySlotField next){
                            current = next;
                        } else {
                            return null;
                        }
                    }
                    return current;

                case SqlQueryPathResolve.Invalid:
                default:
                    return null;
            }
        }

        public SqlQueryPath? GetPathByElement(SqlQueryElement target, out SqlQuerySlotField? targetSlot){
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
        public SqlQueryPath? GetPathById(SqlQuerySlotId targetId, out SqlQuerySlotField? targetSlot){
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
        public SqlQueryPath? GetPathBySelector(SqlQuerySelector selector, out SqlQuerySlotField? targetSlot){
            targetSlot = null;
            if (!IsNormal) return null;
            if (selector.RootId != Id) return null;
            if (selector.Length == 0){
                targetSlot = this;
                return new(Id, []);
            }

            int[] ids = new int[selector.Length];//完走しない限り読み取らないので初期値は気にしない。
            var currentNode = this;
            for (var i = 0; i < selector.Length; i++){
                var role = selector.Roles[i];
                bool found = false;
                foreach (var (childRole, child) in currentNode.IterateChildren()){
                    if (role == childRole){
                        currentNode = child;
                        ids[i] = child.Id.Value;
                        found = true;
                        break;
                    }
                }
                if (!found) return null;//その役割の子要素が見つからなかった場合はnullを返す。
            }
            //完走できたら出力。
            targetSlot = currentNode;
            return new(Id, [.. ids]);
        }
        public SqlQuerySelector? GetSelectorByPath(SqlQueryPath path, out SqlQuerySlotField? targetSlot){
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
                foreach (var (childRole, child) in currentNode.IterateChildren()){
                    if (id == child.Id.Value){
                        currentNode = child;
                        roles[i] = childRole;
                        found = true;
                        break;
                    }
                }
                if (!found) return null;//そのIDの子要素が見つからなかった場合はnullを返す。
            }
            //完走できたら出力。
            targetSlot = currentNode;
            return new(Id, [.. roles]);
        }


        /// <summary>
        /// SqlQueryDraft.Updateの内部で、クエリツリー全体を再構築するために使用されるメソッド。targetに一致する部分をreplacementに置き換えた新しいクエリツリーを返す。
        /// </summary>
        /// <remarks>
        /// path不正はnull、replacementの型不正は例外を返すべき。
        /// 変更位置自体のSlotのIDは維持されなければならない。それ以外は対象のIDを使うか、draftから振り直す。
        /// </remarks>
        public abstract SqlQuerySlotField? ReplaceRecursive(SqlQueryDraft draft, SqlQueryPath path, SqlQuerySlotState replacement, bool refreshId = true);

        #endregion
    }
    /// <summary>
    /// スロットの中身の非ジェネリック基底。
    /// </summary>
    /// <remarks>
    /// ElementもしくはDefault・Empty・Undefinedの状態を表すラッパー。
    /// </remarks>
    public abstract record SqlQuerySlotState {
        public SqlQuerySlotStateEnum StateEnum { get; }
        public virtual SqlQueryElement? Element { get; } // StateEnum == Normal の時のみ実体が入る
        /// <summary>
        /// 不確定型ノードを配下に持つかどうか。
        /// </summary>
        public bool HasUndefined { get; }
        /// <summary>
        /// 対応可能なSQL方言範囲。
        /// </summary>
        public SqlType EnableType { get; }
        public abstract Type GenericType { get; }

        #region 短絡プロパティ
        public bool IsNormal => StateEnum == SqlQuerySlotStateEnum.Normal;
        public bool IsDefault => StateEnum == SqlQuerySlotStateEnum.Default;
        public bool IsEmpty => StateEnum == SqlQuerySlotStateEnum.Empty;
        public bool IsUndefined => StateEnum == SqlQuerySlotStateEnum.Undefined;
        #endregion

        public SqlQuerySlotState(SqlQuerySlotStateEnum stateEnum, SqlQueryElement? element) {
            if (IsNormal) ValidateElement(element!);
            StateEnum = stateEnum;
            Element = element;

            //HasUndefinedとEnableTypeは、SlotよりもElementの状態ではあるが、
            //Elementのコンストラクタは複雑なため、Elementのコンストラクタが完了した後に処理されるこちらに隔離する。
            HasUndefined = false;
            EnableType = IsNormal ? Element!.SafeType : SqlType.All;
            if (IsNormal) {
                foreach (var (_,slot) in element!.IterateChildren()) {
                    HasUndefined |= (slot.IsUndefined);
                    EnableType &= slot.State.EnableType;
                }
            }
        }
        /// <summary>
        /// Elementがジェネリック型に適合するかどうかをチェックして例外を出す。
        /// </summary>
        protected abstract void ValidateElement(SqlQueryElement element);
        /// <summary>
        /// SlotStateがジェネリック型に適合するかどうかをチェックして例外を出す。
        /// </summary>
        protected abstract SqlQuerySlotState ValidateSlotState(SqlQuerySlotState slotState);

        #region 状態遷移用メソッド
        public abstract SqlQuerySlotState ReplaceAlter(SqlQueryElement element);
        public abstract SqlQuerySlotState FillAlter(SqlQueryElement element);
        public abstract SqlQuerySlotState ToEmpty();
        public abstract SqlQuerySlotState ToUndefined();
        public abstract SqlQuerySlotState ToDefault();
        #endregion

        #region Elementのメソッドを呼び出すだけの処理
        public IEnumerable<(SqlQueryElementRole, SqlQuerySlotField)> IterateChildren(){
            if (!IsNormal) yield break;
            else Element!.IterateChildren();
        }
        internal abstract SqlQuerySlotState CloneNode(SqlQueryDraft draft);
        public SqlQuerySlotField? GetChildByRole(SqlQueryElementRole role) => Element?.GetChildByRole(role);
        public SqlQuerySlotField? GetChildById(SqlQuerySlotId id) => Element?.GetChildById(id);
        public SqlQueryElementRole? GetRoleById(SqlQuerySlotId childId, out SqlQuerySlotField? targetSlot){
            if (Element?.GetRoleById(childId, out var slot) is SqlQueryElementRole role){
                targetSlot = slot;
                return role;
            } else {
                targetSlot = null;
                return null;
            }
        }


        /// <summary>
        /// 子要素を置き換えた新しいクエリノードを返す。
        /// </summary>
        /// <param name="role">置き換える子要素の役割を識別する文字列。</param>
        /// <param name="newElement">置き換える新しい子要素。</param>
        /// <returns>置き換え後の新しいクエリノード。</returns>
        public virtual SqlQuerySlotState? ReplaceChildById(SqlQueryDraft draft, SqlQuerySlotId id, SqlQuerySlotState newNode){
            if (GetRoleById(id, out _) is SqlQueryElementRole role) return ReplaceChildByRole(draft, role, newNode);
            else return null;
        }
        public abstract SqlQuerySlotState? ReplaceChildByRole(SqlQueryDraft draft, SqlQueryElementRole role, SqlQuerySlotState newNode);



        #endregion
    }




    /// <summary>
    /// SlotState用の共変性インターフェイス。
    /// </summary>
    /// <remarks>
    /// Elementを最大基底型で扱えるようにする。
    /// これにより、Elementは子要素のSlotFieldを固定の型で扱いつつ、SlotStateは共変性で幅広くElementを保持できる。
    /// つまり、Elementの内部ロジックが固定のSlot型だけ見ればよくなる。
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public interface ISqlQuerySlotState<out T> {
        public T? Element { get; }
    }
    /// <summary>
    /// SlotField用の共変性インターフェイス。
    /// </summary>
    /// <remarks>
    /// 基本、共変性はSlotStateの担当だが、中継メソッドの引数にTを使うことがあるので。
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public interface ISqlQuerySlotField<out T>{
        public ISqlQuerySlotState<T>? State { get; }
    }

    /// <summary>
    /// Elementのプロパティ型としてのスロット。
    /// </summary>
    /// <remarks>
    /// Elementの不変かつ固定の一部として、IDと取りうる状態を保持する。
    /// PointerはIDでこのFieldを指し示し、この中身のState<T>を操作する。
    /// </remarks>
    public sealed record SqlQuerySlotField<T> : SqlQuerySlotField, ISqlQuerySlotField<T> where T : SqlQueryElement {
        public override Type GenericType => typeof(T);
        public override SqlQuerySlotState<T> State { get;}
        ISqlQuerySlotState<T>? ISqlQuerySlotField<T>.State => this.State;
        //フルコンストラクタ
        public SqlQuerySlotField(
            SqlQuerySlotId id,
            SqlQuerySlotCapabilities caps,
            SqlQuerySlotState<T> state
            )
            : base(id, caps, state)
        {
            State = state;
        }

        /// <summary>
        /// StateがCapabilityに適合するかどうかと、Elementがジェネリック型に適合するかどうかを、それぞれチェックして例外を出す。
        /// </summary>
        /// <param name="state"></param>
        protected override SqlQuerySlotState<T> ValidateState(SqlQuerySlotState state) {
            if (state.IsDefault && !Capabilities.HasFlag(SqlQuerySlotCapabilities.AllowDefault)) throw new InvalidOperationException("このスロットはDefault化を許可されていません。");
            if (state.IsEmpty && !Capabilities.HasFlag(SqlQuerySlotCapabilities.AllowEmpty)) throw new InvalidOperationException("このスロットはEmpty化を許可されていません。");
            if (state.IsUndefined && !Capabilities.HasFlag(SqlQuerySlotCapabilities.AllowUndefined)) throw new InvalidOperationException("このスロットはUndefined化を許可されていません。");
            if (state is not SqlQuerySlotState<T> s) throw new InvalidOperationException($"{typeof(T)}型に対応したスロットが必要");
            return s;
        }

        #region ファクトリメソッド
        //全てstaticなので、基底には不要。
        public static SqlQuerySlotField<T> Create(SqlQueryDraft draft, SqlQuerySlotCapabilities caps, SqlQuerySlotState<T> slotState){
            if (slotState.IsDefault && !caps.HasFlag(SqlQuerySlotCapabilities.AllowDefault)) throw new ArgumentException("このスロットはDefault化を許可されていません。");
            if (slotState.IsEmpty && !caps.HasFlag(SqlQuerySlotCapabilities.AllowEmpty)) throw new ArgumentException("このスロットはEmpty化を許可されていません。");
            if (slotState.IsUndefined && !caps.HasFlag(SqlQuerySlotCapabilities.AllowUndefined)) throw new ArgumentException("このスロットはUndefined化を許可されていません。");
            return new SqlQuerySlotField<T>(SqlQuerySlotId.GetNextId(draft), caps, slotState);
        }
        public static SqlQuerySlotField<T> CreateNormal(SqlQueryDraft draft, SqlQuerySlotCapabilities caps, T element){
            return new SqlQuerySlotField<T>(SqlQuerySlotId.GetNextId(draft), caps, new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Normal, element));
        }
        public static SqlQuerySlotField<T> CreateEmpty(SqlQueryDraft draft, SqlQuerySlotCapabilities caps){
            if (!caps.HasFlag(SqlQuerySlotCapabilities.AllowEmpty)) throw new ArgumentException("このスロットはEmpty化を許可されていません。");
            return new SqlQuerySlotField<T>(SqlQuerySlotId.GetNextId(draft), caps, new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Empty, default));
        }
        public static SqlQuerySlotField<T> CreateUndefined(SqlQueryDraft draft, SqlQuerySlotCapabilities caps){
            if (!caps.HasFlag(SqlQuerySlotCapabilities.AllowUndefined)) throw new ArgumentException("このスロットはUndefined化を許可されていません。");
            return new SqlQuerySlotField<T>(SqlQuerySlotId.GetNextId(draft), caps, new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Undefined, default));
        }
        public static SqlQuerySlotField<T> CreateDefault(SqlQueryDraft draft, SqlQuerySlotCapabilities caps){
            if (!caps.HasFlag(SqlQuerySlotCapabilities.AllowDefault)) throw new ArgumentException("このスロットはDefault化を許可されていません。");
            return new SqlQuerySlotField<T>(SqlQuerySlotId.GetNextId(draft), caps, new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Default, default));
        }
        #endregion

        #region 状態遷移用メソッド

        private SqlQuerySlotField<T> PrivateRecreate(SqlQuerySlotState<T> newState){
            return new SqlQuerySlotField<T>(Id, Capabilities, newState);
        }
        public SqlQuerySlotField<T> Recreate(SqlQuerySlotState<T> newState){
            return PrivateRecreate(ValidateState(newState));
        }
        public override SqlQuerySlotField<T> RecreateAlter(SqlQuerySlotState newState){
            return PrivateRecreate(ValidateState(newState));
        }
        public SqlQuerySlotField<T> Replace(T element){
            return PrivateRecreate(new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Normal, element));
        }
        public override SqlQuerySlotField<T> ReplaceAlter(SqlQueryElement element){
            if (element is not T e) throw new ArgumentException($"{typeof(T)}型のElementが必要");
            return Replace(e);
        }
        public SqlQuerySlotField<T> Fill(T element){
            if (!IsUndefined && !IsEmpty) throw new InvalidOperationException("このスロットはUndefinedでもEmptyでもありません。");
            return PrivateRecreate(new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Normal, element));
        }
        public override SqlQuerySlotField<T> FillAlter(SqlQueryElement element){
            if (element is not T e) throw new ArgumentException($"{typeof(T)}型のElementが必要");
            return Fill(e);
        }
        public override SqlQuerySlotField<T> ToEmpty(){
            if (!Capabilities.HasFlag(SqlQuerySlotCapabilities.AllowEmpty)) throw new InvalidOperationException("このスロットはEmpty化を許可されていません。");
            return PrivateRecreate(new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Empty, default));
        }
        public override SqlQuerySlotField<T> ToUndefined(){
            if (!Capabilities.HasFlag(SqlQuerySlotCapabilities.AllowUndefined)) throw new InvalidOperationException("このスロットはUndefined化を許可されていません。");
            return PrivateRecreate(new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Undefined, default));
        }
        public override SqlQuerySlotField<T> ToDefault(){
            if (!Capabilities.HasFlag(SqlQuerySlotCapabilities.AllowDefault)) throw new InvalidOperationException("このスロットはDefault化を許可されていません。");
            return PrivateRecreate(new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Default, default));
        }

        #endregion

        #region IDを辿る処理


        /// <summary>
        /// SqlQueryDraft.Updateの内部で、クエリツリー全体を再構築するために使用されるメソッド。Pahtで指定されたSlotのStateをreplacementに置き換えた新しいクエリツリーを返す。
        /// </summary>
        /// <remarks>
        /// path不正はnull、replacementの型不正は例外を返すべき。
        /// </remarks>
        public override SqlQuerySlotField<T>? ReplaceRecursive(SqlQueryDraft draft, SqlQueryPath path, SqlQuerySlotState replacement, bool refleshId = false){
            switch (ResolvePath(path)){
                case SqlQueryPathResolve.Self:
                    var correctSelf = ValidateState(replacement);
                    if (refleshId) correctSelf = correctSelf.CloneNode(draft);
                    return Recreate(correctSelf);
                case SqlQueryPathResolve.Descend:
                    var childPath = path.RemoveRoot();
                    var childId = childPath.RootId;
                    if (State.GetRoleById(childId, out SqlQuerySlotField? child) is SqlQueryElementRole childRole//Pathの次のIDに対応する子要素があるかどうか
                        && child!.ReplaceRecursive(draft, childPath, replacement, refleshId) is SqlQuerySlotField childResult//掘り進んだどこかでPathの指定先が存在するか否か
                        && State.ReplaceChildByRole(draft, childRole, childResult.State) is SqlQuerySlotState<T> updatedState){//置き換えたSlotStateをジェネリック型に確定でキャスト
                        return Recreate(updatedState);
                    } else {
                        return null;
                    }
                case SqlQueryPathResolve.Invalid:
                default:
                    return null;
            }
        }

        #endregion
    }
    /// <summary>
    /// スロットの中身。Pointerから変更されるデータツリーのトップ。
    /// </summary>
    /// <remarks>
    /// ElementもしくはDefault・Empty・Undefinedの状態を表すラッパー。
    /// </remarks>
    public sealed record SqlQuerySlotState<T> : SqlQuerySlotState, ISqlQuerySlotState<T> where T : SqlQueryElement {
        public override T? Element { get; } // StateEnum == Normal の時のみ実体が入る
        public override Type GenericType => typeof(T);
        //フルコンストラクタ
        public SqlQuerySlotState(
            SqlQuerySlotStateEnum state,
            T? element
            )
            : base(state, element)
        {
        }
        /// <summary>
        /// Elementがジェネリック型に適合するかどうかをチェックして例外を出す。
        /// </summary>
        /// <param name="state"></param>
        protected override void ValidateElement(SqlQueryElement element) {
            if (element is not T) throw new ArgumentException($"{typeof(T)}型のエレメントが必要");
        }
        /// <summary>
        /// SlotStateがジェネリック型に適合するかどうかをチェックして例外を出す。
        /// </summary>
        /// <param name="state"></param>
        protected override SqlQuerySlotState<T> ValidateSlotState(SqlQuerySlotState slotState){
            if (slotState is not SqlQuerySlotState<T> s) throw new ArgumentException($"{typeof(T)}型に対応したスロットが必要");
            else return s;
        }

        #region ファクトリメソッド
        //全てstaticなので、基底には不要。
        public static SqlQuerySlotState<T> CreateNormalWithOldId(T element){
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Normal, element);
        }
        public static SqlQuerySlotState<T> CreateNormalWithClone(SqlQueryDraft draft, T element){
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Normal, (T)element.CloneNode(draft));
        }
        public static SqlQuerySlotState<T> CreateEmpty(){
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Empty, default);
        }
        public static SqlQuerySlotState<T> CreateUndefined(){
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Undefined, default);
        }
        public static SqlQuerySlotState<T> CreateDefault(){
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Default, default);
        }
        #endregion

        #region 状態遷移用メソッド

        public SqlQuerySlotState<T> Replace(T element){
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Normal, element);
        }
        public override SqlQuerySlotState<T> ReplaceAlter(SqlQueryElement element){
            if (element is not T e) throw new ArgumentException($"{typeof(T)}型のElementが必要");
            return Replace(e);
        }
        public SqlQuerySlotState<T> Fill(T element){
            if (!IsUndefined && !IsEmpty) throw new InvalidOperationException("このスロットはUndefinedでもEmptyでもありません。");
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Normal, element);
        }
        public override SqlQuerySlotState<T> FillAlter(SqlQueryElement element){
            if (element is not T e) throw new ArgumentException($"{typeof(T)}型のElementが必要");
            return Fill(e);
        }
        public override SqlQuerySlotState<T> ToEmpty(){
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Empty, default);
        }
        public override SqlQuerySlotState<T> ToUndefined(){
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Undefined, default);
        }
        public override SqlQuerySlotState<T> ToDefault(){
            return new SqlQuerySlotState<T>(SqlQuerySlotStateEnum.Default, default);
        }

        #endregion

        #region Elementのメソッドを呼び出すだけの処理
        internal override SqlQuerySlotState<T> CloneNode(SqlQueryDraft draft){
            if (!IsNormal) return new SqlQuerySlotState<T>(StateEnum, default);
            if (Element!.CloneNode(draft) is T clonedElement) return new SqlQuerySlotState<T>(StateEnum, clonedElement);
            throw new InvalidOperationException("クローンした要素の型が一致しません。");
        }

        /// <summary>
        /// 子要素を置き換えた新しいクエリノードを返す。
        /// </summary>
        /// <param name="role">置き換える子要素の役割を識別する文字列。</param>
        /// <param name="newNode">置き換える新しい子要素。</param>
        /// <returns>置き換え後の新しいクエリノード。</returns>
        public override SqlQuerySlotState<T>? ReplaceChildById(SqlQueryDraft draft, SqlQuerySlotId id, SqlQuerySlotState newNode){
            if (GetRoleById(id, out _) is SqlQueryElementRole role) return ReplaceChildByRole(draft, role, newNode);
            else return null;
        }
        public override SqlQuerySlotState<T>? ReplaceChildByRole(SqlQueryDraft draft, SqlQueryElementRole role, SqlQuerySlotState newNode){
            if (IsNormal && Element!.ReplaceChildByRole(draft, role, newNode) is T correctElement) return Replace(correctElement);
            else return null;
        }
        #endregion
    }



    /// <summary>
    /// SQLクエリ文字列に対応するデータクラスの基底。
    /// </summary>
    public abstract record SqlQueryElement{
        public virtual SqlType SafeType { get; } = SqlType.All;//Allでないエレメントでのみ上書きする。
        public abstract bool HasChild { get; }//子要素を持つかどうか。空文字型以外は全て持つことになる。空の空文字リストもfalseとなる。つまり、Build可能な子要素を持つかどうか。
        public abstract bool HasChildNode { get; }//実文字型の子要素を持つかどうか。Token型やリテラル、名前などもfalseとなる。空のリストもfalseとなる。つまり、Build可能な子要素を持つかどうか。


        /// <summary>
        /// 実行用のクエリを構築する処理。
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract SqlBuiltQueryFragment Build(SqlBuildContext context);
        /// <summary>
        /// 人間用のクエリ構造図を構築する処理。
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context);
        /// <summary>
        /// 直接の子要素を反復処理する為のジェネレータ。
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<(SqlQueryElementRole Role, SqlQuerySlotField Slot)> IterateChildren() { yield break; }
        //再帰的にクローンする必要があるので、この処理ではまずい。
        //internal SqlQueryElement CloneNode() => this with { Id = SqlQueryDraft.GetNewId() };
        internal abstract SqlQueryElement CloneNode(SqlQueryDraft draft);
        //// 構築前に構造の妥当性をチェックする（例：必須の子要素が不足していないか）
        //public virtual void Validate(){
        //    ValidateSelf();
        //    foreach (var (_, element) in IterateChildren()) element.Validate();
        //}
        //protected virtual void ValidateSelf() { }



        public SqlQuerySlotField? GetChildByRole(SqlQueryElementRole role){
            foreach (var (eachRole, child) in IterateChildren()){
                if (eachRole == role) return child;
            }
            return null;
        }
        public SqlQuerySlotField? GetChildById(SqlQuerySlotId id){
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
        public SqlQueryElementRole? GetRoleById(SqlQuerySlotId childId, out SqlQuerySlotField? targetSlot){
            foreach (var (role, child) in IterateChildren()){
                if (child.Id == childId){
                    targetSlot = child;
                    return role;
                }
            }
            targetSlot = null;
            return null;
        }

        /// <summary>
        /// 子要素を更新する処理の基底
        /// </summary>
        /// <remarks>
        /// 子要素はroleによって異なる型になり得るので、非ジェネリックのこのメソッドはAlterではなく本メソッド。
        /// </remarks>
        /// <param name="draft"></param>
        /// <param name="role"></param>
        /// <param name="newNode"></param>
        /// <returns></returns>
        internal abstract SqlQueryElement? ReplaceChildByRole(SqlQueryDraft draft, SqlQueryElementRole role, SqlQuerySlotState newNode);

        #region リスト型用 
        //共変性の都合で、公開プロパティはIReadOnlyList<out T>(あるいは緩めてIEnumerable<out T>)にする必要がある。(内部フィールドはImmutableArrayにすべき。)
        internal SqlQueryElement? AddChild(SqlQueryElementRole role, SqlQuerySlotState newNode) => AddChildren(role, [newNode]);
        internal abstract SqlQueryElement? AddChildren(SqlQueryElementRole role, SqlQuerySlotState[] newNodes);
        internal abstract SqlQueryElement? RemoveChildren(SqlQueryElementRole role, Byte count = 0);
        #endregion

        internal virtual SqlQueryElement AdaptTo(SqlType targetDialect) => this;

    }

}
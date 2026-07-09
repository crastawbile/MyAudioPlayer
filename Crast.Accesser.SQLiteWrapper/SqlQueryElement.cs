namespace Crast.Accesser.SqlWrapper{

    #region 引数用のデータクラスや列挙型

    /// <summary>
    /// 各Elementを識別する為のID。SqlQueryDraft全体で一意かつ不変。
    /// </summary>
    /// <remarks>
    /// ちょっと変更されるたびに作り直されるrecordであるElementに対して、論理的同一性を担保する。
    /// </remarks>
    /// <param name="Value"></param>
    public readonly record struct SqlQueryElementId(long Value);
    /// <summary>
    /// ルートからあるElementまでのパスを表すデータクラス。クエリツリー内の特定のElementを識別する為に使用される。
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="Ids"></param>
    public readonly record struct SqlQueryPath(long[]  Ids){
        public int Length => Ids.Length;
        public SqlQueryElementId Root => new(Ids.First());
        public SqlQueryElementId Leaf => new(Ids.Last());
        public SqlQueryPath AppendLeaf(SqlQueryElementId newNode) => new([.. Ids, newNode.Value]);
        public SqlQueryPath AppendLeaf(params SqlQueryElementId[] newNodes) => new([..Ids, ..newNodes.Select(n => n.Value)]);
        public SqlQueryPath AppendRoot(SqlQueryElementId newNode) => new([newNode.Value, ..Ids]);
        public SqlQueryPath PrependRoot(params SqlQueryElementId[] newNodes) => new([..newNodes.Select(n => n.Value), ..Ids]);
        public SqlQueryPath Removeleaf(int count = 1){
            if (count >= Ids.Length) return new([]);
            if (count <= 0) return this;
            // 範囲演算子 [..^n] は「最初から、末尾からn個手前まで」を指す
            return new(Ids[..^count]);
        }
        public SqlQueryPath RemoveRoot(int count = 1){
            if (count >= Ids.Length) return new([]);
            if (count <= 0) return this;
            // 範囲演算子 [n..] は「n個目から最後まで」を指す
            return new(Ids[count..]);
        }

        /// <summary>
        /// 共通するノードの個数を返すヘルパーメソッド
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        private int CountSharedPath(SqlQueryPath other) {
            int ln = Math.Min(Ids.Length, other.Ids.Length);
            int count = 0;
            for (int i = 0; i < ln; i++) {
                if (Ids[i] != other.Ids[i]) break;
                count++;
            }
            return count;
        } 
        /// <summary>
        /// 最後の共通パスを含む相対パスを返す
        /// </summary>
        /// <remarks>
        /// 自身がA-B-CでotherがA-B-Dなら、B-Cを返す。
        /// 一つも共通していなければnull。
        /// </remarks>
        /// <param name="other"></param>
        /// <returns></returns>
        public SqlQueryPath? GetRelativePath(SqlQueryPath other) {
            int sharedCount = CountSharedPath(other) -1;
            if (sharedCount < 0) return null;
            return new(Ids[sharedCount..]);
        }
        /// <summary>
        /// 最後の共通パスまでのパスを返す
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public SqlQueryPath GetSharedPath(SqlQueryPath other) {
            int sharedCount = CountSharedPath(other);
            return new(Ids[..sharedCount]);
        }
    }
    /// <summary>
    /// 各Elementの子要素を識別する為のデータ。
    /// </summary>
    /// <param name="Role">子要素の役割を識別する列挙値。</param>
    /// <param name="Index">同じ役割を持つ子要素のインデックス。</param>
    public readonly record struct SqlQueryElementRole(SqlQueryElementRoleEnum Role, int Index);

    public enum SqlQueryElementRoleEnum{
        Statement,//実行可能な命令の実体:ISqlStatement:単数
        Relation,//データの供給源（表など）:ITableExpression:複数
        Selection,//出力・対象の指定（列）:IScalarExpression:複数
        Condition,//真偽判定のロジック:IPredicate:単数
        Argument,//演算や関数の入力値:IScalarExpression:複数
        Ordering,//並び替えや集約の基準:ISqlElement (Sort):複数
        Naming//名前定義（別名など）:SqlIdentifier:単数
    }


    #endregion



    /// <summary>
    /// 可変な組み立て中のクエリを表すクラス。スレッドセーフな更新操作を提供する。
    /// </summary>
    public sealed class SqlQueryDraft{
        private static long IdCount = 0;
        internal static SqlQueryElementId GetNewId() => new(Interlocked.Increment(ref IdCount));

        private SqlQueryElement _root; // 不変レコードツリーの根本。

        public SqlQueryDraft(SqlQueryElement root){
            _root = root;
        }

        public void Update(SqlQueryPath path, SqlQueryElement newElement){
            var newClone = CloneNode(newElement);
            while (true){
                var snapshot = _root;
                // 新しいツリーを生成（この計算自体はスナップショットに対して行うので安全）
                if (snapshot.Replace(path, newClone) is not SqlQueryElement newTree){
                    throw new InvalidOperationException($"そのパスに該当するノードはこのクエリ内に存在しない");
                }

                // 参照をアトミックに差し替え。他スレッドに先を越されていたらやり直し（CAS操作）
                if (ReferenceEquals(Interlocked.CompareExchange(ref _root, newTree, snapshot), snapshot)) break;
            }
        }
        /// <summary>
        /// 対象ノードにこのドラフトのIDを振り直したクローンを生成する。
        /// </summary>
        /// <remarks>
        /// 自身の管理下にないIDを持ったノードを配下のツリーに組み込むわけにはいかないので、常にこいつを経由させる。
        /// 元々自身の配下であってもコピペする場合は振り直し必須なので、無条件にこいつ経由の方が安全。
        /// </remarks>
        /// <param name="target"></param>
        /// <returns></returns>
        internal SqlQueryElement? CloneNode(SqlQueryPath path) => _root.GetElement(path)?.CloneNode(this);
        internal SqlQueryElement CloneNode(SqlQueryElement target) => target.CloneNode(this);

        public bool Insert(SqlQueryPath path, SqlQueryElement childTree, SqlQueryPath childPath) {
            while (true){
                var snapshot = _root;
                if (snapshot.GetElement(path) is not SqlQueryElement leaf) return false;
                if (childTree.Replace(childPath, leaf) is not SqlQueryElement newChild) return false;
                if (snapshot.Replace(path, newChild) is not SqlQueryElement newTree) return false;

                // 参照をアトミックに差し替え。他スレッドに先を越されていたらやり直し（CAS操作）
                if (ReferenceEquals(Interlocked.CompareExchange(ref _root, newTree, snapshot), snapshot)) return true;
            }
        }
        public bool Skip(SqlQueryPath path, SqlQueryPath childPath) {
            var sharedPath = path.GetSharedPath(childPath);

            while (true){
                var snapshot = _root;
                if (snapshot.GetElement(sharedPath) is null) return false;
                if (snapshot.GetElement(childPath) is not SqlQueryElement child) return false;
                if (snapshot.Replace(sharedPath, child) is not SqlQueryElement newTree) return false;

                // 参照をアトミックに差し替え。他スレッドに先を越されていたらやり直し（CAS操作）
                if (ReferenceEquals(Interlocked.CompareExchange(ref _root, newTree, snapshot), snapshot)) return true;
            }

        }
    }

    /// <summary>
    /// SQLクエリ文字列に対応するデータクラスの基底。
    /// </summary>
    public abstract record SqlQueryElement{
        public required SqlQueryElementId Id { get; init; }
        public required bool HasUndefined { get; init; }
        public required SqlType Type { get; init; }
        public abstract SqlQueryFragment Build(SqlBuildContext context);
        public abstract SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context);
        // 構築前に構造の妥当性をチェックする（例：必須の子要素が不足していないか）
        public virtual void Validate(){
            ValidateSelf();
            foreach (var (_, element) in IterateChildren()) element.Validate();
        }
        protected abstract void ValidateSelf();

        public abstract SqlQueryElement? GetChildByRole(SqlQueryElementRole role);
        public SqlQueryElement? GetChildById(SqlQueryElementId id){
            foreach (var (_, child) in IterateChildren()){
                if (child.Id == id) return child;
            }
            return null;
        }
        public SqlQueryElement? GetElement(SqlQueryPath path){
            if (path.Root != Id) return null;
            var childPath = path.RemoveRoot();

            SqlQueryElement current = this;
            foreach (var id in childPath.Ids){
                if (current.GetChildById(new SqlQueryElementId(id)) is SqlQueryElement next){
                    current = next;
                }else{
                    return null;
                }
            }
            return current;
        }
        public SqlQueryPath? GetPathByNode(SqlQueryElement target) {
            if (object.ReferenceEquals(this, target)) return new([Id.Value]);
            foreach (var (_, child) in IterateChildren()){
                if (child.GetPathByNode(target) is SqlQueryPath p) return p.AppendRoot(Id);
            }
            return null;
        }
        public SqlQueryPath? GetPathById(SqlQueryElementId targetId){
            if (this.Id == targetId) return new([Id.Value]);
            foreach (var (_, child) in IterateChildren()){
                if (child.GetPathById(targetId) is SqlQueryPath p) return p.AppendRoot(Id);
            }
            return null;
        }
        /// <summary>
        /// このクエリ要素が、targetを子孫要素として含んでいるかどうかを判定する。
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool Has(SqlQueryElement target){
            if (object.ReferenceEquals(this, target)) return true;
            foreach (var (_, Element) in IterateChildren()){
                if (Element.Has(target)) return true;
            }
            return false;
        }
        public SqlQueryElement? GetElementById(SqlQueryElementId id){
            if (Id == id) return this;
            foreach (var (_, Element) in IterateChildren()){
                if (Element.GetElementById(id) is SqlQueryElement result) return result;
            }
            return null;
        }

        /// <summary>
        /// SqlQueryDraft.Updateの内部で、クエリツリー全体を再構築するために使用されるメソッド。targetに一致する部分をreplacementに置き換えた新しいクエリツリーを返す。
        /// </summary>
        /// <param name="target">置き換え対象の要素。</param>
        /// <param name="replacement">置き換える新しい要素。</param>
        /// <param name="result">置き換え後の新しいクエリツリー。</param>
        /// <returns>置き換えが成功したかどうか。</returns>
        public SqlQueryElement? Replace(SqlQueryPath path, SqlQueryElement replacement){
            if (path.Root != Id){return null;}
            var childPath = path.RemoveRoot();
            if (childPath.Length == 0){ return replacement;}

            var childId = childPath.Root;
            if (GetChildById(childId) is SqlQueryElement nextNode
                && nextNode.Replace(childPath, replacement) is SqlQueryElement childResult
                && ReplaceChild(childId, childResult) is SqlQueryElement result
                ) {
                return result;
            } else {
                return null;
            }
        }
        /// <summary>
        /// 直接の子要素を反復処理する為のジェネレータ。
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<(SqlQueryElementRole Role, SqlQueryElement Element)> IterateChildren();
        /// <summary>
        /// 子要素を置き換えた新しいクエリノードを返す。
        /// </summary>
        /// <param name="role">置き換える子要素の役割を識別する文字列。</param>
        /// <param name="newElement">置き換える新しい子要素。</param>
        /// <returns>置き換え後の新しいクエリノード。</returns>
        internal abstract SqlQueryElement? ReplaceChild(SqlQueryElementId id, SqlQueryElement newElement);
        internal abstract SqlQueryElement? ReplaceChild(SqlQueryElementRole role, SqlQueryElement newElement);
        internal abstract SqlQueryElement CloneNode(SqlQueryDraft draft);
        internal abstract SqlQueryElement AddChildren(SqlQueryElementRoleEnum role, SqlQueryElement element);
        internal abstract SqlQueryElement AddChildren(SqlQueryElementRoleEnum role, SqlQueryElement[] elements);
        internal abstract SqlQueryElement RemoveChildren(SqlQueryElementRoleEnum role, int index, int count = 1);

    }


}

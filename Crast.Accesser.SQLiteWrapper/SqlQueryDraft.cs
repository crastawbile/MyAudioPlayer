using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Crast.Accessor.SqlWrapper{

    #region 引数用のデータクラスや列挙型
    /// <summary>
    /// 各Slotを識別する為のID。SqlQueryDraftごとに一意。
    /// </summary>
    /// <remarks>
    /// Draftのツリー内で、エレメントrecord自体の同一性に頼らず抽象的に特定の位置を識別する。
    /// そのため、Slotの内部のElementに関わらず、親Elementが存在し続ける限り同じIDを維持する。
    /// </remarks>
    /// <param name="Value"></param>
    public readonly record struct SqlQuerySlotId(int Value) {
        #region エレメントidの生成管理
        private static readonly ConditionalWeakTable<SqlQueryDraft, StrongBox<int>> _draftCounters = [];
        public static SqlQuerySlotId GetNextId(SqlQueryDraft draft){
            var counter = _draftCounters.GetOrCreateValue(draft);
            return new SqlQuerySlotId(Interlocked.Increment(ref counter.Value));// スレッド安全性の担保
        }
        #endregion
    }
    /// <summary>
    /// ルートからあるSlotまでのID列を表すデータクラス。
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="Ids">structの値型比較のため、値型として扱われるImmutableArray<int>型を利用する。</param>
    public record class SqlQueryPath(SqlQuerySlotId RootId, ImmutableArray<int> Ids){
        public int Length => Ids.Length;
        public bool IsRoot => Ids.Length == 0;
        public SqlQuerySlotId First => Ids.Length == 0 ? RootId : new(Ids[0]);
        public SqlQuerySlotId Leaf => Ids.Length == 0 ? RootId : new(Ids[^1]); // ^1は最後尾の意
        public SqlQueryPath AppendLeaf(SqlQuerySlotId newNode) => new(RootId, [.. Ids, newNode.Value]);
        public SqlQueryPath AppendLeaf(params SqlQuerySlotId[] newNodes) {
            if (newNodes.Length == 0) return this;

            int[] values = new int[newNodes.Length];
            for (var i = 0; i < newNodes.Length; i++) {
                values[i] = newNodes[i].Value;
            }
            return new(RootId, [..Ids,..values]);
        }
        public SqlQueryPath AppendLeaf(SqlQueryPath newPath) => Leaf == newPath.RootId ? new(RootId, [.. Ids, .. newPath.Ids]) : throw new ArgumentException("末尾に直接接続できないpath");
        public SqlQueryPath AppendRoot(SqlQuerySlotId newNode) => new(newNode, [RootId.Value, .. Ids]);
        public SqlQueryPath AppendRoot(params SqlQuerySlotId[] newNodes) {
            if (newNodes.Length == 0) return this;

            int[] values = new int[newNodes.Length - 1];
            for (var i = 0; i < newNodes.Length - 1; i++){
                values[i] = newNodes[i + 1].Value;
            }
            return new(newNodes[0], [.. values, RootId.Value, .. Ids]);
        }
        public SqlQueryPath AppendRoot(SqlQueryPath newPath) => RootId == newPath.Leaf ? new(newPath.RootId, [.. newPath.Ids, .. Ids]) : throw new ArgumentException("先頭に直接接続できないpath");
        public SqlQueryPath RemoveLeaf(int count = 1){
            if (count <= 0 || Ids.Length < count) throw new IndexOutOfRangeException($"Invalid count: {count}");
            // 範囲演算子 [..^n] は「最初から、末尾からn個手前まで」を指す
            return new(RootId, Ids[..^count]);
        }
        public SqlQueryPath RemoveRoot(int count = 1){
            if (count <= 0 || Ids.Length < count) throw new IndexOutOfRangeException($"Invalid count: {count}");
            // 範囲演算子 [n..] は「n個目から最後まで」を指す
            return new(new(Ids[count - 1]), Ids[count..]);
        }

        /// <summary>
        /// 共通するノードの個数を返すヘルパーメソッド
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <param name="other"></param>
        /// <returns>Rootすら違えばnull、Rootのみなら0、と数える。</returns>
        private int? CountSharedPath(SqlQueryPath other){
            if (RootId != other.RootId) return null;
            int ln = Math.Min(Ids.Length, other.Ids.Length);
            int count = 0;
            for (int i = 0; i < ln; i++){
                if (Ids[i] != other.Ids[i]) break;
                count++;
            }
            return count;
        }
        /// <summary>
        /// 最後の共通パスまでのパスを返す
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public SqlQueryPath? GetSharedPath(SqlQueryPath other){
            int? sharedCount = CountSharedPath(other);
            return sharedCount.HasValue ? new(RootId, Ids[..sharedCount.Value]) : null;
        }
        /// <summary>
        /// 最後の共通パスを起点とした相対パスを返す
        /// </summary>
        /// <remarks>
        /// 自身が(A)-B-Cでotherが(A)-B-Dなら、(B)-Cを返す。
        /// 一つも共通していなければnull。
        /// </remarks>
        /// <param name="other"></param>
        /// <returns></returns>
        public SqlQueryPath? GetRelativePath(SqlQueryPath other, out SqlQueryPath? otherRelativePath){
            int? sharedCount = CountSharedPath(other);
            if (!sharedCount.HasValue){
                otherRelativePath = null;
                return null;
            } else if (sharedCount.Value == 0){
                otherRelativePath = other;
                return this;
            }
            otherRelativePath = other.RemoveRoot(sharedCount.Value);
            return RemoveRoot(sharedCount.Value);
        }
        public SqlQueryPath? GetMergedPath(SqlQueryPath child, out SqlQueryPath? anotherRelativePath) {
            if (RootId == child.RootId) {
                anotherRelativePath = this;
                return child;
            }//基点共有時も一応出力する。マージはしてない気がするが。

            anotherRelativePath = default;
            
            //childが自分のどこかから始まっていることを確認する。
            int? jointPos = null;
            for (var i = 0; i < Length; i++) {
                if (Ids[i] == child.RootId.Value) {
                    jointPos = i;
                    break;
                }
            }
            if (jointPos is not int p) return null;

            anotherRelativePath = new(new(Ids[p]), [.. Ids[(p + 1)..]]);
            return new(RootId, [..Ids[..(p + 1)], ..child.Ids]);
        }
    }
    public enum SqlQueryPathResolve{
        /// <summary> パスが無効、またはツリー構造と一致しないため処理不可 </summary>
        Invalid,
        /// <summary> 自身（ルート）に対する操作で確定 </summary>
        Self,
        /// <summary> 子要素へ掘り進んで検証を続行 </summary>
        Descend
    }
    /// <summary>
    /// SqlQueryPointerのメソッドの実行結果を表すenum
    /// </summary>
    /// <remarks>
    /// 正常変動、
    /// 正常無変動、
    /// Pathが途中で変化したために停止、
    /// 対象IDのスロットに到達できなくなったために無効化、
    /// の4種
    /// </remarks>
    public enum SqlQueryPointerResult {
        /// <summary>
        /// 正常に変動完了
        /// </summary>
        SuccessToChange,
        /// <summary>
        /// 正常に変動なしで完了
        /// </summary>
        SuccessNotToChange,
        /// <summary>
        /// 処理の途中でPath参照が外因で変動したため停止
        /// </summary>
        PathChanged,
        /// <summary>
        /// Pointer対象以外の部分でツリーが変動したことによる停止
        /// </summary>
        StructureChanged,
        /// <summary>
        /// Pointerの対象IDに到達できなくなったため無効化
        /// </summary>
        TargetVanished
    }

    #endregion

    /// <summary>
    /// 可変な組み立て中のクエリを表すクラス。
    /// </summary>
    /// <remarks>
    /// 個々のエレメントもしくは枝に対する処理はPointer側に回す。
    /// </remarks>
    public sealed class SqlQueryDraft{
        private SqlQuerySlotField _root; // 不変レコードツリーの根本。
        internal SqlQuerySlotField Root => Volatile.Read(ref _root); //クローンせずに参照をもらうためのプロパティ。
        //Volatile.Readは、マルチスレッド最適化の下でも最新の値を確実に読み取るためのメソッド。
        //これにより、他のスレッドが更新した_rootの値を正しく取得できるようになる。

        public SqlQueryDraft(SqlQuerySlotField root){
            _root = root;
        }
        // Pointer側の各種Commitから呼ばれる、ルート差し替え専用の単一窓口
        internal bool Apply(SqlQuerySlotField snapshot, SqlQuerySlotField newRoot){
            return ReferenceEquals(
                Interlocked.CompareExchange(ref _root, newRoot, snapshot),
                snapshot
            );
        }
        #region 各種のpointer生成
        // Pointerの供給元としてのファクトリ
        public SqlQueryPointer PickByPath(SqlQueryPath path){
            return new SqlQueryPointer(this, path);
        }
        public SqlQueryPointer PickById(SqlQuerySlotId id){
            return new SqlQueryPointer(this, this._root.GetPathById(id, out _));
        }
        public SqlQueryPointer PickBySelector(SqlQuerySelector selector){
            return new SqlQueryPointer(this, this._root.GetPathBySelector(selector, out _));
        }

        #endregion

    }
    /// <summary>
    /// SqlQueryDraft内の特定のノードを指し示すためのクラス。ノードへの安全なアクセスと更新操作を提供する。
    /// </summary>
    /// <remarks>
    /// 無効なPointerの生成も許容する。
    /// </remarks>
    public class SqlQueryPointer{
        private readonly SqlQueryDraft _draft;
        private SqlQueryPath? _path;
        public SqlQueryPath? Path { get { Reload(out var p); return p; } }
        public SqlQuerySlotId? Id => Volatile.Read(ref _path)?.Leaf;//_pathは変動するため、最新のキャッシュ値を保証する
        public SqlQuerySlotField? Target => Reload(out _);
        public bool IsDisposed => Volatile.Read(ref _path) == null;//_pathは変動するため、最新のキャッシュ値を保証する
        public SqlQueryPointer(SqlQueryDraft draft, SqlQueryPath? path){
            _draft = draft;
            _path = path;
            if (path != null && draft.Root.GetSlotField(path) is null) _path = null;
        }
        private SqlQuerySlotField EnsureValidSlot(out SqlQueryPath targetPath){
            if (Reload(out var path) is not SqlQuerySlotField slot) throw new InvalidOperationException("このPointerは無効です。");
            targetPath = path!;
            return slot;
        }
        /// <summary>
        /// _pathをスレッドセーフに更新する。
        /// </summary>
        /// <remarks>
        /// ついでに更新後のスロットとパスを返す。
        /// </remarks>
        /// <returns></returns>
        private SqlQuerySlotField? Reload(out SqlQueryPath? targetPath){
            //EnsureValid();//無効なPointerも存在までは許容するので、ここでは例外を投げない。
            SqlQuerySlotField? targetSlot;
            while (true) {
                //スナップショットの確保
                var currentRoot = _draft.Root;
                var currentPath = Volatile.Read(ref _path);
                if (currentPath == null){
                    targetPath = null;
                    return null;
                }
                targetPath = currentPath;

                if (currentRoot.GetSlotField(currentPath) is SqlQuerySlotField e){
                    //Pathの有効確認
                    targetSlot = e;
                } else if (currentRoot.GetPathById(currentPath.Leaf, out var target) is SqlQueryPath p){
                    //Pathが無効でもIDは有効かもしれない
                    targetPath = p;
                    targetSlot = target;
                } else {
                    if (Interlocked.CompareExchange(ref _path, null, currentPath) == currentPath){
                        //無効と確定したら無効化して終了
                        return null;
                    } else {
                        continue;
                    }
                }

                if (_draft.Root == currentRoot && Interlocked.CompareExchange(ref _path, targetPath, currentPath) == currentPath) break;
            }
            return targetSlot;
        }

        #region 対象スロットのメソッドを使用する中継メソッド
        public SqlBuiltQueryFragment Build(SqlBuildContext context) => EnsureValidSlot(out _).Build(context);
        public SqlDebugBuiltQuery DebugBuild(SqlDebugBuildContext context) => EnsureValidSlot(out _).DebugBuild(context);
        #endregion


        #region 対象スロットを変更してDraftツリー全体を更新するCommitメソッド

        // --- 更改処理のマルチスレッド完全安全な実装例 ---
        /// <summary>
        /// 対象スロットの無条件置換
        /// </summary>
        /// <param name="newNode"></param>
        /// <returns></returns>
        public SqlQueryPointerResult CommitReplace(SqlQuerySlotState newNode){
            // 1. 先頭でReloadし、このスレッドが確認した時点の「初期パス」を確実に固定する
            var targetSlot = Reload(out var targetPath);
            if (targetSlot is null || targetPath is null) return SqlQueryPointerResult.TargetVanished;
            
            while (true){
                //スナップショットを取得（この瞬間のツリー状態で世界を固定）
                var currentRoot = _draft.Root;
                // 2. ループの先頭で、Reload完了時からポインタが動かされていないか検証
                var currentPath = Volatile.Read(ref _path);
                if (currentPath != targetPath) return SqlQueryPointerResult.PathChanged;

                //スナップショットを基準に対象エレメントのパスを更新
                var path = currentPath;
                if (currentRoot.GetSlotField(currentPath) is not null ){
                    //元のpathで問題ない場合
                } else if (currentRoot.GetPathById(currentPath.Leaf, out _) is SqlQueryPath p){
                    //元のpathではないが該当IDのスロットが見つかった場合
                    path = p;
                } else {
                    //スナップショット内にポインタ対象のIDのスロットが存在しない場合
                    if (Interlocked.CompareExchange(ref _path, null, targetPath) == targetPath) {
                        return SqlQueryPointerResult.TargetVanished;
                    } else {
                        return SqlQueryPointerResult.PathChanged;
                    }
                }

                //必要に応じて置換先ノードを編集。

                var addedNode = newNode;

                //スナップショットを置換したツリーを作成。
                //スナップショットに対してPathが存在することは確定しているが、一応ヌルチェックはする。
                if (currentRoot.ReplaceRecursive(_draft, path, addedNode) is not SqlQuerySlotField newRoot) throw new InvalidOperationException("クエリツリーの更新が想定外の理由で失敗しました");


                //確定要求（CAS）
                //一旦、変更はしないがパスのチェックだけしておく。
                if (Volatile.Read(ref _path) != targetPath) return SqlQueryPointerResult.PathChanged;
                //その後、ツリーのチェック。
                if (_draft.Apply(currentRoot, newRoot)) return SqlQueryPointerResult.SuccessToChange;
                // 更改成功！
                // Applyが失敗した＝他スレッドが先に更改した。
                // ループで最初に戻り、新しいスナップショットからPathを検索し直す（自動追従）
            }
        }
        /// <summary>
        /// 対象スロットが空であるとき限定の置換
        /// </summary>
        /// <param name="newNode"></param>
        /// <param name="overwriteDefault">Defaultのスロットでも上書きするかどうか</param>
        /// <returns></returns>
        public SqlQueryPointerResult CommitFill(SqlQuerySlotState newNode, bool overwriteDefault){
            // 1. 先頭でReloadし、このスレッドが確認した時点の「初期パス」を確実に固定する
            var targetSlot = Reload(out var targetPath);
            if (targetSlot is null || targetPath is null) return SqlQueryPointerResult.TargetVanished;

            while (true){
                //スナップショットを取得（この瞬間のツリー状態で世界を固定）
                var currentRoot = _draft.Root;
                // 2. ループの先頭で、Reload完了時からポインタが動かされていないか検証
                var currentPath = Volatile.Read(ref _path);
                if (currentPath != targetPath) return SqlQueryPointerResult.PathChanged;

                //スナップショットを基準に対象エレメントのパスを更新
                var path = currentPath;
                SqlQuerySlotField target;
                if (currentRoot.GetSlotField(currentPath) is SqlQuerySlotField t1){
                    //元のpathで問題ない場合
                    target = t1;
                } else if (currentRoot.GetPathById(currentPath.Leaf, out var t2) is SqlQueryPath p) {
                    //元のpathではないが該当IDのスロットが見つかった場合
                    path = p;
                    target = t2!;
                } else{
                    //スナップショット内にポインタ対象のIDのスロットが存在しない場合
                    if (Interlocked.CompareExchange(ref _path, null, targetPath) == targetPath){
                        return SqlQueryPointerResult.TargetVanished;
                    } else {
                        return SqlQueryPointerResult.PathChanged;
                    }
                }

                //replaceと違って対象スロットが空でない場合もfalseで終了
                if (target.IsNormal) return SqlQueryPointerResult.SuccessNotToChange;
                if (!overwriteDefault && target.IsDefault) return SqlQueryPointerResult.SuccessNotToChange;

                //必要に応じて置換先ノードを編集。

                var addedNode = newNode;

                //スナップショットを置換したツリーを作成。
                //スナップショットに対してPathが存在することは確定しているが、一応ヌルチェックはする。
                if (currentRoot.ReplaceRecursive(_draft, path, addedNode) is not SqlQuerySlotField newRoot) throw new InvalidOperationException("クエリツリーの更新が想定外の理由で失敗しました");


                //確定要求（CAS）
                //一旦、変更はしないがパスのチェックだけしておく。
                if (Volatile.Read(ref _path) != targetPath) return SqlQueryPointerResult.PathChanged;
                //その後、ツリーのチェック。
                if (_draft.Apply(currentRoot, newRoot)) return SqlQueryPointerResult.SuccessToChange;
                // 更改成功！
                // Applyが失敗した＝他スレッドが先に更改した。
                // ループで最初に戻り、新しいスナップショットからPathを検索し直す（自動追従）
            }
        }
        public SqlQueryPointerResult CommitToDefault() => CommitToConst(SqlQuerySlotStateEnum.Default);
        public SqlQueryPointerResult CommitToEmpty() => CommitToConst(SqlQuerySlotStateEnum.Empty);
        public SqlQueryPointerResult CommitToUndefined() => CommitToConst(SqlQuerySlotStateEnum.Undefined);
        protected SqlQueryPointerResult CommitToConst(SqlQuerySlotStateEnum mode){
            // 1. 先頭でReloadし、このスレッドが確認した時点の「初期パス」を確実に固定する
            var targetSlot = Reload(out var targetPath);
            if (targetSlot is null || targetPath is null) return SqlQueryPointerResult.TargetVanished;

            while (true){
                //スナップショットを取得（この瞬間のツリー状態で世界を固定）
                var currentRoot = _draft.Root;
                // 2. ループの先頭で、Reload完了時からポインタが動かされていないか検証
                var currentPath = Volatile.Read(ref _path);
                if (currentPath != targetPath) return SqlQueryPointerResult.PathChanged;

                //スナップショットを基準に対象エレメントのパスを更新
                var path = currentPath;
                SqlQuerySlotField target;
                if (currentRoot.GetSlotField(currentPath) is SqlQuerySlotField t1){
                    //元のpathで問題ない場合
                    target = t1;
                } else if (currentRoot.GetPathById(currentPath.Leaf, out var t2) is SqlQueryPath p){
                    //元のpathではないが該当IDのスロットが見つかった場合
                    path = p;
                    target = t2!;
                } else {
                    //スナップショット内にポインタ対象のIDのスロットが存在しない場合
                    if (Interlocked.CompareExchange(ref _path, null, targetPath) == targetPath){
                        return SqlQueryPointerResult.TargetVanished;
                    } else {
                        return SqlQueryPointerResult.PathChanged;
                    }
                }

                //必要に応じて置換先ノードを編集。

                var addedNode = mode switch{
                    SqlQuerySlotStateEnum.Default => target.State.ToDefault(),
                    SqlQuerySlotStateEnum.Empty => target.State.ToEmpty(),
                    SqlQuerySlotStateEnum.Undefined => target.State.ToUndefined(),
                    _ => throw new ArgumentException("スロットを定数化する場合はDefault,Empty,Undefinedのいずれかを選んでください")
                };

                //スナップショットを置換したツリーを作成。
                //スナップショットに対してPathが存在することは確定しているが、一応ヌルチェックはする。
                if (currentRoot.ReplaceRecursive(_draft, path, addedNode) is not SqlQuerySlotField newRoot) throw new InvalidOperationException("クエリツリーの更新が想定外の理由で失敗しました");


                //確定要求（CAS）
                //一旦、変更はしないがパスのチェックだけしておく。
                if (Volatile.Read(ref _path) != targetPath) return SqlQueryPointerResult.PathChanged;
                //その後、ツリーのチェック。
                if (_draft.Apply(currentRoot, newRoot)) return SqlQueryPointerResult.SuccessToChange;
                // 更改成功！
                // Applyが失敗した＝他スレッドが先に更改した。
                // ループで最初に戻り、新しいスナップショットからPathを検索し直す（自動追従）
            }
        }

        /// <summary>
        /// ツリーの途中を削除する処理
        /// </summary>
        /// <param name="relativeChildPath">ポインター対象に、この相対パスの子を継ぎ変える</param>
        /// <returns></returns>
        public SqlQueryPointerResult CommitSkip(SqlQueryPath relativeChildPath){
            if(relativeChildPath.RootId != Id) throw new ArgumentException("継ぎ先の相対Pathが現在位置から繋がらない");

            // 1. 先頭でReloadし、このスレッドが確認した時点の「初期パス」を確実に固定する
            var targetSlot = Reload(out var targetPath);
            if (targetSlot is null || targetPath is null) return SqlQueryPointerResult.TargetVanished;

            while (true){
                //スナップショットを取得（この瞬間のツリー状態で世界を固定）
                var currentRoot = _draft.Root;
                // 2. ループの先頭で、Reload完了時からポインタが動かされていないか検証
                var currentPath = Volatile.Read(ref _path);
                if (currentPath != targetPath) return SqlQueryPointerResult.PathChanged;

                //スナップショットを基準に対象エレメントのパスを更新
                var path = currentPath;
                if (currentRoot.GetSlotField(currentPath) is SqlQuerySlotField){
                    //元のpathで問題ない場合
                } else if (currentRoot.GetPathById(currentPath.Leaf, out _) is SqlQueryPath p){
                    //元のpathではないが該当IDのスロットが見つかった場合
                    path = p;
                } else {
                    //スナップショット内にポインタ対象のIDのスロットが存在しない場合
                    if (Interlocked.CompareExchange(ref _path, null, targetPath) == targetPath){
                        return SqlQueryPointerResult.TargetVanished;
                    } else {
                        return SqlQueryPointerResult.PathChanged;
                    }
                }

                //スナップショットを基準に継ぎ先エレメントを取得
                SqlQuerySlotField child;
                if (path.GetMergedPath(relativeChildPath, out _) is not SqlQueryPath absoluteChildPath) throw new ArgumentException("継ぎ先の相対Pathが現在位置から繋がらない");

                if (currentRoot.GetSlotField(absoluteChildPath) is SqlQuerySlotField t1){
                    //元のpathで問題ない場合
                    child = t1;
                } else if (currentRoot.GetPathById(relativeChildPath.Leaf, out var t2) is not null){
                    //元のpathではないが該当IDのスロットが見つかった場合
                    child = t2!;
                } else {
                    //スナップショット内にポインタ対象のIDのスロットが存在しない場合
                    if (Volatile.Read(ref _path) == targetPath){
                        return SqlQueryPointerResult.StructureChanged;
                    } else {
                        return SqlQueryPointerResult.PathChanged;
                    }
                }

                //必要に応じて置換先ノードを編集。

                var addedNode = child.State;

                //スナップショットを置換したツリーを作成。
                //スナップショットに対してPathが存在することは確定しているが、一応ヌルチェックはする。
                if (currentRoot.ReplaceRecursive(_draft, path, addedNode) is not SqlQuerySlotField newRoot) throw new InvalidOperationException("クエリツリーの更新が想定外の理由で失敗しました");


                //確定要求（CAS）
                //一旦、変更はしないがパスのチェックだけしておく。
                if (Volatile.Read(ref _path) != targetPath) return SqlQueryPointerResult.PathChanged;
                //その後、ツリーのチェック。
                if (_draft.Apply(currentRoot, newRoot)) return SqlQueryPointerResult.SuccessToChange;
                // 更改成功！
                // Applyが失敗した＝他スレッドが先に更改した。
                // ループで最初に戻り、新しいスナップショットからPathを検索し直す（自動追従）
            }
        }
        /// <summary>
        /// ツリーの途中に構造を挿入する処理
        /// </summary>
        /// <remarks>
        /// 挿入部分は強制でクローン化される。
        /// </remarks>
        /// <param name="relativeChildPath">挿入構造のこの位置に、対象ノードを継ぎ変える</param>
        /// <returns></returns>
        public SqlQueryPointerResult CommitInsert(SqlQuerySlotField insertNodes, SqlQueryPath relativeChildPath){
            if (relativeChildPath.RootId != insertNodes.Id) throw new ArgumentException("継ぎ先位置の相対Pathが継ぎ先構造の根元から繋がらない");
            if (insertNodes.GetSelectorByPath(relativeChildPath, out _) is not SqlQuerySelector selector) throw new ArgumentException("継ぎ先位置の相対Pathに該当が無い");
            var insert = insertNodes.RecreateAlter(insertNodes.State.CloneNode(_draft));
            var childSelector = selector with {RootId = insert.Id };
            var targetChildPath = insert.GetPathBySelector(childSelector, out _)!;

            // 1. 先頭でReloadし、このスレッドが確認した時点の「初期パス」を確実に固定する
            var targetSlot = Reload(out var targetPath);
            if (targetSlot is null || targetPath is null) return SqlQueryPointerResult.TargetVanished;

            while (true){
                //スナップショットを取得（この瞬間のツリー状態で世界を固定）
                var currentRoot = _draft.Root;
                // 2. ループの先頭で、Reload完了時からポインタが動かされていないか検証
                var currentPath = Volatile.Read(ref _path);
                if (currentPath != targetPath) return SqlQueryPointerResult.PathChanged;

                //スナップショットを基準に対象エレメントのパスを更新
                var path = currentPath;
                SqlQuerySlotField target;
                if (currentRoot.GetSlotField(currentPath) is SqlQuerySlotField t1){
                    //元のpathで問題ない場合
                    target = t1;
                } else if (currentRoot.GetPathById(currentPath.Leaf, out var t2) is SqlQueryPath p){
                    //元のpathではないが該当IDのスロットが見つかった場合
                    path = p;
                    target = t2!;
                } else {
                    //スナップショット内にポインタ対象のIDのスロットが存在しない場合
                    if (Interlocked.CompareExchange(ref _path, null, targetPath) == targetPath){
                        return SqlQueryPointerResult.TargetVanished;
                    } else {
                        return SqlQueryPointerResult.PathChanged;
                    }
                }

                //必要に応じて置換先ノードを編集。

                var addedNode = insert.ReplaceRecursive(_draft, targetChildPath, target.State, false)!.State;

                //スナップショットを置換したツリーを作成。
                //スナップショットに対してPathが存在することは確定しているが、一応ヌルチェックはする。
                if (currentRoot.ReplaceRecursive(_draft, path, addedNode, false) is not SqlQuerySlotField newRoot) throw new InvalidOperationException("クエリツリーの更新が想定外の理由で失敗しました");


                //確定要求（CAS）
                //一旦、変更はしないがパスのチェックだけしておく。
                if (Volatile.Read(ref _path) != targetPath) return SqlQueryPointerResult.PathChanged;
                //その後、ツリーのチェック。
                if (_draft.Apply(currentRoot, newRoot)) return SqlQueryPointerResult.SuccessToChange;
                // 更改成功！
                // Applyが失敗した＝他スレッドが先に更改した。
                // ループで最初に戻り、新しいスナップショットからPathを検索し直す（自動追従）
            }
        }

        #endregion

        #region ポインタとしての上下左右の移動処理
        public bool MoveToParent() {
            if (Reload(out _) is null) return false;
            while (true){
                //スナップショットの確保
                var currentRoot = _draft.Root;
                var currentPath = Volatile.Read(ref _path);
                if (currentPath is null) return false;
                var targetPath = currentPath.RemoveLeaf();

                if (currentRoot.GetSlotField(targetPath) is not null){
                    //Pathの有効確認
                } else if (currentRoot.GetPathById(targetPath.Leaf, out _) is SqlQueryPath p){
                    //Pathが無効でもIDは有効かもしれない
                    targetPath = p;
                } else {
                    if (Interlocked.CompareExchange(ref _path, null, currentPath) == currentPath){
                        //無効と確定したら無効化して終了
                        return false;
                    } else {
                        continue;
                    }
                }

                if (_draft.Root == currentRoot
                    && Interlocked.CompareExchange(ref _path, targetPath, currentPath) == currentPath
                    && _draft.Root == currentRoot
                    ) return true;
            }
        }
        public bool MoveToPrevious() {
            if (Reload(out _) is null) return false;
            while (true) {
                //スナップショットの確保
                var currentRoot = _draft.Root;
                var currentPath = Volatile.Read(ref _path);
                if (currentPath is null) return false;
                if (currentPath.IsRoot) return false; // ルートなら次の兄弟はいない

                var parentPath = currentPath.RemoveLeaf();
                SqlQuerySlotField parent;

                if (currentRoot.GetSlotField(parentPath) is SqlQuerySlotField s1){
                    //Pathの有効確認
                    parent = s1;
                } else if (currentRoot.GetPathById(parentPath.Leaf, out var s2) is SqlQueryPath p){
                    //Pathが無効でもIDは有効かもしれない
                    parentPath = p;
                    parent = s2!;
                } else {
                    //無効確定なら無効化して終了
                    if (Interlocked.CompareExchange(ref _path, null, currentPath) == currentPath) return false;
                    continue;
                }
                if (!parent.IsNormal) {
                    //親スロットが定数スロットの場合も移動無し
                    return false;
                }

                //親が持つ子要素をリスト化＋自身の位置を取得
                List<SqlQuerySlotField> children = [];
                int? pos = null;
                int count = 0;
                foreach (var (_, child) in parent.IterateChildren()){
                    if (child.Id == currentPath.Leaf) pos = count;
                    children.Add(child);
                    count++;
                }
                SqlQueryPath targetPath;
                if (pos is null){//自身が居なければ無効化
                    if (Interlocked.CompareExchange(ref _path, null, currentPath) == currentPath) return false;
                    continue;
                } else if (pos == 0){//上の兄弟が存在しないなら自身のまま
                    targetPath = currentPath;
                } else {
                    targetPath = parentPath.AppendLeaf(children[pos.Value - 1].Id);
                }

                if (_draft.Root == currentRoot
                    && Interlocked.CompareExchange(ref _path, targetPath, currentPath) == currentPath
                    && _draft.Root == currentRoot
                    ) return pos != 0;// 移動が発生したか

            }
        }
        public bool MoveToNext(){
            if (Reload(out _) is null) return false;
            while (true){
                //スナップショットの確保
                var currentRoot = _draft.Root;
                var currentPath = Volatile.Read(ref _path);
                if (currentPath is null) return false;
                if (currentPath.IsRoot) return false; // ルートなら次の兄弟はいない

                var parentPath = currentPath.RemoveLeaf();
                SqlQuerySlotField parent;

                if (currentRoot.GetSlotField(parentPath) is SqlQuerySlotField s1){
                    //Pathの有効確認
                    parent = s1;
                } else if (currentRoot.GetPathById(parentPath.Leaf, out var s2) is SqlQueryPath p){
                    //Pathが無効でもIDは有効かもしれない
                    parentPath = p;
                    parent = s2!;
                } else {
                    //無効確定なら無効化して終了
                    if (Interlocked.CompareExchange(ref _path, null, currentPath) == currentPath) return false;
                    continue;
                }
                if (!parent.IsNormal){
                    //親スロットが定数スロットの場合も移動無し
                    return false;
                }

                //親が持つ子要素をリスト化＋自身の位置を取得
                List<SqlQuerySlotField> children = [];
                int? pos = null;
                int count = 0;
                foreach (var (_, child) in parent.IterateChildren()){
                    if (child.Id == currentPath.Leaf) pos = count;
                    children.Add(child);
                    count++;
                }
                SqlQueryPath targetPath;
                if (pos is null){//自身が居なければ無効化
                    if (Interlocked.CompareExchange(ref _path, null, currentPath) == currentPath) return false;
                    continue;
                } else if (pos == children.Count - 1) {//下の兄弟が存在しないなら自身のまま
                    targetPath = currentPath;
                } else {
                    targetPath = parentPath.AppendLeaf(children[pos.Value + 1].Id);
                }

                if (_draft.Root == currentRoot
                    && Interlocked.CompareExchange(ref _path, targetPath, currentPath) == currentPath
                    && _draft.Root == currentRoot
                    ) return pos != children.Count - 1;// 移動が発生したか

            }
        }
        public bool MoveToChildById(SqlQuerySlotId id){
            if (Reload(out _) is null) return false;
            while (true){
                //スナップショットの確保
                var currentRoot = _draft.Root;
                var currentPath = Volatile.Read(ref _path);
                if (currentPath is null) return false;
                var targetPath = currentPath;
                SqlQuerySlotField target;
                SqlQueryPath childPath;

                if (currentRoot.GetSlotField(targetPath) is SqlQuerySlotField s1){
                    //Pathの有効確認
                    target = s1;
                } else if (currentRoot.GetPathById(targetPath.Leaf, out var s2) is SqlQueryPath p){
                    //Pathが無効でもIDは有効かもしれない
                    targetPath = p;
                    target = s2!;
                } else {
                    //無効確定なら無効化して終了
                    if (Interlocked.CompareExchange(ref _path, null, currentPath) == currentPath) return false;
                    continue;
                }
                if (!target.IsNormal || !target.HasChild) {
                    //子を持たないなら移動しない
                    return false;
                }
                if (target.GetChildById(id) is not SqlQuerySlotField child) return false;
                childPath = targetPath.AppendLeaf(child.Id);

                if (_draft.Root == currentRoot
                    && Interlocked.CompareExchange(ref _path, childPath, currentPath) == currentPath
                    && _draft.Root == currentRoot
                    ) return true;
            }
        }
        public bool MoveToChildByRole(SqlQueryElementRole role){
            if (Reload(out _) is null) return false;
            while (true){
                //スナップショットの確保
                var currentRoot = _draft.Root;
                var currentPath = Volatile.Read(ref _path);
                if (currentPath is null) return false;
                var targetPath = currentPath;
                SqlQuerySlotField target;
                SqlQueryPath childPath;

                if (currentRoot.GetSlotField(targetPath) is SqlQuerySlotField s1){
                    //Pathの有効確認
                    target = s1;
                } else if (currentRoot.GetPathById(targetPath.Leaf, out var s2) is SqlQueryPath p){
                    //Pathが無効でもIDは有効かもしれない
                    targetPath = p;
                    target = s2!;
                } else {
                    //無効確定なら無効化して終了
                    if (Interlocked.CompareExchange(ref _path, null, currentPath) == currentPath) return false;
                    continue;
                }
                if (!target.IsNormal || !target.HasChild){
                    //子を持たないなら移動しない
                    return false;
                }
                if (target.GetChildByRole(role) is not SqlQuerySlotField child) return false;
                childPath = targetPath.AppendLeaf(child.Id);

                if (_draft.Root == currentRoot
                    && Interlocked.CompareExchange(ref _path, childPath, currentPath) == currentPath
                    && _draft.Root == currentRoot
                    ) return true;
            }
        }
        #endregion

        #region 離れた位置へのポインタの移動
        public bool MoveByAbsolutePath(SqlQueryPath path) {
            if (_draft.Root.GetSlotField(path) is null) return false;
            Interlocked.Exchange(ref _path, path);
            return true;
        }
        public bool MoveByAbsoluteSelector(SqlQuerySelector selector) {
            if (_draft.Root.GetPathBySelector(selector, out _) is not SqlQueryPath p) return false;
            Interlocked.Exchange(ref _path, p);
            return true;
        }
        #endregion
    }
}



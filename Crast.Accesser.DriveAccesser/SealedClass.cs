
using Crast.Utilities.ExtensionMethods;
using System.ComponentModel.Design;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

/// <summary>
/// 主に引数用のクラスをまとめたファイル。
/// </summary>
/// <remarks>
/// sealed、static、enum辺りが該当。
/// 扱うdriveの種類によって修正が必要なクラスはDriveTypeファイルに分離する。
/// </remarks>
namespace Crast.Accesser.DriveAccesser{


    #region FileSystemPermissionと関連クラス
    //フォルダアクセス権限

    //アクセス種別
    [Flags]
    public enum FileSystemAccessLevel{
        None = 0,
        ReadOnly = 1 << 0,
        AppendOnly = 1 << 1,
        CreateOnly = 1 << 2,
        DeleteOnly = 1 << 3,
        WriteOnly = 1 << 4,
        All = (1 << 5) - 1,
        AppendCreate = AppendOnly | CreateOnly,
        WriteCreate = WriteOnly | CreateOnly,
        Writable = WriteOnly | AppendOnly,
        WritableCreate = WriteOnly | AppendOnly | CreateOnly,
        ReadDelete = ReadOnly | DeleteOnly,//中身を確認してから削除。カット&ペーストのときなど。
        ReadWrite = WriteOnly | ReadOnly,
        ReadWriteCreate = WriteOnly | ReadOnly | CreateOnly,
        ReadWritable = WriteOnly | AppendOnly | ReadOnly,
        NotDelete = All & ~DeleteOnly,
        NotCreate = All & ~CreateOnly,
        NotAppend = All & ~AppendOnly,//絶対に肥大化しない
        NotRead = All & ~ReadOnly,//流出してはならないフォルダだとありえる
    }

    //階層範囲
    public readonly record struct PermissionScope {
        public static readonly PermissionScope SelfOnly = new(0, 1);
        public static readonly PermissionScope ChildrenOnly = new(1, 2);
        public static readonly PermissionScope SelfAndChildren = new(0, 2);
        public static readonly PermissionScope AllWithSelf = new(0, int.MaxValue);
        public static readonly PermissionScope AllLower = new(1, int.MaxValue);
        public static readonly PermissionScope Empty = new(int.MaxValue - 1, int.MaxValue);
        private int Start { get; init; }
        private int End { get; init; }
        public bool IsEmpty => Start >= int.MaxValue - 1;
        public bool Include(int depth) => !IsEmpty && Start <= depth && depth < End;
        public bool Include(PermissionScope other) => !IsEmpty && !other.IsEmpty && Start <= other.Start && other.End <= End;
        public PermissionScope(int start, int end){
            if (start < 0) throw new ArgumentException($"PermissionScopeの開始深さは0以上でなければならない。{start}");
            if (end <= start) throw new ArgumentException($"PermissionScopeの終了深さは開始より大きくなければならない。{end}");
            Start = start;
            End = end;
        }
        /// <summary>
        /// スコープ二つを合成して一つのスコープにしようとする。
        /// </summary>
        /// <remarks>
        /// allowEmptyがfalseの場合、片方がEmptyなら合成失敗扱い。trueなら、片方がEmptyなら逆側をそのまま返す。
        /// </remarks>
        /// <param name="other"></param>
        /// <param name="merged"></param>
        /// <param name="allowEmpty">デフォルトでfalse、片方がEmptyであれば合成できないとみなす。</param>
        /// <returns></returns>
        public bool TryMerge(PermissionScope other, out PermissionScope merged, bool allowEmpty = false){
            if (allowEmpty) {
                if (IsEmpty) { merged = other; return true; }
                else if (other.IsEmpty) { merged = this; return true; }
            } else if (IsEmpty || other.IsEmpty) {
                merged = default; return false;
            }

            if (Start <= other.End + 1 && other.Start <= End + 1) {
                merged = new PermissionScope(Math.Min(Start, other.Start), Math.Max(End, other.End));
                return true;
            } else {
                merged = default;
                return false;
            }
        }
        /// <summary>
        /// 二つのスコープの共通部分を返す。
        /// </summary>
        /// <remarks>
        /// 共通部分が無い場合はEmptyを返す。
        /// </remarks>
        /// <param name="other"></param>
        /// <returns></returns>
        public PermissionScope Trim(PermissionScope other) {
            var start = Math.Max(Start, other.Start);
            var end = Math.Min(End, other.End);
            if (start < end) return new PermissionScope(start, end);
            else return Empty;
        }
        public PermissionScope Rebased(int depth) {
            if (IsEmpty) return Empty;
            if (End <= depth) return Empty;
            return new PermissionScope(Math.Max(0, Start - depth), End - depth);
        }
    }
    public enum PermissionScopeType {
        InformationScope,
        ItemCreateScope,
        FileAccessScope
    }



    //個別権限
    public sealed record FileSystemPermission{

        //基本のパラメータ

        public DriveTypeEnum DriveType { get; init; }
        public DriveItemPath Path { get; init; }
        public PermissionScope InformationScope { get; init; }
        public PermissionScope ItemCreateScope { get; init; }
        public PermissionScope FileAccessScope { get; init; }
        public FileSystemAccessLevel AccessLevel { get; init; }
        public FileSystemType FileType { get; init; }
        public bool CanCreate { get; init; }

        #region 簡易読み取り用のフィールド
        public bool IsDirectory { get; init; }
        public bool CanRead { get; init; }
        public bool CanAppend { get; init; }
        public bool CanDelete { get; init; }
        public bool CanWrite { get; init; }
        public bool CanNotAny { get; init; }
        public bool CanNotAccess { get; init; }
        public bool CanSingleAccess { get; init; }
        #endregion

        //コンストラクタ　正規の組み合わせかチェックする都合でプライマリコンストラクタではない
        public FileSystemPermission(
            DriveTypeEnum driveType,
            DriveItemPath path,
            PermissionScope informationScope,
            PermissionScope itemCreateScope,
            PermissionScope fileAccessScope,
            FileSystemAccessLevel accessLevel,
            FileSystemType fileType
            )
        {
            if (path.DriveType != driveType) throw new ArgumentException($"矛盾した許可型{path}  {driveType}");

            CanCreate = accessLevel.HasFlag(FileSystemAccessLevel.CreateOnly);//Create権限のみ、ファイルアクセス権限とは分けて処理する。
            AccessLevel = accessLevel & FileSystemAccessLevel.NotCreate;

            if (path is IDirectoryPath){
                //起点Pathはディレクトリだが、操作対象は配下のファイルのみ、と言う権限は普通にある。
                //if (fileType != FileSystemType.Directory) throw new ArgumentException($"矛盾した許可型{path}  {fileType}");
                if (!informationScope.Include(itemCreateScope)) throw new ArgumentException($"矛盾したスコープ{informationScope}  {itemCreateScope}");
                if (!itemCreateScope.Include(fileAccessScope)) throw new ArgumentException($"矛盾したスコープ{itemCreateScope}  {fileAccessScope}");
                if (!CanCreate) { itemCreateScope = fileAccessScope; }
            } else if (path is IFilePath){
                if (fileType == FileSystemType.Directory) throw new ArgumentException($"矛盾した許可型{path}  {fileType}");
                if (informationScope != PermissionScope.SelfOnly) throw new ArgumentException($"ファイル起点なら下部構造は見ない{fileAccessScope}");
                if (itemCreateScope != PermissionScope.SelfOnly) throw new ArgumentException($"ファイル起点なら下部構造は見ない{itemCreateScope}");
                if (fileAccessScope != PermissionScope.SelfOnly) throw new ArgumentException($"ファイル起点なら下部構造は見ない{fileAccessScope}");
            } else {
                throw new ArgumentException($"未定義のpath型{path}");
            }

            #region フィールド代入
            DriveType = driveType;
            Path = path;
            InformationScope = informationScope;
            ItemCreateScope = itemCreateScope;
            FileAccessScope = fileAccessScope;
            FileType = fileType;

            CanNotAccess = AccessLevel == FileSystemAccessLevel.None;
            CanSingleAccess = AccessLevel != FileSystemAccessLevel.None && (AccessLevel & (AccessLevel - 1)) == 0;//アクセスレベルが一つだけのときtrue。空権限はfalse。

            CanRead = AccessLevel.HasFlag(FileSystemAccessLevel.ReadOnly);
            CanAppend = AccessLevel.HasFlag(FileSystemAccessLevel.AppendOnly);
            CanDelete = AccessLevel.HasFlag(FileSystemAccessLevel.DeleteOnly);
            CanWrite = AccessLevel.HasFlag(FileSystemAccessLevel.WriteOnly);
            CanNotAny = AccessLevel == FileSystemAccessLevel.None;
            IsDirectory = Path is IDirectoryPath;
            #endregion
        }

        public bool Contains(FileSystemAccessLevel level, bool withCreate = true) => withCreate && CanCreate ? (AccessLevel | FileSystemAccessLevel.CreateOnly).HasFlag(level) : AccessLevel.HasFlag(level);
        public bool HasPartOf(FileSystemAccessLevel level, bool withCreate = true) => withCreate && CanCreate ? (AccessLevel | FileSystemAccessLevel.CreateOnly).InFlag(level) : AccessLevel.InFlag(level);
        public bool Contains(FileSystemType type) => FileType.HasFlag(type);
        public bool Contains(PermissionScope scope, PermissionScopeType type) {
            return type switch {
                PermissionScopeType.InformationScope => InformationScope.Include(scope),
                PermissionScopeType.ItemCreateScope => ItemCreateScope.Include(scope),
                PermissionScopeType.FileAccessScope => FileAccessScope.Include(scope),
                _ => throw new ArgumentException($"未定義のScopeType{type}")
            };
        }
        public async ValueTask<bool> IncludeScopes(FileSystemPermission other) {
            return await Path.GetDepth(other.Path) is int depth &&
                InformationScope.Rebased(depth).Include(other.InformationScope) &&
                ItemCreateScope.Rebased(depth).Include(other.ItemCreateScope) &&
                FileAccessScope.Rebased(depth).Include(other.FileAccessScope)
                ;
        }
        /// <summary>
        /// この個別権限型の対象範囲に、そのDriveItemInfoの対象が入っているかどうかを返す。
        /// </summary>
        /// <remarks>
        /// AccessLevelは問わない。空権限の可能性も否定はできない。
        /// </remarks>
        /// <param name="Info"></param>
        /// <returns></returns>
        public async ValueTask<bool> IncludeItem(DriveItemInfo Info, PermissionScopeType scopeType){
            return Info.DriveType == DriveType &&
                await this.IncludeItemPath(Info.Path, scopeType) &&
                Contains(Info.FileType)
                ;
        }
        public async ValueTask<bool> IsPartOf(FileSystemPermission other){
            return DriveType == other.DriveType &&
                other.Contains(AccessLevel) &&
                (!other.CanCreate || CanCreate) &&
                other.Contains(FileType) &&
                await IncludeScopes(other)
                ;
        }
        public async ValueTask<bool> IsPartOf(Dictionary<string, FileSystemPermission> others){
            foreach (var (_, p) in others){
                if (await IsPartOf(p)) return true;
            }
            return false;
        }

        /// <summary>
        /// 起点パスを引数パスに置き換える
        /// </summary>
        /// <remarks>
        /// 引数パスが自身の起点パス以下でなければnull。
        /// 以下であれば、起点パスを置き換えてscopeもrebasedにする。
        /// </remarks>
        /// <param name="path"></param>
        /// <returns></returns>
        public async ValueTask<FileSystemPermission?> Rebased(DriveItemPath path) {
            if (await Path.GetDepth(path) is int depth) {
                var informationScope = InformationScope.Rebased(depth);
                var itemCreateScope = ItemCreateScope.Rebased(depth);
                var fileAccessScope = FileAccessScope.Rebased(depth);
                
                //スコープが空なら対応する権限も消しておく
                var accessLevel = CanCreate && !itemCreateScope.IsEmpty ? AccessLevel | FileSystemAccessLevel.CreateOnly : AccessLevel;
                if (fileAccessScope.IsEmpty) accessLevel = FileSystemAccessLevel.None;

                return new FileSystemPermission(
                    driveType : DriveType,
                    path : path,
                    informationScope : informationScope,
                    itemCreateScope : itemCreateScope,
                    fileAccessScope : fileAccessScope,
                    accessLevel : accessLevel,
                    fileType : FileType
                    );
            } else {
                return null;
            }
        }
        /// <summary>
        /// 全てのスコープを、引数パス起点の引数スコープ範囲内に限定する。
        /// </summary>
        /// <remarks>
        /// 引数パスが自身の起点パス以上でなければnull。
        /// 以上であれば、起点パスはそのままでスコープのみtrimする。
        /// </remarks>
        /// <param name="path"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        public async ValueTask<FileSystemPermission?> Trim(DriveItemPath path, PermissionScope scope) {
            if (await path.GetDepth(Path) is int depth){
                var targetScope = scope.Rebased(depth);
                var informationScope = InformationScope.Trim(targetScope);
                var itemCreateScope = ItemCreateScope.Trim(targetScope);
                var fileAccessScope = FileAccessScope.Trim(targetScope);

                //スコープが空なら対応する権限も消しておく
                var accessLevel = CanCreate && !itemCreateScope.IsEmpty ? AccessLevel | FileSystemAccessLevel.CreateOnly : AccessLevel;
                if (fileAccessScope.IsEmpty) accessLevel = FileSystemAccessLevel.None;

                return new FileSystemPermission(
                    driveType: DriveType,
                    path: Path,
                    informationScope: informationScope,
                    itemCreateScope: itemCreateScope,
                    fileAccessScope: fileAccessScope,
                    accessLevel: accessLevel,
                    fileType: FileType
                    );
            }else{
                return null;
            }

        }

        //合成可能か判定するのに邪魔になる不要な権限スコープを削除する処理
        public FileSystemPermission EraseAccessScope() {
            return new FileSystemPermission(
                driveType: DriveType,
                path: Path,
                informationScope: InformationScope,
                itemCreateScope: ItemCreateScope,
                fileAccessScope: PermissionScope.Empty,
                accessLevel: CanCreate ? FileSystemAccessLevel.CreateOnly : FileSystemAccessLevel.None,
                fileType: FileType
                );
        }
        public FileSystemPermission EraseCreateScope(){
            return new FileSystemPermission(
                driveType: DriveType,
                path: Path,
                informationScope: InformationScope,
                itemCreateScope: PermissionScope.Empty,
                fileAccessScope: PermissionScope.Empty,
                accessLevel: FileSystemAccessLevel.None,
                fileType: FileType
                );
        }

        #region 個別権限の合成処理
        /// <summary>
        /// Path、FileType、AccesssLevel(Create除く)が一致し、スコープが合成可能であれば、合成して返す
        /// </summary>
        /// <param name="Other"></param>
        /// <param name="merged"></param>
        /// <returns></returns>
        public bool TryMergeScope(FileSystemPermission other, out FileSystemPermission merged) {
            merged = other;
            if (Path != other.Path) return false;
            if (FileType != other.FileType) return false;
            if (AccessLevel != other.AccessLevel) return false;
            if (TryMergeInformationScope(other) is PermissionScope informationScope &&
                TryMergeItemCreateScope(other) is PermissionScope itemCreateScope &&
                TryMergeFileAcccessScope(other) is PermissionScope fileAccessScope                
                ) {
                merged = new FileSystemPermission(
                        driveType : DriveType,
                        path : Path,
                        informationScope : informationScope,
                        itemCreateScope : itemCreateScope == PermissionScope.Empty ? fileAccessScope : itemCreateScope,
                        fileAccessScope : fileAccessScope,
                        accessLevel : itemCreateScope == PermissionScope.Empty ? AccessLevel : AccessLevel | FileSystemAccessLevel.CreateOnly,
                        fileType : FileType
                    );
                return true;
            }
            return false;
        }
        public PermissionScope? TryMergeInformationScope(FileSystemPermission other) {
            if (InformationScope.TryMerge(other.InformationScope, out var scope)) return scope;
            else return null;
        }
        public PermissionScope? TryMergeItemCreateScope(FileSystemPermission other) {
            if (CanCreate) {
                if (!other.CanCreate) return ItemCreateScope;
            } else {
                if (other.CanCreate) return other.ItemCreateScope;
                else return PermissionScope.Empty;
            }
            if (ItemCreateScope.TryMerge(other.ItemCreateScope, out var merged)) return merged;
            else return null;
        }
        public PermissionScope? TryMergeFileAcccessScope(FileSystemPermission other) {
            if (FileAccessScope.TryMerge(other.FileAccessScope, out var scope)) return scope;
            else return null;
        }
        #endregion
    }
    //複合権限
    public sealed class FileSystemPermissionBundle{
        //アクセス権限の原板。ここに書かれていないアクセス権限のaccesserは生成できない。
        private static readonly Dictionary<string, FileSystemPermission> _root = new(){
            ["AbsoluteAccessTest"] = new FileSystemPermission(
                    DriveTypeEnum.LocalDrive,
                    (LocalFilePath)"D:\\AccesserTest",
                    PermissionScope.AllWithSelf,
                    PermissionScope.AllWithSelf,
                    PermissionScope.AllWithSelf,
                    FileSystemAccessLevel.All,
                    FileSystemType.All),
            ["RelativeAccessTest"] = new FileSystemPermission(
                    DriveTypeEnum.LocalDrive,
                    (LocalFilePath)"AccesserTest",
                    PermissionScope.AllWithSelf,
                    PermissionScope.AllWithSelf,
                    PermissionScope.AllWithSelf,
                    FileSystemAccessLevel.All,
                    FileSystemType.All),
        };
        private readonly Dictionary<string, FileSystemPermission> _permissions;
        public static FileSystemPermissionBundle Master =>new (_root);//検証不要なため、ファクトリメソッドを経由する必要が無い。
        public static FileSystemPermissionBundle AccessTestPermissionBundle
            => Master.GetPart(["AbsoluteAccessTest", "RelativeAccessTest"]);
        public static FileSystemPermissionBundle AbsoluteAccessTestPermission
            => Master.GetPart("AbsoluteAccessTest");
        public static FileSystemPermissionBundle RelativeAccessTestPermission
            => Master.GetPart("RelativeAccessTest");
        //コンストラクタはprivate指定。必要な権限はMasterのように対応するプロパティを作成してゲッターから配布する。
        //もしくは、アクセス権限小型化メソッドから生成する。
        //コンストラクタでは非同期処理を行えないため、検証が必要な場合はファクトリメソッドであるCreateを経由する。
        //でも、privateだとどうせ検証不要じゃないか……？
        private FileSystemPermissionBundle(Dictionary<string, FileSystemPermission> permissions){
            _permissions = permissions;
        }
        private static async ValueTask<FileSystemPermissionBundle> Create(Dictionary<string, FileSystemPermission> permissions, FileSystemPermissionBundle? basePermissions = null) {
            var newPermission = new FileSystemPermissionBundle(permissions);
            var upperPermissions = basePermissions?._permissions ?? _root;
            if (await newPermission.IsPartOf(upperPermissions)) throw new UnauthorizedAccessException($"{permissions}は{upperPermissions}の範囲内ではない");
            return newPermission;
        }

        //権限チェック
        //空権限は全ての権限に所属する扱い。

        private async ValueTask<bool> IsPartOf(Dictionary<string, FileSystemPermission> others){
            foreach (var (_, p) in _permissions){
                if (! await p.IsPartOf(others)) return false;
            }
            return true;
        }
        public async ValueTask<bool> Contains(FileSystemPermission permission){
            foreach (var (_, p) in _permissions){
                if (await permission.IsPartOf(p)) return true;
            }
            return false;
        }
        public async ValueTask<bool> Contains(FileSystemPermissionBundle permissions){
            foreach (var (_, p) in permissions._permissions){
                if (!await Contains(p)) return false;
            }
            return true;
        }
        public bool IsEmpty => _permissions.Count == 0;
        public bool IsSingle => _permissions.Count == 1;
        public FileSystemPermission AsSinglePermission(bool singleOnly = false){
            if (!singleOnly || IsSingle){
                return _permissions.First().Value;
            }else{
                throw new ArgumentException($"このアクセス権限は個別権限ではない。{this}");
            }
        }

        //アクセス権限を小さくして作り直す処理
        //空権限の存在は基本的には容認する。容認できないときはallowEmpty=falseで例外を投げる。

        #region _permissionsから特定の条件に合う要素のみ残して作り直すGetPart系列のメソッド
        /// <summary>
        /// _rootの名称で単一取り出し
        /// </summary>
        /// <remarks>
        /// 複合権限型のPermissionsの要素を減らして作り直す処理。Getpart一つで、
        /// ・_rootの名称で単一取り出し、
        /// ・_rootの名称で複数取り出し、
        /// ・DriveTypeで複数取り出し、
        /// ・DriveItemPathを含むものを複数取り出し、
        /// ・AccessLevelで以上か以下を複数取り出し、
        /// 　に対応する。
        /// 個々の個別権限を小さく作り変えるのは、Narrow系列のメソッド。
        /// </remarks>
        /// <param name="key"></param>
        /// <param name="allowEmpty"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public FileSystemPermissionBundle GetPart(string key, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            if (_permissions.TryGetValue(key, out var p)) dict[key] = p;
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException($"{key}に該当する権限が存在しない");
            return new(dict);
        }
        /// <summary>
        /// _rootの名称で複数取り出し
        /// </summary>
        /// <inheritdoc cref="GetPart(string, bool))" path="/remarks"></inheritdoc>
        /// <param name="keys"></param>
        /// <param name="allowEmpty"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public FileSystemPermissionBundle GetPart(IEnumerable<string> keys, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var key in keys){
                if (_permissions.TryGetValue(key, out var p)) dict[key] = p;
            }
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException(string.Join(", ", keys) + "に該当する権限がひとつも存在しない");
            return new(dict);
        }
        /// <summary>
        /// DriveTypeで複数取り出し
        /// </summary>
        /// <inheritdoc cref="GetPart(string, bool))" path="/remarks"></inheritdoc>
        /// <param name="type"></param>
        /// <param name="allowEmpty"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public FileSystemPermissionBundle GetPart(DriveTypeEnum type, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, p) in _permissions){
                if (p.DriveType == type) dict[key] = p;
            }
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException($"{type}に該当する権限が存在しない");
            return new(dict);
        }
        /// <summary>
        /// DriveItemPathで複数取り出し(同じフォルダに対しても拡張子別に権限が分かれていることはある)
        /// </summary>
        /// <inheritdoc cref="GetPart(string, bool))" path="/remarks"></inheritdoc>
        /// <param name="path"></param>
        /// <param name="allowEmpty"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public FileSystemPermissionBundle GetPart(DriveItemPath path, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, p) in _permissions){
                if (p.Path == path) dict[key] = p;
            }
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException($"{path}に該当する権限が存在しない");
            return new(dict);
        }
        /// <summary>
        /// AccessLevelで以上か以下を複数取り出し
        /// </summary>
        /// <inheritdoc cref="GetPart(string, bool))" path="/remarks"></inheritdoc>
        /// <param name="accessLevel"></param>
        /// <param name="isLower"></param>
        /// <param name="allowEmpty"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public FileSystemPermissionBundle GetPart(FileSystemAccessLevel accessLevel, bool isLower = true, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, p) in _permissions){
                if (isLower && p.HasPartOf(accessLevel)) dict[key] = p;
                else if (!isLower && p.Contains(accessLevel)) dict[key] = p;
            }
            if (!allowEmpty && dict.Count == 0){
                if (isLower) throw new ArgumentException($"{accessLevel}以下に該当する権限が存在しない");
                else throw new ArgumentException($"{accessLevel}以上に該当する権限が存在しない");
            }
            return new(dict);
        }
        #endregion

        //全てのAccessLevelを小さく変更する
        public FileSystemPermissionBundle NarrowAccessLevel(FileSystemAccessLevel accessLevel, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, p) in _permissions){
                var newLevel = p.AccessLevel & accessLevel;
                if (newLevel == FileSystemAccessLevel.None) {
                    dict[key] = new FileSystemPermission(
                        p.DriveType,
                        p.Path,
                        p.InformationScope,
                        PermissionScope.Empty,
                        PermissionScope.Empty,
                        FileSystemAccessLevel.None,
                        p.FileType
                        );
                } else if (newLevel == FileSystemAccessLevel.CreateOnly) {
                    dict[key] = new FileSystemPermission(
                        p.DriveType,
                        p.Path,
                        p.InformationScope,
                        p.ItemCreateScope,
                        PermissionScope.Empty,
                        FileSystemAccessLevel.CreateOnly,
                        p.FileType
                        );
                } else {
                    dict[key] = new FileSystemPermission(
                        p.DriveType,
                        p.Path,
                        p.InformationScope,
                        p.ItemCreateScope,
                        p.FileAccessScope,
                        newLevel,
                        p.FileType
                        );
                }
            }
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException($"{accessLevel}に該当する権限が存在しない");
            return new(dict);
        }
        //全ての権限の対応するFileSystemTypeを狭く変更する
        public FileSystemPermissionBundle NarrowFileSystemType(FileSystemType type, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, p) in _permissions){
                var newType = p.FileType & type;
                if (newType == FileSystemType.None) continue;
                dict[key] = p with { FileType = newType };
            }
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException($"{type}に該当する権限が存在しない");
            return new(dict);
        }
        //全ての権限の対応するDriveItemPathとscopeを狭く変更する
        public async ValueTask<FileSystemPermissionBundle> NarrowPathAsync(DriveItemPath path, PermissionScope? scope = null, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, permission) in _permissions){
                //対象パスの下に入るパスを起点とする権限も自動的に含まれる
                if (await permission.Rebased(path) is not FileSystemPermission rebased) continue;

                if (scope is null) {
                    if (!rebased.InformationScope.IsEmpty) dict[key] = rebased;
                } else {
                    if (await permission.Trim(path, scope.Value) is FileSystemPermission trimed && !trimed.InformationScope.IsEmpty) dict[key] = trimed;
                }
            }
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException($"{path}を含む権限が存在しない");
            return new(dict);
        }
        //Narrow系列全部をまとめて実行する
        public async ValueTask<FileSystemPermissionBundle> NarrowAsync(
            DriveItemPath? path = null,
            PermissionScope? scope = null,
            FileSystemType? fileType = null,
            FileSystemAccessLevel? accessLevel = null,
            bool allowEmpty = true
            ){
            FileSystemPermissionBundle result = this;
            if (path is DriveItemPath p) result = await result.NarrowPathAsync(p, scope, allowEmpty);
            if (fileType is FileSystemType t) result = result.NarrowFileSystemType(t, allowEmpty);
            if (accessLevel is FileSystemAccessLevel l) result = result.NarrowAccessLevel(l, allowEmpty);
            return result.MergeAccessLevel();
        }

        //小さくした結果、AccessLevel以外全て同じになったら、足して一つの権限に作り直す。
        public FileSystemPermissionBundle MergeAccessLevel(){
            var after = new Dictionary<string, FileSystemPermission>();
            foreach (var (k, p) in _permissions) after = MergeAccessLevel(after, k, p);
            return new(after);
        }
        //ループ内部の処理を、同名のprivateメソッドとして切り出してある。
        private static Dictionary<string, FileSystemPermission> MergeAccessLevel(Dictionary<string, FileSystemPermission> dict, string key, FileSystemPermission permission){
            bool merged = false;
            foreach (var (k, p) in dict){
                if (p.DriveType == permission.DriveType &&
                    p.Path == permission.Path &&
                    p.FileType == permission.FileType
                ){
                    if (p.FileAccessScope != PermissionScope.Empty &&
                        permission.FileAccessScope != PermissionScope.Empty &&
                        p.FileAccessScope != permission.FileAccessScope
                        ) {
                        continue;
                    }
                    permission.InformationScope.TryMerge(p.InformationScope, out var informationScope);
                    permission.ItemCreateScope.TryMerge(p.ItemCreateScope, out var itemCreateScope);
                    var accessLevel = p.AccessLevel | permission.AccessLevel;
                    if (p.CanCreate || permission.CanCreate) accessLevel |= FileSystemAccessLevel.CreateOnly;
                    dict[k] = new FileSystemPermission(
                        driveType : permission.DriveType,
                        path : permission.Path,
                        informationScope : informationScope,
                        itemCreateScope : itemCreateScope,
                        fileAccessScope : permission.FileAccessScope,
                        accessLevel : accessLevel,
                        fileType : permission.FileType
                        );

                    merged = true;
                }
            }
            if(!merged) dict[key] = permission;
            return dict;
        }
        public FileSystemPermissionBundle MergeFileType(){
            var after = new Dictionary<string, FileSystemPermission>();
            foreach (var (k, p) in _permissions) after = MergeFileType(after, k, p);
            return new(after);
        }
        private static Dictionary<string, FileSystemPermission> MergeFileType(Dictionary<string, FileSystemPermission> dict, string key, FileSystemPermission permission){
            bool merged = false;
            foreach (var (k, p) in dict){
                if (p.DriveType == permission.DriveType &&
                    p.Path == permission.Path &&
                    p.InformationScope == permission.InformationScope &&
                    p.ItemCreateScope == permission.ItemCreateScope &&
                    p.FileAccessScope == permission.FileAccessScope &&
                    p.AccessLevel == permission.AccessLevel 
                ){
                    dict[k] = new FileSystemPermission(
                        driveType: permission.DriveType,
                        path: permission.Path,
                        informationScope: permission.InformationScope,
                        itemCreateScope: permission.ItemCreateScope,
                        fileAccessScope: permission.FileAccessScope,
                        accessLevel: permission.AccessLevel,
                        fileType: permission.FileType | p.FileType
                        );

                    merged = true;
                }
            }
            if (!merged) dict[key] = permission;
            return dict;
        }

        /// <summary>
        /// TraficDriveManager用の、パス一つに対する権限を抽出する処理
        /// </summary>
        /// <remarks>
        /// スコープも(0,1)に限定する。
        /// </remarks>
        /// <param name="path"></param>
        /// <param name="fileType"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public async ValueTask<FileSystemPermissionBundle> ComposeToSinglePathAsync(DriveItemPath path, FileSystemType fileType, FileSystemAccessLevel level) {
            var p = await NarrowAsync(path, PermissionScope.SelfOnly, fileType, level);
            return p.MergeAccessLevel().MergeFileType().MergeAccessLevel().MergeFileType();
        }
        public async ValueTask<FileSystemPermissionBundle> ComposeToSingleDirectoryAsync(DriveItemPath path, FileSystemType fileType){
            var p = await NarrowAsync(path, PermissionScope.SelfAndChildren, fileType, FileSystemAccessLevel.CreateOnly);
            return p.MergeAccessLevel().MergeFileType().MergeAccessLevel().MergeFileType();
        }

    }

    #endregion


}

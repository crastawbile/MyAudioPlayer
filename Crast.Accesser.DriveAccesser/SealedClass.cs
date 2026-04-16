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
        ReadDelete = ReadOnly | DeleteOnly,//チェックしてから削除
        ReadWrite = WriteOnly | ReadOnly,
        ReadWriteCreate = WriteOnly | ReadOnly | CreateOnly,
        ReadWritable = WriteOnly | AppendOnly | ReadOnly,
        NotDelete = All - DeleteOnly,
        NotCreate = All - CreateOnly,
        NotAppend = All - AppendOnly,//絶対に肥大化しない
        NotRead = All - ReadOnly,//ログ等、流出してはならないフォルダだとありえる
    }

    //階層範囲
    [Flags]
    public enum PermissionScope{
        SelfOnly = 1 << 0,        // そのIDのアイテム自身
        ChildrenOnly = 1 << 1,    // 直下の子要素（1階層）
        Recursive = 1 << 2,       // 孫以降全て（API消費リスク高）
        AllWithSelf = (1 << 3) - 1,
        AllLower = ChildrenOnly | Recursive,
        SelfAndChildren = SelfOnly | ChildrenOnly,
    }
    
    //個別権限
    public sealed record FileSystemPermission{

        //基本のパラメータ

        public DriveTypeEnum DriveType { get; init; }
        public DriveItemPath Path { get; init; }
        public PermissionScope Scope { get; init; }
        public FileSystemAccessLevel AccessLevel { get; init; }
        public FileSystemType FileType { get; init; }

        //簡易読み取り用のプロパティ

        public bool IsDirectory { get; init; }
        public bool CanRead { get; init; }
        public bool CanAppend { get; init; }
        public bool CanCreate { get; init; }
        public bool CanDelete { get; init; }
        public bool CanWrite { get; init; }
        public bool CanNotAny { get; init; }

        //コンストラクタ　正規の組み合わせかチェックする都合でプライマリコンストラクタではない
        public FileSystemPermission(
            DriveTypeEnum driveType,
            DriveItemPath path,
            PermissionScope scope,
            FileSystemAccessLevel accessLevel,
            FileSystemType fileType
            )
        {
            if (path.DriveType != driveType) throw new ArgumentException($"矛盾した許可型{path}  {driveType}");

            if (path is IDirectoryPath){
                if (fileType != FileSystemType.Directory) throw new ArgumentException($"矛盾した許可型{path}  {fileType}");
            } else if (path is IFilePath){
                if (fileType == FileSystemType.Directory) throw new ArgumentException($"矛盾した許可型{path}  {fileType}");
                if ((scope | PermissionScope.SelfOnly) != scope) throw new ArgumentException($"矛盾した許可型{path}  {scope}");
                else scope = PermissionScope.SelfOnly;
            } else {
                throw new ArgumentException($"未定義のpath型{path}");
            }

            //プロパティ代入

            DriveType = driveType;
            Path = path;
            Scope = scope;
            AccessLevel = accessLevel;
            FileType = fileType;

            CanRead = (AccessLevel | FileSystemAccessLevel.ReadOnly) == AccessLevel;
            CanAppend = (AccessLevel | FileSystemAccessLevel.AppendOnly) == AccessLevel;
            CanCreate = (AccessLevel | FileSystemAccessLevel.CreateOnly) == AccessLevel;
            CanDelete = (AccessLevel | FileSystemAccessLevel.DeleteOnly) == AccessLevel;
            CanWrite = (AccessLevel | FileSystemAccessLevel.WriteOnly) == AccessLevel;
            CanNotAny = AccessLevel == FileSystemAccessLevel.None;
            IsDirectory = Path is IDirectoryPath;
        }

        public bool IncludeAccessLevel(FileSystemAccessLevel level) { return (AccessLevel | level) == AccessLevel; }        
        public bool IncludeScope(PermissionScope scope){return (Scope | scope) == Scope;}
        public bool IncludeFileSystemType(FileSystemType type){return (FileType | type) == FileType;}
        //public bool IncludeItemPath(DriveItemPath path); PermissionScopeReachHistoryの拡張メソッドで実装

        /// <summary>
        /// この個別権限型の対象範囲に、そのDriveItemInfoの対象が入っているかどうかを返す。
        /// </summary>
        /// <remarks>
        /// AccessLevelは問わない。空権限の可能性も否定はできない。
        /// </remarks>
        /// <param name="Info"></param>
        /// <returns></returns>
        public bool IsItemAllowed(DriveItemInfo Info){
            if (Info.DriveType != DriveType) return false;
            if (!this.IncludeItemPath(Info.Path)) return false;
            if (!IncludeFileSystemType(Info.FileType)) return false;
            return true;
        }
        public bool IsPartOf(FileSystemPermission other){
            if (DriveType != other.DriveType) return false;
            if (!other.IncludeItemPath(Path)) return false;
            if (!other.IncludeAccessLevel(AccessLevel)) return false;
            if (!other.IncludeFileSystemType(FileType)) return false;
            return true;
        }
        public bool IsPartOf(Dictionary<string, FileSystemPermission> others){
            foreach (var (_, p) in others){
                if (IsPartOf(p)) return true;
            }
            return false;
        }
    }
    //複合権限
    public sealed class FileSystemPermissionBundle{
        //アクセス権限の原板。ここに書かれていないアクセス権限のaccesserは生成できない。
        private static readonly Dictionary<string, FileSystemPermission> _root = new(){
            ["AbsoluteAccessTest"] = new FileSystemPermission(
                    DriveTypeEnum.LocalDrive,
                    (LocalFilePath)"D:\\AccesserTest",
                    PermissionScope.AllWithSelf,
                    FileSystemAccessLevel.All,
                    FileSystemType.All),
            ["RelativeAccessTest"] = new FileSystemPermission(
                    DriveTypeEnum.LocalDrive,
                    (LocalFilePath)"AccesserTest",
                    PermissionScope.AllWithSelf,
                    FileSystemAccessLevel.All,
                    FileSystemType.All),
        };
        private readonly Dictionary<string, FileSystemPermission> _permissions;
        public static FileSystemPermissionBundle Master => new(_root);
        public static FileSystemPermissionBundle AccessTestPermissionBundle
            => Master.GetPart(["AbsoluteAccessTest", "RelativeAccessTest"]);
        public static FileSystemPermissionBundle AbsoluteAccessTestPermission
            => Master.GetPart("AbsoluteAccessTest");
        public static FileSystemPermissionBundle RelativeAccessTestPermission
            => Master.GetPart("RelativeAccessTest");
        //コンストラクタはprivate指定。必要な権限は対応するプロパティを作成してゲッターから配布する。
        //もしくは、アクセス権限小型化メソッドから生成する。
        private FileSystemPermissionBundle(Dictionary<string, FileSystemPermission> permissions, FileSystemPermissionBundle? basePermissions = null){
            _permissions = permissions;
            var upperPermissions = _root;
            if (basePermissions != null) { upperPermissions = basePermissions._permissions; }
            IsPartOf(upperPermissions);//権限外なら例外が出る
        }

        //権限チェック
        //空権限は全ての権限に所属する扱い。

        private bool IsPartOf(Dictionary<string, FileSystemPermission> others){
            foreach (var (_, p) in _permissions){
                if (!p.IsPartOf(others)) throw new ArgumentException($"このフォルダアクセス権限は許可されていない。{p}");
            }
            return true;
        }
        public bool Contains(FileSystemPermission permission){
            foreach (var (_, p) in _permissions){
                if (permission.IsPartOf(p)) return true;
            }
            return false;
        }
        public bool Contains(FileSystemPermissionBundle permissions){
            foreach (var (_, p) in permissions._permissions){
                if (!Contains(p)) return false;
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
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException(key + "に該当する権限が存在しない");
            return new FileSystemPermissionBundle(dict, this);
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
            return new FileSystemPermissionBundle(dict, this);
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
            return new FileSystemPermissionBundle(dict);
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
            return new FileSystemPermissionBundle(dict, this);
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
                if (isLower && (p.AccessLevel & accessLevel) == p.AccessLevel) dict[key] = p;
                else if (!isLower && (p.AccessLevel | accessLevel) == p.AccessLevel) dict[key] = p;
            }
            if (!allowEmpty && dict.Count == 0){
                if (isLower) throw new ArgumentException($"{accessLevel}以下に該当する権限が存在しない");
                else throw new ArgumentException($"{accessLevel}以上に該当する権限が存在しない");
            }
            return new FileSystemPermissionBundle(dict, this);
        }
        //全てのAccessLevelを小さく変更する
        public FileSystemPermissionBundle NarrowAccessLevel(FileSystemAccessLevel accessLevel, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, p) in _permissions){
                var newLevel = p.AccessLevel & accessLevel;
                if (newLevel == FileSystemAccessLevel.None) continue;
                dict[key] = p with { AccessLevel = newLevel };
            }
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException($"{accessLevel}に該当する権限が存在しない");
            return new FileSystemPermissionBundle(dict);
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
            return new FileSystemPermissionBundle(dict);
        }
        //全ての権限の対応するDriveItemPathを狭く変更する
        public FileSystemPermissionBundle NarrowPath(DriveItemPath path, bool allowEmpty = true){
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, p) in _permissions){
                if (!p.IncludeItemPath(path)) continue;
                dict[key] = p with { Path = path };
            }
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException($"{path}を含む権限が存在しない");
            return new FileSystemPermissionBundle(dict);
        }
        //Narrow系列全部をまとめて実行する
        public FileSystemPermissionBundle Narrow(DriveItemPath? path = null, FileSystemType? fileType = null, FileSystemAccessLevel? accessLevel = null, bool allowEmpty = true){
            FileSystemPermissionBundle result = this;
            if (path is DriveItemPath p) result = result.NarrowPath(p, allowEmpty);
            if (fileType is FileSystemType t) result = result.NarrowFileSystemType(t, allowEmpty);
            if (accessLevel is FileSystemAccessLevel l) result = result.NarrowAccessLevel(l, allowEmpty);
            return result.MergeAccessLevel();
        }

        //小さくした結果、AccessLevel以外全て同じになったら、足して一つの権限に作り直す。
        public FileSystemPermissionBundle MergeAccessLevel(){
            var after = new Dictionary<string, FileSystemPermission>();
            foreach (var (k, p) in _permissions) after = MergeAccessLevel(after, k, p);
            return new FileSystemPermissionBundle(after);
        }
        //ループ内部の処理を、同名のprivateメソッドとして切り出してある。
        private static Dictionary<string, FileSystemPermission> MergeAccessLevel(Dictionary<string, FileSystemPermission> dict, string key, FileSystemPermission permission){
            foreach (var (k, p) in dict){
                if (p.DriveType == permission.DriveType &&
                    p.Path == permission.Path &&
                    p.Scope == permission.Scope &&
                    p.FileType == permission.FileType
                ){
                    dict[k] = p with { AccessLevel = p.AccessLevel | permission.AccessLevel };
                }else{
                    dict[key] = permission;
                }
            }
            return dict;
        }
    }

    #endregion


}

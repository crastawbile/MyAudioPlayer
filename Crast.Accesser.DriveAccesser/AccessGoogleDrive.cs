using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;


namespace Crast.Accesser.DriveAccesser{

    //全体的にC#っぽくは無い気がするけど、まあPHP8で育った人間なので諦めれ。

    //もう面倒になってきたので、文字列型を可能な限り排除する。
    //ファイルタイプは、内部型で扱う。どうしても必要な時だけstringで生成して渡す。

    [AttributeUsage(AttributeTargets.Field)]
    public class FileSystemSubTypeAttribute : Attribute{
        public required string? LocalDrive { get; init; }
        public required string? GoogleDrive { get; init; }
    }

    /// <summary>
    /// フォルダを含む、ファイル種別を扱う内部型
    /// </summary>
    /// <remarks>
    /// 対応辞書を生成する際、名前が被る場合はより後のものが残る事には注意
    /// </remarks>
    public enum FileSystemType {
        // 各名前の前半で括ったものをFileSystemTypeManagerクラスの定数として
        // 自動で定義するため、カスタム属性だけでなく名付けルールも守れ。
        
        //権限なし
        None = 0,
        
        [FileSystemSubType(
            LocalDrive = null,
            GoogleDrive = "application/vnd.google-apps.folder"
        )]
        Directory = 1 << 0,

        [FileSystemSubType(
            LocalDrive = "",
            GoogleDrive = "application/octet-stream"
        )]
        NoExtension = 1 << 1,
        
        [FileSystemSubType(
            LocalDrive = ".txt",
            GoogleDrive = "text/plain"
        )]
        TextPlain = 1 << 2,
        
        [FileSystemSubType(
            LocalDrive = ".csv",
            GoogleDrive = "text/csv"
        )]
        TextCsv = 1 << 3,
        
        [FileSystemSubType(
            LocalDrive = ".png",
            GoogleDrive = "image/png"
        )]
        ImagePng = 1 << 4,
        
        [FileSystemSubType(
            LocalDrive = ".wav",
            GoogleDrive = "audio/wav"
        )]
        AudioWav = 1 << 5,
        
        [FileSystemSubType(
            LocalDrive = ".mp3",
            GoogleDrive = "audio/mpeg"
        )]
        AudioMp3 = 1 << 6,
        
        [FileSystemSubType(
            LocalDrive = ".json",
            GoogleDrive = "application/json"
        )]
        AppJson = 1 << 7,

        [FileSystemSubType(
            LocalDrive = ".dat",
            GoogleDrive = "application/octet-stream"
        )]
        AppDat = 1 << 8,

        [FileSystemSubType(
            LocalDrive = ".bin",
            GoogleDrive = "application/octet-stream"
        )]
        AppBin = 1 << 9,

        All = (1<< 10) - 1,

        // 再掲
        // 各名前の前半で括ったものをFileSystemTypeManagerクラスの定数として
        // 自動で定義するため、カスタム属性だけでなく名付けルールも守れ。
    }


    public static class FileSystemTypeManager{
        private static readonly Dictionary<string, FileSystemType> _FromExtension = [];
        private static readonly Dictionary<FileSystemType, string?> _ToExtension = [];
        private static readonly Dictionary<string, FileSystemType> _FromMimeType = [];
        private static readonly Dictionary<FileSystemType, string?> _ToMimeType = [];
        private static bool loaded = false;
        public static FileSystemType Text { get; private set; } = FileSystemType.None;
        public static FileSystemType Image { get; private set; } = FileSystemType.None;
        public static FileSystemType Audio { get; private set; } = FileSystemType.None;
        public static FileSystemType App { get; private set; } = FileSystemType.None;
        private static readonly object _lockObj = new();
        static FileSystemTypeManager(){
            LoadEnum();
        }
        /// <summary>
        /// FileSystemTypeの記述を基に、変換用のDictionaryを生成する。
        /// </summary>
        private static void LoadEnum() {
            if (loaded) return;
            lock (_lockObj) {
                var EnumType = typeof(FileSystemType);
                foreach (var f in EnumType.GetFields(BindingFlags.Public | BindingFlags.Static)){
                    var value = (FileSystemType)f.GetValue(null)!;
                    var valueInt = (int)value!;
                    if (valueInt == 0) continue;
                    if ((valueInt & (valueInt - 1)) != 0) { continue; }//個別フラグのみ拾うビットトリック
                    var attr = CustomAttributeExtensions.GetCustomAttribute<FileSystemSubTypeAttribute>(f) ?? null;
                    if (attr == null) continue;
                    var name = f.Name;

                    if (name.StartsWith("Text")) { Text |= value; }
                    else if (name.StartsWith("Image")) { Image |= value; }
                    else if (name.StartsWith("Audio")) { Audio |= value; }
                    else if (name.StartsWith("App")) { App |= value; }

                    var local = attr.LocalDrive!;
                    var google = attr.GoogleDrive!;

                    if (local != null) _FromExtension[local] = value;
                    _ToExtension[value] = local;
                    if (google != null) _FromMimeType[google] = value;
                    _ToMimeType[value] = google;
                }
                loaded = true;
            }
        }

        public static FileSystemType FromExtension(this string extension) {
            if (_FromExtension.TryGetValue(extension, out var type)) return type;
            throw new ArgumentException($"定義されていない拡張子{extension}");
        }
        public static string? ToExtension(this FileSystemType type) {
            return _ToExtension[type];
        }
        public static FileSystemType FromMimeType(this GoogleDriveMetadata metadata){
            if (_FromMimeType.TryGetValue(metadata.MimeType!, out var type)) return type; 
            throw new ArgumentException($"定義されていないMIMEタイプ{metadata.MimeType}");
        }
        public static FileSystemType FromMimeType(this string mimeType){
            if (_FromMimeType.TryGetValue(mimeType, out var type)) return type;
            throw new ArgumentException($"定義されていないMIMEタイプ{mimeType}");
        }
        public static string? ToMimeType(this FileSystemType type){
            return _ToMimeType[type];
        }
    }

    //パス文字列も型を付与する。
    public abstract record DriveItemPath{
        public abstract string Value { get; init; }
        public abstract DriveTypeEnum DriveType { get; }
        public virtual DriveItemPath? Parent { get; }
        //基本的にはパス文字列を扱ってファイル実体には触れないが、
        //DriveItemInfoを生成できるかどうかを確認するメソッドを
        //DriveItemInfoに実装するわけにもいかないので、こっちに置いておく。
        public abstract bool Exists(bool force = false);
    }

    public interface IFilePath { }
    public interface IDirectoryPath { }
    public abstract record LocalDrivePath : DriveItemPath {
        public override string Value { get; init; }
        public override DriveTypeEnum DriveType => DriveTypeEnum.LocalDrive;
        public LocalDrivePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is empty");
            // ここで絶対パスに強制変換
            Value = System.IO.Path.GetFullPath(path);
        }
        public string Name => System.IO.Path.GetFileName(Value);        
        public string NameOnly => System.IO.Path.GetFileNameWithoutExtension(Value);
        public override LocalDirectoryPath? Parent => ParentPath() == null ? null : (LocalDirectoryPath?)ParentPath()!;
        private string? ParentPath() => System.IO.Path.GetDirectoryName(Value); 


    }
    public abstract record GoogleDrivePath : DriveItemPath {
        public override string Value { get; init; }
        public override DriveTypeEnum DriveType => DriveTypeEnum.GoogleDrive;
        public GoogleDrivePath(string id){
            CheckId(id);
            Value = id;
        }
        public override GoogleDirectoryPath? Parent => this.InBank() ? (GoogleDirectoryPath?)this.FromBank().ParentId! : null;
        protected bool CheckId(string id) {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID cannot be empty");

            // 簡易バリデーション：Base64URLで使われない記号（/, \, ., @など）が含まれていないか
            // ファイルIDにドットやスラッシュは含まれません
            if (id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                throw new ArgumentException($"Invalid Google Drive ID format: {id}");

            return true;
        }
        public override bool Exists(bool force = false){
            if (this.InBank()) return true;
            if (!force) return false;
            var accsser = new GoogleDriveAccesser(FileSystemPermissionBundle.Master.NarrowPath(this), singleOnly = false);
            return accsser.ItemExists(this);
        }
    }

    public record LocalFilePath : LocalDrivePath, IFilePath{
        public static implicit operator LocalFilePath(string path) => new(path);
        public LocalFilePath(string path):base(path){}
        public override bool Exists(bool force = false) => System.IO.File.Exists(Value);
        public FileSystemType Extension => System.IO.Path.GetExtension(Value).FromExtension();
    }
    public record LocalDirectoryPath : LocalDrivePath, IDirectoryPath{
        public static implicit operator LocalDirectoryPath(string path) => new(path);
        public LocalDirectoryPath(string path):base(path){}
        public override bool Exists(bool force = false) => Directory.Exists(Value);
    }
    public record GoogleFilePath : GoogleDrivePath, IFilePath{
        public static implicit operator GoogleFilePath(string path) => new(path);
        public GoogleFilePath(string id):base(id){}
    }
    public record GoogleDirectoryPath : GoogleDrivePath, IDirectoryPath{
        public static implicit operator GoogleDirectoryPath(string path) => new(path);
        public GoogleDirectoryPath(string id):base(id){}
    }


    /// <summary>
    /// GoogleDriveのAPI消費を抑えるためのメタデータのキャッシュに使う型。
    /// </summary>
    public record GoogleDriveMetadata(
        GoogleDrivePath Id,
        string Name,
        FileSystemType Type,
        long? Size,
        GoogleDrivePath? ParentId = null,
        string? ETag = null
    ){
        public string? MimeType => Type.ToMimeType();
        public bool IsDirectory => Type == FileSystemType.Directory;
    }
    public static class GoogleDriveMetaDataBank {
        private static readonly Dictionary<GoogleDrivePath, GoogleDriveMetadata> B = [];
        public static void Add(this GoogleDriveMetadata metadata) {
            if(B.ContainsKey(metadata.Id)) throw new ArgumentException($"このIDは既に存在する{metadata}");
            B.Add(metadata.Id, metadata);
        }
        public static void Delete(this GoogleDriveMetadata metadata){
            if (!B.ContainsKey(metadata.Id)) throw new ArgumentException($"このIDは存在しない{metadata}");
            B.Remove(metadata.Id);
        }
        public static void Update(this GoogleDriveMetadata metadata){
            if (!B.ContainsKey(metadata.Id)) throw new ArgumentException($"このIDは存在しない{metadata}");
            B[metadata.Id] =metadata;
        }
        public static GoogleDriveMetadata? FromBank(this GoogleDrivePath path,bool force = false){
            if (B.TryGetValue(path, out var data)) return data;
            if (force) {
                var accsser = new GoogleDriveAccesser(FileSystemPermissionBundle.Master.NarrowPath(path), singleOnly = false);
                if(!accsser.ItemExists(path)) return null;
                data = accsser.Metadata;
                Add(data);
                return data;
            }
            throw new ArgumentException($"このIDはキャッシュに存在しない{path.Value}");
        }
        public static bool InBank(this GoogleDrivePath id) {
            return B.ContainsKey(id);
        }
    }

    /// <summary>
    /// ストレージの種類を問わず、ファイル情報を保持する共通クラス
    /// </summary>
    public record DriveItemInfo(
        DriveTypeEnum DriveType,
        DriveItemPath Path,
        string Name,
        FileSystemType FileType,
        bool IsDirectory,
        long? Size = null,
        DateTime? LastModified = null,
        GoogleDriveMetadata? Metadata = null // GoogleDrive の時だけセットされる
    )
    {
        public static DriveItemInfo From(GoogleDriveMetadata metadata){
            return new DriveItemInfo(
                    DriveType: DriveTypeEnum.GoogleDrive,
                    Path: metadata.Id,
                    Name: metadata.Name,
                    FileType: metadata.Type,
                    Size: metadata.Size,
                    IsDirectory: metadata.IsDirectory,
                    Metadata: metadata
                );
        }
        public static DriveItemInfo From(FileInfo info) {
            return new DriveItemInfo(
                DriveType: DriveTypeEnum.LocalDrive,
                Path: (LocalFilePath)info.FullName,
                Name: info.Name,
                FileType: info.Extension.FromExtension(),
                Size: info.Length,
                IsDirectory: false,
                LastModified: info.LastWriteTime
            );            
        }
        public static DriveItemInfo From(DirectoryInfo info){
            return new DriveItemInfo(
                DriveType: DriveTypeEnum.LocalDrive,
                Path: (LocalDirectoryPath)info.FullName,
                Name: info.Name,
                FileType: FileSystemType.Directory,
                Size: null,
                IsDirectory: true,
                LastModified: info.LastWriteTime
            );
        }
    }
    
    
    //フォルダアクセス権限

    //アクセス種別
    [Flags]
    public enum FileSystemAccessLevel {
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
    //ドライブの種類
    public enum DriveTypeEnum {
        LocalDrive,
        GoogleDrive,
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
    public static class PermissionScopeReachHistory {

        //アクセス検証を行わず即座に弾くパスのリスト。デバッグとかで使うかもしれない。
        private static readonly List<DriveItemPath> Forbidden = [];
        
        private static readonly Dictionary<FileSystemPermission, List<DriveItemPath>> Childrens = [];
        private static readonly Dictionary<FileSystemPermission, List<DriveItemPath>> GrandChildrens = [];

        private static List<DriveItemPath> GetChildrenList(FileSystemPermission p) => Childrens.TryGetValue(p, out var list) ? list : [];
        private static List<DriveItemPath> GetGrandChildrenList(FileSystemPermission p) => GrandChildrens.TryGetValue(p, out var list) ? list : [];

        /// <summary>
        /// permissionがpathを範囲内に含むかどうかを返す。
        /// </summary>
        /// <remarks>
        /// PermissionScopeReachHistoryで定義している拡張メソッド。
        /// PermissionScopeReachHistoryの到達履歴を利用するため。
        /// </remarks>
        /// <param name="permission"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IncludeItemPath(this FileSystemPermission permission, DriveItemPath path){
            if (path.DriveType != permission.DriveType) { return false; }
            var scope = permission.Scope;
            var basePath = permission.Path;

            //自身そのものなら、自身を含む権限かどうかに等しい。
            if (basePath == path) return (scope | PermissionScope.SelfOnly) == scope;
            //自身がfolderでなければ話は終わり
            if (permission.FileType != FileSystemType.Directory) return false;
            //以降、自身ではない場合
            //到達履歴のあるpathであれば、そのように答える
            if ((scope | PermissionScope.ChildrenOnly) == scope && GetChildrenList(permission).Contains(path)) return true;
            if ((scope | PermissionScope.Recursive) == scope && GetGrandChildrenList(permission).Contains(path)) return true;
            //禁止履歴にあるpathであれば、そのように答える
            if (Forbidden.Contains(path)) return false;
            //履歴になければ、実際に辿るしかない
            //対象の親ディレクトリに移動
            var currentPath = path.Parent;
            //対象の親ディレクトリを得られないならこれ以上検証できないので「含まない」
            if (currentPath == null) return false;
            //対象の親が自身なら、子を含む権限かどうかに等しい。
            if (basePath == currentPath) {
                if ((scope | PermissionScope.ChildrenOnly) == scope){
                    Childrens[permission].Add(path);
                    return true;
                } else {
                    return false;
                }
            }
            //以降、自身でも子でも無い場合
            //孫以降を含まない権限なら「含まない」
            if ((scope | PermissionScope.Recursive) != scope) return false;
            //孫以降を含むなら、recursiveに辿っていくしかない。
            currentPath = currentPath.Parent;
            while (currentPath != null){
                if (basePath == currentPath) {
                    GrandChildrens[permission].Add(path);
                    return true;
                }
                currentPath = currentPath.Parent;
            }
            //親を辿れなくなったら「含まない」
            return false;
        }
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
            ){
            if (path.DriveType != driveType) throw new ArgumentException($"矛盾した許可型{path}  {driveType}");

            if (path is IDirectoryPath) {
                if (fileType != FileSystemType.Directory) throw new ArgumentException($"矛盾した許可型{path}  {fileType}");
            } else if(path is IFilePath) {
                if(fileType == FileSystemType.Directory) throw new ArgumentException($"矛盾した許可型{path}  {fileType}");
                if ((scope | PermissionScope.SelfOnly) != scope) throw new ArgumentException($"矛盾した許可型{path}  {scope}");
                else scope = PermissionScope.SelfOnly;
            } else {
                throw new ArgumentException($"未定義のpath型{path}");
            }
            

            DriveType = driveType;
            Path = path;
            Scope = scope;
            AccessLevel = accessLevel;
            FileType = fileType;

            CanRead  = (AccessLevel | FileSystemAccessLevel.ReadOnly) == AccessLevel;
            CanAppend  = (AccessLevel | FileSystemAccessLevel.AppendOnly) == AccessLevel;
            CanCreate  = (AccessLevel | FileSystemAccessLevel.CreateOnly) == AccessLevel;
            CanDelete  = (AccessLevel | FileSystemAccessLevel.DeleteOnly) == AccessLevel;
            CanWrite  = (AccessLevel | FileSystemAccessLevel.WriteOnly) == AccessLevel;
            CanNotAny = AccessLevel == FileSystemAccessLevel.None;
            IsDirectory  = Path is IDirectoryPath;
            }

        public bool IncludeAccessLevel(FileSystemAccessLevel level) {
            return (AccessLevel | level) == AccessLevel;
        }
        public bool IncludeScope(PermissionScope scope){
            return (Scope | scope) == Scope;
        }
        public bool IncludeFileSystemType(FileSystemType type) {
            return (FileType | type) == FileType;
        }
        //PermissionScopeReachHistoryの拡張メソッドで実装
        //public bool IncludeItemPath(DriveItemPath path);

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
        public bool IsPartOf(FileSystemPermission other) {
            if (DriveType!=other.DriveType) return false;
            if (!other.IncludeItemPath(Path)) return false;
            if (!other.IncludeAccessLevel(AccessLevel)) return false;
            if (!other.IncludeFileSystemType(FileType)) return false;
            return true;
        }
        public bool IsPartOf(Dictionary<string, FileSystemPermission> others) {
            foreach (var (_,p) in others) {
                if (IsPartOf(p)) return true;
            }
            return false;
        }
    }
    //複合権限
    public sealed class FileSystemPermissionBundle {
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
        private FileSystemPermissionBundle(Dictionary<string, FileSystemPermission> permissions, FileSystemPermissionBundle? basePermissions=null) {
            _permissions = permissions;
            var upperPermissions = _root;
            if (basePermissions != null){upperPermissions = basePermissions._permissions;}
            IsPartOf(upperPermissions);//権限外なら例外が出る
        }

        //指定した権限がこの権限内なら指定した権限からAccesserを生成する処理
        //単純に権限からAccesserを作るときはAccesserのコンストラクタでいい。

        public LocalDriveAccesser CreateLocalDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = false) {
            if (!allowEmpty && IsEmpty) throw new ArgumentException("空の権限でのaccesser生成は許可しない");
            if (Contains(permission)) { return new LocalDriveAccesser(permission.GetPart(DriveTypeEnum.LocalDrive), allowEmpty, singleOnly); }
            throw new ArgumentException($"このインスタンスに以下のアクセス権限はない。{permission}");
        }

        //権限チェック
        //空権限は全ての権限に所属する扱い。

        private bool IsPartOf(Dictionary<string, FileSystemPermission> others){
            foreach (var (_,p) in _permissions) {
                if(!p.IsPartOf(others)) throw new ArgumentException($"このフォルダアクセス権限は許可されていない。{p}");
            }
            return true;
        }
        public bool Contains(FileSystemPermission permission) {
            foreach (var (_,p) in _permissions) {
                if (permission.IsPartOf(p)) return true; 
            }
            return false;
        }
        public bool Contains(FileSystemPermissionBundle permissions) {
            foreach (var (_,p) in permissions._permissions) {
                if (!Contains(p)) return false;
            }
            return true;
        }
        public bool IsEmpty => _permissions.Count == 0;
        public bool IsSingle => _permissions.Count == 1;
        public FileSystemPermission AsSinglePermission(bool singleOnly=false) {
            if (!singleOnly || IsSingle){
                return _permissions.First().Value;
            } else {
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
        public FileSystemPermissionBundle GetPart(string key, bool allowEmpty = true) {
            var dict = new Dictionary<string, FileSystemPermission>();
            if (_permissions.TryGetValue(key,out var p)) dict[key] = p;
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
            foreach (var key in keys) {
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
        public FileSystemPermissionBundle GetPart(DriveTypeEnum type, bool allowEmpty = true) {
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key,p) in _permissions) {
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
        public FileSystemPermissionBundle GetPart(FileSystemAccessLevel accessLevel,bool isLower=true, bool allowEmpty = true) {
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, p) in _permissions){
                if (isLower && (p.AccessLevel & accessLevel) == p.AccessLevel) dict[key] = p;
                else if (!isLower && (p.AccessLevel | accessLevel) == p.AccessLevel) dict[key] = p;
            }
            if (!allowEmpty && dict.Count == 0) {
                if (isLower) throw new ArgumentException($"{accessLevel}以下に該当する権限が存在しない");
                else throw new ArgumentException($"{accessLevel}以上に該当する権限が存在しない");
            } 
            return new FileSystemPermissionBundle(dict, this);
        }
        //全てのAccessLevelを小さく変更する
        public FileSystemPermissionBundle NarrowAccessLevel(FileSystemAccessLevel accessLevel, bool allowEmpty = true) {
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
        public FileSystemPermissionBundle NarrowPath(DriveItemPath path, bool allowEmpty = true) {
            var dict = new Dictionary<string, FileSystemPermission>();
            foreach (var (key, p) in _permissions){
                if (!p.IncludeItemPath(path)) continue;
                dict[key] = p with { Path = path };
            }
            if (!allowEmpty && dict.Count == 0) throw new ArgumentException($"{path}を含む権限が存在しない");
            return new FileSystemPermissionBundle(dict);
        }
        //Narrow系列全部をまとめて実行する
        public FileSystemPermissionBundle Narrow(DriveItemPath? path = null, FileSystemType? fileType = null, FileSystemAccessLevel? accessLevel = null, bool allowEmpty = true) {
            FileSystemPermissionBundle result = this;
            if (path is DriveItemPath p) result = result.NarrowPath(p, allowEmpty);
            if (fileType is FileSystemType t) result = result.NarrowFileSystemType(t, allowEmpty);
            if (accessLevel is FileSystemAccessLevel l) result = result.NarrowAccessLevel(l, allowEmpty);
            return result.MergeAccessLevel();
        }

        //小さくした結果、AccessLevel以外全て同じになったら、足して一つの権限に作り直す。
        public FileSystemPermissionBundle MergeAccessLevel(){
            return new FileSystemPermissionBundle(MergeAccessLevel(_permissions));
        }
        private static Dictionary<string, FileSystemPermission> MergeAccessLevel(Dictionary<string, FileSystemPermission> before) {
            var after = new Dictionary<string, FileSystemPermission>();
            foreach (var (k, p) in before){
                after = MergeAccessLevel(after, k, p);
            }
            return after;
        }
        private static Dictionary<string, FileSystemPermission> MergeAccessLevel(Dictionary<string, FileSystemPermission> dict, string key,FileSystemPermission permission) {
            foreach (var (k, p) in dict) {
                if (p.DriveType == permission.DriveType &&
                    p.Path == permission.Path &&
                    p.Scope == permission.Scope &&
                    p.FileType == permission.FileType
                    ) {
                    dict[k] = p with { AccessLevel = p.AccessLevel | permission.AccessLevel };
                } else {
                    dict[key] = permission;
                }
            }
            return dict;
        }
    }






    /// <summary>
    /// FolderPermissionを利用したaccesser呼び出しを行うクラスの基底クラス
    /// </summary>
    /// <remarks>
    /// フォルダ名すら隠蔽する前提。
    /// 内部で必要なaccesserはフィールドに入れて便利に使おう。
    /// </remarks>
    public class UsingDriveAccesser {
        public FileSystemPermissionBundle Permissions { get; init; }
        public UsingDriveAccesser(FileSystemPermissionBundle permissions) {
            Permissions = permissions;
        }
        public IDriveAccesser CreateDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly=false){
            if (permission.IsEmpty) return new EmptyDriveAccesser(permission, allowEmpty, singleOnly);
            var p = permission.AsSinglePermission(singleOnly);
            if (p.DriveType == DriveTypeEnum.LocalDrive) { return Permissions.CreateLocalDriveAccesser(permission,allowEmpty,singleOnly); }
            else if (p.DriveType == DriveTypeEnum.GoogleDrive) { return Permissions.CreateGoogleDriveAccesser(permission, allowEmpty, singleOnly); }
            throw new ArgumentException($"定義されていないドライブへのアクセス要求{permission}");
        }
        protected FileSystemPermissionBundle GetSmallPermission(
            DriveTypeEnum driveType,
            DriveItemPath path,
            FileSystemType fileType,
            FileSystemAccessLevel accessLevel,
            bool allowEmpty = false
            ) {
            return Permissions
                    .GetPart(driveType,allowEmpty)
                    .NarrowPath(path, allowEmpty)
                    .NarrowFileSystemType(fileType, allowEmpty)
                    .NarrowAccessLevel(accessLevel,allowEmpty)
                    ;
        }

    }
    /// <summary>
    /// 複数のフォルダに対する権限を持ったDriveAccesser
    /// </summary>
    /// <remarks>
    /// 多数のフォルダを管理するクラスの基底に使う。
    /// </remarks>
    public class MultiDriveAccesser : UsingDriveAccesser {
        public MultiDriveAccesser(FileSystemPermissionBundle permissions) : base(permissions){}

        //個別権限の使い捨てaccesserを生成する
        protected IDriveAccesser GetTemporaryAccesser(
            DriveItemPath path,
            FileSystemType fileType,
            FileSystemAccessLevel requiredIfExist,
            FileSystemAccessLevel requiredIfNotExist,
            bool allowEmpty = false
            ) {
            FileSystemAccessLevel level;
            if (requiredIfNotExist == FileSystemAccessLevel.None || path.Exists(true)) {
                level = requiredIfExist;
            } else {
                level = requiredIfNotExist;
            }
            var p = Permissions.Narrow(
                path: path,
                fileType: fileType,
                accessLevel: level,
                allowEmpty: allowEmpty
                );
            if (p.IsEmpty) throw new ArgumentException($"許可されていないアクセスです: {path}   {Permissions}");
            return CreateDriveAccesser(p);
        }
        public DriveItemPath CreateEmptyFile<DirectoryT>(DirectoryT path, FileSystemType fileType, string fileName, bool canWrite = false)
            where DirectoryT: DriveItemPath,IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: fileType,
                requiredIfExist: canWrite ? FileSystemAccessLevel.WriteOnly : FileSystemAccessLevel.None,
                requiredIfNotExist: FileSystemAccessLevel.CreateOnly
            );
            if(accesser is LocalDriveAccesser la　&& path is LocalDirectoryPath lp) return la.CreateEmptyFile<LocalFilePath,LocalDirectoryPath>(lp,fileName, canWrite);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleDirectoryPath gp) return ga.CreateEmptyFile<GoogleFilePath, GoogleDirectoryPath>(gp, fileName, canWrite);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path} {fileName}");
        }
        public void DeleteFile<FileT>(FileT path, FileSystemType fileType)
            where FileT: DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: fileType,
                requiredIfExist: FileSystemAccessLevel.DeleteOnly,
                requiredIfNotExist: FileSystemAccessLevel.All
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) la.DeleteFile(lp);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) ga.DeleteFile(gp);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }

        public DriveItemPath CreateDirectory<DirectoryT>(DirectoryT path, string name)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.All,
                requiredIfNotExist: FileSystemAccessLevel.CreateOnly
            );
            switch ((path,accesser)) {
                case (LocalDirectoryPath localPath, LocalDriveAccesser localAccesser):
                    return localAccesser.CreateDirectory(localPath, name);
                case (GoogleDirectoryPath googlePath, GoogleDriveAccesser googleAccesser):
                    return googleAccesser.CreateDirectory(googlePath, name);
                default:
                    throw new TypeAccessException($"在り得ないはずの型キャスト{path} {name}");
            }
        }
        public void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope scope = PermissionScope.SelfOnly)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.ReadDelete,
                requiredIfNotExist: FileSystemAccessLevel.All
            );
            switch ((path, accesser)){
                case (LocalDirectoryPath localPath, LocalDriveAccesser localAccesser):
                    localAccesser.DeleteDirectory(localPath, scope);
                    break;
                case (GoogleDirectoryPath googlePath, GoogleDriveAccesser googleAccesser):
                    googleAccesser.DeleteDirectory(googlePath, scope);
                    break;
                default:
                    throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
            }
        }
        public void ClearDirectory<DirectoryT>(DirectoryT path, bool recursive = false)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.ReadDelete,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            switch ((path, accesser))
            {
                case (LocalDirectoryPath localPath, LocalDriveAccesser localAccesser):
                    localAccesser.ClearDirectory(localPath, recursive);
                    break;
                case (GoogleDirectoryPath googlePath, GoogleDriveAccesser googleAccesser):
                    googleAccesser.ClearDirectory(googlePath, recursive);
                    break;
                default:
                    throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
            }
        }

        public DriveItemInfo GetItemInfo(DriveItemPath path) {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) return la.GetItemInfo(lp);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) return ga.GetItemInfo(gp);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(
            DirectoryT path,
            FileSystemAccessLevel requiredLevel = FileSystemAccessLevel.ReadOnly,
            bool recursive = false
        )
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalDirectoryPath lp) return await la.GetFileListAsync(lp, requiredLevel, recursive);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleDirectoryPath gp) return await ga.GetFileListAsync(gp, requiredLevel, recursive);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task SaveRawAsync<FileT>(FileT path, byte[] data)
            where FileT : DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.WriteOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) await la.SaveRawAsync(lp, data);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) await ga.SaveRawAsync(gp, data);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task SaveObjectAsync<dataT, FileT>(FileT path, dataT data)
            where FileT : DriveItemPath, IFilePath  
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.WriteOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) await la.SaveObjectAsync(lp, data);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) await ga.SaveObjectAsync(gp, data);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task AppendFileAsync<FileT>(FileT path, string text, bool withBreak = false) 
            where FileT : DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemTypeManager.Text,
                requiredIfExist: FileSystemAccessLevel.AppendOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) await la.AppendFileAsync(lp, text, withBreak);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) await ga.AppendFileAsync(gp, text, withBreak);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path)
            where FileT : DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) return await la.LoadObjectAsync<dataT,LocalFilePath>(lp);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) return await ga.LoadObjectAsync<dataT,GoogleFilePath>(gp);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task TransferToAsync<T0, T1, FileT>(FileT readPath, SingleDriveAccesserGeneric<T0> target, T1 targetPath)
            where FileT : DriveItemPath, IFilePath where T0 : DriveItemPath where T1 : T0, IFilePath
        {
            var reader = GetTemporaryAccesser(
                path: readPath,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (reader is LocalDriveAccesser la && readPath is LocalFilePath lp) await la.TransferToAsync(lp, target, targetPath);
            else if (reader is GoogleDriveAccesser ga && readPath is GoogleFilePath gp) await ga.TransferToAsync(gp, target, targetPath);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{readPath}");
        }
    }


    //非ジェネリックの共通型としてのインターフェイス
    public interface IDriveAccesser { }
    public abstract class SingleDriveAccesserGeneric<T> : IDriveAccesser
        where T : DriveItemPath
    {
        public FileSystemPermission? Permission { get; init; }
        //Permission == nullの時に面倒なので、プロパティを一通り呼び出せるようにしておく
        public DriveTypeEnum? DriveType => Permission?.DriveType;
        public T? Path => Permission?.Path is T p ? p : null;//型が一致するなら代入、一致しなければfalseを返すC#のis演算子を条件式とする三項演算子
        public PermissionScope? Scope => Permission?.Scope;
        public FileSystemAccessLevel? Level => Permission?.AccessLevel;
        public FileSystemType? FileType => Permission?.FileType;
        public bool IsDirectory => Permission != null && Permission.IsDirectory;
        public bool CanRead => Permission != null && Permission.CanRead;
        public bool CanAppend => Permission != null && Permission.CanAppend;
        public bool CanCreate => Permission != null && Permission.CanCreate;
        public bool CanDelete => Permission != null && Permission.CanDelete;
        public bool CanWrite => Permission != null && Permission.CanWrite;

        //権限チェック時など、一旦空権限で生成すること自体は許容する。
        public SingleDriveAccesserGeneric(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true){
            if (permission.IsEmpty) {
                if (!allowEmpty) throw new ArgumentException($"許可されていない空権限でのAccesser生成");
                IsEmpty = true;
                Permission = null;
            } else {
                IsEmpty = false;
                Permission = permission.AsSinglePermission(singleOnly);
            }
        }
        public bool IsEmpty { get; init; }
        protected void CheckEmpty() {if(IsEmpty) throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない"); }

        // 権限と存在を統合的にチェックする内部メソッド
        protected virtual void ValidateAccess(T path, FileSystemAccessLevel requiredIfExist, FileSystemAccessLevel requiredIfNotExist){
            CheckEmpty();
            //pathを含まないなら権限も何もない
            if (!Permission!.IncludeItemPath(path)) throw new ArgumentException($"アクセス権限のないpathです: {path}");
            
            if (ItemExists(path)){
                // 対象ファイルが存在するならrequiredIfExist権限の確認
                if (requiredIfExist == MyAudioPlayer.FileSystemAccessLevel.None) throw new UnauthorizedAccessException($"存在する{path} の不在を前提とした操作です");
                if ((Permission!.AccessLevel & requiredIfExist) != requiredIfExist)
                    throw new UnauthorizedAccessException($"{path} に対する {requiredIfExist} 権限がありません。");
            }else{
                // 対象ファイルが存在しないならrequiredIfNotExist権限の確認
                if (requiredIfNotExist == MyAudioPlayer.FileSystemAccessLevel.None) throw new UnauthorizedAccessException($"存在しない{path} の存在を前提とした操作です");
                if ((Permission!.AccessLevel & requiredIfNotExist) != requiredIfNotExist)
                    throw new UnauthorizedAccessException($"{path} に対する {requiredIfNotExist} 権限がありません。");
            }
        }
        // --- ドライブ ⇔ 変数 (JSON等で抽象化) ---
        // T型のデータを直接保存/読み込み。内部でStreamとシリアライザを回す
        public abstract Task SaveObjectAsync<dataT,FileT>(FileT path, dataT data) where FileT : T, IFilePath;
        public abstract Task SaveRawAsync<FileT>(FileT path, byte[] data) where FileT : T, IFilePath;  // wavなどのバイナリ用
        public abstract Task AppendFileAsync<FileT>(FileT path, string text, bool withBreak = false) where FileT : T, IFilePath;
        public abstract Task<dataT?> LoadObjectAsync<dataT,FileT>(FileT path) where FileT : T, IFilePath;

        // --- 拡張：ファイル管理 ---
        public abstract FileT CreateEmptyFile<FileT, DirectoryT>(DirectoryT path, string name, bool canWrite = false) where DirectoryT : T, IDirectoryPath where FileT: T,IFilePath;
        public abstract void DeleteFile<FileT>(FileT path) where FileT : T, IFilePath;
        public abstract DirectoryT CreateDirectory<DirectoryT>(DirectoryT path, string name, bool canWrite = false) where DirectoryT : T, IDirectoryPath;
        public abstract void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope scope = PermissionScope.SelfOnly) where DirectoryT : T, IDirectoryPath;
        public abstract void ClearDirectory<DirectoryT>(DirectoryT path, bool recursive = false) where DirectoryT : T, IDirectoryPath;
        public abstract DriveItemInfo GetItemInfo(T path);
        public abstract bool ItemExists(T path);
        public abstract Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(DirectoryT path, FileSystemAccessLevel requiredLevel = FileSystemAccessLevel.ReadOnly, bool recursive = false) where DirectoryT : T, IDirectoryPath;

        // --- ドライブ ⇔ ドライブ (内部転送) ---
        // 自身(Source)から別(Target)へデータを流し込む
        // 実装側で source.OpenStream -> target.SaveStream を行う
        public abstract Task TransferToAsync<T0, T1, FileT>(FileT readpath, SingleDriveAccesserGeneric<T0> target, T1 targetPath)
            where FileT : T, IFilePath where T0 : DriveItemPath where T1 : T0, IFilePath;
        public abstract Task SaveStreamAsync<FileT>(FileT path, Stream stream) where FileT : T, IFilePath;

        // --- 内部用（実装クラスのみが意識する） ---
        // インターフェースのデフォルト実装や protected 的な扱いで定義
        protected abstract Task<Stream> OpenReadStreamAsync<FileT>(FileT path) where FileT : T, IFilePath;
    }
    public class EmptyDriveAccesser : SingleDriveAccesserGeneric<DriveItemPath>{
        public EmptyDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true)
            :base(permission, allowEmpty, singleOnly)
        {
            if (!permission.IsEmpty) throw new ArgumentException("空でない権限でのEmptyDriveAccesser生成");
            if (!allowEmpty) throw new ArgumentException("空権限を許可しない状況下でのEmptyDriveAccesser生成");
        }
        //abstractメソッドを実装するが、全て例外を返すだけで起動はしない。
        public override DriveItemInfo GetItemInfo(DriveItemPath path) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(DirectoryT path, FileSystemAccessLevel requiredLevel = FileSystemAccessLevel.ReadOnly, bool recursive = false) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override bool ItemExists(DriveItemPath path) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override FileT CreateEmptyFile<FileT, DirectoryT>(DirectoryT path, string name, bool canWrite = false) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override void DeleteFile<FileT>(FileT path) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override DirectoryT CreateDirectory<DirectoryT>(DirectoryT path, string name, bool canWrite = false) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope scope = PermissionScope.SelfOnly) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override void ClearDirectory<DirectoryT>(DirectoryT path, bool recursive = false) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task AppendFileAsync<FileT>(FileT path, string text, bool withBreak = false) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveObjectAsync<T,FileT>(FileT path, T data) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task<T?> LoadObjectAsync<T,FileT>(FileT path) where T : default => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveRawAsync<FileT>(FileT path, byte[] data) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        protected override Task<Stream> OpenReadStreamAsync<FileT>(FileT path) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveStreamAsync<FileT>(FileT path, Stream stream) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task TransferToAsync<T0,T1,FileT>(FileT readpath, SingleDriveAccesserGeneric<T0> target, T1 targetPath) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
    }

    public class LocalDriveAccesser : SingleDriveAccesserGeneric<LocalDrivePath>{

        public LocalDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true)
            :base(permission, allowEmpty, singleOnly)
        {}

        public override DriveItemInfo GetItemInfo(LocalDrivePath path){
            ValidateAccess(path, FileSystemAccessLevel.ReadOnly,FileSystemAccessLevel.None);
            if (path is LocalFilePath) {
                var f = new FileInfo(path.Value);
                return new DriveItemInfo(
                        DriveType: MyAudioPlayer.DriveTypeEnum.LocalDrive,
                        Name: f.Name,
                        FileType: f.Extension.FromExtension(),
                        Path: path,
                        Size: f.Length,
                        LastModified: f.LastWriteTime,
                        IsDirectory: false
                    );
            } else {
                var f = new DirectoryInfo(path.Value);
                return new DriveItemInfo(
                        DriveType: MyAudioPlayer.DriveTypeEnum.LocalDrive,
                        Name: f.Name,
                        FileType: FileSystemType.Directory,
                        Path: path,
                        Size: null,
                        LastModified: f.LastWriteTime,
                        IsDirectory: true
                    );
            }

        }
        public override async Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(DirectoryT path, FileSystemAccessLevel requiredLevel = FileSystemAccessLevel.ReadOnly, bool recursive = false){
            CheckEmpty();
            
            //指定したfolderの子に対するアクセス権限があるかどうかをまず確認
            if (Permission!.IncludeScope(PermissionScope.ChildrenOnly) && Permission.Path == path) { }
            else if (Permission!.IncludeScope(PermissionScope.Recursive) && Permission.Path != path && Permission.IncludeItemPath(path)) { }
            else { throw new UnauthorizedAccessException("フォルダへのアクセス権限が不足しています。"); }


            var option = recursive && Permission!.IncludeScope(PermissionScope.Recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var di = new DirectoryInfo(path.Value);

            return di.GetFiles("*", option)
                .Where(f => Permission.IncludeFileSystemType(f.Extension.FromExtension())) // 拡張子フィルタ適用
                .Select(f => new DriveItemInfo(
                    Name: f.Name,
                    DriveType: MyAudioPlayer.DriveTypeEnum.LocalDrive,
                    Path: (LocalFilePath)f.FullName,
                    FileType: f.Extension.FromExtension(),
                    Size: f.Length,
                    LastModified: f.LastWriteTime,
                    IsDirectory: false
                ))
                .ToList();
        }
        public override bool ItemExists(LocalDrivePath path) {
            return path switch{
                LocalFilePath => System.IO.File.Exists(Path!.Value),
                LocalDirectoryPath => Directory.Exists(Path!.Value),
                _ => throw new ArgumentException($"未定義のパス型{path}")
            };
        }
        public override async Task SaveRawAsync<FileT>(FileT path, byte[] data){
            ValidateAccess(path, FileSystemAccessLevel.WriteOnly, FileSystemAccessLevel.None);
            await System.IO.File.WriteAllBytesAsync(
                path.Value,
                data
            );
        }
        public override async Task SaveObjectAsync<dataT,FileT>(FileT path, dataT data){
            ValidateAccess(path, FileSystemAccessLevel.WriteOnly, FileSystemAccessLevel.None);
            await System.IO.File.WriteAllTextAsync(
                path.Value,
                JsonConvert.SerializeObject(data, Formatting.Indented)
            );
        }
        public override async Task AppendFileAsync<FileT>(FileT path, string text, bool withBreak = false){
            ValidateAccess(path, FileSystemAccessLevel.AppendOnly, FileSystemAccessLevel.None);
            var content = withBreak ? text + Environment.NewLine : text;
            await System.IO.File.AppendAllTextAsync(path.Value, content);
        }
        public override FileT CreateEmptyFile<FileT, DirectoryT>(DirectoryT path, string name,bool canWrite = false){
            var filePathString = System.IO.Path.Combine(path.Value, name);
            var filePath = new LocalFilePath(filePathString);
            if (canWrite){
                ValidateAccess(filePath, FileSystemAccessLevel.WriteOnly, FileSystemAccessLevel.CreateOnly);
            }else{
                ValidateAccess(filePath, FileSystemAccessLevel.None, FileSystemAccessLevel.CreateOnly);
            }
            using (System.IO.File.Create(filePath.Value)) { }
            if (filePath is FileT f) return f;
            else throw new TypeAccessException($"在り得ないはずの型キャスト{filePath}");
        }
        public override void DeleteFile<FileT>(FileT path){
            ValidateAccess(path, FileSystemAccessLevel.DeleteOnly, FileSystemAccessLevel.All);//ファイルが存在しないなら何もしないので権限に制限はかけない
            if (System.IO.File.Exists(path.Value)) System.IO.File.Delete(path.Value);
        }
        public override DirectoryT CreateDirectory<DirectoryT>(DirectoryT path, string name, bool canWrite = false) {
            var folderPathString = System.IO.Path.Combine(path.Value, name);
            var folderPath = new LocalDirectoryPath(folderPathString);
            ValidateAccess(folderPath, FileSystemAccessLevel.All, FileSystemAccessLevel.CreateOnly);//フォルダが存在するなら何もしないので権限に制限はかけない
            if (!Directory.Exists(folderPath.Value)) Directory.CreateDirectory(folderPath.Value);
            if (folderPath is DirectoryT f) return f;
            else throw new TypeAccessException($"在り得ないはずの型キャスト{folderPath}");
        }
        //scope==SelfOnlyなら、空フォルダの時のみ削除。そうでなければ例外。
        //SelfAndChildrenなら、中身が削除権限のあるファイルと空フォルダのみであればすべて削除。そうでなければ一切削除せずに例外。
        //AllWithSelfなら、配下のファイル・フォルダ全てに削除権限があればすべて削除。そうでなければ一切削除せずに例外。
        public override void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope scope= PermissionScope.SelfOnly) {
            ValidateAccess(path, FileSystemAccessLevel.DeleteOnly, FileSystemAccessLevel.All);//フォルダが存在しないなら何もしないので権限に制限はかけない
            if (!Directory.Exists(path.Value)) return;

            var di = new DirectoryInfo(path.Value);
            // SelfOnly の場合、中身があったら即例外、中身が無ければ削除して終了
            if (scope == PermissionScope.SelfOnly) {
                if (di.GetFileSystemInfos().Length > 0) {
                    throw new IOException($"ディレクトリが空ではないため削除できません: {path.Value}");
                } else {
                    di.Delete();
                    return;
                }
            }

            SearchOption searchOption = scope switch{
                PermissionScope.SelfAndChildren => SearchOption.TopDirectoryOnly,
                PermissionScope.AllWithSelf => SearchOption.AllDirectories,
                _ => throw new ArgumentException($"不適切な権限指定{scope}")
            };

            // 2. 権限の事前チェック（ドライラン）
            // 配下の全アイテムに対して削除権限があるか確認
            var allItems = di.GetFileSystemInfos("*", searchOption);
            foreach (var item in allItems){
                var itemInfo = item is FileInfo fi ? DriveItemInfo.From(fi) : DriveItemInfo.From((DirectoryInfo)item);
                if (!Permission!.IsItemAllowed(itemInfo)){
                    throw new UnauthorizedAccessException($"配下アイテムの削除権限がありません: {item.FullName}");
                }
            }

            // 3. 実行（ファイルから消し、最後にディレクトリを消す）
            // Localなら Directory.Delete(path, true) でも良いが、
            // 「権限があるものだけ確実に」なら自前で再帰したほうが安全
            di.Delete(true);
        }

        //削除権限のあるファイルを全て削除する。空フォルダ含めフォルダは削除しない。
        public override void ClearDirectory<DirectoryT>(DirectoryT path, bool recursive = false) {
            ValidateAccess(path, FileSystemAccessLevel.ReadDelete, FileSystemAccessLevel.All); // フォルダ自体を見れる必要はある

            var di = new DirectoryInfo(path.Value);
            if (!di.Exists) return;

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            // ファイルだけを抽出
            var files = di.GetFiles("*", searchOption);

            foreach (var file in files){
                var info = DriveItemInfo.From(file);
                if (Permission!.IsItemAllowed(info)) file.Delete();
            }
        }
        public override async Task<dataT?> LoadObjectAsync<dataT,FileT>(FileT path)
            where dataT : default
        {
            ValidateAccess(path, FileSystemAccessLevel.ReadOnly, FileSystemAccessLevel.None);
            var json = await System.IO.File.ReadAllTextAsync(path.Value, Encoding.UTF8);
            return JsonConvert.DeserializeObject<dataT>(json);
        }

        protected override async Task<Stream> OpenReadStreamAsync<FileT>(FileT path){
            ValidateAccess(path, FileSystemAccessLevel.ReadOnly, FileSystemAccessLevel.None);
            return new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        }

        public override async Task SaveStreamAsync<FileT>(FileT path, Stream stream){
            ValidateAccess(path, FileSystemAccessLevel.WriteOnly, FileSystemAccessLevel.None);
            using var fs = new FileStream(path.Value, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.CopyToAsync(fs);
        }

        public override async Task TransferToAsync<T0,T1,FileT>(FileT readPath, SingleDriveAccesserGeneric<T0> target, T1 targetPath){
            // 自身からの読み取りが可能かチェック
            ValidateAccess(readPath, FileSystemAccessLevel.ReadOnly,FileSystemAccessLevel.None);
            using var srcStream = await OpenReadStreamAsync(readPath);
            // 相手側への保存（相手側でも権限チェックが走る）
            await target.SaveStreamAsync(targetPath, srcStream);
        }
    }




    //個々から下、修正前の残骸がそのまま残っている。


    public class AccessGoogleDrive {
        public readonly DriveService _service;
        public List<string> RewritableFolderIds { get; init; }
        public AccessGoogleDrive(string jsonKeyPath,List<string> rewritableFolderIds) { 
            if (!System.IO.File.Exists(jsonKeyPath))
                throw new FileNotFoundException("JSONキーが見つかりません。", jsonKeyPath);

            RewritableFolderIds = rewritableFolderIds;

            // 1. JSONファイルを読み込み、パラメータとしてパースする
            // これにより、SDK内部の CredentialFactory を介さず自前で安全に構築できます
            string json = System.IO.File.ReadAllText(jsonKeyPath);
            var paramsObj = JsonConvert.DeserializeObject<JsonCredentialParameters>(json);

            // 2. サービスアカウント認証のInitializerを組み立てる
            var initializer = new ServiceAccountCredential
                .Initializer(paramsObj!.ClientEmail){
                    Scopes = new[] { DriveService.Scope.Drive }
                }
            ;
            // 秘密鍵（Private Key）を読み込ませる
            initializer.FromPrivateKey(paramsObj.PrivateKey);

            // 3. 認証オブジェクトを生成
            var credential = new ServiceAccountCredential(initializer);

            // 4. サービスを生成
            _service = new DriveService(new BaseClientService.Initializer(){
                HttpClientInitializer = credential,
                ApplicationName = "MyAudioPlayer_AnalysisTool",
            });
        }

        // JSONからパラメータを抽出するためのヘルパークラス
        private class JsonCredentialParameters{
            [JsonProperty("client_email")]
            public required string ClientEmail { get; set; }
            [JsonProperty("private_key")]
            public required string PrivateKey { get; set; }
        }
        public bool CheckRewritableFolder(string folderId) {
            foreach (var dir in RewritableFolderIds){
                if (folderId.StartsWith(dir)) return true;
            }
            throw new NotImplementedException($"{folderId}は変更可能なフォルダではない");
        }

        public async Task DeleteFileAsync(string fileId) {
            CheckRewritableFolder(fileId);
            await _service.Files.Delete(fileId).ExecuteAsync();
        }
        public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetFileList(string folderId) {
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and trashed = false";
            var result = await request.ExecuteAsync();

            return result.Files;
        }
        public async Task<bool> ExistFileAsync(string folderId, string FileName) {
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and name = '{FileName}' and trashed = false";
            var result = await request.ExecuteAsync();

            if (result.Files != null && result.Files.Any()) {
                return true;
            } else {
                return false;
            }
        }


        public async Task<string> UploadFileAsync(string fromPath,string toDir){
            CheckRewritableFolder(toDir);

            var metadata = new Google.Apis.Drive.v3.Data.File(){
                Name = Path.GetFileName(fromPath),
                Parents = new List<string> { toDir }
            };

            using var stream = new FileStream(fromPath, FileMode.Open);
            var request = _service.Files.Create(metadata, stream, "application/octet-stream");
            var progress = await request.UploadAsync();

            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
                throw progress.Exception;

            return request.ResponseBody.Id;
        }
    }

    public class AccessLocalDrive
    {
    }


    public class AudioAnalysisManager{
        // --- 固定設定 ---
        private const string InputFolderId = "1CZhbuAiwWG7x8iNKs5nHOkcplaZu34jo";
        private const string OutputFolderId = "18ASlpbb43mzWz9fB5ecqEZ4TjhnmjZiQ";
        private const string ColabUrl = "https://colab.research.google.com/drive/1f_rlodFaHARzrdi_Rtbc5N_0BHIuPfst";
        private const string FfmpegPath = "ffmpeg.exe"; // パスは環境に合わせて調整
        private const string Address = "audioanalizerbot@audioanalizer.iam.gserviceaccount.com";
        private const string JsonKeyPath = "Credentials/service-account-key.json";
        private const string AppName = "AudioAnalizer";
        private const string SaveFolderDir = "Result";

        private readonly AccessGoogleDrive _access;
        private readonly DriveService _service;
        private readonly ConcurrentQueue<string> _taskQueue = new ConcurrentQueue<string>();
        private bool _isProcessing = false;
        private string _uploadedFileId = "";

        public AudioAnalysisManager(){
            _access=new AccessGoogleDrive(JsonKeyPath,new List<string> {InputFolderId,OutputFolderId });
            _service = _access._service;

        }

        public async Task DiagnosticTestAsync()
        {
            try
            {
                MainWindow.Log("--- 診断開始 ---");

                // 1. 権限の確認
                MainWindow.Log($"使用中のサービスアカウント: {((ServiceAccountCredential)_service.HttpClientInitializer).Id}");

                // 2. フォルダが見えるかテスト
                MainWindow.Log("Outputフォルダのリスト取得を試行...");
                var request = _service.Files.List();
                request.Q = $"'{OutputFolderId}' in parents and trashed = false";
                //request.PageSize = 1;

                // ここで止まるなら：認証 or ネットワークの問題
                var result = await request.ExecuteAsync().ConfigureAwait(false);

                MainWindow.Log($"接続成功。フォルダ内のファイル数: {result.Files.Count}");

                if (result.Files.Count == 0)
                {
                    MainWindow.Log("警告: フォルダは見えるが中身が空です。サービスアカウントに共有されていますか？");
                }

                // 3. heartbeat.txt を直接探す
                MainWindow.Log("heartbeat.txt をピンポイントで探します...");
                request.Q = $"'{InputFolderId}' in parents and name = 'heartbeat.txt'";
                var hResult = await request.ExecuteAsync().ConfigureAwait(false);

                if (hResult.Files.Any())
                {
                    MainWindow.Log($"発見: 更新時刻 {hResult.Files[0].ModifiedTimeDateTimeOffset}");
                }
                else
                {
                    MainWindow.Log("不検出: heartbeat.txt が見つかりません。Colab側で作成されていますか？");
                }
            }
            catch (Exception ex)
            {
                MainWindow.Log($"【致命的エラー】: {ex.Message}");
            }
        }



        /// <summary>
        /// 外部から楽曲を投入するエントリーポイント
        /// </summary>
        public void AddToQueue(string sourceFilePath){
            _taskQueue.Enqueue(sourceFilePath);
            MainWindow.Log($"{sourceFilePath}をキューに追加");
            if (!_isProcessing){Task.Run(() => ProcessQueueAsync());}
        }

        private async Task ProcessQueueAsync(){
            _isProcessing = true;

            while (_taskQueue.TryDequeue(out var sourceFile)){
                MainWindow.Log("ProcessQueueAsyncを開始");
                try{
                    // 1. 生存確認 & 必要ならColab起動
                    if (!await IsColabAliveAsync()){
                        MainWindow.Log("Colabを起動");
                        Console.WriteLine("[C#] Colabが停止している可能性があるため起動します...");
                        LaunchColab();
                        await Task.Delay(15000); // ブラウザ起動とVM接続のバッファ
                    }else {
                        MainWindow.Log("Colab生存を確認");
                        Console.WriteLine("[C#] Colabの生存を確認");
                    }

                    // 2. WAV変換
                    string workWavPath = await EnsureWavFormatAsync(sourceFile);

                    MainWindow.Log("wav変換完了");

                    // 3. アップロード
                    Console.WriteLine($"[C#] {Path.GetFileName(workWavPath)} をアップロード中...");
                    string fileId = await UploadAudioAsync(workWavPath);

                    MainWindow.Log("アップロード完了");

                    // 4. 解析完了監視 (done.txt)
                    Console.WriteLine("[C#] 解析完了を待機中...");
                    if (await PollForCompletionAsync()){
                        // 5. 結果取得
                        string downloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results", Path.GetFileNameWithoutExtension(sourceFile));
                        await DownloadResultsAsync(downloadDir);

                        // 6. 後始末（Drive上の音源と完了フラグを削除）
                        await _service.Files.Delete(fileId).ExecuteAsync();
                        await DeleteFileByNameAsync(OutputFolderId, "done.txt");
                        Console.WriteLine($"[C#] {Path.GetFileName(sourceFile)} の全工程が完了。");
                    }

                    // 一時的に作成したWAVがあれば削除
                    if (workWavPath != sourceFile && System.IO.File.Exists(workWavPath)){
                        System.IO.File.Delete(workWavPath);
                    }
                }catch (Exception ex){
                    Console.WriteLine($"[C#] 致命的エラー ({Path.GetFileName(sourceFile)}): {ex.Message}");
                }
            }
            _isProcessing = false;
        }

        #region Drive & System Operations

        private async Task<string> UploadAudioAsync(string path){

            MainWindow.Log("UploadAudioAsync開始");

            var metadata = new Google.Apis.Drive.v3.Data.File(){
                Name = Path.GetFileName(path),
                Parents = new List<string> { InputFolderId }
            };

            using var stream = new FileStream(path, FileMode.Open);
            var request = _service.Files.Create(metadata, stream, "application/octet-stream");
            MainWindow.Log("request.UploadAsync()開始");
            var progress = await request.UploadAsync();

            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
                throw progress.Exception;

            return request.ResponseBody.Id;
        }

        private async Task<bool> PollForCompletionAsync(){
            // 最大待機時間を設ける場合はここでカウンタを回す
            while (true){
                var request = _service.Files.List();
                request.Q = $"'{OutputFolderId}' in parents and name = 'done.txt' and trashed = false";
                var result = await request.ExecuteAsync();

                if (result.Files != null && result.Files.Any()) return true;

                await Task.Delay(10000); // 10秒おきに監視
            }
        }

        private async Task DownloadResultsAsync(string saveDir){
            if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

            var listRequest = _service.Files.List();
            listRequest.Q = $"'{OutputFolderId}' in parents and name != 'done.txt' and name != 'heartbeat.txt' and trashed = false";
            var result = await listRequest.ExecuteAsync();

            foreach (var file in result.Files){
                var getRequest = _service.Files.Get(file.Id);
                string localPath = Path.Combine(saveDir, file.Name);

                using var stream = new FileStream(localPath, FileMode.Create);
                await getRequest.DownloadAsync(stream);

                // ダウンロード後にDrive上の結果ファイルを消す場合はここでDeleteを呼ぶ
                await _service.Files.Delete(file.Id).ExecuteAsync();
            }
        }

        public async Task<bool> IsColabAliveAsync(){

            MainWindow.Log($"生存確認開始");

            var request = _service.Files.List();
            // Trashed = false を入れることで、ゴミ箱の中身を誤検知するのを防ぎます
            request.Q = $"'{InputFolderId}' in parents and name = 'heartbeat.txt' and trashed = false";
            // 重要：ここを API仕様の "modifiedTime" に固定
            request.Fields = "files(id, modifiedTime)";

            try{
                var result = await request.ExecuteAsync();
                var heartbeat = result.Files.FirstOrDefault();

                if (heartbeat?.ModifiedTimeDateTimeOffset == null) return false;

                // 通信が成功し、時間が取得できれば「接続確立」と判定
                return (DateTimeOffset.Now - heartbeat.ModifiedTimeDateTimeOffset.Value).TotalMinutes < 2;
            }catch (Exception ex){
                // 401 や 403 が出れば設定（共有など）の問題、ここに来なければコードの問題
                MainWindow.Log($"接続テスト失敗: {ex.Message}");
                return false;
            }
        }

        private void LaunchColab(){
            Process.Start(new ProcessStartInfo(ColabUrl) { UseShellExecute = true });

            // ブラウザの読み込み待機
            Task.Delay(10000).Wait();

            // 「すべてのセルを実行」ショートカット送信
            SendKeys.SendWait("^({F9})");
        }

        private async Task<string> EnsureWavFormatAsync(string inputPath){
            if (Path.GetExtension(inputPath).ToLower() == ".wav") return inputPath;

            MainWindow.Log("wave変換処理開始");

            string outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileNameWithoutExtension(inputPath) + "_work.wav");

            var startInfo = new ProcessStartInfo{
                FileName = FfmpegPath,
                Arguments = $"-i \"{inputPath}\" \"{outputPath}\" -y",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            await Task.Run(() => process!.WaitForExit());

            return outputPath;
        }

        private async Task DeleteFileByNameAsync(string folderId, string fileName){
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and name = '{fileName}' and trashed = false";
            var result = await request.ExecuteAsync();
            foreach (var file in result.Files){
                await _service.Files.Delete(file.Id).ExecuteAsync();
            }
        }

        #endregion
    }
}




using System.Reflection;

/// <summary>
/// 扱うドライブの種類が増えたら再構成する必要がある共通クラスをまとめるファイル
/// </summary>

namespace Crast.Accesser.DriveAccesser{



    /// <summary>
    /// FileSystemPermissionクラスのDriveTypeプロパティで使用する列挙型。
    /// </summary>
    public enum DriveTypeEnum{
        LocalDrive,
        GoogleDrive,
    }

    #region 拡張子、MIMEタイプ等を扱う共通型であるFileSystemTypeとその関連

    /// <summary>
    /// FileSystemTypeのサブタイプを定義するためのカスタム属性。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class FileSystemSubTypeAttribute : Attribute{
        public required string? LocalDrive { get; init; }
        public required string? GoogleDrive { get; init; }
    }

    /// <summary>
    /// フォルダを含む、ファイル種別を扱う内部型
    /// </summary>
    /// <remarks>
    /// 対応辞書を生成する際、名前が被る場合はより後のものが残る事には注意
    /// </remarks>
    public enum FileSystemType{
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

        All = (1 << 10) - 1,

        // 再掲
        // 各名前の前半で括ったものをFileSystemTypeManagerクラスの定数として
        // 自動で定義するため、カスタム属性だけでなく名付けルールも守れ。
    }

    /// <summary>
    /// FileSystemTypeを拡張子やMIMEタイプに変換するためのマネージャークラス。
    /// </summary>
    /// <remarks>
    /// FileSystemTypeのカスタム属性を含む記述を基に、変換用のDictionaryを自動で生成する。
    /// </remarks>
    internal static class FileSystemTypeManager{
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
        //静的コンストラクタ
        static FileSystemTypeManager(){
            LoadEnum();
        }
        /// <summary>
        /// FileSystemTypeの記述を基に、変換用のDictionaryを生成する。
        /// </summary>
        private static void LoadEnum(){
            if (loaded) return;
            lock (_lockObj){
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

        public static FileSystemType FromExtension(this string extension){
            if (_FromExtension.TryGetValue(extension, out var type)) return type;
            throw new ArgumentException($"定義されていない拡張子{extension}");
        }
        public static string? ToExtension(this FileSystemType type){
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

    #endregion

    /// <summary>
    /// ストレージの種類を問わず、ファイル情報を保持する共通クラス
    /// </summary>
    /// <remarks>
    /// Fromメソッドは、System.IO.FileInfo、System.IO.DirectoryInfo、GoogleDriveMetadataの三種類に対応している。
    /// 基本、情報確認用の返り値型だが、FileSystemPermission.IsItemAllowed()の引数型としても使う。
    /// </remarks>
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
        public static DriveItemInfo From(FileInfo info){
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

    //FileSystemPermission.IncludeItemPath()を管理するために作ったが、GoogleDrive用であって、LocalDriveはフルパス文字列から判別するべき。
    //その辺込みで修正の必要はある。
    public static class PermissionScopeReachHistory{

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
            if (basePath == currentPath){
                if ((scope | PermissionScope.ChildrenOnly) == scope){
                    Childrens[permission].Add(path);
                    return true;
                }else{
                    return false;
                }
            }
            //以降、自身でも子でも無い場合
            //孫以降を含まない権限なら「含まない」
            if ((scope | PermissionScope.Recursive) != scope) return false;
            //孫以降を含むなら、recursiveに辿っていくしかない。
            currentPath = currentPath.Parent;
            while (currentPath != null){
                if (basePath == currentPath){
                    GrandChildrens[permission].Add(path);
                    return true;
                }
                currentPath = currentPath.Parent;
            }
            //親を辿れなくなったら「含まない」
            return false;
        }
    }


}

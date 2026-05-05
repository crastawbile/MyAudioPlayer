using System;
using System.Collections.Concurrent;
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
    [Flags]
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
        private static readonly Lock _lockObj = new();
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
    /// 基本、情報確認用の返り値型だが、FileSystemPermission.IncludeItem()の引数型としても使う。
    /// </remarks>
    public record DriveItemInfo(
        DriveTypeEnum DriveType,
        DriveItemPath Path,
        string Name,
        FileSystemType FileType,
        bool IsDirectory,
        long? Size = null,
        DateTime? LastModified = null,
        CachedNode? Cache = null // GoogleDrive の時だけセットされる
    ){
        public static DriveItemInfo From(CachedNode cache){
            return new DriveItemInfo(
                    DriveType: DriveTypeEnum.GoogleDrive,
                    Path: cache.Id,
                    Name: cache.Name,
                    FileType: cache.FileType,
                    Size: cache.Size,
                    IsDirectory: cache.IsDirectory,
                    Cache: cache
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

    #region CachedResult関連
    public readonly record struct CachedNode(
        DriveItemPath Id,//DriveItemInfoへの変換など、パスそのものもrecord内部にある方が楽。
        DriveItemPath[] ParentIds,
        FileSystemType FileType,
        bool IsDirectory,
        String Name,
        long? Size,
        bool IsTrashed,
        int? Version, //GoogleDriveのファイルのバージョン。ローカルドライブでは常にnull。
        DateTimeOffset? LastModified//最終更新日時。ファイル自体の更新日時。
    );
    public readonly record struct CachedResult{
        public CachedNode? Node { get; init; }
        public CachedError? LogicalError { get; init; }
        public CachedError? SecurityError { get; init; }
        public CachedError? TransientError { get; init; }
        public DateTimeOffset LastVerified { get; init; }//最終検証日時。実際に確認してキャッシュを更新したラスト。
        public DateTimeOffset LastChecked { get; init; }//最終参照日時。このキャッシュを利用したラスト。

        public CachedResult RemakeWithData(CachedNode data) => this with {
            Node = data,
            LogicalError = null,
            SecurityError = null,
            TransientError = null,
            LastVerified = DateTimeOffset.Now,
            LastChecked = DateTimeOffset.Now
        };
        public CachedResult RemakeWithError(CachedError error) {
            return error.Type switch{
                CacheableErrorType.Logical => this with { Node = null, LogicalError = error, SecurityError = null, TransientError = null, LastVerified = DateTimeOffset.Now, LastChecked = DateTimeOffset.Now },
                CacheableErrorType.Security => this with { SecurityError = error, TransientError = null, LastVerified = DateTimeOffset.Now, LastChecked = DateTimeOffset.Now },
                CacheableErrorType.Transient => this with { TransientError = error, LastVerified = DateTimeOffset.Now, LastChecked = DateTimeOffset.Now },
                _ => throw new ArgumentException($"定義されていないエラー種別{error.Type}"),
            };
        }
        public CachedResult RemakeWithCheckedTime() => this with { LastChecked = DateTimeOffset.Now };
    }

    public enum CacheableErrorType{
        None,// 定義外のエラー
        Logical,// 404: 永続的な不在
        Security,// 403: 権限不足
        Transient// 5xx, Timeout: 一時的な失敗
    }
    public readonly record struct CachedError(
        CacheableErrorType Type,
        int? HttpStatusCode,
        string Message,
        DateTimeOffset ErrorTime
    );
    public readonly record struct CacheStrategy(
            bool CacheFirst,//キャッシュのデータと実データアクセス、どちらを優先するか。
            TimeSpan? CacheValidityDuration,//キャッシュの有効期間。これを過ぎたらキャッシュは無効とみなす。
            bool CacheNegativeResults,//アクセスエラーもキャッシュするかどうか
            bool AllowFallback,//優先した方でデータが取得できなかった際に、もう一方も試すかどうか
            bool RefreshCache,//キャッシュが存在する場合に、実データで上書きするかどうか
            bool AddCache,//キャッシュに存在しないデータを実データから取得した際に、キャッシュに追加するかどうか
            bool BackgroundRefresh//バックグラウンドでキャッシュを更新するかどうか
        ) {
        public static CacheStrategy CACHE_FIRST(TimeSpan? maxAge) => new(
            CacheFirst: true,
            CacheValidityDuration: maxAge,
            CacheNegativeResults: true,
            AllowFallback: true,
            RefreshCache: true,
            AddCache: true,
            BackgroundRefresh: true
        );
        public static CacheStrategy REALTIME_ONLY() => new(
            CacheFirst: false,
            CacheValidityDuration: null,
            CacheNegativeResults: false,
            AllowFallback: false,
            RefreshCache: false,
            AddCache: false,
            BackgroundRefresh: false
        );
        public static CacheStrategy NETWORK_FIRST(TimeSpan? maxAge) => new(
            CacheFirst: false,
            CacheValidityDuration: maxAge,
            CacheNegativeResults: true,
            AllowFallback: true,
            RefreshCache: true,
            AddCache: true,
            BackgroundRefresh: false
        );
        public static CacheStrategy CACHE_ONLY(TimeSpan? maxAge) => new(
            CacheFirst: true,
            CacheValidityDuration: maxAge,
            CacheNegativeResults: true,
            AllowFallback: false,
            RefreshCache: false,
            AddCache: false,
            BackgroundRefresh: false
        );
        public static CacheStrategy REALTIME_FIRST(TimeSpan? maxAge) => new(
            CacheFirst: false,
            CacheValidityDuration: maxAge,
            CacheNegativeResults: false,
            AllowFallback: true,
            RefreshCache: false,
            AddCache: false,
            BackgroundRefresh: false
        );
        public static CacheStrategy NETWORK_ONLY(TimeSpan? maxAge) => new(
            CacheFirst: false,
            CacheValidityDuration: maxAge,
            CacheNegativeResults: true,
            AllowFallback: false,
            RefreshCache: false,
            AddCache: false,
            BackgroundRefresh: false
        );

        /// <summary>
        /// 指定した時刻がキャッシュの有効期間内かどうかを判定します。
        /// </summary>
        /// <param name="targetTime">判定対象の時刻</param>
        /// <returns>有効期間内であればtrue、そうでなければfalse</returns>
        public bool InTimeLimit(DateTimeOffset targetTime) {
            if (CacheValidityDuration == null) return true;
            return DateTimeOffset.Now - targetTime <= CacheValidityDuration;
        }
    }
    #endregion

    /// <summary>
    /// パス解決を担当するクラス。キャッシュログも持つ。
    /// </summary>
    /// <remarks>
    /// Accesserと違って権限に制限されない代わりに、途中経過を外部に出力しない事を前提とする。
    /// 
    /// PathBaseDriveの処理が追加された際、共通で受け取るこっちにも追加する必要がある。
    /// </remarks>
    public static class PermissionScopeReachHistory{

        //アクセス検証を行わず即座に弾くパスのリスト。デバッグとかで使うかもしれない。
        private static readonly DriveItemPath[] Forbidden = [];


        //キャッシュされた情報置き場。非同期書き込みに対応できるConcurrentDictionary型。
        private static readonly ConcurrentDictionary<DriveTypeEnum, ConcurrentDictionary<DriveItemPath, CachedResult>> CachedResults = new();
        //キャッシュの更新。実データを取得した場合。
        public static void UpdateCache(DriveItemPath id, CachedNode data){
            // GetOrAddを使うことで、キーが存在しない場合は新しく生成してくれます
            var dict = CachedResults.GetOrAdd(id.DriveType, _ => new ConcurrentDictionary<DriveItemPath, CachedResult>());
            dict[id] = dict.TryGetValue(id, out var existing) ? existing.RemakeWithData(data) : new CachedResult().RemakeWithData(data);
        }
        //キャッシュの更新。エラーの場合。
        public static void UpdateCache(DriveItemPath id, CachedError error){
            var dict = CachedResults.GetOrAdd(id.DriveType, _ => new ConcurrentDictionary<DriveItemPath, CachedResult>());
            dict[id] = dict.TryGetValue(id, out var existing) ? existing.RemakeWithError(error) : new CachedResult().RemakeWithError(error);
        }
        //最終検証日時に基づくキャッシュの一斉削除。
        public static void ClearCacheByLastVerified(DateTimeOffset threshold) {
            foreach (var dict in CachedResults.Values){
                // ※ConcurrentDictionary は foreach で回している最中の要素削除が許可されているため、削除処理自体はシンプルになります。
                foreach (var (key,cache) in dict) {
                    if(cache.LastVerified < threshold) dict.Remove(key, out _);
                }
            }
        }
        //最終参照日時に基づくキャッシュの一斉削除。
        public static void ClearCacheByLastChecked(DateTimeOffset threshold) {
            foreach (var dict in CachedResults.Values){
                foreach (var (key, cache) in dict){
                    if (cache.LastChecked < threshold) dict.Remove(key, out _);
                }
            }
        }

        //キャッシュを参照して存在すれば最終参照時間を更新して返す基本処理。
        private static CachedResult? GetCachedResult(this DriveItemPath id){
            if (CachedResults.TryGetValue(id.DriveType, out var dict) && dict.TryGetValue(id, out var result)){
                var remaked = result.RemakeWithCheckedTime();
                dict[id] = remaked;
                return remaked;
            } else {
                return null;
            }
        }
        //指定のDriveTypeのキャッシュをyield returnしつつ、最終参照時間を更新する基本処理。
        private static IEnumerable<(DriveItemPath Id, CachedResult Result)> GenerateCachedResults(DriveTypeEnum driveType){
            if (CachedResults.TryGetValue(driveType, out var dict)){
                foreach (var (id, result) in dict){
                    var remaked = result.RemakeWithCheckedTime();
                    dict[id] = remaked;
                    yield return (id, remaked);
                }
            }
        }

        private static bool TryGetParentIds(this DriveItemPath id, out DriveItemPath[] parentIds){
            parentIds = [];
            if (id.GetCachedResult() is CachedResult result && result.Node is CachedNode node){
                foreach (var parent in node.ParentIds) {
                    if (parent != id) parentIds = parentIds.Append(parent).ToArray();
                }
                if (parentIds.Length > 0) return true;
                else return false;                
            }
            return false;
        }
        private static bool TryGetChildIds(this DriveItemPath parentId, out DriveItemPath[] childIds){
            childIds = [];
            foreach (var (id,result) in GenerateCachedResults(parentId.DriveType)){
                if (result.Node is CachedNode node && node.ParentIds.Contains(parentId)){
                    childIds = childIds.Append(id).ToArray();
                }
            }
            if (childIds.Length > 0) return true;
            else return false;
        }
        public static DriveItemPath[] GetCachedNodeIds(DriveTypeEnum driveType) =>
            CachedResults.TryGetValue(driveType, out var dict) ? dict.Keys.ToArray() : [];

        public static DriveItemPath[] GetCachedNodeParentIds(this DriveItemPath id) => id.TryGetParentIds(out var parentIds) ? parentIds : [];
        public static DriveItemPath[] GetCachedNodeChildIds(this DriveItemPath parentId) => parentId.TryGetChildIds(out var childIds) ? childIds : [];
        public static CachedResult?[] GetParentCachedResult(this DriveItemPath id) =>
            id.TryGetParentIds(out var parentIds) ? parentIds.Select(pid => pid.GetCachedResult()).ToArray() : [];
        public static CachedResult?[] GetChildCachedResult(this DriveItemPath parentId) => 
            parentId.TryGetChildIds(out var childIds) ? childIds.Select(cid => cid.GetCachedResult()).ToArray() : [];

        private static TraficDriveManager InnerAccesser { get; }= new TraficDriveManager(FileSystemPermissionBundle.Master);
        public static bool InCache(this DriveItemPath id) => CachedResults.TryGetValue(id.DriveType, out var dict) && dict.ContainsKey(id);
        /// <summary>
        /// 親Pathを返す。必要なら実アクセスもする。
        /// </summary>
        /// <remarks>
        /// デフォルトのCacheStrategyは、CacheStrategy.CACHE_FIRST(TimeSpan.FromMinutes(60))
        /// </remarks>
        /// <param name="path"></param>
        /// <param name="strategy"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static async ValueTask<DriveItemPath[]> Parents(this DriveItemPath path, CacheStrategy? strategy = null) {
            CacheStrategy cs;
            if (strategy == null) cs = CacheStrategy.CACHE_FIRST(TimeSpan.FromMinutes(60));
            else cs = strategy.Value;

            if (path is PathBaseDrivePath pathBaseDrivePath){
                return Path.GetDirectoryName(pathBaseDrivePath.Value) is string parentPath ? [(LocalDirectoryPath)parentPath!] : [];
            } else if (path is IdBaseDrivePath idBaseDrivepath){
                if (GetCachedResult(idBaseDrivepath) is CachedResult result && result.Node?.ParentIds is DriveItemPath[] ids) return ids;

                if (idBaseDrivepath is GoogleDrivePath googleDrivePath) {
                    var info = await InnerAccesser.GetItemInfoAsync(googleDrivePath, cs);
                    return info.Cache?.ParentIds ?? [];
                }else{
                    throw new ArgumentException($"定義されていないIdBaseDrivePathのサブクラス{idBaseDrivepath.GetType()}");
                }
            } else {
                throw new ArgumentException($"定義されていないDriveItemPathのサブクラス{path.GetType()}");
            }
        }
        //この辺は、GoogleDriveAccesser.ItemExistAsync()とかその辺をちゃんと作った後に改修する必要はある。
        public static async ValueTask<bool> Exists(this DriveItemPath path, CacheStrategy? strategy = null) {
            CacheStrategy cs;
            if (strategy == null) cs = CacheStrategy.CACHE_FIRST(TimeSpan.FromMinutes(60));
            else cs = strategy.Value;

            if (path is PathBaseDrivePath pathBaseDrivepath){
                return File.Exists(pathBaseDrivepath.Value);
            } else if (path is IdBaseDrivePath idBaseDrivepath){
                if (idBaseDrivepath is GoogleDrivePath googleDrivePath) {
                    return await InnerAccesser.ItemExistsAsync(googleDrivePath, cs);
                }else{
                    throw new ArgumentException($"定義されていないIdBaseDrivePathのサブクラス{idBaseDrivepath.GetType()}");
                }
            } else {
                throw new ArgumentException($"定義されていないDriveItemPathのサブクラス{path.GetType()}");
            }
        }
        public static async ValueTask<string> GetName(this DriveItemPath path, CacheStrategy? strategy = null) {
            CacheStrategy cs;
            if (strategy == null) cs = Config.CacheStrategy;
            else cs = strategy.Value;

            if (path is PathBaseDrivePath pathBaseDrivePath){
                return Path.GetFileName(pathBaseDrivePath.Value);
            }else if (path is IdBaseDrivePath idBaseDrivepath){
                if (GetCachedResult(idBaseDrivepath) is CachedResult result && result.Node?.Name is string name) return name;
                
                if (idBaseDrivepath is GoogleDrivePath googleDrivePath){
                    var info = await InnerAccesser.GetItemInfoAsync(googleDrivePath, cs);
                    return info.Name;
                }else{
                    throw new ArgumentException($"定義されていないIdBaseDrivePathのサブクラス{idBaseDrivepath.GetType()}");
                }
            }else{
                throw new ArgumentException($"定義されていないDriveItemPathのサブクラス{path.GetType()}");
            }
        }

        /// <summary>
        /// FileTypeの一致と、Pathを含んでいることが非nullの前提。
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="child"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        public static async ValueTask<int?> GetDepth(this DriveItemPath parent, DriveItemPath child) {
            if (parent.DriveType != child.DriveType) return null;
            if (parent == null || child == null) return null;
            if (parent == child) return 0;

            DriveItemPath[] current = [child];
            DriveItemPath[] next = [];
            int depth = 0;
            while (current != null) {
                foreach (var temp in current) {
                    foreach (var id in await temp.Parents()) {
                        if (id == parent){
                            return depth;
                        } else if (id != temp) {
                            next = [.. next, id];
                        }
                    }
                }
                if(next.Length == 0) break;
                current = next;
                next = [];
                depth++;
            }
            return null;
        }
        /// <summary>
        /// permissionがpathを指定のScope内に含むかどうかを返す。
        /// </summary>
        /// <remarks>
        /// PermissionScopeReachHistoryで定義している拡張メソッド。
        /// PermissionScopeReachHistoryの到達履歴を利用するため。
        /// </remarks>
        /// <param name="permission"></param>
        /// <param name="path"></param>
        /// <param name="scopeType"></param>
        public static async ValueTask<bool> IncludeItemPath(this FileSystemPermission permission, DriveItemPath path, PermissionScopeType scopeType){
            if (path.DriveType != permission.DriveType) return false;

            var scope = scopeType switch{
                PermissionScopeType.InformationScope => permission.InformationScope,
                PermissionScopeType.ItemCreateScope => permission.ItemCreateScope,
                PermissionScopeType.FileAccessScope => permission.FileAccessScope,
                _ => throw new ArgumentException($"定義されていないPermissionScopeType{scopeType}"),
            };
            var depth = await permission.Path.GetDepth(path);
            return depth is int d && scope.Include(d);
        }
    }


}

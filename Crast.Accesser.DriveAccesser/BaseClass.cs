

using System.Text;

/// <summary>
/// 各Drive用のクラスで継承するためのクラスをまとめたファイル。
/// </summary>


namespace Crast.Accesser.DriveAccesser{

    public abstract record DriveItemPath{
        public abstract string Value { get; init; }
        public abstract DriveTypeEnum DriveType { get; }
        //public abstract DriveItemPath[] Parents();
        //基本的にはパス文字列を扱ってファイル実体には触れないが、
        //DriveItemInfoを生成できるかどうかを確認するメソッドを
        //DriveItemInfoに実装するわけにもいかないので、こっちに置いておく。
        //public abstract bool Exists(bool force = false);
        //ParentsとExistsは、PermissionScopeReachHistoryの拡張メソッドに移行。
    }
    public interface IFilePath { }
    public interface IDirectoryPath { }
    public abstract record PathBaseDrivePath : DriveItemPath { }
    public abstract record IdBaseDrivePath : DriveItemPath { }


    /// <summary>
    /// Accesserの派生先で必要な追加引数をまとめて受け入れるための型。
    /// </summary>
    /// <remarks>
    /// ・オンラインストレージ用のCacheStrategy
    /// </remarks>
    public readonly record struct AccesserOption {
        public static readonly AccesserOption None = new();
        public static readonly AccesserOption Basic = new(Config.CacheStrategy);
        public readonly CacheStrategy? CacheStrategy { get; init; }

        public AccesserOption(CacheStrategy? cacheStrategy = null){
            CacheStrategy = cacheStrategy;
        }
        public static implicit operator AccesserOption(CacheStrategy? cacheStrategy) => new(cacheStrategy);
    }

    /// <summary>
    /// 個別権限に対応するAccesserの、非ジェネリックな共通インターフェイス。
    /// </summary>
    public interface IDriveAccesser : IDisposable{
        public FileSystemPermission? Permission { get; init; }
        #region Permissionのプロパティを直接呼び出せるようにするためのプロパティ群
        public DriveTypeEnum? DriveType => Permission?.DriveType;
        public DriveItemPath? Path => Permission?.Path;
        public PermissionScope? InformationScope => Permission?.InformationScope;
        public PermissionScope? ItemCreateScope => Permission?.ItemCreateScope;
        public PermissionScope? FileAccessScope => Permission?.FileAccessScope;
        public FileSystemAccessLevel? Level => Permission?.AccessLevel;
        public FileSystemType? FileType => Permission?.FileType;
        public bool IsDirectory => Permission != null && Permission.IsDirectory;
        public bool CanRead => Permission != null && Permission.CanRead;
        public bool CanAppend => Permission != null && Permission.CanAppend;
        public bool CanCreate => Permission != null && Permission.CanCreate;
        public bool CanDelete => Permission != null && Permission.CanDelete;
        public bool CanWrite => Permission != null && Permission.CanWrite;
        public bool IsEmpty { get; init; }
        #endregion

        // --- ドライブ ⇔ 変数 (JSON等で抽象化) ---
        // T型のデータを直接保存/読み込み。内部でStreamとシリアライザを回す
        public abstract Task SaveObjectAsync(IFilePath path, object data, AccesserOption option = default);
        public abstract Task<dataT?> LoadObjectAsync<dataT, noneT>(IFilePath path, AccesserOption option = default);
        public abstract Task SaveRawAsync(IFilePath path, ReadOnlyMemory<byte> data, AccesserOption option = default);  // wavなどのバイナリ用
        public abstract Task<byte[]> LoadRawAsync(IFilePath path, AccesserOption option = default);
        public abstract Task AppendRawAsync(IFilePath path, ReadOnlyMemory<byte> data, AccesserOption option = default);
        public abstract Task AppendTextAsync(IFilePath path, string text, bool withBreak = false, AccesserOption option = default);
        public abstract IAsyncEnumerable<string> ReadLinesAsync(IFilePath path, Encoding? encoding, AccesserOption option = default);
        public abstract Task SaveTextAsync(IFilePath path, string text, Encoding? encoding = null, AccesserOption option = default);
        public abstract Task<string> LoadTextAsync(IFilePath path, Encoding? encoding = null, AccesserOption option = default);


        // --- 拡張：ファイル管理 ---
        public abstract Task<IFilePath> CreateEmptyFile(IDirectoryPath path, string name, FileSystemType fileType, bool canWrite = false, AccesserOption option = default);
        public abstract Task DeleteFile(IFilePath path, AccesserOption option = default);
        public abstract Task<IDirectoryPath> CreateDirectory(IDirectoryPath path, string name, bool canWrite = false, AccesserOption option = default);
        public abstract Task DeleteDirectory(IDirectoryPath path, PermissionScope? scope = null, AccesserOption option = default);
        public abstract Task ClearDirectory(IDirectoryPath path, FileSystemType fileType = FileSystemType.All, bool recursive = false, AccesserOption option = default);
        public abstract Task<DriveItemInfo> GetItemInfo(DriveItemPath path, AccesserOption option = default);
        public abstract Task<bool> ItemExists(DriveItemPath path, AccesserOption option = default);
        public abstract Task<List<DriveItemInfo>> GetFileListAsync(
            IDirectoryPath path,
            FileSystemType fileType = FileSystemType.All,
            bool recursive = false,
            AccesserOption option = default
            );

        // --- ドライブ ⇔ ドライブ (内部転送) ---
        // 自身(Source)から別(Target)へデータを流し込む
        // 実装側で source.OpenStream -> target.SaveStream を行う
        public abstract Task TransferToAsync(IFilePath readPath, IDriveAccesser target, IFilePath targetPath, AccesserOption option = default);
        public abstract Task SaveStreamAsync(IFilePath path, Stream stream, AccesserOption option = default);

        // --- 内部用（実装クラスのみが意識する） ---
        // インターフェースのデフォルト実装や protected 的な扱いで定義
        protected abstract Task<Stream> OpenReadStreamAsync(IFilePath path, AccesserOption option = default);

    }
    internal abstract class SingleDriveAccesserGeneric<pathT> : IDriveAccesser
        where pathT : DriveItemPath
    {
        public FileSystemPermission? Permission { get; init; }
        #region Permissionのプロパティを直接呼び出せるようにするためのプロパティ群
        public DriveTypeEnum? DriveType => Permission?.DriveType;
        public DriveItemPath? Path => Permission?.Path;
        public PermissionScope? InformationScope => Permission?.InformationScope;
        public PermissionScope? ItemCreateScope => Permission?.ItemCreateScope;
        public PermissionScope? FileAccessScope => Permission?.FileAccessScope;
        public FileSystemAccessLevel? Level => Permission?.AccessLevel;
        public FileSystemType? FileType => Permission?.FileType;
        public bool IsDirectory => Permission != null && Permission.IsDirectory;
        public bool CanRead => Permission != null && Permission.CanRead;
        public bool CanAppend => Permission != null && Permission.CanAppend;
        public bool CanCreate => Permission != null && Permission.CanCreate;
        public bool CanDelete => Permission != null && Permission.CanDelete;
        public bool CanWrite => Permission != null && Permission.CanWrite;
        public bool IsEmpty { get; init; }
        #endregion
        //権限チェック時など、一旦空権限で生成すること自体は許容する。
        public SingleDriveAccesserGeneric(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true){
            if (permission.IsEmpty){
                if (!allowEmpty) throw new ArgumentException($"許可されていない空権限でのAccesser生成");
                IsEmpty = true;
                Permission = null;
            }else{
                IsEmpty = false;
                Permission = permission.AsSinglePermission(singleOnly);
                BasePath = Permission.Path;
            }
        }
        public virtual void Dispose() {}
        protected void CheckEmpty() { if (IsEmpty) throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない"); }
        protected DriveItemPath? BasePath = null;

        // 権限と存在を統合的にチェックする内部メソッド
        protected virtual async ValueTask ValidateAccess(pathT path, FileSystemAccessLevel requiredIfExist, FileSystemAccessLevel requiredIfNotExist){
            CheckEmpty();
            //pathを含まないなら権限も何もない
            if (! await Permission!.IncludeItemPath(path, PermissionScopeType.InformationScope)) throw new ArgumentException($"アクセス権限のないpathです: {path}");

            if (ItemExists(path)){
                // 対象ファイルが存在するならrequiredIfExist権限の確認
                if (requiredIfExist == FileSystemAccessLevel.None) throw new UnauthorizedAccessException($"存在する{path} の不在を前提とした操作です");
                if ((Permission!.AccessLevel & requiredIfExist) != requiredIfExist)
                    throw new UnauthorizedAccessException($"{path} に対する {requiredIfExist} 権限がありません。");
            }else{
                // 対象ファイルが存在しないならrequiredIfNotExist権限の確認
                if (requiredIfNotExist == FileSystemAccessLevel.None) throw new UnauthorizedAccessException($"存在しない{path} の存在を前提とした操作です");
                if ((Permission!.AccessLevel & requiredIfNotExist) != requiredIfNotExist)
                    throw new UnauthorizedAccessException($"{path} に対する {requiredIfNotExist} 権限がありません。");
            }
        }

        //ダブルディスパッチの前段をdynamicで踏みつぶす形で各メソッドを実装。
        //基底クラス→抽象クラスのインターフェイスの明示的実装側では、dynamicで踏みつぶす。
        //これにより、抽象クラス→実装クラス側では、ジェネリック型による交差型指定がそのまま静的解析で通る。
        //基底・抽象・実装の三段階で成立する戦術なので、トリプルディスパッチとでも呼ぶべきか。

        //1、UseDriveAccesserを継承した外部インスタンスの中で、返り値型IDriveAccesserで(例えば)LocalDriveAccesserが生成される。
        //この時、静的にはIDriveAccesserだが、実際のメモリ内ではLocalDriveAccesserである。
        //
        //2、対象のaccesserのメソッドの呼び出しを行う。
        //これは静的に行われるため、IDriveAccesser型に対して行われるが、対象は実際にはLocalDriveAccesser型であるため、順に祖先型に遡っていき、
        //SingleDriveAccesserGenericの時点で、IDriveAccesserの明示的実装に行きあたって止まる。
        //よって、インターフェイス内での宣言ではなく、抽象型の中で行われたインターフェイスの明示的実装の方が採用される。
        //この時点で、静的な引数の型チェックが行われる(引数型の異なる同名メソッドが存在する場合もあるため)。
        //
        //3、選択された実装に従って動的処理が実行される。
        //ダブルディスパッチ戦略に従って、まずは引数の動的型チェックを行う。
        //その後の(dynamic)thisは実際の型であるLocalDriveAccesserとして解決され、LocalDriveAccesserで実装された処理に移行する。
        //引数も、(dynamic) により、実際の型で受け渡される。
        //
        //結果、外部インスタンス側はIDriveAccesserを静的に見ているだけで、LocalDriveAccesserのメソッドを呼び出して実行することができている。
        //また、具体的実装を担保するLocalDriveAccesserは、LocalFilePathという強い型制約の中で実装を記述できる。
        //dynamicを扱うのは中継するSingleDriveAccesserGenericのみであるため、呼び出し側も実装側もdynamicによる型変換を意識する必要が無い。


        // --- ドライブ ⇔ 変数 (JSON等で抽象化) ---
        // T型のデータを直接保存/読み込み。内部でStreamとシリアライザを回す
        public abstract Task SaveObjectAsync<dataT, FileT>(FileT path, dataT data, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.SaveObjectAsync(IFilePath path, object data, AccesserOption option){
            if (path is pathT) await (Task)((dynamic)this).SaveObjectAsync((dynamic)path, data, option);
            else throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
        }
        public abstract Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task<dataT?> IDriveAccesser.LoadObjectAsync<dataT, noneT>(IFilePath path, AccesserOption option) where dataT : default{
            if (path is pathT){
                return await (Task<dataT?>)((dynamic)this).LoadObjectAsync<dataT>((dynamic)path, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task SaveRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default) where FileT : pathT, IFilePath;  // wavなどのバイナリ用
        async Task IDriveAccesser.SaveRawAsync(IFilePath path, ReadOnlyMemory<byte> data, AccesserOption option){
            if (path is pathT){
                await (Task)((dynamic)this).SaveRawAsync((dynamic)path, data, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task AppendRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.AppendRawAsync(IFilePath path, ReadOnlyMemory<byte> data, AccesserOption option){
            if (path is pathT){
                await (Task)((dynamic)this).AppendRawAsync((dynamic)path, data, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task<byte[]> LoadRawAsync<FileT>(FileT path, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task<byte[]> IDriveAccesser.LoadRawAsync(IFilePath path, AccesserOption option){
            if (path is pathT){
                return await (Task<byte[]>)((dynamic)this).LoadRawAsync((dynamic)path, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task AppendTextAsync<FileT>(FileT path, string text, bool withBreak = false, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.AppendTextAsync(IFilePath path, string text, bool withBreak, AccesserOption option){
            if (path is pathT){
                await (Task)((dynamic)this).AppendFileAsync((dynamic)path, text, withBreak, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract IAsyncEnumerable<string> ReadLinesAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default) where FileT : pathT, IFilePath;
        IAsyncEnumerable<string> IDriveAccesser.ReadLinesAsync(IFilePath path, Encoding? encoding, AccesserOption option){
            if (path is pathT p){
                return ((dynamic)this).ReadLinesAsync((dynamic)p, encoding, option);
            } else {
                throw new InvalidOperationException("パスの型不一致");
            }
        }
        public abstract Task SaveTextAsync<FileT>(FileT path, string text, Encoding? encoding = null, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.SaveTextAsync(IFilePath path, string text, Encoding? encoding, AccesserOption option){
            if (path is pathT){
                await ((dynamic)this).SaveTextAsync((dynamic)path, text, encoding, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task<string> LoadTextAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task<string> IDriveAccesser.LoadTextAsync(IFilePath path, Encoding? encoding, AccesserOption option){
            if (path is pathT){
                return await ((dynamic)this).LoadTextAsync((dynamic)path, encoding, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }


        // --- 拡張：ファイル管理 ---
        public abstract FileT CreateEmptyFile<FileT, DirectoryT>(DirectoryT path, string name, FileSystemType fileType = FileSystemType.All, bool canWrite = false, AccesserOption option = default) where DirectoryT : pathT, IDirectoryPath where FileT : pathT, IFilePath;
        async Task<IFilePath> IDriveAccesser.CreateEmptyFile(IDirectoryPath path, string name, FileSystemType fileType,bool canWrite, AccesserOption option){
            if (path is pathT){
                return await ((dynamic)this).CreateEmptyFile((dynamic)path, name, fileType, canWrite, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract void DeleteFile<FileT>(FileT path, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.DeleteFile(IFilePath path, AccesserOption option){
            if (path is pathT){
                await ((dynamic)this).DeleteFile((dynamic)path, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract DirectoryT CreateDirectory<DirectoryT>(DirectoryT path, string name, bool canWrite = false, AccesserOption option = default) where DirectoryT : pathT, IDirectoryPath;
        async Task<IDirectoryPath> IDriveAccesser.CreateDirectory(IDirectoryPath path, string name, bool canWrite, AccesserOption option){
            if (path is pathT){
                return await ((dynamic)this).CreateDirectory((dynamic)path, name, canWrite, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope? scope = null, AccesserOption option = default) where DirectoryT : pathT, IDirectoryPath;
        async Task IDriveAccesser.DeleteDirectory(IDirectoryPath path, PermissionScope? scope, AccesserOption option){
            if (path is pathT){
                await ((dynamic)this).DeleteDirectory((dynamic)path, scope, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract void ClearDirectory<DirectoryT>(DirectoryT path, FileSystemType fileType = FileSystemType.All, bool recursive = false, AccesserOption option = default) where DirectoryT : pathT, IDirectoryPath;
        async Task IDriveAccesser.ClearDirectory(IDirectoryPath path, FileSystemType fileType, bool recursive, AccesserOption option){
            if (path is pathT){
                await ((dynamic)this).ClearDirectory((dynamic)path, fileType, recursive, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }

        public abstract bool ItemExists(pathT path, AccesserOption option = default);
        async Task<bool> IDriveAccesser.ItemExists(DriveItemPath path, AccesserOption option){
            if (path is pathT){
                return await ((dynamic)this).ItemExists((dynamic)path, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract DriveItemInfo GetItemInfo(pathT path, AccesserOption option = default);
        async Task<DriveItemInfo> IDriveAccesser.GetItemInfo(DriveItemPath path, AccesserOption option){
            if (path is pathT){
                return await ((dynamic)this).GetItemInfo((dynamic)path, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(
            DirectoryT path,
            FileSystemType fileType = FileSystemType.All,
            bool recursive = false,
            AccesserOption option = default
            )
            where DirectoryT : pathT, IDirectoryPath;
        async Task<List<DriveItemInfo>> IDriveAccesser.GetFileListAsync(IDirectoryPath path, FileSystemType fileType, bool recursive, AccesserOption option){
            if (path is pathT){
                return await ((dynamic)this).GetFileListAsync((dynamic)path, fileType, recursive, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }

        // --- ドライブ ⇔ ドライブ (内部転送) ---
        // 自身(Source)から別(Target)へデータを流し込む
        // 実装側で source.OpenStream -> target.SaveStream を行う
        public abstract Task TransferToAsync<T0, T1, FileT>(FileT readpath, SingleDriveAccesserGeneric<T0> target, T1 targetPath, AccesserOption option = default)
            where FileT : pathT, IFilePath where T0 : DriveItemPath where T1 : T0, IFilePath;
        async Task IDriveAccesser.TransferToAsync(IFilePath readpath, IDriveAccesser target, IFilePath targetPath, AccesserOption option){
            if (readpath is pathT){
                await ((dynamic)this).TransferToAsync((dynamic)readpath, (dynamic)target, (dynamic)targetPath, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {readpath?.GetType().Name} または {targetPath?.GetType().Name}");
            }
        }
        public abstract Task SaveStreamAsync<FileT>(FileT path, Stream stream, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.SaveStreamAsync(IFilePath path, Stream stream, AccesserOption option){
            if (path is pathT){
                await ((dynamic)this).SaveStreamAsync((dynamic)path, stream, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }

        // --- 内部用（実装クラスのみが意識する） ---
        // インターフェースのデフォルト実装や protected 的な扱いで定義
        protected abstract Task<Stream> OpenReadStreamAsync<FileT>(FileT path, AccesserOption option = default) where FileT : pathT, IFilePath;
        async Task<Stream> IDriveAccesser.OpenReadStreamAsync(IFilePath path, AccesserOption option){
            if (path is pathT){
                return await ((dynamic)this).OpenReadStreamAsync((dynamic)path, option);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
    }
    internal class EmptyDriveAccesser : SingleDriveAccesserGeneric<DriveItemPath>{
        public EmptyDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true)
            : base(permission, allowEmpty, singleOnly)
        {
            if (!permission.IsEmpty) throw new ArgumentException("空でない権限でのEmptyDriveAccesser生成");
            if (!allowEmpty) throw new ArgumentException("空権限を許可しない状況下でのEmptyDriveAccesser生成");
        }
        //abstractメソッドを実装するが、全て例外を返すだけで起動はしない。
        public override DriveItemInfo GetItemInfo(DriveItemPath path, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(DirectoryT path, FileSystemType fileType = FileSystemType.All, bool recursive = false, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override bool ItemExists(DriveItemPath path, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override FileT CreateEmptyFile<FileT, DirectoryT>(DirectoryT path, string name, FileSystemType fileType, bool canWrite = false, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override void DeleteFile<FileT>(FileT path, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override DirectoryT CreateDirectory<DirectoryT>(DirectoryT path, string name, bool canWrite = false, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope? scope = null, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override void ClearDirectory<DirectoryT>(DirectoryT path, FileSystemType fileType = FileSystemType.All, bool recursive = false, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task AppendTextAsync<FileT>(FileT path, string text, bool withBreak = false, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveObjectAsync<T, FileT>(FileT path, T data, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task<T?> LoadObjectAsync<T, FileT>(FileT path, AccesserOption option = default) where T : default => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task<byte[]> LoadRawAsync<FileT>(FileT path, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        protected override Task<Stream> OpenReadStreamAsync<FileT>(FileT path, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveStreamAsync<FileT>(FileT path, Stream stream, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task TransferToAsync<T0, T1, FileT>(FileT readpath, SingleDriveAccesserGeneric<T0> target, T1 targetPath, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override IAsyncEnumerable<string> ReadLinesAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveTextAsync<FileT>(FileT path, string text, Encoding? encoding = null, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task<string> LoadTextAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task AppendRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
    }


}



using System.Text;

/// <summary>
/// 各Drive用のクラスで継承するためのクラスをまとめたファイル。
/// </summary>


namespace Crast.Accesser.DriveAccesser{

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



    /// <summary>
    /// 個別権限に対応するAccesserの、非ジェネリックな共通インターフェイス。
    /// </summary>
    public interface IDriveAccesser : IDisposable{
        public FileSystemPermission? Permission { get; init; }
        #region Permissionのプロパティを直接呼び出せるようにするためのプロパティ群
        public DriveTypeEnum? DriveType => Permission?.DriveType;
        public DriveItemPath? Path => Permission?.Path;
        public PermissionScope? Scope => Permission?.Scope;
        public FileSystemAccessLevel? Level => Permission?.AccessLevel;
        public FileSystemType? FileType => Permission?.FileType;
        public bool IsDirectory => Permission != null && Permission.IsDirectory;
        public bool CanRead => Permission != null && Permission.CanRead;
        public bool CanAppend => Permission != null && Permission.CanAppend;
        public bool CanCreate => Permission != null && Permission.CanCreate;
        public bool CanDelete => Permission != null && Permission.CanDelete;
        public bool CanWrite => Permission != null && Permission.CanWrite;
        #endregion

        public bool IsEmpty { get; init; }
        // --- ドライブ ⇔ 変数 (JSON等で抽象化) ---
        // T型のデータを直接保存/読み込み。内部でStreamとシリアライザを回す
        public abstract Task SaveObjectAsync(IFilePath path, object data);
        public abstract Task<dataT?> LoadObjectAsync<dataT, noneT>(IFilePath path);
        public abstract Task SaveRawAsync(IFilePath path, byte[] data);  // wavなどのバイナリ用
        public abstract Task<byte[]> LoadRawAsync(IFilePath path);
        public abstract Task AppendFileAsync(IFilePath path, string text, bool withBreak = false);
        public abstract IAsyncEnumerable<string> ReadLinesAsync(IFilePath path, Encoding? encoding);


        // --- 拡張：ファイル管理 ---
        public abstract Task<IFilePath> CreateEmptyFile(IDirectoryPath path, string name, bool canWrite = false);
        public abstract Task DeleteFile(IFilePath path);
        public abstract Task<IDirectoryPath> CreateDirectory(IDirectoryPath path, string name, bool canWrite = false);
        public abstract Task DeleteDirectory(IDirectoryPath path, PermissionScope scope = PermissionScope.SelfOnly);
        public abstract Task ClearDirectory(IDirectoryPath path, bool recursive = false);
        public abstract Task<DriveItemInfo> GetItemInfo(DriveItemPath path);
        public abstract Task<bool> ItemExists(DriveItemPath path);
        public abstract Task<List<DriveItemInfo>> GetFileListAsync(IDirectoryPath path, FileSystemAccessLevel requiredLevel = FileSystemAccessLevel.ReadOnly, bool recursive = false);

        // --- ドライブ ⇔ ドライブ (内部転送) ---
        // 自身(Source)から別(Target)へデータを流し込む
        // 実装側で source.OpenStream -> target.SaveStream を行う
        public abstract Task TransferToAsync(IFilePath readPath, IDriveAccesser target, IFilePath targetPath);
        public abstract Task SaveStreamAsync(IFilePath path, Stream stream);

        // --- 内部用（実装クラスのみが意識する） ---
        // インターフェースのデフォルト実装や protected 的な扱いで定義
        protected abstract Task<Stream> OpenReadStreamAsync(IFilePath path);

    }
    internal abstract class SingleDriveAccesserGeneric<pathT> : IDriveAccesser
        where pathT : DriveItemPath
    {
        public FileSystemPermission? Permission { get; init; }
        //Permission == nullの時に面倒なので、プロパティを一通り呼び出せるようにしておく
        public DriveTypeEnum? DriveType => Permission?.DriveType;
        public pathT? Path => Permission?.Path is pathT p ? p : null;//型が一致するなら代入、一致しなければfalseを返すC#のis演算子を条件式とする三項演算子
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
            if (permission.IsEmpty){
                if (!allowEmpty) throw new ArgumentException($"許可されていない空権限でのAccesser生成");
                IsEmpty = true;
                Permission = null;
            }else{
                IsEmpty = false;
                Permission = permission.AsSinglePermission(singleOnly);
            }
        }
        public bool IsEmpty { get; init; }
        public virtual void Dispose() {}
        protected void CheckEmpty() { if (IsEmpty) throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない"); }

        // 権限と存在を統合的にチェックする内部メソッド
        protected virtual void ValidateAccess(pathT path, FileSystemAccessLevel requiredIfExist, FileSystemAccessLevel requiredIfNotExist){
            CheckEmpty();
            //pathを含まないなら権限も何もない
            if (!Permission!.IncludeItemPath(path)) throw new ArgumentException($"アクセス権限のないpathです: {path}");

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
        public abstract Task SaveObjectAsync<dataT, FileT>(FileT path, dataT data) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.SaveObjectAsync(IFilePath path, object data){
            if (path is pathT){
                await (Task)((dynamic)this).SaveObjectAsync((dynamic)path, data);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path) where FileT : pathT, IFilePath;
        async Task<dataT?> IDriveAccesser.LoadObjectAsync<dataT, noneT>(IFilePath path) where dataT : default{
            if (path is pathT){
                return await (Task<dataT?>)((dynamic)this).LoadObjectAsync<dataT>((dynamic)path);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task SaveRawAsync<FileT>(FileT path, byte[] data) where FileT : pathT, IFilePath;  // wavなどのバイナリ用
        async Task IDriveAccesser.SaveRawAsync(IFilePath path, byte[] data){
            if (path is pathT){
                await (Task)((dynamic)this).SaveRawAsync((dynamic)path, data);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task<byte[]> LoadRawAsync<FileT>(FileT path) where FileT : pathT, IFilePath;
        async Task<byte[]> IDriveAccesser.LoadRawAsync(IFilePath path){
            if (path is pathT){
                return await (Task<byte[]>)((dynamic)this).LoadRawAsync((dynamic)path);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task AppendFileAsync<FileT>(FileT path, string text, bool withBreak = false) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.AppendFileAsync(IFilePath path, string text, bool withBreak = false){
            if (path is pathT){
                await (Task)((dynamic)this).AppendFileAsync((dynamic)path, text, withBreak);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract IAsyncEnumerable<string> ReadLinesAsync<FileT>(FileT path, Encoding? encoding = null) where FileT : pathT, IFilePath;
        IAsyncEnumerable<string> IDriveAccesser.ReadLinesAsync(IFilePath path, Encoding? encoding){
            if (path is pathT p){
                return ((dynamic)this).ReadLinesAsync((dynamic)p, encoding);
            } else {
                throw new InvalidOperationException("パスの型不一致");
            }
        }


        // --- 拡張：ファイル管理 ---
        public abstract FileT CreateEmptyFile<FileT, DirectoryT>(DirectoryT path, string name, bool canWrite = false) where DirectoryT : pathT, IDirectoryPath where FileT : pathT, IFilePath;
        async Task<IFilePath> IDriveAccesser.CreateEmptyFile(IDirectoryPath path, string name, bool canWrite = false){
            if (path is pathT){
                return await ((dynamic)this).CreateEmptyFile((dynamic)path, name, canWrite);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract void DeleteFile<FileT>(FileT path) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.DeleteFile(IFilePath path){
            if (path is pathT){
                await ((dynamic)this).DeleteFile((dynamic)path);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract DirectoryT CreateDirectory<DirectoryT>(DirectoryT path, string name, bool canWrite = false) where DirectoryT : pathT, IDirectoryPath;
        async Task<IDirectoryPath> IDriveAccesser.CreateDirectory(IDirectoryPath path, string name, bool canWrite = false){
            if (path is pathT){
                return await ((dynamic)this).CreateDirectory((dynamic)path, name, canWrite);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope scope = PermissionScope.SelfOnly) where DirectoryT : pathT, IDirectoryPath;
        async Task IDriveAccesser.DeleteDirectory(IDirectoryPath path, PermissionScope scope){
            if (path is pathT){
                await ((dynamic)this).DeleteDirectory((dynamic)path, scope);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract void ClearDirectory<DirectoryT>(DirectoryT path, bool recursive = false) where DirectoryT : pathT, IDirectoryPath;
        async Task IDriveAccesser.ClearDirectory(IDirectoryPath path, bool recursive){
            if (path is pathT){
                await ((dynamic)this).ClearDirectory((dynamic)path, recursive);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract DriveItemInfo GetItemInfo(pathT path);
        async Task<DriveItemInfo> IDriveAccesser.GetItemInfo(DriveItemPath path){
            if (path is pathT){
                return await ((dynamic)this).GetItemInfo((dynamic)path);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract bool ItemExists(pathT path);
        async Task<bool> IDriveAccesser.ItemExists(DriveItemPath path){
            if (path is pathT){
                return await ((dynamic)this).ItemExists((dynamic)path);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
        public abstract Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(DirectoryT path, FileSystemAccessLevel requiredLevel = FileSystemAccessLevel.ReadOnly, bool recursive = false)
            where DirectoryT : pathT, IDirectoryPath;
        async Task<List<DriveItemInfo>> IDriveAccesser.GetFileListAsync(IDirectoryPath path, FileSystemAccessLevel requiredLevel, bool recursive){
            if (path is pathT){
                return await ((dynamic)this).GetFileListAsync((dynamic)path, requiredLevel, recursive);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }

        // --- ドライブ ⇔ ドライブ (内部転送) ---
        // 自身(Source)から別(Target)へデータを流し込む
        // 実装側で source.OpenStream -> target.SaveStream を行う
        public abstract Task TransferToAsync<T0, T1, FileT>(FileT readpath, SingleDriveAccesserGeneric<T0> target, T1 targetPath)
            where FileT : pathT, IFilePath where T0 : DriveItemPath where T1 : T0, IFilePath;
        async Task IDriveAccesser.TransferToAsync(IFilePath readpath, IDriveAccesser target, IFilePath targetPath){
            if (readpath is pathT){
                await ((dynamic)this).TransferToAsync((dynamic)readpath, (dynamic)target, (dynamic)targetPath);
            }else{
                throw new ArgumentException($"不適切なパス型: {readpath?.GetType().Name} または {targetPath?.GetType().Name}");
            }
        }
        public abstract Task SaveStreamAsync<FileT>(FileT path, Stream stream) where FileT : pathT, IFilePath;
        async Task IDriveAccesser.SaveStreamAsync(IFilePath path, Stream stream){
            if (path is pathT){
                await ((dynamic)this).SaveStreamAsync((dynamic)path, stream);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }

        // --- 内部用（実装クラスのみが意識する） ---
        // インターフェースのデフォルト実装や protected 的な扱いで定義
        protected abstract Task<Stream> OpenReadStreamAsync<FileT>(FileT path) where FileT : pathT, IFilePath;
        async Task<Stream> IDriveAccesser.OpenReadStreamAsync(IFilePath path){
            if (path is pathT){
                return await ((dynamic)this).OpenReadStreamAsync((dynamic)path);
            }else{
                throw new ArgumentException($"不適切なパス型: {path?.GetType().Name}");
            }
        }
    }
    internal class EmptyDriveAccesser : SingleDriveAccesserGeneric<DriveItemPath>
    {
        public EmptyDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true)
            : base(permission, allowEmpty, singleOnly)
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
        public override Task SaveObjectAsync<T, FileT>(FileT path, T data) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task<T?> LoadObjectAsync<T, FileT>(FileT path) where T : default => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveRawAsync<FileT>(FileT path, byte[] data) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task<byte[]> LoadRawAsync<FileT>(FileT path) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        protected override Task<Stream> OpenReadStreamAsync<FileT>(FileT path) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveStreamAsync<FileT>(FileT path, Stream stream) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task TransferToAsync<T0, T1, FileT>(FileT readpath, SingleDriveAccesserGeneric<T0> target, T1 targetPath) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");

    }


}

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
        protected void CheckEmpty() { if (IsEmpty) throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない"); }

        // 権限と存在を統合的にチェックする内部メソッド
        protected virtual void ValidateAccess(T path, FileSystemAccessLevel requiredIfExist, FileSystemAccessLevel requiredIfNotExist){
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
        // --- ドライブ ⇔ 変数 (JSON等で抽象化) ---
        // T型のデータを直接保存/読み込み。内部でStreamとシリアライザを回す
        public abstract Task SaveObjectAsync<dataT, FileT>(FileT path, dataT data) where FileT : T, IFilePath;
        public abstract Task SaveRawAsync<FileT>(FileT path, byte[] data) where FileT : T, IFilePath;  // wavなどのバイナリ用
        public abstract Task AppendFileAsync<FileT>(FileT path, string text, bool withBreak = false) where FileT : T, IFilePath;
        public abstract Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path) where FileT : T, IFilePath;

        // --- 拡張：ファイル管理 ---
        public abstract FileT CreateEmptyFile<FileT, DirectoryT>(DirectoryT path, string name, bool canWrite = false) where DirectoryT : T, IDirectoryPath where FileT : T, IFilePath;
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
    public class EmptyDriveAccesser : SingleDriveAccesserGeneric<DriveItemPath>
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
        protected override Task<Stream> OpenReadStreamAsync<FileT>(FileT path) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task SaveStreamAsync<FileT>(FileT path, Stream stream) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
        public override Task TransferToAsync<T0, T1, FileT>(FileT readpath, SingleDriveAccesserGeneric<T0> target, T1 targetPath) => throw new UnauthorizedAccessException($"空権限Accesserであるため、メソッドを起動できない");
    }


}

using System.Text;

namespace Crast.Accesser.DriveAccesser{

    public abstract record GoogleDrivePath : IdBaseDrivePath{
        public override string Value { get; init; }
        public override DriveTypeEnum DriveType => DriveTypeEnum.GoogleDrive;
        public GoogleDrivePath(string id){
            CheckId(id);
            Value = id;
        }
        protected static bool CheckId(string id){
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID cannot be empty");

            // 簡易バリデーション：Base64URLで使われない記号（/, \, ., @など）が含まれていないか
            // ファイルIDにドットやスラッシュは含まれません
            if (id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                throw new ArgumentException($"Invalid Google Drive ID format: {id}");

            return true;
        }
        //Parents(),Exists(),GetName()に関してはPermissionScopeReachHistoryの拡張メソッドに依存する。
    }
    public sealed record GoogleFilePath : GoogleDrivePath, IFilePath{
        public static implicit operator GoogleFilePath(string path) => new(path);
        public GoogleFilePath(string id) : base(id) { }
    }
    public sealed record GoogleDirectoryPath : GoogleDrivePath, IDirectoryPath{
        public static implicit operator GoogleDirectoryPath(string path) => new(path);
        public GoogleDirectoryPath(string id) : base(id) { }
    }

    //現状、ダミー実装のみ。
    internal sealed class GoogleDriveAccesser : SingleDriveAccesserGeneric<GoogleDrivePath>
    {
        public GoogleDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true) : base(permission, allowEmpty, singleOnly)
        {
        }

        public override Task AppendRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task AppendTextAsync<FileT>(FileT path, string text, bool withBreak = false, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task ClearDirectoryAsync<DirectoryT>(DirectoryT path, FileSystemType fileType = FileSystemType.All, PermissionScope? scope = null, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task<DirectoryT> CreateDirectoryAsync<DirectoryT>(DirectoryT path, string name, bool canWrite = false, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task<FileT> CreateEmptyFileAsync<FileT, DirectoryT>(DirectoryT path, string name, FileSystemType fileType = FileSystemType.All, bool canWrite = false, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task DeleteDirectoryAsync<DirectoryT>(DirectoryT path, PermissionScope? scope = null, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task DeleteFileAsync<FileT>(FileT path, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override IAsyncEnumerable<DriveItemInfo> GetFileListAsync<DirectoryT>(DirectoryT path, FileSystemType fileType = FileSystemType.All, PermissionScope? scope = null, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<DriveItemInfo> GetItemInfoAsync(GoogleDrivePath path, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<bool> ItemExistsAsync(GoogleDrivePath path, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path, AccesserOption option = default) where dataT : default
        {
            throw new NotImplementedException();
        }

        public override Task<byte[]> LoadRawAsync<FileT>(FileT path, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task<string> LoadTextAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override IAsyncEnumerable<string> ReadLinesAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task SaveObjectAsync<dataT, FileT>(FileT path, dataT data, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task SaveRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task SaveStreamAsync<FileT>(FileT path, Stream stream, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task SaveTextAsync<FileT>(FileT path, string text, Encoding? encoding = null, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        public override Task TransferToAsync<T0, T1, FileT>(FileT readpath, SingleDriveAccesserGeneric<T0> target, T1 targetPath, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }

        protected override Task<Stream> OpenReadStreamAsync<FileT>(FileT path, AccesserOption option = default)
        {
            throw new NotImplementedException();
        }
    }



}

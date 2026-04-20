using System.Text;

namespace Crast.Accesser.DriveAccesser{

    public abstract record GoogleDrivePath : DriveItemPath{
        public override string Value { get; init; }
        public override DriveTypeEnum DriveType => DriveTypeEnum.GoogleDrive;
        public GoogleDrivePath(string id){
            CheckId(id);
            Value = id;
        }
        public override GoogleDirectoryPath? Parent => this.InBank() ? this.FromBank()?.ParentId : null;
        public GoogleDirectoryPath? GetParent(bool force) => this.InBank() ? this.FromBank(force)?.ParentId : null;
        public string? GetName(bool force) => this.InBank() ? this.FromBank(force)?.Name : null;
        protected static bool CheckId(string id){
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
            var accsser = new GoogleDriveAccesser(FileSystemPermissionBundle.Master.NarrowPath(this), singleOnly : false);
            return accsser.ItemExists(this);
        }
    }
    public sealed record GoogleFilePath : GoogleDrivePath, IFilePath{
        public static implicit operator GoogleFilePath(string path) => new(path);
        public GoogleFilePath(string id) : base(id) { }
    }
    public sealed record GoogleDirectoryPath : GoogleDrivePath, IDirectoryPath{
        public static implicit operator GoogleDirectoryPath(string path) => new(path);
        public GoogleDirectoryPath(string id) : base(id) { }
    }

    /// <summary>
    /// GoogleDriveのAPI消費を抑えるためのメタデータのキャッシュに使う型。
    /// </summary>
    public record GoogleDriveMetadata(
        GoogleDrivePath Id,
        string Name,
        FileSystemType Type,
        long? Size,
        GoogleDirectoryPath? ParentId = null,
        string? ETag = null
    )
    {
        public string? MimeType => Type.ToMimeType();
        public bool IsDirectory => Type == FileSystemType.Directory;
    }
    internal static class GoogleDriveMetaDataBank{
        private static readonly Dictionary<GoogleDrivePath, GoogleDriveMetadata> B = [];
        public static void Add(this GoogleDriveMetadata metadata){
            if (B.ContainsKey(metadata.Id)) throw new ArgumentException($"このIDは既に存在する{metadata}");
            B.Add(metadata.Id, metadata);
        }
        public static void Delete(this GoogleDriveMetadata metadata){
            if (!B.ContainsKey(metadata.Id)) throw new ArgumentException($"このIDは存在しない{metadata}");
            B.Remove(metadata.Id);
        }
        public static void Update(this GoogleDriveMetadata metadata){
            if (!B.ContainsKey(metadata.Id)) throw new ArgumentException($"このIDは存在しない{metadata}");
            B[metadata.Id] = metadata;
        }
        public static GoogleDriveMetadata? FromBank(this GoogleDrivePath path, bool force = false){
            if (B.TryGetValue(path, out var data)) return data;
            if (force){
                var accsser = new GoogleDriveAccesser(FileSystemPermissionBundle.Master.NarrowPath(path), singleOnly : false);
                if (!accsser.ItemExists(path)) return null;
                data = accsser.GetItemInfo(path).Metadata!;
                Add(data);
                return data;
            }
            throw new ArgumentException($"このIDはキャッシュに存在しない{path.Value}");
        }
        public static bool InBank(this GoogleDrivePath id){
            return B.ContainsKey(id);
        }
    }


    //現状、ダミー実装のみ。
    internal class GoogleDriveAccesser : SingleDriveAccesserGeneric<GoogleDrivePath>
    {
        public GoogleDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true) : base(permission, allowEmpty, singleOnly)
        {
        }

        public override Task AppendRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data)
        {
            throw new NotImplementedException();
        }

        public override Task AppendTextAsync<FileT>(FileT path, string text, bool withBreak = false)
        {
            throw new NotImplementedException();
        }

        public override void ClearDirectory<DirectoryT>(DirectoryT path, FileSystemType fileType = FileSystemType.All, bool recursive = false)
        {
            throw new NotImplementedException();
        }

        public override DirectoryT CreateDirectory<DirectoryT>(DirectoryT path, string name, bool canWrite = false)
        {
            throw new NotImplementedException();
        }

        public override FileT CreateEmptyFile<FileT, DirectoryT>(DirectoryT path, string name, FileSystemType fileType, bool canWrite = false)
        {
            throw new NotImplementedException();
        }

        public override void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope scope = PermissionScope.SelfOnly)
        {
            throw new NotImplementedException();
        }

        public override void DeleteFile<FileT>(FileT path)
        {
            throw new NotImplementedException();
        }

        public override Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(
            DirectoryT path,
            FileSystemType fileType = FileSystemType.All,
            bool recursive = false
        ){
            throw new NotImplementedException();
        }

        public override DriveItemInfo GetItemInfo(GoogleDrivePath path)
        {
            throw new NotImplementedException();
        }

        public override bool ItemExists(GoogleDrivePath path)
        {
            throw new NotImplementedException();
        }

        public override Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path) where dataT : default
        {
            throw new NotImplementedException();
        }

        public override Task<byte[]> LoadRawAsync<FileT>(FileT path)
        {
            throw new NotImplementedException();
        }

        public override Task<string> LoadTextAsync<FileT>(FileT path, Encoding? encoding = null)
        {
            throw new NotImplementedException();
        }

        public override IAsyncEnumerable<string> ReadLinesAsync<FileT>(FileT path, Encoding? encoding = null)
        {
            throw new NotImplementedException();
        }

        public override Task SaveObjectAsync<dataT, FileT>(FileT path, dataT data)
        {
            throw new NotImplementedException();
        }

        public override Task SaveRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data)
        {
            throw new NotImplementedException();
        }

        public override Task SaveStreamAsync<FileT>(FileT path, Stream stream)
        {
            throw new NotImplementedException();
        }

        public override Task SaveTextAsync<FileT>(FileT path, string text, Encoding? encoding = null)
        {
            throw new NotImplementedException();
        }

        public override Task TransferToAsync<T0, T1, FileT>(FileT readpath, SingleDriveAccesserGeneric<T0> target, T1 targetPath)
        {
            throw new NotImplementedException();
        }

        protected override Task<Stream> OpenReadStreamAsync<FileT>(FileT path)
        {
            throw new NotImplementedException();
        }
    }



}

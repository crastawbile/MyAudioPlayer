namespace Crast.Accesser.DriveAccesser{

    public abstract record GoogleDrivePath : DriveItemPath{
        public override string Value { get; init; }
        public override DriveTypeEnum DriveType => DriveTypeEnum.GoogleDrive;
        public GoogleDrivePath(string id){
            CheckId(id);
            Value = id;
        }
        public override GoogleDirectoryPath? Parent => this.InBank() ? (GoogleDirectoryPath?)this.FromBank().ParentId! : null;
        protected bool CheckId(string id){
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
    public record GoogleFilePath : GoogleDrivePath, IFilePath{
        public static implicit operator GoogleFilePath(string path) => new(path);
        public GoogleFilePath(string id) : base(id) { }
    }
    public record GoogleDirectoryPath : GoogleDrivePath, IDirectoryPath{
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
        GoogleDrivePath? ParentId = null,
        string? ETag = null
    )
    {
        public string? MimeType => Type.ToMimeType();
        public bool IsDirectory => Type == FileSystemType.Directory;
    }
    public static class GoogleDriveMetaDataBank{
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
                var accsser = new GoogleDriveAccesser(FileSystemPermissionBundle.Master.NarrowPath(path), singleOnly = false);
                if (!accsser.ItemExists(path)) return null;
                data = accsser.Metadata;
                Add(data);
                return data;
            }
            throw new ArgumentException($"このIDはキャッシュに存在しない{path.Value}");
        }
        public static bool InBank(this GoogleDrivePath id){
            return B.ContainsKey(id);
        }
    }



}

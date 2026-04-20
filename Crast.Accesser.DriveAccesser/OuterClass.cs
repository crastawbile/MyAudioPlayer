using System.Security;
using System.Text;

namespace Crast.Accesser.DriveAccesser{



    /// <summary>
    /// FolderPermissionを利用したaccesser呼び出しを行うクラスの基底クラス
    /// </summary>
    /// <remarks>
    /// フォルダ名すら隠蔽する前提。
    /// 内部で必要なaccesserはフィールドに入れて便利に使おう。
    /// </remarks>
    public sealed class SolidDrivemanager{
        private readonly Dictionary<string, IDriveAccesser> _Accessers = [];
        public SolidDrivemanager(Dictionary<string, FileSystemPermissionBundle> permissions){
            //コンストラクタで、扱うAccesserを生成・保持する。他のAccesserは一切扱わない。
            foreach (var (name, permission) in permissions){
                var p = permission.AsSinglePermission(true);
                if (p.DriveType == DriveTypeEnum.LocalDrive) { _Accessers.Add(name, new LocalDriveAccesser(permission)); }
                else if (p.DriveType == DriveTypeEnum.GoogleDrive) { _Accessers.Add(name, new GoogleDriveAccesser(permission)); }
                else { throw new ArgumentException($"定義されていないドライブへのアクセス要求{permission}"); }
            }
        }
        private IDriveAccesser GetSolidAccesser(string name){
            if (!_Accessers.TryGetValue(name, out var accesser)) throw new ArgumentException($"存在しないaccesserの呼び出し{name}");
            return accesser;
        }
        public async Task<IFilePath> CreateEmptyFile<FileT>(string accesserName, FileT path, FileSystemType fileType, string fileName, bool canWrite = false)
            where FileT : DriveItemPath, IDirectoryPath
        {
            return await GetSolidAccesser(accesserName).CreateEmptyFile(path, fileName,fileType, canWrite);
        }
        public async Task DeleteFile<FileT>(string accesserName, FileT path)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).DeleteFile(path);
        }

        public async Task<IDirectoryPath> CreateDirectory<DirectoryT>(string accesserName, DirectoryT path, string name)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            return await GetSolidAccesser(accesserName).CreateDirectory(path, name);
        }
        public async Task DeleteDirectory<DirectoryT>(string accesserName, DirectoryT path, PermissionScope scope = PermissionScope.SelfOnly)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            await GetSolidAccesser(accesserName).DeleteDirectory(path, scope);
        }
        public async Task ClearDirectory<DirectoryT>(string accesserName, DirectoryT path, FileSystemType fileType = FileSystemType.All, bool recursive = false)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            await GetSolidAccesser(accesserName).ClearDirectory(path, fileType, recursive);
        }

        public async Task<DriveItemInfo> GetItemInfo(string accesserName, DriveItemPath path)
        {
            return await GetSolidAccesser(accesserName).GetItemInfo(path);
        }
        public async Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(
            string accesserName,
            DirectoryT path,
            FileSystemType fileType = FileSystemType.All,
            bool recursive = false
        )
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            return await GetSolidAccesser(accesserName).GetFileListAsync(path, fileType, recursive);
        }

        public async Task SaveObjectAsync<FileT>(string accesserName, FileT path, object data)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).SaveObjectAsync(path, data);
        }
        public async Task<dataT?> LoadObjectAsync<dataT, FileT>(string accesserName, FileT path)
            where FileT : DriveItemPath, IFilePath
        {
            return await GetSolidAccesser(accesserName).LoadObjectAsync<dataT, FileT>(path);
        }
        public async Task SaveRawAsync<FileT>(string accesserName, FileT path, ReadOnlyMemory<byte> data)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).SaveRawAsync(path, data);
        }
        public async Task AppendRawAsync<FileT>(string accesserName, FileT path, ReadOnlyMemory<byte> data)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).AppendRawAsync(path, data);
        }

        public async Task<byte[]> LoadRawAsync<FileT>(string accesserName, FileT path)
            where FileT : DriveItemPath, IFilePath
        {
            return await GetSolidAccesser(accesserName).LoadRawAsync(path);
        }
        public async Task SaveTextAsync<FileT>(string accesserName, FileT path, string text, Encoding? encoding = null)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).SaveTextAsync(path, text, encoding);
        }
        public async Task<string> LoadTextAsync<FileT>(string accesserName, FileT path, Encoding? encoding = null)
            where FileT : DriveItemPath, IFilePath
        {
            return await GetSolidAccesser(accesserName).LoadTextAsync(path, encoding);
        }

        public async Task AppendTextAsync<FileT>(string accesserName, FileT path, string text, bool withBreak = false)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).AppendTextAsync(path, text, withBreak);
        }
        public IAsyncEnumerable<string> ReadLinesAsync<FileT>(string accesserName, FileT path, Encoding? encoding = null)
            where FileT : DriveItemPath, IFilePath
        {
            return GetSolidAccesser(accesserName).ReadLinesAsync(path, encoding);
        }
        public async Task TransferToAsync<FileT1, FileT2>(string readerName, FileT1 readPath, string targetName, FileT2 targetPath)
            where FileT1 : DriveItemPath, IFilePath where FileT2 : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(readerName).TransferToAsync(readPath, GetSolidAccesser(targetName), targetPath);
        }

    }
    /// <summary>
    /// 複数のフォルダに対する権限を持ったDriveAccesser
    /// </summary>
    /// <remarks>
    /// 多数のフォルダを管理するクラスの基底に使う。
    /// </remarks>
    public sealed class TraficDriveManager{
        public FileSystemPermissionBundle Permissions { get; init; }
        public TraficDriveManager(FileSystemPermissionBundle permissions){
            Permissions = permissions;
        }

        //個別権限の使い捨てaccesserを生成する
        private IDriveAccesser GetTemporaryAccesser(
            DriveItemPath path,
            FileSystemType fileType,
            FileSystemAccessLevel requiredIfExist,
            FileSystemAccessLevel requiredIfNotExist
        )
        {
            FileSystemAccessLevel level;
            if (requiredIfNotExist == FileSystemAccessLevel.None || path.Exists(true)){
                level = requiredIfExist;
            }else{
                level = requiredIfNotExist;
            }
            var permission = Permissions
                .Narrow(path,fileType,level)
                .MergeAccessLevel()
                ;
            if (permission.IsEmpty) throw new ArgumentException($"許可されていないアクセスです: {path}   {Permissions}");
            var p = permission.AsSinglePermission();
            if (p.DriveType == DriveTypeEnum.LocalDrive) { return new LocalDriveAccesser(permission); }
            else if (p.DriveType == DriveTypeEnum.GoogleDrive) { return new GoogleDriveAccesser(permission); }
            throw new ArgumentException($"定義されていないドライブへのアクセス要求{permission}");
        }
        public async Task<IFilePath> CreateEmptyFile<FileT>(FileT path, string fileName, FileSystemType fileType, bool canWrite = false)
        where FileT : DriveItemPath, IDirectoryPath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: fileType,
                requiredIfExist: canWrite ? FileSystemAccessLevel.WriteOnly : FileSystemAccessLevel.None,
                requiredIfNotExist: FileSystemAccessLevel.CreateOnly
            );
            return await accesser.CreateEmptyFile(path, fileName,fileType, canWrite);
        }
        public async Task DeleteFile<FileT>(FileT path, FileSystemType fileType)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: fileType,
                requiredIfExist: FileSystemAccessLevel.DeleteOnly,
                requiredIfNotExist: FileSystemAccessLevel.All
            );
            await accesser.DeleteFile(path);
        }

        public async Task<IDirectoryPath> CreateDirectory<DirectoryT>(DirectoryT path, string name)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.All,
                requiredIfNotExist: FileSystemAccessLevel.CreateOnly
            );
            return await accesser.CreateDirectory(path, name);
        }
        public async Task DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope scope = PermissionScope.SelfOnly)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.ReadDelete,
                requiredIfNotExist: FileSystemAccessLevel.All
            );
            await accesser.DeleteDirectory(path, scope);
        }
        public async Task ClearDirectory<DirectoryT>(DirectoryT path, FileSystemType fileType = FileSystemType.All, bool recursive = false)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.ReadDelete,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            await accesser.ClearDirectory(path, fileType, recursive);
        }

        public async Task<DriveItemInfo> GetItemInfo(DriveItemPath path){
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            return await accesser.GetItemInfo(path);
        }
        public async Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(
            DirectoryT path,
            FileSystemType fileType = FileSystemType.All,
            FileSystemAccessLevel requiredLevel = FileSystemAccessLevel.All,
            bool recursive = false
        )
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: requiredLevel,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            return await accesser.GetFileListAsync(path, fileType, recursive);
        }

        public async Task SaveObjectAsync<FileT>(FileT path, object data)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.WriteOnly,
                requiredIfNotExist: FileSystemAccessLevel.WriteCreate
            );
            await accesser.SaveObjectAsync(path, data);
        }
        public async Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            return await accesser.LoadObjectAsync<dataT, FileT>(path);
        }
        public async Task SaveRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.WriteOnly,
                requiredIfNotExist: FileSystemAccessLevel.WriteCreate
            );
            await accesser.SaveRawAsync(path, data);
        }
        public async Task AppendRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.AppendOnly,
                requiredIfNotExist: FileSystemAccessLevel.AppendCreate
            );
            await accesser.AppendRawAsync(path, data);
        }

        public async Task<byte[]> LoadRawAsync<FileT>(FileT path)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            return await accesser.LoadRawAsync(path);
        }
        public async Task SaveTextAsync<FileT>(FileT path, string text, Encoding? encoding = null)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.WriteOnly,
                requiredIfNotExist: FileSystemAccessLevel.WriteCreate
            );
            await accesser.SaveTextAsync(path,text,encoding);
        }
        public async Task<string> LoadTextAsync<FileT>(FileT path, Encoding? encoding = null)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            return await accesser.LoadTextAsync(path, encoding);
        }

        public async Task AppendTextAsync<FileT>(FileT path, string text, bool withBreak = false)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemTypeManager.Text,
                requiredIfExist: FileSystemAccessLevel.AppendOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            await accesser.AppendTextAsync(path, text, withBreak);
        }
        public IAsyncEnumerable<string> ReadLinesAsync<FileT>(FileT path, Encoding? encoding = null)
            where FileT : DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemTypeManager.Text,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            return accesser.ReadLinesAsync(path,encoding);
        }
        public async Task TransferToAsync<FileT1,FileT2>(FileT1 readPath, IDriveAccesser target, FileT2 targetPath)
            where FileT1 : DriveItemPath, IFilePath where FileT2 : DriveItemPath, IFilePath
        {
            using var reader = GetTemporaryAccesser(
                path: readPath,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            await reader.TransferToAsync(readPath, target, targetPath);
        }
    }




}

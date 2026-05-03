using System.Security;
using System.Text;

namespace Crast.Accesser.DriveAccesser{



    /// <summary>
    /// 既定のAccesser呼び出しを行うクラス
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

        #region メタデータ取得
        public async ValueTask<bool> ItemExistsAsync(string accesserName, DriveItemPath path, AccesserOption option = default){
            return await GetSolidAccesser(accesserName).ItemExistsAsync(path,option);
        }
        public async ValueTask<DriveItemInfo> GetItemInfoAsync(string accesserName, DriveItemPath path, AccesserOption option = default){
            return await GetSolidAccesser(accesserName).GetItemInfoAsync(path,option);
        }
        public async IAsyncEnumerable<DriveItemInfo> GetFileListAsync<DirectoryT>(
            string accesserName,
            IDirectoryPath path,
            FileSystemType fileType = FileSystemType.All,
            PermissionScope? scope = null,
            AccesserOption option = default
        )
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            await foreach (var info in GetSolidAccesser(accesserName).GetFileListAsync(path, fileType, scope, option)) yield return info;
        }
        #endregion

        #region ファイル・フォルダの作成と削除
        public async Task<IFilePath> CreateEmptyFileAsync<FileT>(
            string accesserName,
            FileT path,
            FileSystemType fileType,
            string fileName,
            bool canWrite = false,
            AccesserOption option = default
            )
            where FileT : DriveItemPath, IDirectoryPath
        {
            return await GetSolidAccesser(accesserName).CreateEmptyFileAsync(path, fileName, fileType, canWrite,option);
        }
        public async Task DeleteFileAsync<FileT>(string accesserName, FileT path, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).DeleteFileAsync(path,option);
        }
        public async Task<IDirectoryPath> CreateDirectoryAsync<DirectoryT>(string accesserName, IDirectoryPath path, string name, bool canWrite = false, AccesserOption option = default)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            return await GetSolidAccesser(accesserName).CreateDirectoryAsync(path, name, canWrite, option);
        }
        public async Task DeleteDirectoryAsync<DirectoryT>(string accesserName, IDirectoryPath path, PermissionScope? scope = null, AccesserOption option = default)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            await GetSolidAccesser(accesserName).DeleteDirectoryAsync(path, scope, option);
        }
        public async Task ClearDirectoryAsync<DirectoryT>(
            string accesserName,
            IDirectoryPath path,
            FileSystemType fileType = FileSystemType.All,
            PermissionScope? scope = null,
            AccesserOption option = default
            )
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            await GetSolidAccesser(accesserName).ClearDirectoryAsync(path, fileType, scope, option);
        }
        #endregion

        #region ファイルの読み取りと書き込み
        public async Task SaveObjectAsync<FileT>(string accesserName, FileT path, object data, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).SaveObjectAsync(path, data, option);
        }
        public async Task<dataT?> LoadObjectAsync<dataT, FileT>(string accesserName, FileT path, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            return await GetSolidAccesser(accesserName).LoadObjectAsync<dataT, FileT>(path, option);
        }
        public async Task SaveRawAsync<FileT>(string accesserName, FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).SaveRawAsync(path, data, option);
        }
        public async Task AppendRawAsync<FileT>(string accesserName, FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).AppendRawAsync(path, data, option);
        }

        public async Task<byte[]> LoadRawAsync<FileT>(string accesserName, FileT path, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            return await GetSolidAccesser(accesserName).LoadRawAsync(path, option);
        }
        public async Task SaveTextAsync<FileT>(string accesserName, FileT path, string text, Encoding? encoding = null, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).SaveTextAsync(path, text, encoding, option);
        }
        public async Task<string> LoadTextAsync<FileT>(string accesserName, FileT path, Encoding? encoding = null, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            return await GetSolidAccesser(accesserName).LoadTextAsync(path, encoding, option);
        }

        public async Task AppendTextAsync<FileT>(string accesserName, FileT path, string text, bool withBreak = false, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(accesserName).AppendTextAsync(path, text, withBreak, option);
        }
        public IAsyncEnumerable<string> ReadLinesAsync<FileT>(string accesserName, FileT path, Encoding? encoding = null, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            return GetSolidAccesser(accesserName).ReadLinesAsync(path, encoding, option);
        }
        public async Task TransferToAsync<FileT1, FileT2>(string readerName, FileT1 readPath, string targetName, FileT2 targetPath, AccesserOption option = default)
            where FileT1 : DriveItemPath, IFilePath where FileT2 : DriveItemPath, IFilePath
        {
            await GetSolidAccesser(readerName).TransferToAsync(readPath, GetSolidAccesser(targetName), targetPath, option);
        }
        #endregion

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
        private async ValueTask<IDriveAccesser> GetTemporaryAccesser(
            DriveItemPath path,
            FileSystemType fileType,
            FileSystemAccessLevel level
        ){
            var permission = level == FileSystemAccessLevel.CreateOnly ?
                await Permissions.ComposeToSingleDirectoryAsync(path, fileType) :
                await Permissions.ComposeToSinglePathAsync(path, fileType, level);
            if (permission.IsEmpty) throw new ArgumentException($"{this}に許可されていないアクセスです: {path}");

            return path.DriveType switch{
                DriveTypeEnum.LocalDrive => new LocalDriveAccesser(permission),
                DriveTypeEnum.GoogleDrive => new GoogleDriveAccesser(permission),
                _ => throw new ArgumentException($"定義されていないドライブへのアクセス要求{permission}"),
            };
        }

        #region メタデータ関連
        public async Task<bool> ItemExistsAsync(DriveItemPath path, AccesserOption option = default){
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.None
            );
            return await accesser.ItemExistsAsync(path, option);
        }
        public async Task<DriveItemInfo> GetItemInfoAsync(DriveItemPath path, AccesserOption option = default){
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.None
            );
            return await accesser.GetItemInfoAsync(path, option);
        }
        #endregion

        #region 作成・削除
        public async Task<IFilePath> CreateEmptyFileAsync<FileT>(FileT path, string fileName, FileSystemType fileType, bool canWrite = false, AccesserOption option = default)
        where FileT : DriveItemPath, IDirectoryPath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: fileType,
                level: FileSystemAccessLevel.CreateOnly
            );
            return await accesser.CreateEmptyFileAsync(path, fileName, fileType, canWrite, option);
        }
        public async Task DeleteFileAsync<FileT>(FileT path, FileSystemType fileType, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: fileType,
                level: FileSystemAccessLevel.DeleteOnly
            );
            await accesser.DeleteFileAsync(path);
        }
        public async Task<IDirectoryPath> CreateDirectoryAsync<DirectoryT>(DirectoryT path, string name, bool canWrite, AccesserOption option = default)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                level: FileSystemAccessLevel.CreateOnly
            );
            return await accesser.CreateDirectoryAsync(path, name, canWrite, option);
        }
        #endregion

        public async Task SaveObjectAsync<FileT>(FileT path, object data, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.WriteOnly
            );
            await accesser.SaveObjectAsync(path, data, option);
        }
        public async Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.ReadOnly
            );
            return await accesser.LoadObjectAsync<dataT, FileT>(path, option);
        }
        public async Task SaveRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.WriteOnly
            );
            await accesser.SaveRawAsync(path, data, option);
        }
        public async Task AppendRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.AppendOnly
            );
            await accesser.AppendRawAsync(path, data, option);
        }

        public async Task<byte[]> LoadRawAsync<FileT>(FileT path, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.ReadOnly
            );
            return await accesser.LoadRawAsync(path, option);
        }
        public async Task SaveTextAsync<FileT>(FileT path, string text, Encoding? encoding = null, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.WriteOnly
            );
            await accesser.SaveTextAsync(path,text,encoding, option);
        }
        public async Task<string> LoadTextAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.ReadOnly
            );
            return await accesser.LoadTextAsync(path, encoding, option);
        }

        public async Task AppendTextAsync<FileT>(FileT path, string text, bool withBreak = false, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemTypeManager.Text,
                level: FileSystemAccessLevel.AppendOnly
            );
            await accesser.AppendTextAsync(path, text, withBreak, option);
        }
        public async IAsyncEnumerable<string> ReadLinesAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default)
            where FileT : DriveItemPath, IFilePath
        {
            using var accesser = await GetTemporaryAccesser(
                path: path,
                fileType: FileSystemTypeManager.Text,
                level: FileSystemAccessLevel.ReadOnly
            );
            await foreach (var line in accesser.ReadLinesAsync(path, encoding, option)) yield return line;
        }
        public async Task TransferToAsync<FileT1,FileT2>(FileT1 readPath, IDriveAccesser target, FileT2 targetPath, AccesserOption option = default)
            where FileT1 : DriveItemPath, IFilePath where FileT2 : DriveItemPath, IFilePath
        {
            using var reader = await GetTemporaryAccesser(
                path: readPath,
                fileType: FileSystemType.All,
                level: FileSystemAccessLevel.ReadOnly
            );
            await reader.TransferToAsync(readPath, target, targetPath, option);
        }
    }




}

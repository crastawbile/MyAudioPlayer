namespace Crast.Accesser.DriveAccesser{



    /// <summary>
    /// FolderPermissionを利用したaccesser呼び出しを行うクラスの基底クラス
    /// </summary>
    /// <remarks>
    /// フォルダ名すら隠蔽する前提。
    /// 内部で必要なaccesserはフィールドに入れて便利に使おう。
    /// </remarks>
    public class UsingDriveAccesser{
        public FileSystemPermissionBundle Permissions { get; init; }
        public UsingDriveAccesser(FileSystemPermissionBundle permissions){
            Permissions = permissions;
        }
        protected IDriveAccesser CreateDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = false){
            if (permission.IsEmpty) return new EmptyDriveAccesser(permission, allowEmpty, singleOnly);
            var p = permission.AsSinglePermission(singleOnly);
            if (p.DriveType == DriveTypeEnum.LocalDrive) { return Permissions.CreateLocalDriveAccesser(permission, allowEmpty, singleOnly); }
            else if (p.DriveType == DriveTypeEnum.GoogleDrive) { return Permissions.CreateGoogleDriveAccesser(permission, allowEmpty, singleOnly); }
            throw new ArgumentException($"定義されていないドライブへのアクセス要求{permission}");
        }
    }
    /// <summary>
    /// 複数のフォルダに対する権限を持ったDriveAccesser
    /// </summary>
    /// <remarks>
    /// 多数のフォルダを管理するクラスの基底に使う。
    /// </remarks>
    public class MultiDriveAccesser : UsingDriveAccesser{
        public MultiDriveAccesser(FileSystemPermissionBundle permissions) : base(permissions) { }

        //個別権限の使い捨てaccesserを生成する
        protected IDriveAccesser GetTemporaryAccesser(
            DriveItemPath path,
            FileSystemType fileType,
            FileSystemAccessLevel requiredIfExist,
            FileSystemAccessLevel requiredIfNotExist,
            bool allowEmpty = false
            )
        {
            FileSystemAccessLevel level;
            if (requiredIfNotExist == FileSystemAccessLevel.None || path.Exists(true)){
                level = requiredIfExist;
            }else{
                level = requiredIfNotExist;
            }
            var p = Permissions.Narrow(
                path: path,
                fileType: fileType,
                accessLevel: level,
                allowEmpty: allowEmpty
                );
            if (p.IsEmpty) throw new ArgumentException($"許可されていないアクセスです: {path}   {Permissions}");
            return CreateDriveAccesser(p);
        }
        public DriveItemPath CreateEmptyFile<DirectoryT>(DirectoryT path, FileSystemType fileType, string fileName, bool canWrite = false)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: fileType,
                requiredIfExist: canWrite ? FileSystemAccessLevel.WriteOnly : FileSystemAccessLevel.None,
                requiredIfNotExist: FileSystemAccessLevel.CreateOnly
            );
            if (accesser is LocalDriveAccesser la && path is LocalDirectoryPath lp) return la.CreateEmptyFile<LocalFilePath, LocalDirectoryPath>(lp, fileName, canWrite);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleDirectoryPath gp) return ga.CreateEmptyFile<GoogleFilePath, GoogleDirectoryPath>(gp, fileName, canWrite);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path} {fileName}");
        }
        public void DeleteFile<FileT>(FileT path, FileSystemType fileType)
            where FileT : DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: fileType,
                requiredIfExist: FileSystemAccessLevel.DeleteOnly,
                requiredIfNotExist: FileSystemAccessLevel.All
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) la.DeleteFile(lp);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) ga.DeleteFile(gp);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }

        public DriveItemPath CreateDirectory<DirectoryT>(DirectoryT path, string name)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.All,
                requiredIfNotExist: FileSystemAccessLevel.CreateOnly
            );
            switch ((path, accesser)){
                case (LocalDirectoryPath localPath, LocalDriveAccesser localAccesser):
                    return localAccesser.CreateDirectory(localPath, name);
                case (GoogleDirectoryPath googlePath, GoogleDriveAccesser googleAccesser):
                    return googleAccesser.CreateDirectory(googlePath, name);
                default:
                    throw new TypeAccessException($"在り得ないはずの型キャスト{path} {name}");
            }
        }
        public void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope scope = PermissionScope.SelfOnly)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.ReadDelete,
                requiredIfNotExist: FileSystemAccessLevel.All
            );
            switch ((path, accesser)){
                case (LocalDirectoryPath localPath, LocalDriveAccesser localAccesser):
                    localAccesser.DeleteDirectory(localPath, scope);
                    break;
                case (GoogleDirectoryPath googlePath, GoogleDriveAccesser googleAccesser):
                    googleAccesser.DeleteDirectory(googlePath, scope);
                    break;
                default:
                    throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
            }
        }
        public void ClearDirectory<DirectoryT>(DirectoryT path, bool recursive = false)
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.ReadDelete,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            switch ((path, accesser)){
                case (LocalDirectoryPath localPath, LocalDriveAccesser localAccesser):
                    localAccesser.ClearDirectory(localPath, recursive);
                    break;
                case (GoogleDirectoryPath googlePath, GoogleDriveAccesser googleAccesser):
                    googleAccesser.ClearDirectory(googlePath, recursive);
                    break;
                default:
                    throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
            }
        }

        public DriveItemInfo GetItemInfo(DriveItemPath path){
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) return la.GetItemInfo(lp);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) return ga.GetItemInfo(gp);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(
            DirectoryT path,
            FileSystemAccessLevel requiredLevel = FileSystemAccessLevel.ReadOnly,
            bool recursive = false
        )
            where DirectoryT : DriveItemPath, IDirectoryPath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.Directory,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalDirectoryPath lp) return await la.GetFileListAsync(lp, requiredLevel, recursive);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleDirectoryPath gp) return await ga.GetFileListAsync(gp, requiredLevel, recursive);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task SaveRawAsync<FileT>(FileT path, byte[] data)
            where FileT : DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.WriteOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) await la.SaveRawAsync(lp, data);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) await ga.SaveRawAsync(gp, data);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task SaveObjectAsync<dataT, FileT>(FileT path, dataT data)
            where FileT : DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.WriteOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) await la.SaveObjectAsync(lp, data);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) await ga.SaveObjectAsync(gp, data);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task AppendFileAsync<FileT>(FileT path, string text, bool withBreak = false)
            where FileT : DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemTypeManager.Text,
                requiredIfExist: FileSystemAccessLevel.AppendOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) await la.AppendFileAsync(lp, text, withBreak);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) await ga.AppendFileAsync(gp, text, withBreak);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path)
            where FileT : DriveItemPath, IFilePath
        {
            var accesser = GetTemporaryAccesser(
                path: path,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (accesser is LocalDriveAccesser la && path is LocalFilePath lp) return await la.LoadObjectAsync<dataT, LocalFilePath>(lp);
            else if (accesser is GoogleDriveAccesser ga && path is GoogleFilePath gp) return await ga.LoadObjectAsync<dataT, GoogleFilePath>(gp);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
        }
        public async Task TransferToAsync<T0, T1, FileT>(FileT readPath, SingleDriveAccesserGeneric<T0> target, T1 targetPath)
            where FileT : DriveItemPath, IFilePath where T0 : DriveItemPath where T1 : T0, IFilePath
        {
            var reader = GetTemporaryAccesser(
                path: readPath,
                fileType: FileSystemType.All,
                requiredIfExist: FileSystemAccessLevel.ReadOnly,
                requiredIfNotExist: FileSystemAccessLevel.None
            );
            if (reader is LocalDriveAccesser la && readPath is LocalFilePath lp) await la.TransferToAsync(lp, target, targetPath);
            else if (reader is GoogleDriveAccesser ga && readPath is GoogleFilePath gp) await ga.TransferToAsync(gp, target, targetPath);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{readPath}");
        }
    }




}

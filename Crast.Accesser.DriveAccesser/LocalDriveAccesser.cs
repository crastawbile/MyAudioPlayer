using System.Text;
using Newtonsoft.Json;


namespace Crast.Accesser.DriveAccesser{

    public abstract record LocalDrivePath : DriveItemPath{
        public override string Value { get; init; }
        public override DriveTypeEnum DriveType => DriveTypeEnum.LocalDrive;
        public LocalDrivePath(string path){
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is empty");
            // ここで絶対パスに強制変換
            Value = Path.GetFullPath(path);
        }
        public string Name => Path.GetFileName(Value);
        public string NameOnly => Path.GetFileNameWithoutExtension(Value);
        public override LocalDirectoryPath? Parent => ParentPath() == null ? null : (LocalDirectoryPath?)ParentPath()!;
        private string? ParentPath() => Path.GetDirectoryName(Value);
    }
    public sealed record LocalFilePath : LocalDrivePath, IFilePath{
        public static implicit operator LocalFilePath(string path) => new(path);
        public LocalFilePath(string path) : base(path) { }
        public override bool Exists(bool force = false) => File.Exists(Value);
        public FileSystemType Extension => Path.GetExtension(Value).FromExtension();
    }
    public sealed record LocalDirectoryPath : LocalDrivePath, IDirectoryPath{
        public static implicit operator LocalDirectoryPath(string path) => new(path);
        public LocalDirectoryPath(string path) : base(path) { }
        public override bool Exists(bool force = false) => Directory.Exists(Value);
    }



    internal class LocalDriveAccesser : SingleDriveAccesserGeneric<LocalDrivePath>{

        public LocalDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true)
            : base(permission, allowEmpty, singleOnly)
        { }

        public override DriveItemInfo GetItemInfo(LocalDrivePath path){
            ValidateAccess(path, FileSystemAccessLevel.ReadOnly, FileSystemAccessLevel.None);
            if (path is LocalFilePath){
                var f = new FileInfo(path.Value);
                return new DriveItemInfo(
                        DriveType: DriveTypeEnum.LocalDrive,
                        Name: f.Name,
                        FileType: f.Extension.FromExtension(),
                        Path: path,
                        Size: f.Length,
                        LastModified: f.LastWriteTime,
                        IsDirectory: false
                    );
            }else{
                var f = new DirectoryInfo(path.Value);
                return new DriveItemInfo(
                        DriveType: DriveTypeEnum.LocalDrive,
                        Name: f.Name,
                        FileType: FileSystemType.Directory,
                        Path: path,
                        Size: null,
                        LastModified: f.LastWriteTime,
                        IsDirectory: true
                    );
            }

        }
        public override async Task<List<DriveItemInfo>> GetFileListAsync<DirectoryT>(DirectoryT path, FileSystemAccessLevel requiredLevel = FileSystemAccessLevel.ReadOnly, bool recursive = false){
            CheckEmpty();

            //指定したfolderの子に対するアクセス権限があるかどうかをまず確認
            if (Permission!.IncludeScope(PermissionScope.ChildrenOnly) && Permission.Path == path) { }
            else if (Permission!.IncludeScope(PermissionScope.Recursive) && Permission.Path != path && Permission.IncludeItemPath(path)) { }
            else { throw new UnauthorizedAccessException("フォルダへのアクセス権限が不足しています。"); }

            var option = recursive && Permission!.IncludeScope(PermissionScope.Recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var di = new DirectoryInfo(path.Value);

            return di.GetFiles("*", option)
                .Where(f => Permission.IncludeFileSystemType(f.Extension.FromExtension())) // 拡張子フィルタ適用
                .Select(f => new DriveItemInfo(
                    Name: f.Name,
                    DriveType: DriveTypeEnum.LocalDrive,
                    Path: (LocalFilePath)f.FullName,
                    FileType: f.Extension.FromExtension(),
                    Size: f.Length,
                    LastModified: f.LastWriteTime,
                    IsDirectory: false
                ))
                .ToList();
        }
        public override bool ItemExists(LocalDrivePath path){
            return path switch{
                LocalFilePath => File.Exists(Path!.Value),
                LocalDirectoryPath => Directory.Exists(Path!.Value),
                _ => throw new ArgumentException($"未定義のパス型{path}")
            };
        }
        public override async Task SaveObjectAsync<dataT, FileT>(FileT path, dataT data){
            ValidateAccess(path, FileSystemAccessLevel.WriteOnly, FileSystemAccessLevel.WriteCreate);
            await File.WriteAllTextAsync(
                path.Value,
                JsonConvert.SerializeObject(data, Formatting.Indented)
            );
        }
        public override async Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path)
            where dataT : default
        {
            ValidateAccess(path, FileSystemAccessLevel.ReadOnly, FileSystemAccessLevel.None);
            var json = await System.IO.File.ReadAllTextAsync(path.Value, Encoding.UTF8);
            return JsonConvert.DeserializeObject<dataT>(json);
        }
        public override async Task SaveRawAsync<FileT>(FileT path, byte[] data){
            ValidateAccess(path, FileSystemAccessLevel.WriteOnly, FileSystemAccessLevel.WriteCreate);
            await File.WriteAllBytesAsync(
                path.Value,
                data
            );
        }
        public override async Task<byte[]> LoadRawAsync<FileT>(FileT path){
            ValidateAccess(path, FileSystemAccessLevel.ReadOnly, FileSystemAccessLevel.None);
            return await File.ReadAllBytesAsync(path.Value);
        }
        public override async Task AppendFileAsync<FileT>(FileT path, string text, bool withBreak = false){
            ValidateAccess(path, FileSystemAccessLevel.AppendOnly, FileSystemAccessLevel.None);
            var content = withBreak ? text + Environment.NewLine : text;
            await File.AppendAllTextAsync(path.Value, content);
        }
        public override async IAsyncEnumerable<string> ReadLinesAsync<FileT>(FileT path, Encoding? encoding = null){
            ValidateAccess(path, FileSystemAccessLevel.ReadOnly, FileSystemAccessLevel.None);
            using var reader = new StreamReader(path.Value, encoding ?? Config.Encoding);
            while (await reader.ReadLineAsync() is { } line)yield return line;
        }
        public override FileT CreateEmptyFile<FileT, DirectoryT>(DirectoryT path, string name, bool canWrite = false){
            var filePathString = System.IO.Path.Combine(path.Value, name);
            var filePath = new LocalFilePath(filePathString);
            if (canWrite){
                ValidateAccess(filePath, FileSystemAccessLevel.WriteOnly, FileSystemAccessLevel.CreateOnly);
            }else{
                ValidateAccess(filePath, FileSystemAccessLevel.None, FileSystemAccessLevel.CreateOnly);
            }
            using (File.Create(filePath.Value)) { }
            if (filePath is FileT f) return f;
            else throw new TypeAccessException($"在り得ないはずの型キャスト{filePath}");
        }
        public override void DeleteFile<FileT>(FileT path){
            ValidateAccess(path, FileSystemAccessLevel.DeleteOnly, FileSystemAccessLevel.All);//ファイルが存在しないなら何もしないので権限に制限はかけない
            if (File.Exists(path.Value)) File.Delete(path.Value);
        }
        public override DirectoryT CreateDirectory<DirectoryT>(DirectoryT path, string name, bool canWrite = false){
            var folderPathString = System.IO.Path.Combine(path.Value, name);
            var folderPath = new LocalDirectoryPath(folderPathString);
            ValidateAccess(folderPath, FileSystemAccessLevel.All, FileSystemAccessLevel.CreateOnly);//フォルダが存在するなら何もしないので権限に制限はかけない
            if (!Directory.Exists(folderPath.Value)) Directory.CreateDirectory(folderPath.Value);
            if (folderPath is DirectoryT f) return f;
            else throw new TypeAccessException($"在り得ないはずの型キャスト{folderPath}");
        }
        //scope==SelfOnlyなら、空フォルダの時のみ削除。そうでなければ例外。
        //SelfAndChildrenなら、中身が削除権限のあるファイルと空フォルダのみであればすべて削除。そうでなければ一切削除せずに例外。
        //AllWithSelfなら、配下のファイル・フォルダ全てに削除権限があればすべて削除。そうでなければ一切削除せずに例外。
        public override void DeleteDirectory<DirectoryT>(DirectoryT path, PermissionScope scope = PermissionScope.SelfOnly){
            ValidateAccess(path, FileSystemAccessLevel.DeleteOnly, FileSystemAccessLevel.All);//フォルダが存在しないなら何もしないので権限に制限はかけない
            if (!Directory.Exists(path.Value)) return;

            var di = new DirectoryInfo(path.Value);
            // SelfOnly の場合、中身があったら即例外、中身が無ければ削除して終了
            if (scope == PermissionScope.SelfOnly){
                if (di.GetFileSystemInfos().Length > 0){
                    throw new IOException($"ディレクトリが空ではないため削除できません: {path.Value}");
                }else{
                    di.Delete();
                    return;
                }
            }

            SearchOption searchOption = scope switch{
                PermissionScope.SelfAndChildren => SearchOption.TopDirectoryOnly,
                PermissionScope.AllWithSelf => SearchOption.AllDirectories,
                _ => throw new ArgumentException($"不適切な権限指定{scope}")
            };

            // 2. 権限の事前チェック（ドライラン）
            // 配下の全アイテムに対して削除権限があるか確認
            var allItems = di.GetFileSystemInfos("*", searchOption);
            foreach (var item in allItems){
                var itemInfo = item is FileInfo fi ? DriveItemInfo.From(fi) : DriveItemInfo.From((DirectoryInfo)item);
                if (!Permission!.IsItemAllowed(itemInfo)){
                    throw new UnauthorizedAccessException($"配下アイテムの削除権限がありません: {item.FullName}");
                }
            }

            // 3. 実行（ファイルから消し、最後にディレクトリを消す）
            // Localなら Directory.Delete(path, true) でも良いが、
            // 「権限があるものだけ確実に」なら自前で再帰したほうが安全
            di.Delete(true);
        }

        //削除権限のあるファイルを全て削除する。空フォルダ含めフォルダは削除しない。
        public override void ClearDirectory<DirectoryT>(DirectoryT path, bool recursive = false){
            ValidateAccess(path, FileSystemAccessLevel.ReadDelete, FileSystemAccessLevel.All); // フォルダ自体を見れる必要はある

            var di = new DirectoryInfo(path.Value);
            if (!di.Exists) return;

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            // ファイルだけを抽出
            var files = di.GetFiles("*", searchOption);

            foreach (var file in files){
                var info = DriveItemInfo.From(file);
                if (Permission!.IsItemAllowed(info)) file.Delete();
            }
        }

        protected override async Task<Stream> OpenReadStreamAsync<FileT>(FileT path){
            ValidateAccess(path, FileSystemAccessLevel.ReadOnly, FileSystemAccessLevel.None);
            return new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        }

        public override async Task SaveStreamAsync<FileT>(FileT path, Stream stream){
            ValidateAccess(path, FileSystemAccessLevel.WriteOnly, FileSystemAccessLevel.None);
            using var fs = new FileStream(path.Value, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.CopyToAsync(fs);
        }

        public override async Task TransferToAsync<T0, T1, FileT>(FileT readPath, SingleDriveAccesserGeneric<T0> target, T1 targetPath){
            // 自身からの読み取りが可能かチェック
            ValidateAccess(readPath, FileSystemAccessLevel.ReadOnly, FileSystemAccessLevel.None);
            using var srcStream = await OpenReadStreamAsync(readPath);
            // 相手側への保存（相手側でも権限チェックが走る）
            await target.SaveStreamAsync(targetPath, srcStream);
        }
    }


}

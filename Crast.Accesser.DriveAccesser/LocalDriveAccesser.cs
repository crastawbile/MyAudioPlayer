using Crast.Utilities.ExtensionMethods;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using System.Text;


namespace Crast.Accesser.DriveAccesser{

    public abstract record LocalDrivePath : PathBaseDrivePath{
        public override string Value { get; init; }
        public override DriveTypeEnum DriveType => DriveTypeEnum.LocalDrive;
        public LocalDrivePath(string path){
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is empty");
            // ここで絶対パスに強制変換。フォルダパスであっても末尾の区切り文字は無しで統一する。
            Value = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        public string Name => Path.GetFileName(Value);
        public string NameOnly => Path.GetFileNameWithoutExtension(Value);
        
        /// <summary>
        /// 拡張メソッドのParents()と違って、LocalDriveであることが確定しているため非同期でない。
        /// </summary>
        /// <returns></returns>
        public LocalDirectoryPath[] Parents() => ParentPath() == null ? [] : [(LocalDirectoryPath)ParentPath()!];
        private string? ParentPath() => Path.GetDirectoryName(Value);
        /// <summary>
        /// 拡張メソッドのGetDepthと違って、LocalDriveであることが確定しているため非同期でない。
        /// </summary>
        /// <remarks>
        /// 一応、ファイルかフォルダか分からない状態で静的解析にかかる場合のための繋ぎ。
        /// </remarks>
        /// <param name="path"></param>
        /// <returns></returns>
        public int? GetDepth(LocalDrivePath path) {
            if (this is LocalFilePath f) return f.GetDepth(path);
            else if (this is LocalDirectoryPath d) return d.GetDepth(path);
            else throw new ArgumentException($"未定義のpath型{path}");
        }
    }
    public sealed record LocalFilePath : LocalDrivePath, IFilePath{
        public static implicit operator LocalFilePath(string path) => new(path);//stringからの暗黙変換
        public LocalFilePath(string path) : base(path) { }
        /// <summary>
        /// 拡張メソッドのExists()と違って、LocalDriveであることが確定しているため非同期でない。
        /// </summary>
        /// <remarks>
        /// 一応、引数の型を揃えるためにforceはあるが使わない。
        /// </remarks>
        /// <returns></returns>
        public bool Exists(bool force = false) => File.Exists(Value);
        /// <summary>
        /// 拡張メソッドのGetDepthと違って、LocalDriveであることが確定しているため非同期でない。
        /// </summary>
        /// <remarks>
        /// ファイルの下に構造は無い前提なので、自身なら0、それ以外はnullを返す。
        /// </remarks>
        /// <param name="path"></param>
        /// <returns></returns>
        public new int? GetDepth(LocalDrivePath path) => path == this ? 0 : null;
        public FileSystemType FileType => Path.GetExtension(Value).FromExtension();
    }
    public sealed record LocalDirectoryPath : LocalDrivePath, IDirectoryPath{
        public static implicit operator LocalDirectoryPath(string path) => new(path);//stringからの暗黙変換
        public LocalDirectoryPath(string path) : base(path) { }
        /// <summary>
        /// 拡張メソッドのExists()と違って、LocalDriveであることが確定しているため非同期でない。
        /// </summary>
        /// <remarks>
        /// 一応、引数の型を揃えるためにforceはあるが使わない。
        /// </remarks>
        /// <returns></returns>
        public bool Exists(bool force = false) => Directory.Exists(Value);
        /// <summary>
        /// 拡張メソッドのGetDepthと違って、LocalDriveであることが確定しているため非同期でない。
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public new int? GetDepth(LocalDrivePath path) {
            var count = 0;
            LocalDrivePath? current = path;
            while (current != null) {
                if (current == this) return count;
                current = current.Parents()[0];
                count++;
            }
            return null;            
        }
        public int? GetDepth(FileInfo info) {
            var count = 0;
            var current = info.FullName;
            while (current != null){
                if (current == Value) return count;
                current = Path.GetDirectoryName(current);
                count++;
            }
            return null;
        }
    }



    internal sealed class LocalDriveAccesser : SingleDriveAccesserGeneric<LocalDrivePath>{

        public LocalDriveAccesser(FileSystemPermissionBundle permission, bool allowEmpty = false, bool singleOnly = true)
            : base(permission, allowEmpty, singleOnly)
        {
            if(Permission?.Path is LocalDrivePath ldp) BasePath = ldp;
        }
        private readonly new LocalDrivePath? BasePath = null;

        //処理がちゃんと通ったら、整備性のために共通処理をまとめる。それまでは放置
        protected override async ValueTask ValidateAccess(LocalDrivePath path, FileSystemAccessLevel requiredIfExist, FileSystemAccessLevel requiredIfNotExist){
            // 1. 基底クラスの権限＆パススコープ＆存在チェック
            await base.ValidateAccess(path, requiredIfExist, requiredIfNotExist);

            // 2. LocalDrive特有の拡張子チェック
            if (path is LocalFilePath filePath){
                if (!Permission!.Contains(filePath.FileType)){
                    throw new UnauthorizedAccessException($"このアクセッサーでは {filePath.FileType} タイプの操作は許可されていません。");
                }
            }
        }

        public override ValueTask<DriveItemInfo> GetItemInfoAsync(LocalDrivePath path, AccesserOption option = default){
            return ValueTask.FromResult(GetItemInfo(path));
        }
        public DriveItemInfo GetItemInfo(LocalDrivePath path, AccesserOption option = default){
            //ファイルかフォルダかで処理が完全に切り替わるためヘルパーメソッドに書き出し。
            if (path is LocalFilePath f) return GetFileInfo(f);
            else if (path is LocalDirectoryPath d) return GetDirectoryInfo(d);
            else throw new ArgumentException($"未定義のパス型{path}");
        }
        private DriveItemInfo GetFileInfo(LocalFilePath path) {
            CheckEmpty();
            if (!File.Exists(path.Value)) throw new FileNotFoundException($"存在しないファイルパスに対する操作{path}");
            if (BasePath?.GetDepth(path) is int depth && Permission!.InformationScope.Include(depth)){
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
            } else {
                throw new UnauthorizedAccessException("ファイルへのアクセス権限が不足しています。");
            }
        }
        private DriveItemInfo GetDirectoryInfo(LocalDirectoryPath path){
            CheckEmpty();
            if (!Directory.Exists(path.Value)) throw new FileNotFoundException($"存在しないフォルダパスに対する操作{path}");
            if (BasePath?.GetDepth(path) is int depth && Permission!.InformationScope.Include(depth)){
                var d = new DirectoryInfo(path.Value);
                return new DriveItemInfo(
                        DriveType: DriveTypeEnum.LocalDrive,
                        Name: d.Name,
                        FileType: FileSystemType.Directory,
                        Path: path,
                        Size: null,
                        LastModified: d.LastWriteTime,
                        IsDirectory: true
                    );
            } else {
                throw new UnauthorizedAccessException("フォルダへのアクセス権限が不足しています。");
            }
        }
        public override IAsyncEnumerable<DriveItemInfo> GetFileListAsync<DirectoryT>(
            DirectoryT path,
            FileSystemType fileType = FileSystemType.All,
            PermissionScope? scope = null,
            AccesserOption option = default
            )
        {
            CheckEmpty();
            var infoScope = scope != null ? scope.Value : PermissionScope.ChildrenOnly;
            //まずは対象Pathが権限範囲内かどうか、もしくは、権限範囲が対象pathの下部構造か。そして権限スコープと引数スコープの共通範囲が存在するかどうか。
            //共通範囲が存在しないなら空配列を返して終了。
            if (GetFileListAsync((dynamic)path, infoScope, out LocalDirectoryPath basePath, out PermissionScope targetScope, out List<DriveItemInfo> result)) return result.FromEnumerable();
            if (targetScope == PermissionScope.SelfOnly) return result.FromEnumerable();
            //これ以降は、basePathを起点とするtargetScope範囲のtargetType種別のファイルをリストアップする処理。
            var targetType = Permission!.FileType & fileType;
            var searchOption = PermissionScope.SelfAndChildren.Include(targetScope) ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
            var di = new DirectoryInfo(path.Value);
            
            return di.EnumerateFiles("*", searchOption)
                .Where(f => 
                    f.Extension.FromExtension().InFlag(targetType)
                ) // 拡張子フィルタ適用
                .Where(f =>
                    basePath.GetDepth(new LocalFilePath(f.FullName)) is int d &&
                    targetScope.Include(d)
                )//スコープでフィルタする処理
                .Select(f => new DriveItemInfo(
                    Name: f.Name,
                    DriveType: DriveTypeEnum.LocalDrive,
                    Path: (LocalFilePath)f.FullName,
                    FileType: f.Extension.FromExtension(),
                    Size: f.Length,
                    LastModified: f.LastWriteTime,
                    IsDirectory: false
                ))//各要素をDriveItemInfo型に変換
                .FromEnumerable();//非同期ストリーム型に変換
        }
        //内部処理の一部を同名メソッドで切り出してある
        //早期returnの場合はtrue、後の処理に進む場合はfalse
        private bool GetFileListAsync(LocalDirectoryPath path, PermissionScope infoScope, out LocalDirectoryPath? basePath, out PermissionScope targetScope, out List<DriveItemInfo> result) {
            basePath = default;
            targetScope = default;
            result = new List<DriveItemInfo>();
            if (BasePath is LocalDirectoryPath directoryPath1 && directoryPath1.GetDepth(path) is int depth1){
                basePath = path;
                targetScope = InformationScope!.Value.Rebased(depth1).Trim(infoScope);
                if (targetScope == PermissionScope.Empty) return true;
            }else if (path.GetDepth(BasePath!) is int depth2){
                if (BasePath is LocalFilePath filePath){
                    //権限パスのファイル一つだけが表示範囲であり、GetFiles()が不要な場合
                    if (infoScope.Include(depth2)) result.Add(GetFileInfo(filePath));
                    return true;
                }else if (BasePath is LocalDirectoryPath directoryPath2){
                    basePath = directoryPath2;
                    targetScope = infoScope.Rebased(depth2).Trim(InformationScope!.Value);
                    if (targetScope == PermissionScope.Empty) return true;
                }
            }else{
                return true;
            }
            return false;
        }
        public override ValueTask<bool> ItemExistsAsync(LocalDrivePath path, AccesserOption option = default){
            return ValueTask.FromResult(ItemExists(path));
        }
        public bool ItemExists(LocalDrivePath path, AccesserOption option = default){
            return path switch{
                LocalFilePath => File.Exists(path!.Value),
                LocalDirectoryPath => Directory.Exists(path!.Value),
                _ => throw new ArgumentException($"未定義のパス型{path}")
            };
        }

        public override async Task SaveObjectAsync<dataT, FileT>(FileT path, dataT data, AccesserOption option = default){
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            await SaveTextAsync(path, json);
        }
        public override async Task<dataT?> LoadObjectAsync<dataT, FileT>(FileT path, AccesserOption option = default)
            where dataT : default
        {
            var json = await LoadTextAsync(path);
            return JsonConvert.DeserializeObject<dataT>(json);
        }
        public override async Task SaveRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default){
            CheckEmpty();
            if (!CanWrite) throw new UnauthorizedAccessException($"{this}は書込権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!ItemExists(path) && !CanCreate) throw new UnauthorizedAccessException($"{path}の作成権限が無い"); 

            using var stream = new FileStream(
                path.Value,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                true
                );
            await stream.WriteAsync(data);
        }
        public override async Task AppendRawAsync<FileT>(FileT path, ReadOnlyMemory<byte> data, AccesserOption option = default){
            CheckEmpty();
            if (!CanAppend) throw new UnauthorizedAccessException($"{this}は追記権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!ItemExists(path)) throw new ArgumentException($"{path}にファイルが存在しない");

            using var stream = new FileStream(
                path.Value,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                true
                );
            await stream.WriteAsync(data);
        }
        public override async Task<byte[]> LoadRawAsync<FileT>(FileT path, AccesserOption option = default){
            CheckEmpty();
            if (!CanRead) throw new UnauthorizedAccessException($"{this}は読取権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!ItemExists(path)) throw new ArgumentException($"{path}にファイルが存在しない");

            using var stream = new FileStream(
                path.Value,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true
                );
            var data = new byte[stream.Length];
            await stream.ReadExactlyAsync(data.AsMemory());
            return data;
        }
        public override async Task AppendTextAsync<FileT>(FileT path, string text, bool withBreak = false, AccesserOption option = default){
            CheckEmpty();
            if (!CanAppend) throw new UnauthorizedAccessException($"{this}は追記権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!ItemExists(path)) throw new ArgumentException($"{path}にファイルが存在しない");

            var content = withBreak ? text + Environment.NewLine : text;
            using var stream = new FileStream(
                path.Value,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                true
                );
            using var writer = new StreamWriter(stream, Config.Encoding);
            await writer.WriteAsync(content);
        }
        public override async IAsyncEnumerable<string> ReadLinesAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default){
            CheckEmpty();
            if (!CanRead) throw new UnauthorizedAccessException($"{this}は読取権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!ItemExists(path)) throw new ArgumentException($"{path}にファイルが存在しない");

            using var stream = new FileStream(
                path.Value,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true
                );
            using var reader = new StreamReader(stream, encoding ?? Config.Encoding, detectEncodingFromByteOrderMarks: true);
            while (await reader.ReadLineAsync() is { } line) yield return line;
        }
        public override async Task SaveTextAsync<FileT>(FileT path, string text,Encoding? encoding = null, AccesserOption option = default){
            CheckEmpty();
            if (!CanWrite) throw new UnauthorizedAccessException($"{this}は書込権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!ItemExists(path) && !CanCreate) throw new UnauthorizedAccessException($"{path}の作成権限が無い");
            
            using var stream = new FileStream(
                        path.Value,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        4096,
                        true
                        );
            using var writer = new StreamWriter(stream, encoding ?? Config.Encoding);
            await writer.WriteAsync(text);
        }
        public override async Task<string> LoadTextAsync<FileT>(FileT path, Encoding? encoding = null, AccesserOption option = default){
            CheckEmpty();
            if (!CanRead) throw new UnauthorizedAccessException($"{this}は読取権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!ItemExists(path)) throw new ArgumentException($"{path}にファイルが存在しない");

            using var stream = new FileStream(
                path.Value,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true
                );
            using var reader = new StreamReader(
                stream,
                encoding ?? Config.Encoding,
                detectEncodingFromByteOrderMarks: true
                );
            return await reader.ReadToEndAsync();
        }

        public override Task<FileT> CreateEmptyFileAsync<FileT, DirectoryT>(DirectoryT path, string name, FileSystemType fileType, bool canWrite = false, AccesserOption option = default){
            CheckEmpty();
            if (!CanCreate) throw new UnauthorizedAccessException($"{this}は作成権限を持たない");
            if (!Permission!.FileType.HasFlag(fileType)) throw new UnauthorizedAccessException($"このファイルタイプの作成権限がありません: {fileType}");
            var filePathString = System.IO.Path.Combine(path.Value, name);
            var filePath = new LocalFilePath(filePathString);
            var depth = 0;
            if (BasePath!.GetDepth(filePath) is int d) depth = d;
            else throw new UnauthorizedAccessException($"{path}に対する作成権限が無い");
            if (!Permission!.ItemCreateScope.Include(depth)) throw new UnauthorizedAccessException($"{path}に対する作成権限が無い");

            if (File.Exists(filePath.Value)) {
                if (!canWrite) throw new UnauthorizedAccessException($"{filePath}は既に存在する");
                if (!CanWrite || !Permission!.FileAccessScope.Include(depth)) throw new UnauthorizedAccessException($"{filePath}に対する上書権限が無い");
            }

            using (File.Create(filePath.Value)) { }// File.Create は内部的に FileShare.None を使う独占的な実装だが即時クローズするので影響は無いはず
            if (filePath is FileT f) return Task.FromResult(f);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{filePath}");
        }
        public override Task DeleteFileAsync<FileT>(FileT path, AccesserOption option = default){
            CheckEmpty();
            if (!CanDelete) throw new UnauthorizedAccessException($"{this}は削除権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");

            if (File.Exists(path.Value)) File.Delete(path.Value);
            return Task.CompletedTask;
        }
        public override Task<DirectoryT> CreateDirectoryAsync<DirectoryT>(DirectoryT path, string name, bool canWrite = false, AccesserOption option = default){
            CheckEmpty();
            if (!CanCreate) throw new UnauthorizedAccessException($"{this}は作成権限を持たない");
            if (!Permission!.FileType.HasFlag(FileSystemType.Directory)) throw new UnauthorizedAccessException($"{this}にフォルダの作成権限がありません");

            var folderPathString = System.IO.Path.Combine(path.Value, name);
            var folderPath = new LocalDirectoryPath(folderPathString);
            CreateDirectory(folderPath);
            if (folderPath is DirectoryT f) return Task.FromResult(f);
            else throw new TypeAccessException($"在り得ないはずの型キャスト{folderPath}");
        }
        private void CreateDirectory(LocalDirectoryPath path) {
            if (BasePath!.GetDepth(path) is not int depth) throw new UnauthorizedAccessException($"{this}の範囲外に対する操作");
            if (!Permission!.InformationScope.Include(depth)) throw new UnauthorizedAccessException($"{this}の範囲外に対する操作");
            if (!Permission!.ItemCreateScope.Include(depth)) throw new UnauthorizedAccessException($"{path}に対する作成権限が無い");
            var parentPath = path.Parents().FirstOrDefault() ?? throw new UnauthorizedAccessException($"{this}の範囲外に対する操作");
            
            if (!parentPath.Exists()) CreateDirectory(parentPath);
            Directory.CreateDirectory(path.Value);//既に存在しても正常終了する。
        }

        //scope==SelfOnlyなら、空フォルダの時のみ削除。そうでなければ例外。
        //SelfAndChildrenなら、中身が削除権限のあるファイルと空フォルダのみであればすべて削除。そうでなければ一切削除せずに例外。
        //AllWithSelfなら、配下のファイル・フォルダ全てに削除権限があればすべて削除。そうでなければ一切削除せずに例外。
        public override Task DeleteDirectoryAsync<DirectoryT>(DirectoryT path, PermissionScope? scope = null, AccesserOption option = default){
            CheckEmpty();
            if (!CanDelete) throw new UnauthorizedAccessException($"{this}は削除権限を持たない");
            if (!Permission!.FileType.HasFlag(FileSystemType.Directory)) throw new UnauthorizedAccessException($"フォルダの削除権限がありません");
            int depth;
            if (BasePath!.GetDepth(path) is int d) depth = d;
            else throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!Permission!.FileAccessScope.Include(depth)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!Directory.Exists(path.Value)) return Task.CompletedTask;//存在しないなら何もせずに終了

            //この時点で、pathにフォルダは存在するし、そのフォルダ自体の削除権限はある。

            var deleteScope = scope == null ? Permission.FileAccessScope.Rebased(depth) : Permission.FileAccessScope.Rebased(depth).Trim(scope.Value);
            var di = new DirectoryInfo(path.Value);
            // SelfOnly の場合、中身があったら即例外、中身が無ければ削除して終了
            if (deleteScope == PermissionScope.SelfOnly){
                if (di.GetFileSystemInfos().Length > 0){
                    throw new IOException($"ディレクトリが空ではないため削除できません: {path.Value}");
                }else{
                    di.Delete();
                    return Task.CompletedTask;
                }
            }

            System.IO.SearchOption searchOption = 
                PermissionScope.SelfAndChildren.Include(deleteScope) ?
                System.IO.SearchOption.TopDirectoryOnly :
                System.IO.SearchOption.AllDirectories;

            LocalDirectoryPath targetPath;
            if(path is LocalDirectoryPath p) targetPath = p;
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");
            //targetPathを起点としたdeleteScopeの範囲のファイルが全てPermission.FileTypeの範疇であるなら、削除できる。
            //そうでなければ例外。

            // 2. 権限の事前チェック（ドライラン）
            // 配下の全アイテムに対して削除権限があるか確認
            var allItems = di.EnumerateFiles("*", searchOption);
            foreach (var item in allItems){
                if (targetPath.GetDepth(item) is not int feDepth ||
                    !deleteScope.Include(feDepth) ||
                    !item.Extension.FromExtension().InFlag(Permission.FileType)
                    ){
                    throw new UnauthorizedAccessException($"配下アイテムの削除権限がありません: {item.FullName}");
                }
            }

            // 3. 実行（ファイルから消し、最後にディレクトリを消す）
            // Localなら Directory.Delete(path, true) でも良いが、
            // 「権限があるものだけ確実に」なら自前で再帰したほうが安全
            di.Delete(true);
            return Task.CompletedTask;
        }

        //削除権限のあるファイルを全て削除する。空フォルダ含めフォルダは削除しない。
        public override async Task ClearDirectoryAsync<DirectoryT>(DirectoryT path, FileSystemType fileType = FileSystemType.All, PermissionScope? scope = null, AccesserOption option = default){
            CheckEmpty();
            if (!CanDelete) throw new UnauthorizedAccessException($"{this}は削除権限を持たない");
            var targetType = Permission!.FileType & fileType;
            if (targetType == FileSystemType.None) throw new UnauthorizedAccessException($"{fileType}の削除権限が一切ありません");
            int depth;
            if (BasePath!.GetDepth(path) is int d) depth = d;
            else throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!Permission!.FileAccessScope.Include(depth + 1)) throw new UnauthorizedAccessException($"{path}の配下に対するアクセス権限が無い");
            if (!Directory.Exists(path.Value)) throw new ArgumentException($"{path}にフォルダが存在しない");

            var deleteScope = scope == null ? Permission.FileAccessScope.Rebased(depth) : Permission.FileAccessScope.Rebased(depth).Trim(scope.Value);
            if (deleteScope == PermissionScope.Empty) throw new UnauthorizedAccessException($"{path}の配下に対するアクセス権限が無い");
            var di = new DirectoryInfo(path.Value);

            System.IO.SearchOption searchOption =
                PermissionScope.SelfAndChildren.Include(deleteScope) ?
                System.IO.SearchOption.TopDirectoryOnly :
                System.IO.SearchOption.AllDirectories;
            LocalDirectoryPath targetPath;
            if (path is LocalDirectoryPath p) targetPath = p;
            else throw new TypeAccessException($"在り得ないはずの型キャスト{path}");

            //targetPathを起点としたdeleteScopeの範囲のtargetTypeの範疇であるファイルを全て削除する。

            // ファイルだけを抽出
            var files = di.EnumerateFiles("*", searchOption);

            foreach (var file in files){
                if (targetPath.GetDepth(file) is int feDepth &&
                    deleteScope.Include(feDepth) &&
                    file.Extension.FromExtension().InFlag(targetType)
                    ){
                    file.Delete();
                }
            }
        }

        protected override async Task<Stream> OpenReadStreamAsync<FileT>(FileT path, AccesserOption option = default){
            CheckEmpty();
            if (!CanRead) throw new UnauthorizedAccessException($"{this}は読取権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!ItemExists(path)) throw new ArgumentException($"{path}にファイルが存在しない");

            return new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        }

        public override async Task SaveStreamAsync<FileT>(FileT path, Stream stream, AccesserOption option = default){
            CheckEmpty();
            if (!CanWrite) throw new UnauthorizedAccessException($"{this}は書込権限を持たない");
            if (!System.IO.Path.GetExtension(path.Value).FromExtension().InFlag(Permission!.FileType)) throw new UnauthorizedAccessException($"{path}の拡張子に対するアクセス権限が無い");
            if (BasePath!.GetDepth(path) is not int d || !Permission!.FileAccessScope.Include(d)) throw new UnauthorizedAccessException($"{path}に対するアクセス権限が無い");
            if (!ItemExists(path) && !CanCreate) throw new UnauthorizedAccessException($"{path}の作成権限が無い");

            using var fs = new FileStream(path.Value, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.CopyToAsync(fs);
        }

        public override async Task TransferToAsync<T0, T1, FileT>(FileT readPath, SingleDriveAccesserGeneric<T0> target, T1 targetPath, AccesserOption option = default){
            using var srcStream = await OpenReadStreamAsync(readPath);
            await target.SaveStreamAsync(targetPath, srcStream);
        }
    }


}

using System.Collections.Concurrent;
using System.Diagnostics;

//DriveAccesserを利用する形への修正前の残骸を置いてあるファイル。

namespace Crast.Accesser.DriveAccesser{



    public class AccessGoogleDrive {
        public readonly DriveService _service;
        public List<string> RewritableFolderIds { get; init; }
        public AccessGoogleDrive(string jsonKeyPath,List<string> rewritableFolderIds) { 
            if (!System.IO.File.Exists(jsonKeyPath))
                throw new FileNotFoundException("JSONキーが見つかりません。", jsonKeyPath);

            RewritableFolderIds = rewritableFolderIds;

            // 1. JSONファイルを読み込み、パラメータとしてパースする
            // これにより、SDK内部の CredentialFactory を介さず自前で安全に構築できます
            string json = System.IO.File.ReadAllText(jsonKeyPath);
            var paramsObj = JsonConvert.DeserializeObject<JsonCredentialParameters>(json);

            // 2. サービスアカウント認証のInitializerを組み立てる
            var initializer = new ServiceAccountCredential
                .Initializer(paramsObj!.ClientEmail){
                    Scopes = new[] { DriveService.Scope.Drive }
                }
            ;
            // 秘密鍵（Private Key）を読み込ませる
            initializer.FromPrivateKey(paramsObj.PrivateKey);

            // 3. 認証オブジェクトを生成
            var credential = new ServiceAccountCredential(initializer);

            // 4. サービスを生成
            _service = new DriveService(new BaseClientService.Initializer(){
                HttpClientInitializer = credential,
                ApplicationName = "MyAudioPlayer_AnalysisTool",
            });
        }

        // JSONからパラメータを抽出するためのヘルパークラス
        private class JsonCredentialParameters{
            [JsonProperty("client_email")]
            public required string ClientEmail { get; set; }
            [JsonProperty("private_key")]
            public required string PrivateKey { get; set; }
        }
        public bool CheckRewritableFolder(string folderId) {
            foreach (var dir in RewritableFolderIds){
                if (folderId.StartsWith(dir)) return true;
            }
            throw new NotImplementedException($"{folderId}は変更可能なフォルダではない");
        }

        public async Task DeleteFileAsync(string fileId) {
            CheckRewritableFolder(fileId);
            await _service.Files.Delete(fileId).ExecuteAsync();
        }
        public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetFileList(string folderId) {
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and trashed = false";
            var result = await request.ExecuteAsync();

            return result.Files;
        }
        public async Task<bool> ExistFileAsync(string folderId, string FileName) {
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and name = '{FileName}' and trashed = false";
            var result = await request.ExecuteAsync();

            if (result.Files != null && result.Files.Any()) {
                return true;
            } else {
                return false;
            }
        }


        public async Task<string> UploadFileAsync(string fromPath,string toDir){
            CheckRewritableFolder(toDir);

            var metadata = new Google.Apis.Drive.v3.Data.File(){
                Name = Path.GetFileName(fromPath),
                Parents = new List<string> { toDir }
            };

            using var stream = new FileStream(fromPath, FileMode.Open);
            var request = _service.Files.Create(metadata, stream, "application/octet-stream");
            var progress = await request.UploadAsync();

            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
                throw progress.Exception;

            return request.ResponseBody.Id;
        }
    }

    public class AccessLocalDrive
    {
    }


    public class AudioAnalysisManager{
        // --- 固定設定 ---
        private const string InputFolderId = "1CZhbuAiwWG7x8iNKs5nHOkcplaZu34jo";
        private const string OutputFolderId = "18ASlpbb43mzWz9fB5ecqEZ4TjhnmjZiQ";
        private const string ColabUrl = "https://colab.research.google.com/drive/1f_rlodFaHARzrdi_Rtbc5N_0BHIuPfst";
        private const string FfmpegPath = "ffmpeg.exe"; // パスは環境に合わせて調整
        private const string Address = "audioanalizerbot@audioanalizer.iam.gserviceaccount.com";
        private const string JsonKeyPath = "Credentials/service-account-key.json";
        private const string AppName = "AudioAnalizer";
        private const string SaveFolderDir = "Result";

        private readonly AccessGoogleDrive _access;
        private readonly DriveService _service;
        private readonly ConcurrentQueue<string> _taskQueue = new ConcurrentQueue<string>();
        private bool _isProcessing = false;
        private string _uploadedFileId = "";

        public AudioAnalysisManager(){
            _access=new AccessGoogleDrive(JsonKeyPath,new List<string> {InputFolderId,OutputFolderId });
            _service = _access._service;

        }

        public async Task DiagnosticTestAsync()
        {
            try
            {
                MainWindow.Log("--- 診断開始 ---");

                // 1. 権限の確認
                MainWindow.Log($"使用中のサービスアカウント: {((ServiceAccountCredential)_service.HttpClientInitializer).Id}");

                // 2. フォルダが見えるかテスト
                MainWindow.Log("Outputフォルダのリスト取得を試行...");
                var request = _service.Files.List();
                request.Q = $"'{OutputFolderId}' in parents and trashed = false";
                //request.PageSize = 1;

                // ここで止まるなら：認証 or ネットワークの問題
                var result = await request.ExecuteAsync().ConfigureAwait(false);

                MainWindow.Log($"接続成功。フォルダ内のファイル数: {result.Files.Count}");

                if (result.Files.Count == 0)
                {
                    MainWindow.Log("警告: フォルダは見えるが中身が空です。サービスアカウントに共有されていますか？");
                }

                // 3. heartbeat.txt を直接探す
                MainWindow.Log("heartbeat.txt をピンポイントで探します...");
                request.Q = $"'{InputFolderId}' in parents and name = 'heartbeat.txt'";
                var hResult = await request.ExecuteAsync().ConfigureAwait(false);

                if (hResult.Files.Any())
                {
                    MainWindow.Log($"発見: 更新時刻 {hResult.Files[0].ModifiedTimeDateTimeOffset}");
                }
                else
                {
                    MainWindow.Log("不検出: heartbeat.txt が見つかりません。Colab側で作成されていますか？");
                }
            }
            catch (Exception ex)
            {
                MainWindow.Log($"【致命的エラー】: {ex.Message}");
            }
        }



        /// <summary>
        /// 外部から楽曲を投入するエントリーポイント
        /// </summary>
        public void AddToQueue(string sourceFilePath){
            _taskQueue.Enqueue(sourceFilePath);
            MainWindow.Log($"{sourceFilePath}をキューに追加");
            if (!_isProcessing){Task.Run(() => ProcessQueueAsync());}
        }

        private async Task ProcessQueueAsync(){
            _isProcessing = true;

            while (_taskQueue.TryDequeue(out var sourceFile)){
                MainWindow.Log("ProcessQueueAsyncを開始");
                try{
                    // 1. 生存確認 & 必要ならColab起動
                    if (!await IsColabAliveAsync()){
                        MainWindow.Log("Colabを起動");
                        Console.WriteLine("[C#] Colabが停止している可能性があるため起動します...");
                        LaunchColab();
                        await Task.Delay(15000); // ブラウザ起動とVM接続のバッファ
                    }else {
                        MainWindow.Log("Colab生存を確認");
                        Console.WriteLine("[C#] Colabの生存を確認");
                    }

                    // 2. WAV変換
                    string workWavPath = await EnsureWavFormatAsync(sourceFile);

                    MainWindow.Log("wav変換完了");

                    // 3. アップロード
                    Console.WriteLine($"[C#] {Path.GetFileName(workWavPath)} をアップロード中...");
                    string fileId = await UploadAudioAsync(workWavPath);

                    MainWindow.Log("アップロード完了");

                    // 4. 解析完了監視 (done.txt)
                    Console.WriteLine("[C#] 解析完了を待機中...");
                    if (await PollForCompletionAsync()){
                        // 5. 結果取得
                        string downloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results", Path.GetFileNameWithoutExtension(sourceFile));
                        await DownloadResultsAsync(downloadDir);

                        // 6. 後始末（Drive上の音源と完了フラグを削除）
                        await _service.Files.Delete(fileId).ExecuteAsync();
                        await DeleteFileByNameAsync(OutputFolderId, "done.txt");
                        Console.WriteLine($"[C#] {Path.GetFileName(sourceFile)} の全工程が完了。");
                    }

                    // 一時的に作成したWAVがあれば削除
                    if (workWavPath != sourceFile && System.IO.File.Exists(workWavPath)){
                        System.IO.File.Delete(workWavPath);
                    }
                }catch (Exception ex){
                    Console.WriteLine($"[C#] 致命的エラー ({Path.GetFileName(sourceFile)}): {ex.Message}");
                }
            }
            _isProcessing = false;
        }

        #region Drive & System Operations

        private async Task<string> UploadAudioAsync(string path){

            MainWindow.Log("UploadAudioAsync開始");

            var metadata = new Google.Apis.Drive.v3.Data.File(){
                Name = Path.GetFileName(path),
                Parents = new List<string> { InputFolderId }
            };

            using var stream = new FileStream(path, FileMode.Open);
            var request = _service.Files.Create(metadata, stream, "application/octet-stream");
            MainWindow.Log("request.UploadAsync()開始");
            var progress = await request.UploadAsync();

            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
                throw progress.Exception;

            return request.ResponseBody.Id;
        }

        private async Task<bool> PollForCompletionAsync(){
            // 最大待機時間を設ける場合はここでカウンタを回す
            while (true){
                var request = _service.Files.List();
                request.Q = $"'{OutputFolderId}' in parents and name = 'done.txt' and trashed = false";
                var result = await request.ExecuteAsync();

                if (result.Files != null && result.Files.Any()) return true;

                await Task.Delay(10000); // 10秒おきに監視
            }
        }

        private async Task DownloadResultsAsync(string saveDir){
            if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

            var listRequest = _service.Files.List();
            listRequest.Q = $"'{OutputFolderId}' in parents and name != 'done.txt' and name != 'heartbeat.txt' and trashed = false";
            var result = await listRequest.ExecuteAsync();

            foreach (var file in result.Files){
                var getRequest = _service.Files.Get(file.Id);
                string localPath = Path.Combine(saveDir, file.Name);

                using var stream = new FileStream(localPath, FileMode.Create);
                await getRequest.DownloadAsync(stream);

                // ダウンロード後にDrive上の結果ファイルを消す場合はここでDeleteを呼ぶ
                await _service.Files.Delete(file.Id).ExecuteAsync();
            }
        }

        public async Task<bool> IsColabAliveAsync(){

            MainWindow.Log($"生存確認開始");

            var request = _service.Files.List();
            // Trashed = false を入れることで、ゴミ箱の中身を誤検知するのを防ぎます
            request.Q = $"'{InputFolderId}' in parents and name = 'heartbeat.txt' and trashed = false";
            // 重要：ここを API仕様の "modifiedTime" に固定
            request.Fields = "files(id, modifiedTime)";

            try{
                var result = await request.ExecuteAsync();
                var heartbeat = result.Files.FirstOrDefault();

                if (heartbeat?.ModifiedTimeDateTimeOffset == null) return false;

                // 通信が成功し、時間が取得できれば「接続確立」と判定
                return (DateTimeOffset.Now - heartbeat.ModifiedTimeDateTimeOffset.Value).TotalMinutes < 2;
            }catch (Exception ex){
                // 401 や 403 が出れば設定（共有など）の問題、ここに来なければコードの問題
                MainWindow.Log($"接続テスト失敗: {ex.Message}");
                return false;
            }
        }

        private void LaunchColab(){
            Process.Start(new ProcessStartInfo(ColabUrl) { UseShellExecute = true });

            // ブラウザの読み込み待機
            Task.Delay(10000).Wait();

            // 「すべてのセルを実行」ショートカット送信
            SendKeys.SendWait("^({F9})");
        }

        private async Task<string> EnsureWavFormatAsync(string inputPath){
            if (Path.GetExtension(inputPath).ToLower() == ".wav") return inputPath;

            MainWindow.Log("wave変換処理開始");

            string outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileNameWithoutExtension(inputPath) + "_work.wav");

            var startInfo = new ProcessStartInfo{
                FileName = FfmpegPath,
                Arguments = $"-i \"{inputPath}\" \"{outputPath}\" -y",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            await Task.Run(() => process!.WaitForExit());

            return outputPath;
        }

        private async Task DeleteFileByNameAsync(string folderId, string fileName){
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and name = '{fileName}' and trashed = false";
            var result = await request.ExecuteAsync();
            foreach (var file in result.Files){
                await _service.Files.Delete(file.Id).ExecuteAsync();
            }
        }

        #endregion
    }
}




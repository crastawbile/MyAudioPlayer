using Microsoft.Data.Sqlite;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Xps.Packaging;
using Crast.Accesser.DriveAccesser;

//ファイル実体はC:\Users\FMV\source\repos\MyAudioPlayerに存在している。

//FFTまでは、データをdoubleで保持する用に全修正。

namespace MyAudioPlayer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public static MainWindow? Instance { get; private set; } // 外部から呼び出すための目印
        public MainWindow(){
            InitializeComponent();
            Instance = this; // 自分自身をInstanceに登録
            RunTest();//仮コードの出力をとりあえず見たいとき用
        }
        // 外部から MainWindow.Log("...") で呼べるようにする
        public static void Log(string message)
        {
            // UIスレッドで実行することを保証する書き方
            Instance!.Dispatcher.Invoke(() =>{
                Instance.LogBlock.Text += $"[{System.DateTime.Now:HH:mm:ss}] {message}\n";
                Instance.LogViewer.ScrollToEnd(); // 自動スクロール（ScrollViewerにx:Name="LogViewer"を付けた場合）
            });
        }

        //再生基本機能
        private WaveOutEvent? outputDevice; // PCのスピーカーに相当
        private AudioFileReader? audioReader; // ファイルの読み手（蛇口）
        private OffsetSampleProvider? offsetProvider;
        private VolumeSampleProvider? volumeProvider;

        private void PlayAudio(AudioData data) {
            if (data.FilePath == null) return;
            StopAudio();// 既に再生中の場合は一度止める
            try {
                // 1. ファイルを読み込む（蛇口を開く）
                audioReader = new AudioFileReader(data.FilePath);
                //再生タイミング補正
                //サンプル数。正で遅延、負で切り落とし。
                offsetProvider = new OffsetSampleProvider(audioReader);
                int sampleAdjustment = 11967 * audioReader.WaveFormat.Channels;
                if (sampleAdjustment > 0) {
                    // 正の値：先頭に指定サンプル分の無音を挿入
                    offsetProvider.DelayBySamples = sampleAdjustment;
                } else {
                    // 負の値：先頭から指定サンプル分を読み飛ばす
                    offsetProvider.SkipOverSamples = Math.Abs(sampleAdjustment);
                }
                //音量補正
                volumeProvider = new VolumeSampleProvider(offsetProvider) {
                    Volume = 0.15f
                };

                // 2. スピーカー（出力デバイス）を準備
                outputDevice = new WaveOutEvent();
                // 3. スピーカーに蛇口を繋ぐ
                outputDevice.Init(volumeProvider);
                // 4. 再生開始
                outputDevice.Play();
                Log($"Started playing: {System.IO.Path.GetFileName(data.FilePath)}");
            } catch (System.Exception ex) { Log($"Playback Error: {ex.Message}"); }
        }
        private void StopAudio() {
            // 出口を止めて、リソースを解放する
            if (outputDevice != null) {
                outputDevice.Stop();
                outputDevice.Dispose();
                outputDevice = null;
            }
            // 蛇口を閉じる
            if (audioReader != null) {
                audioReader.Dispose();
                audioReader = null;
            }
        }
        //private MixingSampleProvider? mixer;


        private void PlayWithoutInstrumental(AudioData tergetAudio, AudioData offvocalAudio) {
            //tergetAudioの調整値が未算出ならエラーで止める
            if (tergetAudio.OffvocalAdjustments == null) {
                Log("Error: Target audio has no offvocal adjustment values calculated.");
                return;
            }
            var offvocalBuffer = new AllBufferedSpreader(offvocalAudio);
            var offvocalLeftAudio = new AudioBufferReader(offvocalBuffer, tergetAudio, 0);
            var offvocalRightAudio = new AudioBufferReader(offvocalBuffer, tergetAudio, 1);
            var offvocalStereo = new MultiplexingSampleProvider(new[] { offvocalLeftAudio, offvocalRightAudio }, 2);
            offvocalStereo.ConnectInputToOutput(0, 0);
            offvocalStereo.ConnectInputToOutput(1, 1);

            var tergetReader = new AudioFileReader(tergetAudio.FilePath);
            var reversedTergetProvider = new VolumeSampleProvider(tergetReader) { Volume = -1.0f };
            var sampleProvider = new MixingSampleProvider(new ISampleProvider[] { reversedTergetProvider, offvocalStereo });

            StopAudio();
            outputDevice = new WaveOutEvent();
            outputDevice.Init(sampleProvider);
            //outputDevice.Init(mixingRightAudio);
            outputDevice.Play();
        }


        private void RunTest() {
            Log("RunTest開始");

            var accesser = new MultiDriveAccesser(FileSystemPermissionBundle.AccessTestPermissionBundle);
            accesser.CreateEmptyFile(DriveTypeEnum.LocalDrive, "D:\\AccesserTest","AccesserTest.txt",true);
            accesser.CreateEmptyFile(DriveTypeEnum.LocalDrive, "AccesserTest", "AccesserTest.txt",true);

            //string currentPath;
            //currentPath = @"G:\CD音源\他音源\mp3音源\Illusionista!\01. イリュージョニスタ！ (M@STER VERSION).mp3";
            //currentPath = @"G:\CD音源\他音源\mp3音源\cm12\cm052\01.谷の底で咲く花は.mp3";

            //var accesser = new AudioAnalysisManager();
            //accesser.DiagnosticTestAsync();
            //try {
            //    accesser.AddToQueue(currentPath);
            //}catch(Exception ex) {
            //    MainWindow.Log($"{ex.Message}");
            //}

            //var spreader = new AllBufferedSpreader(new AudioData(currentPath));
            //int ln = spreader.LeftBuffer!.Length;
            //int FFTCount = ln / 256;
            //int FFTBlockSize = 256 * 8;
            //var magnitudes = new LinearArray<float>(FFTCount,FFTBlockSize);
            //var phases = new LinearArray<float>(FFTCount, FFTBlockSize);
            //spreader.LeftBuffer.AsSpan().CreateFFTMagnitudesAndPhases(
            //    magnitudes,
            //    phases,
            //    new System.Numerics.Complex[FFTBlockSize],
            //    FFTBlockSize,
            //    256,
            //    1.0e-9,
            //    true
            //    );
            //magnitudes.ToHeatmap("FFT_Test");
            //magnitudes.ToCsv("FFT_Test");

            //var analizer = new AudioAnalizer(spreader, true);
            //CsvDebugExporter.ExportMatrix(analizer.SelfSimilarityMatrix,"test.csv");
            //CsvDebugExporter.ExportList(spreader.LeftBuffer, "test.csv");
            //ArrayExporter.ToCsv(spreader.LeftBuffer.AsSpan(3_000_000,1_000_000), "test");
            //ArrayExporter.ToCsv(analizer.FrequencyChromaDatas, "test");
            //ArrayExporter.ToHeatmap(analizer.SelfSimilarityMatrix, "test");

            //string currentPath;
            //var testAudioList = new List<AudioData>();
            //currentPath = @"G:\CD音源\他音源\mp3音源\Illusionista!\01. イリュージョニスタ！ (M@STER VERSION).mp3";
            //testAudioList.Add(new AudioData(currentPath));
            //testAudioList[0].OffvocalAdjustments = new AudioAdjustmentValues(11967, 11967, 1.0f, 1.0f);
            //currentPath = @"G:\CD音源\他音源\mp3音源\Illusionista!\03. イリュージョニスタ！ (M@STER VERSION) (オリジナル・カラオケ).mp3";
            //testAudioList.Add(new AudioData(currentPath));
            //testAudioList[1].OffvocalAdjustments = new AudioAdjustmentValues(0, 0, -1.0f, -1.0f);
            ////testAudio.DebugOutput();
            ////PlayWithoutInstrumental(testAudioList[0], testAudioList[1]);
            //AudioAdjustmentCalculator calculator = new AudioAdjustmentCalculator(testAudioList[0], testAudioList[1]);
            //calculator.AudioAdjustmentCalculate();
            //Log(calculator.AudioAdjustmentValues.LeftOffsetSamples.ToString());
            //Log(calculator.AudioAdjustmentValues.RightOffsetSamples.ToString());
            //Log(calculator.AudioAdjustmentValues.LeftVolumeRatio.ToString());
            //Log(calculator.AudioAdjustmentValues.RightVolumeRatio.ToString());
            //testAudioList[0].OffvocalAdjustments = calculator.AudioAdjustmentValues;
            //PlayWithoutInstrumental(testAudioList[0], testAudioList[1]);
            Log("RunTest終了");
        }
    }


}





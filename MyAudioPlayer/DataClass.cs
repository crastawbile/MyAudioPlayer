using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyAudioPlayer{
    //Profile系統のrecordは、画面上で表示・編集する情報を管理する。
    //そうではない内部処理用の情報は、Dataクラスのプロパティとして管理。
    //Profileを扱うメソッドはDataクラス側で持ち、Profile自体は不変フィールドのみとする。
    //現物CDに対応するのがDiskData,
    //楽曲に対応するのがMusicData,
    //音源ファイルに対応するのがAudioData。

    public class AudioData{
        //Profileに含めない、内部処理用情報群
        //MusicDataID　現実の楽曲と対応する
        public int? MusicDataID { get; private set; }
        //DiskDataID　現実のCD(または類する流通単位)と対応する
        public int? DiskDataID { get; private set; }
        //AudioDataID　音源データファイルと対応する
        public int? AudioDataID { get; private set; }
        //音源ファイルパス
        public string? FilePath { get; private set; }
        //音源データから読み取った楽曲データ情報
        public AudioParameter AudioParameter { get; private set; }
        protected bool SetParameter(){
            if (FilePath == null) { return false; }
            using (var reader = new AudioFileReader(FilePath)){
                AudioParameter = new AudioParameter(
                    FilePath,
                    reader.TotalTime,
                    reader.WaveFormat.AverageBytesPerSecond * 8,
                    reader.WaveFormat.SampleRate,
                    reader.WaveFormat.Channels,
                    reader.WaveFormat.BitsPerSample,
                    reader.Length
                );
            }
            return true;
        }
        public AudioProfile Profile { get; private set; }
        //offvocal合成用の、調整値レコード
        public AudioAdjustmentValues? OffvocalAdjustments { get; set; } = null;

        //SQLからAudioDataIDで読み込む用のコンストラクタ

        //音源ファイルパスから読み込む用のコンストラクタ
        public AudioData(string filePath){
            FilePath = filePath;
            SetParameter();
        }
        //テスト出力
        public void DebugOutput(){
            MainWindow.Log($"--- Audio Info: {FilePath} ---");
            MainWindow.Log($"TotalTime: {AudioParameter.TotalTime}");
            MainWindow.Log($"Bitrate: {AudioParameter.Bitrate}");
            MainWindow.Log($"SampleRate: {AudioParameter.SampleRate}");
            MainWindow.Log($"Channels: {AudioParameter.Channels}");
            MainWindow.Log($"BitsPerSample: {AudioParameter.BitsPerSample}");
            MainWindow.Log($"Length: {AudioParameter.Length}");
        }
    }
    public sealed record AudioProfile{
        //対応するDiskDataのID
        public int? DiskDataID { get; init; }
        //対応するDiskDataのトラック番号
        public int? TrackNumber { get; init; }
        //対応するMusicDataのID
        public int? MusicDataID { get; init; }
        //歌唱者タイプ(offvocal、通常、ソロ音源、カバー音源)
        public SingerType SingerType { get; init; }
        //歌唱者タイプ内の何番目か
        public int? SingerTypeIndex { get; init; }
        //歌唱者リスト(キャラクター名=>CV名)
        public Dictionary<string, string> Singers { get; private set; } = new();
    }
    //歌唱者タイプのenum。
    public enum SingerType{
        Offvocal,//オフボーカル音源
        Normal,//通常音源
        Solo,//ソロ音源
        Cover,//カバー音源
        Alternate,//別バージョン音源
    }
    public enum AudioType {
        Long,//フルサイズ音源
        Short,//ショートサイズ音源
        Remix,//リミックス音源
        Stage,//ステージ音源
        RadioDrama,//ラジオドラマ音源
        VoiceCollection,//ボイス集音源
    }

    /// <summary>
    /// 音源データ情報
    /// </summary>
    /// <remarks>
    /// 楽曲データから直接読み取る情報群、よって、常に全部揃っている想定。
    /// </remarks>
    public sealed record AudioParameter(
        string FilePath,
        TimeSpan TotalTime,
        int Bitrate,
        int SampleRate,
        int Channels,
        int BitsPerSample,
        long Length
    );

    //ボーカル抽出用のパラメータ群
    public sealed record AudioAdjustmentValues(
            int LeftOffsetSamples,
            int RightOffsetSamples,
            float LeftVolumeRatio,
            float RightVolumeRatio
        );

    public record DiskData {
        public DiskProfile? Profile { get; set; }
        public int? DiskDataID { get; set; }
        //各トラックの音源データ。キーはトラック番号。基本的に1から始まる。
        public Dictionary<int,AudioData>? AudioDict { get; set; }
    }
    public sealed record DiskProfile(
        //CDシリーズ名
        string SeriesName,
        //CDタイトル
        string DiskTitle,
        //発売日
        DateTime ReleaseDate,
        //所持状態
        bool? isOwned
    );

    public class MusicData{
        public MusicProfile Profile { get; private set; }
        public Dictionary<int, AudioData> AudioTracks { get; private set; }
        //各音源所持状況は三値倫理で管理
        //trueは所持、falseは見所持、nullはそもそも存在しない音源
        public Dictionary<int, bool?> AudioOwnership { get; private set; }
    }
    /// <summary>
    /// 楽曲情報
    /// </summary>
    /// <remarks>
    /// プロパティはenumに列挙しておくこと
    /// 基本的に、公式サイトから手動で入力する想定。
    /// </remarks>

    public sealed record MusicProfile{
        //楽曲タイトル
        public string Title { get; init; }
        //CD発売日
        public DateTime ReleaseDate { get; init; }
        //作詞者
        public string Lyricist { get; init; }
        //作曲者
        public string Composer { get; init; }
        //編曲者
        public string Arranger { get; init; }
        //歌唱者リスト(キャラクター名=>CV名)
        public Dictionary<string, string> Singers { get; init; }
        //その他クレジット
        public string ExtraCredits { get; init; }
        //基本分類
        public string Category { get; init; }
        //色分類
        public string Color { get; init; }
        //物語型
        public string StoryType { get; init; }
    }
    enum MusicProfileField{
        Title,
        ReleaseDate,
        Lyricist,
        Composer,
        Arranger,
        Singers,
        ExtraCredits,
        Category,
        Color,
        StoryType
    }

}

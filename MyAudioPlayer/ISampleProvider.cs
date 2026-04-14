using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyAudioPlayer{
    //根本的に、ISampleProviderに干渉する部分。
    //AudioAdjustmentValuesを左右のみで作ってるんだから、こっちもステレオ音源前提で構わんな。

    //メモリに展開する部分。左右別々のバッファとする。
    public class AllBufferedSpreader : IDisposable{
        public double[]? LeftBuffer { get; private set; }
        public double[]? RightBuffer { get; private set; }
        public WaveFormat WaveFormat { get; init; }
        public string FilePath { get; init; }
        //コンストラクタ
        public AllBufferedSpreader(AudioData audioData, int marginSamplesSize = 0){
            FilePath = audioData.FilePath!;
            using (var reader = new AudioFileReader(FilePath)){
                LeftBuffer = new double[reader.Length / reader.WaveFormat.BlockAlign + marginSamplesSize * 2];
                RightBuffer = new double[reader.Length / reader.WaveFormat.BlockAlign + marginSamplesSize * 2];
                int stepSize = 1024;//一度に汲み取る単位
                var bucket = new float[stepSize * reader.WaveFormat.Channels];
                int validSamplesCount;
                int bufferdOffset = 0;
                while ((validSamplesCount = reader.Read(bucket, 0, bucket.Length)) > 0){
                    int validFrameCount = validSamplesCount / reader.WaveFormat.Channels;
                    for (var step = 0; step < validFrameCount; step++){
                        LeftBuffer[marginSamplesSize + bufferdOffset + step] = bucket[step * reader.WaveFormat.Channels];
                        RightBuffer[marginSamplesSize + bufferdOffset + step] = bucket[step * reader.WaveFormat.Channels + 1];
                    }
                    bufferdOffset += validFrameCount;
                }
                //WaveFormatは参照ではなく、コピーが必要。更に、チャンネル数1に変更する必要がある。
                WaveFormat = WaveFormat.CreateCustomFormat(
                    reader.WaveFormat.Encoding,
                    reader.WaveFormat.SampleRate,
                    1, // チャンネル数1に変更
                    reader.WaveFormat.AverageBytesPerSecond / reader.WaveFormat.Channels,
                    reader.WaveFormat.BlockAlign / reader.WaveFormat.Channels,
                    reader.WaveFormat.BitsPerSample
                );
            }
        }
        public void Dispose(){
            LeftBuffer = null;
            RightBuffer = null;
            // これで、このインスタンスが破棄された際に巨大なメモリがGC対象になります
        }
    }
    //バッファを1チャンネルのみ読み出すreader。
    //オフセットと音量の調整も行う。
    public class AudioBufferReader : ISampleProvider{
        private double[] _buffer;
        private double volumeRatio;
        public WaveFormat WaveFormat { get; init; }
        private int Position;
        public AudioBufferReader(AllBufferedSpreader spreader, AudioData tergetAudio, int channel){
            if (channel == 0){
                _buffer = spreader.LeftBuffer!;
                Position = -tergetAudio.OffvocalAdjustments!.LeftOffsetSamples;
                volumeRatio = tergetAudio.OffvocalAdjustments.LeftVolumeRatio;
            }else{
                _buffer = spreader.RightBuffer!;
                Position = -tergetAudio.OffvocalAdjustments!.RightOffsetSamples;
                volumeRatio = tergetAudio.OffvocalAdjustments.RightVolumeRatio;
            }
            WaveFormat = spreader.WaveFormat;
        }
        public int Read(float[] buffer, int offset, int count){
            int validSamplesCount = 0;
            for (var i1 = 0; i1 < count; i1++){
                if (Position < 0){
                    buffer[offset + i1] = 0.0f;//開始位置より前は無音扱い
                    validSamplesCount++;
                }else if (Position < _buffer.Length){
                    //有効範囲なら、音量補正を適用して読み出し
                    buffer[offset + i1] = (float)(_buffer[Position] * volumeRatio);
                    validSamplesCount++;
                }else{
                    buffer[offset + i1] = 0.0f;//終了位置より後は無音扱い
                    //validSamplesCountは増やさないが、無音は確実に入れておく
                }
                Position++;
            }
            return validSamplesCount;
        }
    }
}

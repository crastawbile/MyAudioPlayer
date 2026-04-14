using MathNet.Numerics.IntegralTransforms;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace MyAudioPlayer{
    public class AudioAnalizer{
        private double[] _sourceBuffer { get; init; }
        private double[] _absoluteBuffer { get; init; }
        private double[] _workSpaceAtSourceBufferSize1;
        private double[] _workSpaceAtSourceBufferSize2;
        private double[] _workSpaceAtSourceBufferSize3;
        private int _sourceBufferSize { get; init; }
        private int _samplingRate;
        public LinearArray<float> FrequencyMagnitudeDatas { get; private set; }
        public LinearArray<float> FrequencyChromaDatas { get; private set; }
        public LinearArray<float> FrequencyPhaseDatas { get; private set; }
        public float[] SpectralFlux { get; private set; }
        private int[] FrequencyToNote { get; init; }
        public LinearArray<float> SelfSimilarityMatrix { get; private set; }
        public float FrequencyConst { get; init; }//FFT結果のインデックスが対応する周波数
        public IEnumerable<string> FrequencyList { get; private set; }//CSV書き出し時のヘッダーとして使う周波数リスト
        public int FFTSize { get; init; } = 8192;//FFTするブロックのサイズ
        private int _FFTStepSize;//FFTする際のずらし幅。半分ずつずらしていく。
        public int FFTCount{ get; private set; }//FFTするブロックの数
        public int FrequencyCount{ get; init; }
        public float MagnitudeMinimum = 0.000001f;//FFT後の値を、無音扱いする閾値
        public AudioAnalizer(AllBufferedSpreader spreader,bool fromLeft) {
            if (fromLeft){
                _sourceBuffer = spreader.LeftBuffer!;
            }else {
                _sourceBuffer = spreader.RightBuffer!;
            }
            _sourceBufferSize = _sourceBuffer.Length;
            _absoluteBuffer = new double[_sourceBufferSize];
            _sourceBuffer.AsSpan().ChainToAbsolute(_absoluteBuffer.AsSpan());
            //for (var i= 0;i<_sourceBuffer.Length;i++) { _absoluteBuffer[i] = Math.Abs(_sourceBuffer[i]); }
            _workSpaceAtSourceBufferSize1 = new double[_sourceBufferSize];
            _workSpaceAtSourceBufferSize2 = new double[_sourceBufferSize];
            _workSpaceAtSourceBufferSize3 = new double[_sourceBufferSize];
            _samplingRate = spreader.WaveFormat.SampleRate;
            FrequencyConst = (float)spreader.WaveFormat.SampleRate / FFTSize;
            FrequencyCount = FFTSize / 2;
            FrequencyList= Enumerable.Range(0, FFTSize / 2).Select(i => (i * FrequencyConst).ToString("F1"));
            _FFTStepSize = FFTSize / 2;
            FFTCount = (_sourceBuffer.Length - FFTSize) / _FFTStepSize;
            FrequencyMagnitudeDatas = new LinearArray<float>(FFTCount,FrequencyCount);
            FrequencyChromaDatas = new LinearArray<float>(FFTCount, 12);
            FrequencyPhaseDatas = new LinearArray<float>(FFTCount, FrequencyCount);
            SelfSimilarityMatrix = new LinearArray<float>(FFTCount, FFTCount);
            SpectralFlux = new float[FFTCount];
            FrequencyToNote = new int[FrequencyCount];
            for (var frequencyIndex=0;frequencyIndex<FrequencyCount;frequencyIndex++) {
                float hz = frequencyIndex * FrequencyConst;
                if (hz < 20.0f || hz > 4186.0f) continue; // ピアノの範囲外は無視 というかmp3の範囲外？

                // 周波数 -> MIDIノート番号
                double midi = 69 + 12 * Math.Log2(hz / 440.0);
                // MIDI -> 12音階のインデックス (0=C, 1=C#...)
                int note = (int)Math.Round(midi) % 12;
                if (note < 0) note += 12;
                FrequencyToNote[frequencyIndex] = note;
            }
            SetFrequencyDatas();
            SetSpectralFlux();
            //SetSelfSimilarityMatrixFromMagnitude();
            SetSelfSimilarityMatrixFromChroma();

            MainWindow.Log("テスト部分開始");
            var result = new double[241];
            Parallel.For(60, 240, bpm => { result[bpm] = BeatTest(bpm); });
            ArrayExporter.ToCsv(result, "test_beat1");
            Parallel.For(60, 240, bpm => { result[bpm] = BeatTestByLittle(bpm); });
            ArrayExporter.ToCsv(result, "test_beat2");
            BeatTest(168, 0, true);
            BeatTestByLittle(168, true);
            //BeatTestLoop(168, [1, 2, 3, 5, 10, 20, 30, 50, 100, 200, 300, 500, 1000]);
            MainWindow.Log("テスト部分終了");

        }


        private void SetFrequencyDatas(){
            //MainWindow.Log("SetFrequencyDatas開始");
            Complex[] sampleBlock=new Complex[FFTSize];
            int blockNumber = 0;
            int blockIndex;
            //先に窓関数の係数を準備しておく
            double[] window=new double[FFTSize];
            for(blockIndex=0;blockIndex<FFTSize;blockIndex++){
                window[blockIndex] = (0.42 - 0.5 * Math.Cos(2 * Math.PI * blockIndex / (FFTSize - 1)) + 0.08 * Math.Cos(4 * Math.PI * blockIndex / (FFTSize - 1)));
            }
            int frequencyIndex;
            while (blockNumber<FFTCount) {
                for (blockIndex=0;blockIndex<FFTSize;blockIndex++) {
                    sampleBlock[blockIndex] = (Complex)_sourceBuffer[blockNumber * _FFTStepSize + blockIndex] * window[blockIndex];
                }
                Fourier.Forward(sampleBlock,FourierOptions.Matlab);
                var minSq = MagnitudeMinimum * MagnitudeMinimum;
                //ループ前のspan化
                var currentMag = FrequencyMagnitudeDatas[blockNumber];
                var currentPhase = FrequencyPhaseDatas[blockNumber];
                for (frequencyIndex=0;frequencyIndex< FrequencyCount; frequencyIndex++){
                    //不要な平方根計算を避けるために二乗で大きさを比較する
                    var magSq= sampleBlock[frequencyIndex].Real * sampleBlock[frequencyIndex].Real +
                        sampleBlock[frequencyIndex].Imaginary * sampleBlock[frequencyIndex].Imaginary;
                    if (magSq < minSq){//閾値以下の微小量はこの時点で0扱い
                        currentMag[frequencyIndex] = 0.0f;
                        currentPhase[frequencyIndex] = 0.0f;
                    }else {
                        currentMag[frequencyIndex] = (float)Math.Sqrt(magSq);
                        currentPhase[frequencyIndex] = (float)sampleBlock[frequencyIndex].Phase;
                    }
                }
                MagnitudeToChroma(FrequencyMagnitudeDatas[blockNumber], FrequencyChromaDatas[blockNumber]);
                blockNumber++;
            }
        }
        private void SetSpectralFlux() {
            for (int FFTIndex = 1; FFTIndex < FFTCount; FFTIndex++){
                float sum = 0;
                var currentFrame = FrequencyMagnitudeDatas[FFTIndex];
                var prevFrame = FrequencyMagnitudeDatas[FFTIndex - 1];
                // Span へのアクセスになるので、境界チェックが最適化され爆速になる
                for (int frequencyIndex = 0; frequencyIndex < FrequencyCount; frequencyIndex++){
                    // 前のフレームとの差分（精度優先のため、増加分だけをカウントするRectified方式）
                    float diff = currentFrame[frequencyIndex] - prevFrame[frequencyIndex];
                    if (diff > 0) sum += diff;
                }
                SpectralFlux[FFTIndex] = sum;
            }
        }
        private void SetSelfSimilarityMatrixFromMagnitude() {
            float selfSum;
            float[] selfSums = new float[FFTCount];
            for (int selfIndex = 0; selfIndex < FFTCount; selfIndex++) {
                selfSum = 0;
                //ループ対象のspan化
                var currentFrame = FrequencyMagnitudeDatas[selfIndex];
                for (int frequencyIndex = 0; frequencyIndex < FrequencyCount; frequencyIndex++){
                    selfSum += currentFrame[frequencyIndex] * currentFrame[frequencyIndex];
                }
                //selfSumsは、0かどうかと、最後の類似性計算時の平方根以外で使わないので、先に平方根取っておく
                selfSums[selfIndex] = (float)Math.Sqrt(selfSum);
            }
            for (var i = 0; i < FFTCount; i++){
                var matrixRow = SelfSimilarityMatrix[i];
                var sourceA = FrequencyMagnitudeDatas[i];
                for (var j = i; j < FFTCount; j++){
                    var similarity = VectorProcessor.CalculateCosineSimilarity(sourceA, FrequencyMagnitudeDatas[j], selfSums[i], selfSums[j]);
                    matrixRow[j] = similarity;
                    SelfSimilarityMatrix[j][i] = similarity;
                }
            }
        }
        private void SetSelfSimilarityMatrixFromChroma(){
            float selfSum;
            float[] selfSums = new float[FFTCount];
            var chromaDatas = FrequencyChromaDatas.Clone();
            SmoothChroma(ref chromaDatas);
            for (var i=0;i<FFTCount;i++)chromaDatas[i].ConvertInZScore();

            for (int selfIndex = 0; selfIndex < FFTCount; selfIndex++){
                selfSum = 0;
                //ループ対象のspan化
                var currentFrame = chromaDatas[selfIndex];
                for (int chromaIndex = 0; chromaIndex < 12; chromaIndex++){
                    selfSum += currentFrame[chromaIndex] * currentFrame[chromaIndex];
                }
                //selfSumsは、0かどうかと、最後の類似性計算時の平方根以外で使わないので、先に平方根取っておく
                selfSums[selfIndex] = (float)Math.Sqrt(selfSum);
            }
            for (var i = 0; i < FFTCount; i++){
                var matrixRow = SelfSimilarityMatrix[i];
                var sourceA = chromaDatas[i]; 
                for (var j = i; j < FFTCount; j++) {
                    var similarity=VectorProcessor.CalculateCosineSimilarity(sourceA, chromaDatas[j], selfSums[i], selfSums[j]);
                    matrixRow[j]=similarity;
                    SelfSimilarityMatrix[j][i]=similarity;
                }
            }

        }
        private void MagnitudeToChroma(ReadOnlySpan<float> magnitudes,Span<float> chroma) {
            chroma.Clear();
            for (int frequencyIndex = 0; frequencyIndex < FrequencyCount; frequencyIndex++){
                chroma[FrequencyToNote[frequencyIndex]] += magnitudes[frequencyIndex];
            }
            //精度向上のため、対数化
            for (var noteIndex=0;noteIndex<12;noteIndex++) chroma[noteIndex] = (float)Math.Max(0.0,2.0+Math.Log2(chroma[noteIndex]+ 1e-6f));
            // 精度向上のため、12次元の中で正規化（最大値を1にする等）
            float max = 0.0f;
            for (int i = 0; i < 12; i++) { if (chroma[i] > max) max = chroma[i]; }
            if (max > 0) { for (int i = 0; i < 12; i++) chroma[i] /= max; }
        }
        private void SmoothChroma(ref LinearArray<float> chromaDatas) {
            // --- 【追加】時間軸でスムージング ---
            var originalChroma = chromaDatas.Clone();
            int windowSize = 30; // 前後合計で約1秒分（環境に合わせて調整）

            for (int i = 0; i < FFTCount; i++){
                int count = 0;
                //ループ対象のspan化
                var mainChroma = chromaDatas[i];
                mainChroma.Clear();
                for (int j = Math.Max(0, i - windowSize / 2); j < Math.Min(FFTCount, i + windowSize / 2); j++){
                    //ループ対象のspan化
                    var subChroma = originalChroma[j];
                    for (int n = 0; n < 12; n++){
                        mainChroma[n] += subChroma[n];
                    }
                    count++;
                }
                for (int n = 0; n < 12; n++){
                    mainChroma[n] /= count; // 平均化
                }
            }
        }

        private double BeatTest(int beat,int windowSize=0,bool toCsv=false) {
            double beatStep=_samplingRate*60.0/beat;
            int beatRange = (int)Math.Ceiling(beatStep);
            var pulseShape = new double[beatRange].AsSpan();
            _absoluteBuffer.ChainToPulseShape(beatStep,pulseShape);
            if (toCsv) { pulseShape.ToCsv("BeatTest_pulseShape"); }
            return VectorProcessor.CalculateVariance(pulseShape, VectorProcessor.CalculateMean(pulseShape));
        }

        //windowSizeでpulseShapeDiffがどう変わるかのテスト
        private void BeatTestLoop(int beat,int[] windowSizes) {
            double beatStep = _samplingRate * 60 / beat;
            int beatRange = (int)Math.Ceiling(beatStep);
            var pulseShapes = new LinearArray<double>(windowSizes.Length,beatRange);
            var workspaceAtPulseSize = new double[beatRange].AsSpan();
            for (var i=0;i<windowSizes.Length;i++) {
                _absoluteBuffer.AsSpan()
                    //.ChainToMovingAverage(100,_workSpaceAtSourceBufferSize1)
                    .ChainToCompressedByMean(new double[_sourceBufferSize / 256], 256, false)
                    .ChainToPulseShape(beatStep, pulseShapes[i])
                    .ConvertInLaggedDifference(windowSizes[i], true,workspaceAtPulseSize)
                    .ConvertInLimit(0,null)
                    ;
            }
            pulseShapes.ToCSV("pulseShapeDiff");
        }

        //256サンプル毎に圧縮した波形データからでも十分読み取れるかのテスト
        private double BeatTestByLittle(int beat, bool toCsv = false){
            double beatStep = _samplingRate * 60.0 / beat / 256;
            int beatRange = (int)Math.Ceiling(beatStep);
            var pulseShape = new double[beatRange].AsSpan();
            _absoluteBuffer
                .ChainToCompressedByMean(new double[_sourceBufferSize / 256],256,false)
                .ChainToPulseShape(beatStep, pulseShape);
            if (toCsv) { pulseShape.ToCsv("BeatTestByLittle_pulseShape"); }
            return VectorProcessor.CalculateVariance(pulseShape, VectorProcessor.CalculateMean(pulseShape));
        }

    }
    //一般的なベクトル処理ではない、音響解析に特徴的な波形処理を行うメソッドチェイン用メソッドを置くためのクラス
    public static class AudioAnalizerHelper {

        public static void GenerateBlackmanWindow<T>(this Span<T> window)
            where T : struct, IFloatingPointIeee754<T>
        {
            int n = window.Length;
            T twopi = T.CreateChecked(2.0 * Math.PI);
            T fourpi = T.CreateChecked(4.0 * Math.PI);
            T den = T.CreateChecked(n - 1);

            for (int i = 0; i < n; i++){
                T phase = T.CreateChecked(i) / den;
                window[i] = T.CreateChecked(0.42)
                          - T.CreateChecked(0.5) * T.Cos(twopi * phase)
                          + T.CreateChecked(0.08) * T.Cos(fourpi * phase);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<Complex> PrepareFFTBlock(this ReadOnlySpan<double> data, ReadOnlySpan<double> window, Span<Complex> outputSpace){
            int ln = data.Length;
            for (int i = 0; i < ln; i++){ outputSpace[i] = new Complex(data[i] * window[i], 0.0); }
            return outputSpace;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExtractMagnitudeAndPhase(
            this ReadOnlySpan<Complex> fftResult,
            Span<float> magnitudes,
            Span<float> phases,
            double magnitudeThreshold)
        {
            double minSq = magnitudeThreshold * magnitudeThreshold;
            for (int i = 0; i < magnitudes.Length; i++){
                var c = fftResult[i];
                double magSq = c.Real * c.Real + c.Imaginary * c.Imaginary;
                if (magSq < minSq){
                    magnitudes[i] = 0f;
                    phases[i] = 0f;
                }else{
                    magnitudes[i] = (float)Math.Sqrt(magSq);
                    phases[i] = (float)c.Phase;
                }
            }
        }
        public static void CreateFFTMagnitudesAndPhases(
                this ReadOnlySpan<double> data,
                LinearArray<float> Magnitudes,
                LinearArray<float> phases,
                Complex[] workSpace,
                int FFTBlockSize,
                int FFTStepSize,
                double magnitudeThreshold,
                bool asZeroPadding)
        {
            int ln= data.Length;
            if (workSpace.Length != FFTBlockSize) throw new ArgumentException("woekSpaceの長さはちょうどFFTBlockSizeでなければならない", nameof(workSpace));
            var FFTBlock = workSpace.AsSpan();

            if (asZeroPadding){
                var window = new double[FFTStepSize];
                window.GenerateBlackmanWindow();
                int currentHead = 0;
                int currentIndex = 0;
                while (currentHead + FFTStepSize < ln){
                    FFTBlock.Clear();
                    data
                        .Slice(currentHead, FFTStepSize)
                        .PrepareFFTBlock(window, FFTBlock.Slice((FFTBlockSize - FFTStepSize) / 2, FFTStepSize))
                        ;
                    Fourier.Forward(workSpace, FourierOptions.Matlab);
                    FFTBlock.ExtractMagnitudeAndPhase(Magnitudes[currentIndex], phases[currentIndex], magnitudeThreshold);
                    currentIndex++;
                    currentHead += FFTStepSize;
                }
                return;
            }else {
                var window = new double[FFTBlockSize];
                window.GenerateBlackmanWindow();
                int ratio = FFTBlockSize / FFTStepSize;
                int currentIndex = ratio/2;
                int currentHead = FFTStepSize / 2;

                FFTBlock[..currentIndex]
                    .Clear()
                    ;
                while (currentHead + FFTBlockSize < ln){
                    FFTBlock.Clear();
                    data
                        .Slice(currentHead, FFTBlockSize)
                        .PrepareFFTBlock(window, FFTBlock)
                        ;
                    Fourier.Forward(workSpace, FourierOptions.Matlab);
                    FFTBlock.ExtractMagnitudeAndPhase(Magnitudes[currentIndex], phases[currentIndex], magnitudeThreshold);
                    currentIndex++;
                    currentHead += FFTStepSize;
                }
                FFTBlock[currentIndex..]
                    .Clear()
                    ;
                return;
            }

        }

        public static void GenerateTableFromFrequencyToNote(
                this int[] outputSpace,
                int FFTBlockSize,
                int SampleRate
            )
        {
            int count = FFTBlockSize / 2;
            float coefficient = (float)SampleRate / (float)FFTBlockSize;
            for (var i = 0; i < count; i++){
                float hz = i * coefficient;
                if (hz < 20.0f || hz > 4186.0f) continue; // ピアノの範囲外は無視 というかmp3の範囲外？

                // 周波数 -> MIDIノート番号
                double midi = 69 + 12 * Math.Log2(hz / 440.0);
                // MIDI -> 12音階のインデックス (0=C, 1=C#...)
                int note = (int)Math.Round(midi) % 12;
                if (note < 0) note += 12;
                outputSpace[i] = note;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<float> MagnitudeToChroma(
            this ReadOnlySpan<float> magnitude,
            Span<float> chroma,
            int FFTBlockSize,
            int[] FrequencyToNote
            )
        {
            chroma.Clear();
            for (int i = 0; i < FFTBlockSize; i++) { chroma[FrequencyToNote[i]] += magnitude[i]; }
            //精度向上のため、対数化            
            for (var noteIndex = 0; noteIndex < 12; noteIndex++) chroma[noteIndex] = (float)Math.Max(0.0, 2.0 + Math.Log2(chroma[noteIndex] + 1e-6f));
            // 精度向上のため、12次元の中で正規化（最大値を1にする等）
            return chroma.ConvertInNormalized(1.0f);
        }
        private static LinearArray<float> MagnitudesToChromas(
            this LinearArray<float> magnitudes,
            LinearArray<float> chromas,
            int FFTBlockSize,
            int[] FrequencyToNote
            )
        {
            int ln= magnitudes.Length;
            Parallel.For(0, ln, i => { magnitudes[i].MagnitudeToChroma(chromas[i], FFTBlockSize, FrequencyToNote); });
            return chromas;
        }

        /// <summary>
        /// 波形の畳み込み処理。double限定。
        /// </summary>
        /// <param name="continuousWave"></param>
        /// <param name="beatStep">畳みこむ長さ。</param>
        /// <param name="outputSpace">beatStep切り上げサイズのspan</param>
        /// <returns></returns>
        public static Span<double> ChainToPulseShape(this ReadOnlySpan<double> continuousWave, double beatStep,Span<double> outputSpace) {
            ArgumentOutOfRangeException.ThrowIfLessThan(beatStep, 1.0);

            //beatStepはfloatでもまだ少し余裕があるはずだけど、念のためにdoubleで扱う
            int beatRange = (int)Math.Ceiling(beatStep);
            if (outputSpace.Length < beatRange) throw new ArgumentException("Output space is too small.", nameof(outputSpace));
            double currentStep = 0.0;
            //beatRange-beatStepとcurrentHead-currentStepが合わさると、1以上ズレる恐れがある
            //片方だけなら、1未満になることを保証できる。
            while (currentStep + beatRange < continuousWave.Length){
                int currentHead = (int)Math.Floor(currentStep);
                for (int i = 0; i < beatRange; i++) {
                    outputSpace[i] += continuousWave[currentHead + i];
                }
                currentStep += beatStep;
            }
            return outputSpace[..beatRange];
        }
    }
}

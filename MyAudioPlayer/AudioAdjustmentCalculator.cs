using System;
using System.Collections.Generic;
using System.Text;

namespace MyAudioPlayer{
    public class AudioAdjustmentCalculator{
        private AllBufferedSpreader TergetAudio;
        private AllBufferedSpreader OffvocalAudio;
        private int _bufferMarginSamples;
        private int _targetLeftStart;
        private int _targetRightStart;
        private int _targetLeftEnd;
        private int _targetRightEnd;
        private int _offvocalLeftStart;
        private int _offvocalRightStart;
        private int _offvocalLeftEnd;
        private int _offvocalRightEnd;
        public AudioAdjustmentValues AudioAdjustmentValues { get; private set; } = new AudioAdjustmentValues(0, 0, 1.0f, 1.0f);
        //コンストラクタ
        public AudioAdjustmentCalculator(AudioData targetAudio, AudioData offvocalAudio){
            _bufferMarginSamples = targetAudio.AudioParameter.SampleRate * 10; //確実を期して、10秒分のマージンを取る
            TergetAudio = new AllBufferedSpreader(targetAudio, _bufferMarginSamples);
            OffvocalAudio = new AllBufferedSpreader(offvocalAudio, _bufferMarginSamples);
        }
        //有音範囲の端を返す。
        //普通に両端返して良かった気もするけど、今更なので放置。
        private int SearchSoundExistPosition(in double[] buffer, bool fromStart, double threshold = 0.005, int requiredConsecutive = 100){
            int count = 0;
            if (fromStart){
                for (var i1 = _bufferMarginSamples; i1 < buffer.Length; i1++){
                    if (Math.Abs(buffer[i1]) > threshold){
                        count++;
                        if (count >= requiredConsecutive){
                            return i1 - requiredConsecutive + 1;
                        }
                    }else{
                        count = 0;
                    }
                }
            }else{
                for (var i1 = buffer.Length - 1 - _bufferMarginSamples; i1 >= 0; i1--){
                    if (Math.Abs(buffer[i1]) > threshold){
                        count++;
                        if (count >= requiredConsecutive){
                            return i1 + requiredConsecutive - 1;
                        }
                    }else{
                        count = 0;
                    }
                }
            }
            //有音部分が存在しないのはさすがにエラーを返す
            throw new Exception("No sound found in the provided buffer.");
        }
        private Task<int> SearchOffsetSamples(double[] targetBuffer, double[] offvocalBuffer, int targetStart, int targetEnd, int offvocalStart, int offvocalEnd, int marginSamples = 5000){
            return Task.Run(() => {
                //有音開始位置差分値から最大限速い位置を確定
                int startOffsetSamples = Math.Max(targetStart - offvocalStart - marginSamples, -_bufferMarginSamples);
                //有音終了位置差分値から最大限遅い位置を確定
                int endOffsetSamples = Math.Min(targetEnd - offvocalEnd + marginSamples, _bufferMarginSamples);
                //開始位置-marginから終了位置+marginまでのズレ範囲で、音源全域で音量二乗平均の合計を取って、最小になるズレの位置を探す
                double sum;
                double mixedSample;
                //５００サンプルずつのトップ１０、
                //それぞれの前後２４０を３０サンプルずつのトップ５、
                //それぞれの前後１５を１サンプルずつのベスト１、
                //と３段階で絞り込む。
                List<int> best10Offsets = new List<int>();
                List<double> best10Sums = new List<double>();
                for (var i1 = startOffsetSamples; i1 <= endOffsetSamples; i1 += 500){
                    sum = 0.0f;
                    for (var i2 = targetStart; i2 <= targetEnd; i2 += 100){
                        mixedSample = targetBuffer[i2] - offvocalBuffer[i2 - i1];
                        sum += mixedSample * mixedSample;
                    }
                    //上位10個のみ、該当のoffsetとsumを保持
                    for (var rank = 0; rank < 10; rank++){
                        if (rank >= best10Sums.Count || sum < best10Sums[rank]){
                            best10Sums.Insert(rank, sum);
                            best10Offsets.Insert(rank, i1);
                            break;
                        }
                    }
                    if (best10Sums.Count > 10){
                        best10Sums.RemoveAt(10);
                        best10Offsets.RemoveAt(10);
                    }
                }
                List<int> best5Offsets = new List<int>();
                List<double> best5Sums = new List<double>();
                foreach (var baseOffset in best10Offsets){
                    for (var i1 = baseOffset - 240; i1 <= baseOffset + 240; i1 += 30){
                        sum = 0.0f;
                        for (var i2 = targetStart; i2 <= targetEnd; i2 += 100){
                            mixedSample = targetBuffer[i2] - offvocalBuffer[i2 - i1];
                            sum += mixedSample * mixedSample;
                        }
                        //上位5個のみ、該当のoffsetとsumを保持
                        for (var rank = 0; rank < 5; rank++){
                            if (rank >= best5Sums.Count || sum < best5Sums[rank]){
                                best5Sums.Insert(rank, sum);
                                best5Offsets.Insert(rank, i1);
                                break;
                            }
                        }
                        if (best5Sums.Count > 5){
                            best5Sums.RemoveAt(5);
                            best5Offsets.RemoveAt(5);
                        }
                    }
                }
                double? minimumSum = null;
                int bestOffset = 0;
                foreach (var baseOffset in best5Offsets){
                    for (var i1 = baseOffset - 15; i1 <= baseOffset + 15; i1++){
                        sum = 0.0f;
                        for (var i2 = targetStart; i2 <= targetEnd; i2 += 100){
                            mixedSample = targetBuffer[i2] - offvocalBuffer[i2 - i1];
                            sum += mixedSample * mixedSample;
                        }
                        if (minimumSum == null || sum < minimumSum){
                            minimumSum = sum;
                            bestOffset = i1;
                        }
                    }
                }
                return bestOffset;
            });
        }
        private Task<float> SearchVolumeRatio(double[] targetBuffer, double[] offvocalBuffer, int targetStart, int targetEnd, int offsetSamples){
            return Task.Run(() => {
                float low = 0.8f;
                float high = 1.2f;
                int samplePos;
                double mixedSample;
                double sum = 0f;
                for (samplePos = targetStart; samplePos < targetEnd; samplePos += 10){
                    mixedSample = targetBuffer[samplePos] - offvocalBuffer[samplePos - offsetSamples] * low;
                    sum += mixedSample * mixedSample;
                }
                double lowValue = sum;
                sum = 0f;
                for (samplePos = targetStart; samplePos < targetEnd; samplePos += 10){
                    mixedSample = targetBuffer[samplePos] - offvocalBuffer[samplePos - offsetSamples] * high;
                    sum += mixedSample * mixedSample;
                }
                double highValue = sum;

                float nearerVolumeRatio;
                double nearerValue;
                float fartherVolumeRatio;
                float middleVolumeRatio;
                double middleValue;
                if (lowValue < highValue){
                    nearerVolumeRatio = low;
                    nearerValue = lowValue;
                    fartherVolumeRatio = high;
                }else{
                    nearerVolumeRatio = high;
                    nearerValue = highValue;
                    fartherVolumeRatio = low;
                }
                for (var i1 = 0; i1 < 40; i1++){
                    middleVolumeRatio = (nearerVolumeRatio + fartherVolumeRatio) / 2.0f;
                    sum = 0f;
                    for (samplePos = targetStart; samplePos < targetEnd; samplePos += 10){
                        mixedSample = targetBuffer[samplePos] - offvocalBuffer[samplePos - offsetSamples] * middleVolumeRatio;
                        sum += mixedSample * mixedSample;
                    }
                    middleValue = sum;
                    if (middleValue == nearerValue){
                        break; // 完全一致したら終了
                    }else if (middleValue < nearerValue){
                        fartherVolumeRatio = nearerVolumeRatio;
                        nearerVolumeRatio = middleVolumeRatio;
                        nearerValue = middleValue;
                    }else{
                        fartherVolumeRatio = middleVolumeRatio;
                    }
                }
                return nearerVolumeRatio;
            });
        }
        public void AudioAdjustmentCalculate(double threshold = 0.005, int requiredConsecutive = 100){
            MainWindow.Log("AudioAdjustmentCalculate開始");
            int marginSamples = TergetAudio.WaveFormat.SampleRate / 10; //100ms分のマージンを取る

            //左チャンネルから
            //有音開始位置差分値
            _targetLeftStart = SearchSoundExistPosition(TergetAudio.LeftBuffer!, true, threshold, requiredConsecutive);
            _offvocalLeftStart = SearchSoundExistPosition(OffvocalAudio.LeftBuffer!, true, threshold, requiredConsecutive);
            //有音終了位置差分値
            _targetLeftEnd = SearchSoundExistPosition(TergetAudio.LeftBuffer!, false, threshold, requiredConsecutive);
            _offvocalLeftEnd = SearchSoundExistPosition(OffvocalAudio.LeftBuffer!, false, threshold, requiredConsecutive);
            var leftOffsetTask = SearchOffsetSamples(
                TergetAudio.LeftBuffer!,
                OffvocalAudio.LeftBuffer!,
                _targetLeftStart,
                _targetLeftEnd,
                _offvocalLeftStart,
                _offvocalLeftEnd,
                marginSamples
            );
            //右チャンネルも同様に
            _targetRightStart = SearchSoundExistPosition(TergetAudio.RightBuffer!, true, threshold, requiredConsecutive);
            _offvocalRightStart = SearchSoundExistPosition(OffvocalAudio.RightBuffer!, true, threshold, requiredConsecutive);
            _targetRightEnd = SearchSoundExistPosition(TergetAudio.RightBuffer!, false, threshold, requiredConsecutive);
            _offvocalRightEnd = SearchSoundExistPosition(OffvocalAudio.RightBuffer!, false, threshold, requiredConsecutive);
            var rightOffsetTask = SearchOffsetSamples(
                TergetAudio.RightBuffer!,
                OffvocalAudio.RightBuffer!,
                _targetRightStart,
                _targetRightEnd,
                _offvocalRightStart,
                _offvocalRightEnd,
                marginSamples
            );
            Task allOffsetTasks = Task.WhenAll(leftOffsetTask, rightOffsetTask);
            try{
                allOffsetTasks.Wait();
            }catch{
                foreach (var innerEx in allOffsetTasks.Exception!.InnerExceptions){
                    MainWindow.Log($"詳細エラー: {innerEx.Message}");
                }
            }
            AudioAdjustmentValues = AudioAdjustmentValues with{
                LeftOffsetSamples = leftOffsetTask.Result,
                RightOffsetSamples = rightOffsetTask.Result
            };
            //音量比率も算出。offvocal側の音量を変化させる。0.8倍から1.2倍まで、二分探索でfloatの限界まで探索する。
            var leftVolumeTask = SearchVolumeRatio(
                TergetAudio.LeftBuffer!,
                OffvocalAudio.LeftBuffer!,
                _targetLeftStart,
                _targetLeftEnd,
                AudioAdjustmentValues.LeftOffsetSamples
            );
            var rightVolumeTask = SearchVolumeRatio(
                TergetAudio.RightBuffer!,
                OffvocalAudio.RightBuffer!,
                _targetRightStart,
                _targetRightEnd,
                AudioAdjustmentValues.RightOffsetSamples
            );
            Task.WaitAll(leftVolumeTask, rightVolumeTask);
            AudioAdjustmentValues = AudioAdjustmentValues with{
                LeftVolumeRatio = leftVolumeTask.Result,
                RightVolumeRatio = rightVolumeTask.Result
            };
        }
    }
}

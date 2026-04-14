using MathNet.Numerics;
using Microsoft.VisualBasic.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SQLitePCL;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Windows;

namespace MyAudioPlayer{
    /// <summary>
    /// 二次元配列を装填した配列。[]でアクセス可能。
    /// </summary>
    /// <remarks>
    /// 配列を内部に保持するstructであるため、引数にとるときは必ずrefやinを使用すること。
    /// コピーのコストは払ってはいけない。
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public struct LinearArray<T> : IEnumerable<T>{
        private T[] B { get; init; }
        /// <summary>
        /// 指定した行と列の要素にアクセスします。
        /// </summary>
        public T this[int row, int column]{
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => B[row * ColumnsCount + column];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => B[row * ColumnsCount + column] = value;
        }
        /// <summary>
        /// 指定した行の Span を返します。
        /// これにより array[row][column] という記述での高速なアクセスが可能になります。
        /// </summary>
        /// <remarks>
        /// ゲッターで呼び出したspanに対してアクセスするため、これだけで書き込みも可能
        /// </remarks>
        public Span<T> this[int row]{
            [MethodImpl(MethodImplOptions.AggressiveInlining)]//インライン化を強制することでどうたらこうたら。
            get => B.AsSpan(row * ColumnsCount, ColumnsCount);
        }
        public int ColumnsCount { get; init; }
        public int RowsCount { get; init; }
        public int Length { get => B.Length; }
        public LinearArray(int rowsCount,int columnsCount) {
            ColumnsCount = columnsCount;
            RowsCount = rowsCount;
            B=new T[ColumnsCount*RowsCount];
        }
        public T Get(int row, int column) => B[row * ColumnsCount + column];
        public Span<T> GetRowSpan(int row) => B.AsSpan(row*ColumnsCount,ColumnsCount);
        public Memory<T> GetRowMemory(int row) => B.AsMemory(row * ColumnsCount, ColumnsCount);
        public void Set(int row, int column, T value) {
            B[row * ColumnsCount + column] = value;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<T> GetEnumerator() {
            foreach (var item in B) yield return item;
        }
        public readonly IEnumerable<Memory<T>> EnumerateRows() { for (int r = 0; r < RowsCount; r++) yield return B.AsMemory(r * ColumnsCount, ColumnsCount); }
        public bool ToCSV(string fileName, int? limit = 100_000_000, bool doAsMuchAsPossible = false){
            return ArrayExporter.ToCsv(this,fileName,limit,doAsMuchAsPossible);
        }
        /// <summary>
        /// 指定した行に、ソースデータの値を一括代入します。
        /// </summary>
        /// <remarks>
        /// data.AsSpan().CopyTo(linearArray[row])の形の方が速い。
        /// </remarks>
        public void SetRow(int row, ReadOnlySpan<T> source){
            if (source.Length != ColumnsCount){
                throw new ArgumentException($"Source length ({source.Length}) must match ColumnsCount ({ColumnsCount}).");
            }
            // AsSpan を使って対象行の範囲を取得し、一括コピー
            source.CopyTo(B.AsSpan(row * ColumnsCount, ColumnsCount));
        }
        /// <summary>
        /// 指定した行を特定の値（デフォルト値など）で高速に埋めます。
        /// </summary>
        /// <remarks>
        /// linearArray[row].Fill(value)の形の方が速い。
        /// 0埋めなら、linearArray[row].Clear()という手もある。
        /// </remarks>
        public void FillRow(int row, T value){B.AsSpan(row * ColumnsCount, ColumnsCount).Fill(value);}
        /// <summary>
        /// 配列の実体をコピーして、新しい LinearArray を生成します。
        /// </summary>
        public readonly LinearArray<T> Clone(){
            var clone = new LinearArray<T>(RowsCount, ColumnsCount);
            // Span を使ってメモリを一括コピー（最速）
            B.AsSpan().CopyTo(clone.B);
            return clone;
        }
        /// <summary>
        /// 指定した範囲とステップ（間隔）で新しい LinearArray を生成します。
        /// </summary>
        /// <param name="startRow">開始行（含む）</param>
        /// <param name="rowCount">抽出する行数</param>
        /// <param name="step">ステップ（1なら連続、2なら1行飛ばし）</param>
        public LinearArray<T> Slice(int startRow, int rowCount, int step = 1){
            if (startRow + (rowCount - 1) * step >= RowsCount){
                throw new ArgumentOutOfRangeException(nameof(rowCount), "範囲が元の配列を超えています。");
            }
            var result = new LinearArray<T>(rowCount, ColumnsCount);
            for (int i = 0; i < rowCount; i++){
                // 元の配列から i * step 行目を取得し、新しい配列の i 行目にコピー
                this[startRow + i * step].CopyTo(result[i]);
            }
            return result;
        }

        /// <summary>
        /// 全行を対象に、n行おきに抽出した新しい LinearArray を返します。
        /// </summary>
        public LinearArray<T> Downsample(int step){
            int newCount = (RowsCount + step - 1) / step; // 切り上げ計算
            return Slice(0, newCount, step);
        }

    }
    //主にデバッグ用の、配列の中身を書き出す手段の詰め合わせ
    public static class ArrayExporter{
        private static string BasePath => AppDomain.CurrentDomain.BaseDirectory;
        public static string GetFullPath(string absolutePath) { return System.IO.Path.Combine(BasePath, absolutePath); }
        public static bool ToCsv<T>(this LinearArray<T> data, string fileName="test", int? limit=100_000_000,bool doAsMuchAsPossible=true){
            int rowsLimit=data.RowsCount-1;
            int columnsLimit=data.ColumnsCount-1;
            bool toEnd = true;
            if (limit != null && limit < data.Length){
                if (!doAsMuchAsPossible) return false;
                rowsLimit = (int)limit / data.ColumnsCount;
                columnsLimit= (int)limit % data.ColumnsCount;
                toEnd = false;
            }
            Span<T> row;
            using var writer = new StreamWriter(GetFullPath($"{fileName}.csv"));
            for (var rowIndex=0;rowIndex<rowsLimit;rowIndex++) {
                row=data.GetRowSpan(rowIndex);
                for (var columnIndex=0;columnIndex<data.ColumnsCount-1;columnIndex++) {
                    writer.Write(row[columnIndex]);
                    writer.Write(",");
                }
                writer.Write(row[data.ColumnsCount-1]);
                writer.WriteLine();
            }
            row = data.GetRowSpan(rowsLimit);
            for (var columnIndex = 0; columnIndex < columnsLimit-1; columnIndex++){
                writer.Write(row[columnIndex]);
                writer.Write(",");
            }
            writer.Write(data[rowsLimit - 1][columnsLimit - 1]);
            writer.WriteLine();
            if (toEnd){
                return true;
            }else{
                writer.WriteLine("上限に到達して停止");
                return false;
            }
        }
        public static bool ToCsv<T>(this T[][] matrix, string fileName = "test", long? limit = 100_000_000, bool doAsMuchAsPossible = false) {
            if (limit == null) return JaggedToCsvWithoutLimit(matrix, fileName);
            if (doAsMuchAsPossible)return JaggedToCsvWithLimit(matrix, fileName, (long)limit);
            long totalCount = 0;
            for (var i = 0; i < matrix.Length; i++) {
                totalCount += matrix[i].Length;
                if (totalCount>limit) { return false; }
            }
            return JaggedToCsvWithoutLimit(matrix, fileName);
        }
        private static bool JaggedToCsvWithLimit<T>(T[][] matrix, string fileName, long limit){
            int currentCount = 0;
            using var writer = new StreamWriter(GetFullPath($"{fileName}.csv"));

            foreach (var row in matrix){
                // 行ごとの処理
                for (int j = 0; j < row.Length-1; j++){
                    if (currentCount >= limit) {
                        writer.WriteLine();
                        writer.WriteLine("上限に到達して停止");
                        return false;
                    }
                    currentCount++;
                    writer.Write(row[j]);
                    writer.Write(",");
                }
                writer.Write(row[row.Length-1]);
                writer.WriteLine(); // 行の終わりに改行
            }
            return true;
        }
        private static bool JaggedToCsvWithoutLimit<T>(T[][] matrix, string fileName){
            using var writer = new StreamWriter(GetFullPath($"{fileName}.csv"));
            foreach (var row in matrix){
                for (int j = 0; j < row.Length-1; j++){
                    writer.Write(row[j]);
                    writer.Write(",");
                }
                writer.Write(row[row.Length - 1]);
                writer.WriteLine();
            }
            return true;
        }
        public static bool SpanToCsv<T>(Span<T> data, string fileName = "test", string header = "Value", int? limit = 100_000_000, bool doAsMuchAsPossible = false){
            int totalToWrite= data.Length;
            if (limit!=null && data.Length > limit) {
                if (!doAsMuchAsPossible) return false;
                totalToWrite = (int)limit;
            }
            using var writer = new StreamWriter(GetFullPath($"{fileName}.csv"));
            writer.WriteLine($"index,{header}");
            for (var i=0;i<totalToWrite;i++) {
                writer.Write(i);
                writer.Write(",");
                writer.Write(data[i]);
                writer.WriteLine();
            }
            if (totalToWrite == data.Length){
                return true;
            }else{
                writer.WriteLine("上限に到達して停止");
                return false;
            }
        }
        //CSVをエクセル上で確認する時は、散布図を使うとインデックス行を読んでくれる。
        public static bool ToCsv(this Span<double> data, string fileName = "test", string header = "Value", int? limit = 100_000_000, bool doAsMuchAsPossible = false){
            return SpanToCsv(data, fileName, header, limit, doAsMuchAsPossible);
        }
        public static bool ToCsv(this Span<float> data, string fileName = "test", string header = "Value", int? limit = 100_000_000, bool doAsMuchAsPossible = false){
            return SpanToCsv(data, fileName, header, limit, doAsMuchAsPossible);
        }

        public static bool ToCsv<T>(this T[] data, string fileName="test", string header = "Value", int? limit = 100_000_000, bool doAsMuchAsPossible = false){
            int totalToWrite;
            if (limit != null){
                if (limit < data.Length){
                    if (!doAsMuchAsPossible) return false;
                    totalToWrite = (int)limit;
                }else{
                    totalToWrite = data.Length;
                }
            }else{
                totalToWrite = data.Length;
            }
            using var sw = new StreamWriter(GetFullPath($"{fileName}.csv"));
            sw.WriteLine($"index,{header}");
            for (var i = 0; i < totalToWrite; i++)sw.WriteLine($"{i},{data[i]}");
            return totalToWrite == data.Length;
        }

        public static void ToHeatmap(this LinearArray<float> data, string fileName = "test", float min = float.MaxValue, float max = float.MinValue){
            //正規化のベースになる最大値・最小値を、与えられていないなら計測する。
            if (min == float.MaxValue && max == float.MinValue){
                foreach (var value in data){
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            float range = max - min;
            if (range < 1e-9) range = 1.0f; // 0除算防止及び、ノイズの塗りつぶし

            // 2. ImageSharpの画像を作成 (L8は8bitグレースケール、Rgb24ならカラー)
            using var image = new Image<L8>(data.ColumnsCount, data.RowsCount);

            // 修正後のピクセル書き込み部分
            image.ProcessPixelRows(accessor => {
                for (int y = 0; y < accessor.Height; y++){
                    Span<L8> pixelRow = accessor.GetRowSpan(y);
                    var dataRow = data.GetRowSpan(y); // LinearArray の行

                    for (int x = 0; x < accessor.Width; x++){
                        float normalized = (dataRow[x] - min) / range;
                        pixelRow[x] = new L8((byte)(normalized * 255));
                    }
                }
            });
            // 3. 保存（拡張子からフォーマットを自動判別）
            image.Save(GetFullPath($"{fileName}.png"));
        }
        public static void ToHeatmap(this LinearArray<double> data, string fileName="test",double min = double.MaxValue,double max = double.MinValue){
            //正規化のベースになる最大値・最小値を、与えられていないなら計測する。
            if (min == double.MaxValue && max == double.MinValue) {
                foreach (var value in data) {
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            double range = max - min;
            if (range < 1e-9) range = 1.0; // 0除算防止及び、ノイズの塗りつぶし

            // 2. ImageSharpの画像を作成 (L8は8bitグレースケール、Rgb24ならカラー)
            using var image = new Image<L8>(data.ColumnsCount, data.RowsCount);

            // 修正後のピクセル書き込み部分
            image.ProcessPixelRows(accessor =>{
                for (int y = 0; y < accessor.Height; y++){
                    Span<L8> pixelRow = accessor.GetRowSpan(y);
                    var dataRow = data.GetRowSpan(y); // LinearArray の行

                    for (int x = 0; x < accessor.Width; x++){
                        double normalized = (dataRow[x] - min) / range;
                        pixelRow[x] = new L8((byte)(normalized * 255));
                    }
                }
            });
            // 3. 保存（拡張子からフォーマットを自動判別）
            image.Save(GetFullPath($"{ fileName}.png"));
        }
        public static void ToHeatmap(this float[][] data, string fileName = "test", float min = float.MaxValue, float max = float.MinValue){
            //正規化のベースになる最大値・最小値を、与えられていないなら計測する。
            if (min == float.MaxValue && max == float.MinValue){
                foreach (var row in data){
                    foreach (var value in row){
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }
            }
            int width = data[0].Length;

            float range = max - min;
            if (range < 1e-9) range = 1.0f; // 0除算防止及び、ノイズの塗りつぶし

            // 2. ImageSharpの画像を作成 (L8は8bitグレースケール、Rgb24ならカラー)
            using var image = new Image<L8>(width, data.Length);

            // 修正後のピクセル書き込み部分
            image.ProcessPixelRows(accessor => {
                for (int y = 0; y < accessor.Height; y++){
                    Span<L8> pixelRow = accessor.GetRowSpan(y);
                    var dataRow = data[y].AsSpan(0, width);

                    for (int x = 0; x < accessor.Width; x++){
                        float normalized = (dataRow[x] - min) / range;
                        pixelRow[x] = new L8((byte)(normalized * 255));
                    }
                }
            });
            // 3. 保存（拡張子からフォーマットを自動判別）
            image.Save(GetFullPath($"{fileName}.png"));
        }
        public static void ToHeatmap(this double[][] data, string fileName = "test", double min = double.MaxValue, double max = double.MinValue){
            //正規化のベースになる最大値・最小値を、与えられていないなら計測する。
            if (min == double.MaxValue && max == double.MinValue){
                foreach (var row in data){
                    foreach (var value in row) {
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }
            }
            int width = data[0].Length;

            double range = max - min;
            if (range < 1e-9) range = 1.0; // 0除算防止及び、ノイズの塗りつぶし

            // 2. ImageSharpの画像を作成 (L8は8bitグレースケール、Rgb24ならカラー)
            using var image = new Image<L8>(width, data.Length);

            // 修正後のピクセル書き込み部分
            image.ProcessPixelRows(accessor => {
                for (int y = 0; y < accessor.Height; y++){ 
                    Span<L8> pixelRow = accessor.GetRowSpan(y);
                    var dataRow = data[y].AsSpan(0, width); 

                    for (int x = 0; x < accessor.Width; x++){
                        double normalized = (dataRow[x] - min) / range;
                        pixelRow[x] = new L8((byte)(normalized * 255));
                    }
                }
            });
            // 3. 保存（拡張子からフォーマットを自動判別）
            image.Save(GetFullPath($"{fileName}.png"));
        }
    }

    //Spanを扱うメソッド群。基本、拡張メソッド化する。
    public static class VectorProcessor{
        /// <summary>
        /// 加算平均
        /// </summary>
        /// <remarks>整数型はOnDoubleで対応</remarks>
        /// <typeparam name="T">浮動小数型のみ</typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CalculateMean<T>(this ReadOnlySpan<T> data) where T : struct, IFloatingPoint<T>{
            if (data.IsEmpty) return T.Zero;
            T sum = T.Zero;
            foreach (var v in data) sum += v;
            return sum / T.CreateChecked(data.Length);
        }
        /// <inheritdoc cref="CalculateMean{T}(ReadOnlySpan{T})" path="summary"/>
        /// <remarks>小数型は通常版で対応</remarks>
        /// <typeparam name="T">整数型のみ</typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateMeanOnDouble<T>(this ReadOnlySpan<T> data) where T : struct, IBinaryInteger<T>{
            if (data.IsEmpty) return 0.0;
            double sum = 0.0;
            foreach (var v in data) sum += double.CreateChecked(v);
            return sum / data.Length;
        }
        /// <summary>
        /// 分散
        /// </summary>
        /// <remarks>
        /// 平均値をメソッド呼び出しごとに再計算するのは無駄なので、呼び出し側で事前に用意する前提。
        /// 無いなら無いで算出はする。
        /// 平均値との差の二乗の、加算平均
        /// </remarks>
        /// <typeparam name="T">浮動小数型のみ。整数型はOnDoubleで対応</typeparam>
        /// <param name="data"></param>
        /// <param name="mean">平均値：CalculateMean(data)</param>
        /// <param name="unbiased">true の場合は不偏分散(n-1)、false の場合は母分散(n)で割ります。</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CalculateVariance<T>(this ReadOnlySpan<T> data, T? mean=null,bool unbiased = false) where T : struct, IFloatingPoint<T>{
            if (data.Length < 2) return T.Zero;
            T m = mean ?? CalculateMean(data);
            T sumOfSquares = T.Zero;
            foreach (var v in data){
                var diff = v - m;
                sumOfSquares += diff * diff;
            }
            if (unbiased) return sumOfSquares / T.CreateChecked(data.Length - 1); 
            else return sumOfSquares / T.CreateChecked(data.Length); 
        }
        /// <inheritdoc cref="CalculateVariance{T}(ReadOnlySpan{T}, T?, bool)"/>
        /// <typeparam name="T">整数型のみ。小数型は通常版で対応</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateVarianceOnDouble<T>(this ReadOnlySpan<T> data, double? mean = null, bool unbiased = false) where T : struct, IBinaryInteger<T>{
            if (data.Length < 2) return 0.0;
            double m = mean ?? CalculateMeanOnDouble(data);
            double sumOfSquares = 0.0;
            foreach (var v in data){
                var diff = double.CreateTruncating(v) - m;
                sumOfSquares += diff * diff;
            }
            if (unbiased) return sumOfSquares / (data.Length - 1);
            else return sumOfSquares / data.Length;
        }
        /// <summary>
        /// 標準偏差
        /// </summary>
        /// <remarks>
        /// 平均値をメソッド呼び出しごとに再計算するのは無駄なので、呼び出し側で事前に用意する前提。
        /// 無いなら無いで算出はする。
        /// 平均値との差の、二乗平均
        /// </remarks>
        /// <typeparam name="T">浮動小数型のみ。整数型はOnDoubleで対応</typeparam>
        /// <param name="data"></param>
        /// <param name="mean">平均値：CalculateMeanOnDouble(data)</param>
        /// <param name="unbiased">true の場合は不偏分散(n-1)、false の場合は母分散(n)で割ります。</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CalculateDeviation<T>(this ReadOnlySpan<T> data, T? mean = null, bool unbiased = false) where T : struct, IFloatingPoint<T>, IRootFunctions<T>{
            var variance = CalculateVariance(data, mean, unbiased);
            return T.Sqrt(variance);
        }
        /// <inheritdoc cref="CalculateDeviation{T}(ReadOnlySpan{T}, T?, bool)"/>
        /// <typeparam name="T">整数型のみ。小数型は通常版で対応</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateDeviationOnDouble<T>(this ReadOnlySpan<T> data, double? mean = null, bool unbiased = false) where T : struct, IBinaryInteger<T>{
            return double.Sqrt(CalculateVarianceOnDouble(data, mean, unbiased));
        }
        public static T CalculateNorm<T>(this ReadOnlySpan<T> data) where T : struct, IFloatingPoint<T>, IRootFunctions<T> {
            var sum = T.Zero;
            foreach (var v in data) { sum += v * v; }
            return T.Sqrt(sum);
        }
        public static double CalculateNormOnDouble<T>(this ReadOnlySpan<T> data) where T : struct, IBinaryInteger<T>{
            var sum = T.Zero;
            foreach (var v in data) { sum += v * v; }
            return double.Sqrt(double.CreateTruncating(sum));
        }

        /// <summary>
        /// コサイン類似度計算
        /// </summary>
        /// <remarks>
        /// 行列計算時など、各ベクトルの大きさ(ノルム)は先に出しておいた方がいい。→CalculateNorm()
        /// 無いなら無いで算出はする。
        /// </remarks>
        /// <param name="vectorA"></param>
        /// <param name="vectorB"></param>
        /// <param name="normA">vectorAの各要素の二乗和の平方根</param>
        /// <param name="normB">vectorBの各要素の二乗和の平方根</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CalculateCosineSimilarity<T>(this ReadOnlySpan<T> vectorA, ReadOnlySpan<T> vectorB, T? normA=null, T? normB=null) where T : struct, IFloatingPoint<T>, IRootFunctions<T>{
            if (vectorA.Length != vectorB.Length) throw new ArgumentException("Lengths must match.");
            if (vectorA.IsEmpty) return T.Zero;

            if (normA.HasValue && normB.HasValue){
                // 両方のノルムが既知の場合（行列計算の最適化用）
                T nA = normA.Value;
                T nB = normB.Value;
                if (nA == T.Zero || nB == T.Zero) { return nA == nB ? T.One : T.Zero; }
                T crossSum = T.Zero;
                for (int i = 0; i < vectorA.Length; i++) crossSum += vectorA[i] * vectorB[i];
                return crossSum / (nA * nB);
            } else if (normA.HasValue){
                return CalculateCosineSimilarity(vectorA, vectorB, normA, vectorB.CalculateNorm());
            } else if (normB.HasValue){
                return CalculateCosineSimilarity(vectorA, vectorB, vectorA.CalculateNorm(), normB);
            } else {
                //両方のノルムが未知の場合、平方根計算を一回省く
                T sumA = T.Zero;
                T sumB = T.Zero;
                T crossSum = T.Zero;
                for (int i = 0; i < vectorA.Length; i++){
                    sumA += vectorA[i] * vectorA[i];
                    sumB += vectorB[i] * vectorB[i];
                    crossSum += vectorA[i] * vectorB[i];
                }
                if (sumA == T.Zero || sumB == T.Zero){ return (sumA == T.Zero && sumB == T.Zero) ? T.One : T.Zero;　}
                return crossSum / T.Sqrt(sumA * sumB);//floatの上限を超える可能性は警戒。
            }
        }
        /// <inheritdoc cref="CalculateCosineSimilarity{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, T?, T?)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateCosineSimilarityOnDouble<T>(this ReadOnlySpan<T> vectorA, ReadOnlySpan<T> vectorB, double? normA, double? normB) where T : struct, IBinaryInteger<T>{
            if (vectorA.Length != vectorB.Length) throw new ArgumentException("Lengths must match.");
            if (vectorA.IsEmpty) return 0.0;

            if (normA.HasValue && normB.HasValue){
                // 両方のノルムが既知の場合（行列計算の最適化用）
                double nA = normA.Value;
                double nB = normB.Value;
                if (nA == 0.0 || nB == 0.0) { return nA == nB ? 1.0 : 0.0; }
                double crossSum = 0.0;
                for (int i = 0; i < vectorA.Length; i++) crossSum += double.CreateTruncating(vectorA[i] * vectorB[i]);
                return crossSum / (nA * nB);
            }else if (normA.HasValue){
                return CalculateCosineSimilarityOnDouble(vectorA, vectorB, normA, vectorB.CalculateNormOnDouble());
            }else if (normB.HasValue){
                return CalculateCosineSimilarityOnDouble(vectorA, vectorB, vectorA.CalculateNormOnDouble(), normB);
            }else{
                //両方のノルムが未知の場合、平方根計算を一回省く
                T sumA = T.Zero;
                T sumB = T.Zero;
                T crossSum = T.Zero;
                for (int i = 0; i < vectorA.Length; i++){
                    T a = vectorA[i];
                    T b = vectorB[i];
                    sumA += a * a;
                    sumB += b * b;
                    crossSum += a * b;
                }
                if (sumA == T.Zero || sumB == T.Zero) { return sumA == sumB ? 1.0 : 0.0; }
                return double.CreateTruncating(crossSum) / double.Sqrt(double.CreateTruncating(sumA * sumB));
            }
        }

        //フィルタ処理系列　完全に小数値の事だけ考える

        /// <summary>
        /// 数値型のSpanを別の数値型に変換する。
        /// </summary>
        /// <typeparam name="TTo"></typeparam>
        /// <typeparam name="TFrom"></typeparam>
        /// <param name="data"></param>
        /// <param name="outputSpace"></param>
        /// <param name="createChecked">trueでCreateChecked、falseでCreateTruncating、nullでCreateSaturating</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<TTo> ChainToOtherNumber<TTo,TFrom>(Span<TFrom> data,Span<TTo> outputSpace,bool? createChecked=false)
            where TFrom : struct, INumber<TFrom>
            where TTo : struct, INumber<TTo>
        {
            int ln = data.Length;
            if (ln > outputSpace.Length) throw new ArgumentException("Lengths must match.");

            if (createChecked==true) {
                for (var i = 0; i < ln; i++) { outputSpace[i] = TTo.CreateChecked(data[i]); }//精度が下がるなら例外を出す。
            }else if (createChecked==null) {
                for (var i = 0; i < ln; i++) { outputSpace[i] = TTo.CreateSaturating(data[i]); }//上限・下限で飽和していい類の数値
            }else {
                for (var i = 0; i < ln; i++) { outputSpace[i] = TTo.CreateTruncating(data[i]); }//精度が犠牲になるが一番早い
            }
            return outputSpace[..ln];
        }



        /// <summary>
        /// Z-Score：平均値との差を標準偏差で割る。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="outputSpace"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ChainToZScore<T>(this ReadOnlySpan<T> data,Span<T> outputSpace)
            where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            int ln = data.Length;
            if (outputSpace.Length < ln) throw new ArgumentException("Output space is too small.", nameof(outputSpace));

            if (ln < 2){
                outputSpace[..ln].Clear();
                return outputSpace[..ln];
            }

            var average = data.CalculateMean();
            var deviation = data.CalculateDeviation(average);
            if (deviation > T.Zero){
                var multiplyer = T.One / deviation;
                for (var i = 0; i < ln; i++) { outputSpace[i] = (data[i] - average) * multiplyer; }
            }else{
                for (var i = 0; i < ln; i++) { outputSpace[i] = T.Zero; }
            }
            return outputSpace;
        }
        /// <inheritdoc cref="ChainToZScore{T}(ReadOnlySpan{T}, Span{T})" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ConvertInZScore<T>(this Span<T> data)
            where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            return ChainToZScore(data, data);
        }
        /// <summary>
        /// 移動平均:各値を、前後size分の加算平均に置き換える 
        /// </summary>
        /// <remarks>
        /// 内部的に累積和を使っているため、floatで巨大なSpanを使う場合は多少の誤差が出る可能性がある
        /// </remarks>
        /// <param name="data"></param>
        /// <param name="size"></param>
        /// <param name="outputSpace"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ChainToMovingAverage<T>(this ReadOnlySpan<T> data, Span<T> outputSpace, int size)
            where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(size, 0);
            int ln = data.Length;
            if (outputSpace.Length < ln) throw new ArgumentException("Output space is too small.", nameof(outputSpace));
            if (size == 0){
                if (!Unsafe.AreSame(ref Unsafe.AsRef(in data[0]), ref outputSpace[0]))
                    data.CopyTo(outputSpace);
                return outputSpace[..ln];
            }
            //size+1以下なら、全ての値が全体の加算平均になって終わり。
            if (ln<size+2) {
                T mean = data.CalculateMean();
                outputSpace[..ln].Fill(mean);
                return outputSpace[..ln];
            }
            T sum = T.Zero;
            for (var i = 0; i < size; i++) { sum += data[i]; }
            //そうでなければ、窓の大きさは変動する。
            //size*2+1以上あればsize*2+1まで拡大するが、より小さければ上限は変わる。
            if (ln > size*2) {
                //窓が徐々に大きくなっていく範囲の処理
                for (var i = 0; i < size; i++){
                    sum += data[i + size];
                    outputSpace[i] = sum / T.CreateTruncating(i + size + 1);
                }
                //窓の大きさが最大のsize*2+1である範囲の処理
                T windowWidth = T.CreateTruncating(size * 2 + 1);
                for (var i = size; i < ln - size; i++){
                    sum += data[i + size];
                    outputSpace[i] = sum / windowWidth;
                    sum -= data[i - size];
                }
                //窓が徐々に小さくなっていく範囲の処理
                for (var i = ln - size; i < ln; i++){
                    outputSpace[i] = sum / T.CreateTruncating(i + size + 1);
                    sum -= data[i - size];
                }
                return outputSpace[..ln];
            } else {
                int centerStart = ln-size;//現在地+sizeに要素が存在しなくなる位置
                int centerEnd = size;//現在地-sizeに要素が存在し始める位置
                T windowWidth = T.CreateTruncating(ln);
                //窓が徐々に大きくなっていく範囲の処理
                for (var i = 0; i < centerStart; i++){
                    sum += data[i + size];
                    outputSpace[i] = sum / T.CreateTruncating(i + size + 1);
                }
                //窓の大きさが最大のlnである範囲の処理
                for (var i = centerStart; i < centerEnd; i++){
                    outputSpace[i] = sum / windowWidth;
                }
                //窓が徐々に小さくなっていく範囲の処理
                for (var i = centerEnd; i < ln; i++){
                    outputSpace[i] = sum / T.CreateTruncating(i + size + 1);
                    sum -= data[i - size];
                }
                return outputSpace[..ln];
            }
        }
        /// <inheritdoc cref="ChainToMovingAverage{T}(ReadOnlySpan{T}, Span{T}, int)"/>
        /// <param name="workSpace">size+1サイズのバッファ。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ConvertInMovingAverage<T>(this Span<T> data, Span<T> workSpace, int size)
            where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(size, 0);
            int ln = data.Length;
            if (size==0) { return data; }
            //size+1以下なら、全ての値が全体の加算平均になって終わり。
            if (ln < size + 2){
                T mean = data.CalculateMean();
                data.Fill(mean);
                return data;
            }
            //workSpaceは、書き込まれる位置から、size個手前まで、一時的に保存するリングバッファとして使う
            int ringSize = size + 1;
            if (workSpace.Length < ringSize) throw new ArgumentException("Work space is too small.", nameof(workSpace));

            //size+2以上なら、窓の大きさは変動する
            T sum = T.Zero;
            T windowWidth = T.CreateChecked(size);
            for (var i = 0; i < size; i++) { sum += data[i]; }
            int ringIndex = 0;
            for (var i = 0; i < ln; i++) {
                if (i+size<ln) {
                    sum += data[i + size];
                    windowWidth++;
                }
                workSpace[ringIndex] = data[i];
                data[i] = sum / windowWidth;
                ringIndex = (ringIndex + 1) % ringSize;
                if (i - size > -1) {
                    sum -= workSpace[ringIndex];
                    windowWidth--;
                }
            }
            return data;
        }
        /// <summary>
        /// ラグ差分　
        /// </summary>
        /// <param name="data"></param>
        /// <param name="lagSize"></param>
        /// <param name="asLoop"></param>
        /// <param name="outputSpace"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ChainToLaggedDifference<T>(this ReadOnlySpan<T> data,int lagSize,bool asLoop,Span<T> outputSpace)
            where T : struct, INumber<T>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(lagSize, 1);
            int ln = data.Length;
            if (outputSpace.Length < ln) throw new ArgumentException("Output space is too small.", nameof(outputSpace));
            if (ln <= lagSize) throw new ArgumentException("ラグサイズが要素数以上の場合は対応外");

            //先頭部分の処理
            if (asLoop) {
                for (var i = 0; i < lagSize; i++) outputSpace[i] = data[i] - data[i + ln - lagSize];
            } else {
                for (var i = 0; i < lagSize; i++) outputSpace[i] = T.Zero;
            }
            //メインループ　逆順で回すより正順で回す方が明確に早いらしい。
            for (var i = lagSize; i < ln; i--) outputSpace[i] = data[i] - data[i - lagSize];
            return outputSpace[..ln];
        }
        /// <inheritdoc cref="ChainToLaggedDifference(Span{double}, int, bool, Span{double})" path="/summary"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ConvertInLaggedDifference<T>(this Span<T> data, int lagSize, bool asLoop, Span<T> workSpace)
            where T : struct, INumber<T>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(lagSize, 1);
            int ln = data.Length;
            if (workSpace.Length < lagSize) throw new ArgumentException("Work space is too small.", nameof(workSpace));
            if (ln <= lagSize) throw new ArgumentException("ラグサイズが要素数以上の場合は対応外");

            //先頭部分の処理
            if (asLoop){
                for (var i = 0; i < lagSize; i++)workSpace[i] = data[i] - data[i + ln - lagSize];
            }else{
                for (var i = 0; i < lagSize; i++)workSpace[i] = T.Zero;
            }
            //メインループ
            for (var i = ln - 1; i >= lagSize; i--)data[i] = data[i] - data[i - lagSize];
            workSpace[..lagSize].CopyTo(data[..lagSize]);
            return data;
        }
        /// <summary>
        /// 数値制限　境界値を超える分をその既定値に置き換える
        /// </summary>
        /// <param name="data"></param>
        /// <param name="minLimit">下限境界値</param>
        /// <param name="maxLimit">上限境界値</param>
        /// <param name="minValue">下限値超えを置き換える値</param>
        /// <param name="maxValue">上限値超えを置き換える値</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ChainToLimit<T>(
            this ReadOnlySpan<T> data,
            Span<T> outputSpace,
            T? minLimit = null,
            T? maxLimit = null,
            T? minValue = null,
            T? maxValue = null
        )
            where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            int ln = data.Length;
            if (outputSpace.Length < ln) throw new ArgumentException("Output space is too small.", nameof(outputSpace));

            if (minLimit.HasValue && maxLimit.HasValue){
                var low = minValue ?? minLimit.Value;
                var high = maxValue ?? maxLimit.Value;
                for (int i = 0; i < data.Length; i++) { outputSpace[i] = T.Clamp(data[i], low, high); }
            }else if (minLimit.HasValue){
                var low = minValue ?? minLimit.Value;
                for (int i = 0; i < data.Length; i++) { outputSpace[i] = T.Max(data[i], low); }
            }else if (maxLimit.HasValue){
                var high = maxValue ?? maxLimit.Value;
                for (int i = 0; i < data.Length; i++) { outputSpace[i] = T.Min(data[i], high); }
            }else if(!Unsafe.AreSame(ref Unsafe.AsRef(in data[0]), ref outputSpace[0])){
                data.CopyTo(outputSpace[..ln]);
            }
            return outputSpace[..ln];
        }
        /// <inheritdoc cref="ChainToLimit{T}(ReadOnlySpan{T}, Span{T}, T?, T?, T?, T?)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ConvertInLimit<T>(
            this Span<T> data,
            T? minLimit = null,
            T? maxLimit = null,
            T? minValue = null,
            T? maxValue = null
        )
                where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            return ChainToLimit(data, data, minLimit, maxLimit, minValue, maxValue);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ChainToAbsolute<T>(this ReadOnlySpan<T> data,Span<T> outputSpace)
            where T : struct, INumber<T>
        {
            int ln = data.Length;
            if (outputSpace.Length < ln) throw new ArgumentException("Output space is too small.", nameof(outputSpace));

            for (var i = 0; i < ln; i++) outputSpace[i] = T.Abs(data[i]);
            return outputSpace[..ln];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ConvertInAbsolute<T>(this Span<T> data)
            where T : struct, INumber<T>
        {
            return ChainToAbsolute(data, data);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ChainToScaled<T>(this ReadOnlySpan<T> data,Span<T> outputSpace, T multiplyer)
            where T : struct, INumber<T>
        {
            int ln = data.Length;
            if (outputSpace.Length < ln) throw new ArgumentException("Output space is too small.", nameof(outputSpace));

            for (var i = 0; i < ln; i++) outputSpace[i] = data[i] * multiplyer;
            return outputSpace[..ln];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ConvertInScaled<T>(this Span<T> data, T multiplyer)
            where T : struct, INumber<T>
        {
            return ChainToScaled(data, data, multiplyer);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ChainToNormalized<T>(this ReadOnlySpan<T> data, Span<T> outputSpace, T max)
            where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            int ln = data.Length;
            if (outputSpace.Length < ln) throw new ArgumentException("Output space is too small.", nameof(outputSpace));

            T currentMax = T.Zero;
            for (var i = 0; i < ln; i++) currentMax = T.Max(currentMax, T.Abs(data[i]));

            if (currentMax==T.Zero){//ゼロ除算ガード
                if (!Unsafe.AreSame(ref Unsafe.AsRef(in data[0]), ref outputSpace[0]))
                    data.CopyTo(outputSpace);
                return outputSpace[..ln];
            }

            T multiplyer = max/currentMax;
            for (var i = 0; i < ln; i++) outputSpace[i] = data[i] * multiplyer;
            return outputSpace[..ln];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ConvertInNormalized<T>(this Span<T> data, T max)
            where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            return ChainToNormalized(data, data, max);
        }




        /// <summary>
        /// 圧縮　stepSize毎にfuncで1要素に変換する。
        /// </summary>
        /// <remarks>
        /// デリゲート呼び出しがボトルネックになる可能性はある。
        /// 加算平均に関してはByMeanで。
        /// </remarks>
        /// <param name="data"></param>
        /// <param name="stepSize">1でも一応計算する。</param>
        /// <param name="func"></param>
        /// <param name="useFraction">末尾の端数分を使うかどうか。falseなら切り捨てる。</param>
        /// <param name="outputSpace"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ChainToCompressed<T>(this ReadOnlySpan<T> data, Span<T> outputSpace, int stepSize,Func<ReadOnlySpan<T>,T> func,bool useFraction)
            where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(stepSize, 1);
            int ln = data.Length;
            int stepsCount;
            stepsCount = ln / stepSize;
            int outputSize;
            if (useFraction) {
                outputSize = (ln + stepSize - 1) / stepSize;
            }else {
                outputSize = stepsCount;
            }
            if (outputSpace.Length < outputSize) throw new ArgumentException("Output space is too small.", nameof(outputSpace));

            for (var i = 0; i < stepsCount; i++) {outputSpace[i] = func(data.Slice(i * stepSize, stepSize));}
            if (outputSize > stepsCount) {outputSpace[stepsCount] = func(data[((stepsCount) * stepSize)..]);}
            return outputSpace[..outputSize];
        }
        /// <summary>
        /// 加算平均による圧縮　stepSize毎に加算平均で1要素に変換する。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="stepSize">1でも一応計算する。</param>
        /// <param name="func"></param>
        /// <param name="useFraction">末尾の端数分を使うかどうか。falseなら切り捨てる。</param>
        /// <param name="outputSpace"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ChainToCompressedByMean<T>(this ReadOnlySpan<T> data, Span<T> outputSpace, int stepSize, bool useFraction)
            where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(stepSize, 1);
            int ln = data.Length;
            int stepsCount;
            stepsCount = ln / stepSize;
            int outputSize;
            if (useFraction){
                outputSize = (ln + stepSize - 1) / stepSize;
            }else{
                outputSize = stepsCount;
            }
            if (outputSpace.Length < outputSize) throw new ArgumentException("Output space is too small.", nameof(outputSpace));

            for (var i = 0; i < stepsCount; i++) { outputSpace[i] = CalculateMean(data.Slice(i * stepSize, stepSize)); }
            if (outputSize > stepsCount) { outputSpace[stepsCount] = CalculateMean(data[((stepsCount) * stepSize)..]); }
            return outputSpace[..outputSize];
        }
    }
}

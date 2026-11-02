namespace Crast.Utilities.ExtensionMethods{
    public static class ExtensionMethods{
        /// <summary>
        /// HasFlag()の逆。
        /// </summary>
        /// <remarks>
        /// 親フラグの方が短い記述である場合に、parent.HasFlag(child)の代わりに使う。
        /// </remarks>
        /// <typeparam name="MyEnum"></typeparam>
        /// <param name="child"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static bool InFlag<MyEnum>(this MyEnum child, MyEnum parent)where MyEnum : struct, Enum { return parent.HasFlag(child); }
        /// <summary>
        /// 既存のEnumerableを非同期ストリームとして返すためのメソッド
        /// </summary>
        /// <remarks>
        /// async IAsyncEnumerable<T>の内部で、
        /// return FromEnumerable(既存のEnumerable);
        /// とすることで非同期ストリームを返す。
        /// 
        /// System.Linq.Asyncがあるなら、そっちを使う方がいい気はする。
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<T> FromEnumerable<T>(this IEnumerable<T> source){foreach (var item in source) yield return item;}
        /// <summary>
        /// PHPのarray_splice風の処理を、末尾の確定している可変長添字配列であるList型に対して行う。
        /// </summary>
        public static IList<T> Splice<T>(this IList<T> origin, int offset,　int? length = null, IEnumerable<T>? replacement = null) {
            var size = origin.Count;
            if (offset < -size || offset > size) throw new ArgumentOutOfRangeException($"{size}個のコレクションの{offset}番目に挿入しようとしている");
            var start = offset >= 0 ? offset : size + offset; 
            var result = origin.Take(start);
            if (replacement is IEnumerable<T> c) {
                result = result.Concat(c);
            }
            if (length == null) {
            }else if (length >= 0 && start + length < size) {
                result = result.Concat(origin.Skip(start + length.Value));
            } else if (length < 0 && start - length < size) {
                result = result.Concat(origin.Skip(size + length.Value));
            }
            return [..result];
        }
        /// <summary>
        /// LINQのConcat()による連結とTake()・Skip()による切り出しを組み合わせた挿入処理の一行化。
        /// </summary>
        public static IEnumerable<T> LINQInsert<T>(this IEnumerable<T> origin, int index, IEnumerable<T> added){
            return origin
                .Take(index)
                .Concat(added)
                .Concat(origin.Skip(index));
        }
        /// <summary>
        /// LINQのConcat()による連結とTake()・Skip()による切り出しを組み合わせた挿入処理の逆処理の一行化。
        /// </summary>
        /// <remarks>origin.LINQInsert(index,added)に等しい処理を行う。</remarks>
        public static IEnumerable<T> LINQEmbed<T>(this IEnumerable<T> added, int index, IEnumerable<T> origin){
            return origin
                .Take(index)
                .Concat(added)
                .Concat(origin.Skip(index));
        }
        /// <summary>
        /// LINQのTake()・Skip()による切り出しを組み合わせたslice処理の一行化。
        /// </summary>
        public static IEnumerable<T> LINQSlice<T>(this IEnumerable<T> origin, int index, int count) {
            return origin.Skip(index).Take(count);
        }
        /// <summary>
        /// LINQのConcat()による連結とTake()・Skip()による切り出しを組み合わせた逆slice処理の一行化。
        /// </summary>
        public static IEnumerable<T> LINQWithout<T>(this IEnumerable<T> origin, int index, int count){
            return origin.Take(index).Concat(origin.Skip(index + count));
        }
        /// <summary>
        /// 先頭に(T)を付ける型変換処理をメソッドチェイン記法で行う。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T ChainCastTo<T>(this object obj) => (T)obj;
    }
}

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
    }
}

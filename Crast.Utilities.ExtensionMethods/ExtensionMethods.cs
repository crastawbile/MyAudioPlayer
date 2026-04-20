namespace Crast.Utilities.ExtensionMethods
{
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

    }
}

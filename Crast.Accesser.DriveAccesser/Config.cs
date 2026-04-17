using System;
using System.Collections.Generic;
using System.Text;

namespace Crast.Accesser.DriveAccesser{
    internal static class Config{
        //文字コードのデフォルト設定
        // Python等との互換性を考慮し、BOMなしUTF-8をデフォルトにする
        public static readonly Encoding Encoding = new UTF8Encoding(false);
    }
}

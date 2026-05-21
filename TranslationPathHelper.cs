using System.IO;

/// <summary>
/// 提供翻译相关文件路径的兼容性支持，同时兼容 BepInEx 和 ReiPatcher 环境。
/// </summary>
public static class TranslationPathHelper
{
    /// <summary>
    /// 获取翻译文件的根目录（包含语言子目录）。
    /// 如果检测到 BepInEx 目录存在，则返回 BepInEx/Translation/{language}；
    /// 否则返回 Translation/{language}（ReiPatcher 模式）。
    /// </summary>
    public static string GetTranslationPath(string appDirectory, string language)
    {
        string bepInExPath = Path.Combine(appDirectory, "BepInEx");
        if (Directory.Exists(bepInExPath))
        {
            return Path.Combine(appDirectory, "BepInEx", "Translation", language);
        }

        return Path.Combine(appDirectory, "Translation", language);
    }
}

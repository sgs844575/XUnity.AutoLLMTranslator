using System.IO;

public static class PathUtils
{
    public static bool EnsureFolderExists(string folderPath)
    {
        try
        {
            // 检查路径是否是null或空字符串
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("文件夹路径不能为空或空白字符串。", nameof(folderPath));
            }

            // 检查文件夹是否存在
            if (!Directory.Exists(folderPath))
            {
                // 如果不存在，则创建文件夹
                Directory.CreateDirectory(folderPath);
                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("PathUtils", $"无法创建文件夹 '{folderPath}'。错误: {ex.Message}");
            return false;
        }
    }

    public static bool EnsureFileExists(string filePath, string initialContent = "")
    {
        try
        {
            if (!File.Exists(filePath))
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, initialContent);
                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("PathUtils", $"操作文件时出错: {ex.Message}");
            return false;
        }
    }
}

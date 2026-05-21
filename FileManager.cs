using System.IO;
using System.Collections.Generic;

public class FileManager
{
    public List<string> GetAllTxtFiles(string rootFolder)
    {
        List<string> txtFiles = new List<string>();
        try
        {
            // 获取当前目录的所有 .txt 文件（包含子目录）
            string[] files = Directory.GetFiles(
                rootFolder,
                "*.txt",
                SearchOption.AllDirectories
            );

            txtFiles.AddRange(files);
        }
        catch (UnauthorizedAccessException)
        {
            Logger.Warning("FileManager", $"无权限访问 {rootFolder} 的子目录");
        }
        catch (DirectoryNotFoundException)
        {
            Logger.Warning("FileManager", $"目录 {rootFolder} 不存在");
        }

        return txtFiles;
    }

    public List<string> GetAllJsonFiles(string rootFolder)
    {
        List<string> jsonFiles = new List<string>();
        try
        {
            string[] files = Directory.GetFiles(
                rootFolder,
                "*.json",
                SearchOption.AllDirectories
            );
            jsonFiles.AddRange(files);
        }
        catch (UnauthorizedAccessException)
        {
            Logger.Warning("FileManager", $"无权限访问 {rootFolder} 的子目录");
        }
        catch (DirectoryNotFoundException)
        {
            Logger.Warning("FileManager", $"目录 {rootFolder} 不存在");
        }

        return jsonFiles;
    }

    public string ReadFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }
        return string.Empty;
    }

    public void WriteFile(string filePath, string content)
    {
        PathUtils.EnsureFileExists(filePath, content);
    }
}

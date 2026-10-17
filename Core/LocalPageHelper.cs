// CustomizedBrowser\Core\LocalPageHelper.cs
using System;
using System.IO;
using System.Reflection;

namespace CustomizedBrowser.Core
{
    /// <summary>
    /// 本地页面帮助类
    /// 负责管理本地默认页面的创建、加载和路径解析
    /// </summary>
    public static class LocalPageHelper
    {
        /// <summary>
        /// 获取本地默认页面的URL（file:// 协议）
        /// </summary>
        /// <param name="customPath">用户自定义的本地页面路径</param>
        /// <returns>本地页面的URL</returns>
        public static string GetLocalPageUrl(string? customPath = null)
        {
            string filePath = GetLocalPagePath(customPath);
            return new Uri(filePath).AbsoluteUri;
        }

        /// <summary>
        /// 获取本地默认页面的物理文件路径
        /// </summary>
        /// <param name="customPath">用户自定义的本地页面路径</param>
        /// <returns>本地页面的物理路径</returns>
        public static string GetLocalPagePath(string? customPath = null)
        {
            // 如果用户指定了自定义路径，优先使用
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                if (File.Exists(customPath))
                {
                    return customPath;
                }
            }

            // 使用内置默认页面（确保文件存在）
            EnsureDefaultPageExists();
            return AppConstants.GetLocalDefaultPagePath();
        }

        /// <summary>
        /// 确保默认页面文件存在
        /// 如果数据目录中没有默认页面，则从嵌入资源或模板复制
        /// </summary>
        public static void EnsureDefaultPageExists()
        {
            string targetPath = AppConstants.GetLocalDefaultPagePath();
            
            if (File.Exists(targetPath))
            {
                return;
            }

            try
            {
                // 确保目录存在
                string? directoryPath = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // 尝试从嵌入资源复制
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "CustomizedBrowser.Core.newtab.html";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(targetPath);
                    stream.CopyTo(fileStream);
                    return;
                }

                // 如果嵌入资源不存在，尝试从应用目录复制
                string appDirectory = AppContext.BaseDirectory;
                string sourcePath = Path.Combine(appDirectory, "Core", "newtab.html");
                
                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, targetPath, true);
                    return;
                }

                // 最后兜底：生成一个简单的默认页面
                GenerateFallbackPage(targetPath);
            }
            catch (Exception)
            {
                // 静默处理，下次启动会重试
            }
        }

        /// <summary>
        /// 生成兜底的简单默认页面
        /// </summary>
        private static void GenerateFallbackPage(string targetPath)
        {
            string html = @"<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <title>新标签页</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            color: #fff;
            margin: 0;
        }
        .search-box {
            background: rgba(255,255,255,0.95);
            border-radius: 30px;
            padding: 15px 25px;
            display: flex;
            align-items: center;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            width: 400px;
        }
        .search-box input {
            flex: 1;
            border: none;
            outline: none;
            font-size: 16px;
            color: #333;
        }
        h1 { font-weight: 300; margin-bottom: 30px; }
    </style>
</head>
<body>
    <h1>Customized Browser</h1>
    <div class='search-box'>
        <input type='text' id='searchInput' placeholder='搜索或输入网址' autofocus>
    </div>
    <script>
        document.getElementById('searchInput').addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                var q = this.value.trim();
                if (q) {
                    if (q.startsWith('http')) window.location.href = q;
                    else if (q.includes('.') && !q.includes(' ')) window.location.href = 'https://' + q;
                    else window.location.href = 'https://www.google.com/search?q=' + encodeURIComponent(q);
                }
            }
        });
    </script>
</body>
</html>";
            
            File.WriteAllText(targetPath, html);
        }

        /// <summary>
        /// 判断本地默认页面是否存在
        /// </summary>
        /// <param name="customPath">用户自定义的本地页面路径</param>
        /// <returns>是否存在</returns>
        public static bool LocalPageExists(string? customPath = null)
        {
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                return File.Exists(customPath);
            }
            return File.Exists(AppConstants.GetLocalDefaultPagePath());
        }
    }
}

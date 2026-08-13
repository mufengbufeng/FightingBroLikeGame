using System;

namespace EF.Resource
{
    /// <summary>
    /// 校验微信和抖音小游戏 SDK 已知不支持的远端 URL 格式。
    /// </summary>
    internal static class MiniGameRemoteUrlValidator
    {
        private static readonly char[] AuthorityTerminatorChars = { '/', '?', '#' };

        /// <summary>
        /// 校验单个小游戏资源服务器根地址。
        /// </summary>
        public static void Validate(string remoteUrl, bool allowDevelopmentLoopbackWithPort = false)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl))
            {
                throw new InvalidOperationException("小游戏资源服务器地址不能为空");
            }

            if (remoteUrl.IndexOf('\\') >= 0)
            {
                throw new InvalidOperationException("小游戏资源服务器地址不能包含反斜杠：" + remoteUrl);
            }

            if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("小游戏资源服务器地址必须是 HTTP 或 HTTPS URL：" + remoteUrl);
            }

            if (HasExplicitPort(remoteUrl, uri) &&
                !(allowDevelopmentLoopbackWithPort && uri.IsLoopback))
            {
                throw new InvalidOperationException("小游戏资源服务器地址不能包含端口（开发构建的 loopback 地址除外）：" + remoteUrl);
            }

            if (uri.AbsolutePath.Contains("//"))
            {
                throw new InvalidOperationException("小游戏资源服务器地址的路径不能包含双斜杠：" + remoteUrl);
            }
        }

        /// <summary>
        /// 判断原始 URL authority 是否显式写入端口。
        /// Uri 会将 :80 和 :443 规范化为默认端口，因此不能只依赖 IsDefaultPort。
        /// </summary>
        private static bool HasExplicitPort(string remoteUrl, Uri uri)
        {
            string value = remoteUrl.Trim();
            int authorityStart = uri.Scheme.Length + 3;
            int authorityEnd = value.IndexOfAny(AuthorityTerminatorChars, authorityStart);
            string authority = authorityEnd >= 0
                ? value.Substring(authorityStart, authorityEnd - authorityStart)
                : value.Substring(authorityStart);

            if (authority.StartsWith("[", StringComparison.Ordinal))
            {
                int hostEnd = authority.IndexOf(']');
                return hostEnd >= 0 && hostEnd + 1 < authority.Length && authority[hostEnd + 1] == ':';
            }

            return authority.LastIndexOf(':') >= 0;
        }
    }
}

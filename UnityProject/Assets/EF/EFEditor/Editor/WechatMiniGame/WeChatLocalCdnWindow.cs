using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EF.Editor.WechatMiniGame
{
    /// <summary>
    /// 配置并启动仅用于本机调试的微信小游戏 CDN 服务。
    /// </summary>
    public sealed class WeChatLocalCdnWindow : EditorWindow
    {
        private const string MenuPath = "微信小游戏/本地 CDN 工具";
        private const string WindowTitle = "本地 CDN";
        private const string DefaultHost = "127.0.0.1";
        private const int DefaultPort = 18081;
        private const string DefaultNodeExecutable = "node";
        private const string PackageVersionFileName = "DefaultPackage.version";
        private const string PackageManifestPattern = "DefaultPackage_*.bytes";
        private const string ServerScriptFileName = "serve-wechat-streaming-assets.js";

        private string _cdnRoot;
        private string _nodeExecutable;
        private string _statusMessage;
        private MessageType _statusType;
        private Process _serverProcess;

        /// <summary>
        /// 打开本地 CDN 配置窗口。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<WeChatLocalCdnWindow>(WindowTitle);
            window.minSize = new Vector2(520f, 280f);
            window.Show();
        }

        private void OnEnable()
        {
            _cdnRoot = EditorPrefs.GetString(CdnRootPreferenceKey, FindLatestPackageRoot());
            _nodeExecutable = EditorPrefs.GetString(NodeExecutablePreferenceKey, DefaultNodeExecutable);
            RefreshStatus();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("CDN 资源包目录", EditorStyles.boldLabel);
            DrawCdnRootField();
            DrawNodeExecutableField();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("服务地址", GetEndpoint());
            DrawServiceButtons();
        }

        private void DrawCdnRootField()
        {
            EditorGUI.BeginChangeCheck();
            string newRoot = EditorGUILayout.TextField("CDN 文件夹", _cdnRoot ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                _cdnRoot = newRoot;
                _statusMessage = "目录已修改，保存后会校验资源包。";
                _statusType = MessageType.Info;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选择文件夹"))
            {
                SelectCdnRoot();
            }

            if (GUILayout.Button("使用最新构建目录"))
            {
                UseLatestPackageRoot();
            }

            if (GUILayout.Button("保存位置"))
            {
                SaveCdnRoot();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNodeExecutableField()
        {
            EditorGUI.BeginChangeCheck();
            string newNodeExecutable = EditorGUILayout.TextField("Node 可执行文件", _nodeExecutable ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                _nodeExecutable = newNodeExecutable;
                EditorPrefs.SetString(NodeExecutablePreferenceKey, _nodeExecutable);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("选择 Node", GUILayout.Width(100f)))
            {
                SelectNodeExecutable();
            }

            if (GUILayout.Button("使用 PATH", GUILayout.Width(100f)))
            {
                _nodeExecutable = DefaultNodeExecutable;
                EditorPrefs.SetString(NodeExecutablePreferenceKey, _nodeExecutable);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawServiceButtons()
        {
            bool isOwnedServerRunning = TryGetOwnedServer(out _);
            string startButtonText = isOwnedServerRunning ? "重启临时 CDN" : "启动临时 CDN";

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(startButtonText, GUILayout.Height(28f)))
            {
                StartOrRestartServer();
            }

            using (new EditorGUI.DisabledScope(!isOwnedServerRunning))
            {
                if (GUILayout.Button("停止本工具启动的 CDN", GUILayout.Height(28f)))
                {
                    StopOwnedServer();
                }
            }

            if (GUILayout.Button("打开地址", GUILayout.Height(28f)))
            {
                Application.OpenURL(GetEndpoint());
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SelectCdnRoot()
        {
            string initialDirectory = Directory.Exists(_cdnRoot) ? _cdnRoot : GetProjectRoot();
            string selectedDirectory = EditorUtility.OpenFolderPanel("选择本地 CDN 资源包目录", initialDirectory, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedDirectory))
            {
                return;
            }

            _cdnRoot = ResolvePackageRoot(selectedDirectory);
            SaveCdnRoot();
        }

        private void UseLatestPackageRoot()
        {
            string latestPackageRoot = FindLatestPackageRoot();
            if (string.IsNullOrWhiteSpace(latestPackageRoot))
            {
                SetStatus("未找到可用资源包，请先执行 YooAsset 资源构建。", MessageType.Warning);
                return;
            }

            _cdnRoot = latestPackageRoot;
            SaveCdnRoot();
        }

        private void SaveCdnRoot()
        {
            if (!TryValidatePackageRoot(_cdnRoot, out string packageRoot, out string message))
            {
                SetStatus(message, MessageType.Error);
                return;
            }

            _cdnRoot = packageRoot;
            EditorPrefs.SetString(CdnRootPreferenceKey, _cdnRoot);
            if (TryGetOwnedServer(out _) || IsPortInUse(DefaultPort))
            {
                message = $"目录已保存。运行中的 CDN 需要重启后才会使用新目录。{message}";
            }

            SetStatus(message, MessageType.Info);
        }

        private void SelectNodeExecutable()
        {
            string selectedFile = EditorUtility.OpenFilePanel("选择 Node 可执行文件", GetProjectRoot(), "exe");
            if (string.IsNullOrWhiteSpace(selectedFile))
            {
                return;
            }

            _nodeExecutable = selectedFile;
            EditorPrefs.SetString(NodeExecutablePreferenceKey, _nodeExecutable);
        }

        private void StartOrRestartServer()
        {
            if (!TryValidatePackageRoot(_cdnRoot, out string packageRoot, out string message))
            {
                SetStatus(message, MessageType.Error);
                return;
            }

            if (TryGetOwnedServer(out _))
            {
                StopOwnedServer();
            }

            if (IsPortInUse(DefaultPort))
            {
                SetStatus($"端口 {DefaultPort} 已被其他进程占用。为避免关闭未知进程，本工具不会接管它。", MessageType.Warning);
                return;
            }

            string serverScriptPath = GetServerScriptPath();
            if (!File.Exists(serverScriptPath))
            {
                SetStatus($"未找到本地 CDN 服务脚本：{serverScriptPath}", MessageType.Error);
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(_nodeExecutable) ? DefaultNodeExecutable : _nodeExecutable.Trim(),
                    Arguments = QuoteArgument(serverScriptPath),
                    WorkingDirectory = GetRepositoryRoot(),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.EnvironmentVariables["WX_YOO_CDN_ROOT"] = packageRoot;
                startInfo.EnvironmentVariables["WX_ASSET_HOST"] = DefaultHost;
                startInfo.EnvironmentVariables["WX_ASSET_PORT"] = DefaultPort.ToString();

                _serverProcess = Process.Start(startInfo);
                if (_serverProcess == null)
                {
                    SetStatus("未能启动本地 CDN 进程。", MessageType.Error);
                    return;
                }

                SessionState.SetInt(ServerProcessIdSessionKey, _serverProcess.Id);
                SessionState.SetString(ServerProcessStartTicksSessionKey, _serverProcess.StartTime.ToUniversalTime().Ticks.ToString());
                _cdnRoot = packageRoot;
                EditorPrefs.SetString(CdnRootPreferenceKey, _cdnRoot);
                SetStatus($"本地 CDN 已启动：{GetEndpoint()}，资源目录：{_cdnRoot}", MessageType.Info);
                Debug.Log($"[WeChatLocalCdn] 已启动本地 CDN，PID={_serverProcess.Id}，目录={_cdnRoot}");
            }
            catch (Exception exception)
            {
                ClearOwnedServerSession();
                SetStatus($"启动本地 CDN 失败：{exception.Message}", MessageType.Error);
                Debug.LogError($"[WeChatLocalCdn] 启动失败：{exception}");
            }
        }

        private void StopOwnedServer()
        {
            if (!TryGetOwnedServer(out Process process))
            {
                SetStatus("没有正在由本工具启动的 CDN 进程。", MessageType.Warning);
                return;
            }

            try
            {
                process.Kill();
                process.WaitForExit(3000);
                SetStatus("本工具启动的本地 CDN 已停止。", MessageType.Info);
                Debug.Log($"[WeChatLocalCdn] 已停止本地 CDN，PID={process.Id}");
            }
            catch (Exception exception)
            {
                SetStatus($"停止本地 CDN 失败：{exception.Message}", MessageType.Error);
                Debug.LogError($"[WeChatLocalCdn] 停止失败：{exception}");
            }
            finally
            {
                _serverProcess = null;
                ClearOwnedServerSession();
            }
        }

        private void RefreshStatus()
        {
            if (TryValidatePackageRoot(_cdnRoot, out string packageRoot, out string message))
            {
                _cdnRoot = packageRoot;
                if (TryGetOwnedServer(out Process process))
                {
                    SetStatus($"本工具启动的 CDN 正在运行，PID={process.Id}。{message}", MessageType.Info);
                    return;
                }

                if (IsPortInUse(DefaultPort))
                {
                    SetStatus($"端口 {DefaultPort} 已被其他进程占用。{message}", MessageType.Warning);
                    return;
                }

                SetStatus(message, MessageType.Info);
                return;
            }

            SetStatus(message, MessageType.Warning);
        }

        private bool TryGetOwnedServer(out Process process)
        {
            process = _serverProcess;
            if (IsRunning(process))
            {
                return true;
            }

            _serverProcess = null;
            int processId = SessionState.GetInt(ServerProcessIdSessionKey, 0);
            string startTicksText = SessionState.GetString(ServerProcessStartTicksSessionKey, string.Empty);
            if (processId <= 0 || !long.TryParse(startTicksText, out long startTicks))
            {
                return false;
            }

            try
            {
                Process candidate = Process.GetProcessById(processId);
                if (!IsRunning(candidate) || candidate.StartTime.ToUniversalTime().Ticks != startTicks)
                {
                    ClearOwnedServerSession();
                    return false;
                }

                _serverProcess = candidate;
                process = candidate;
                return true;
            }
            catch (ArgumentException)
            {
                ClearOwnedServerSession();
                return false;
            }
            catch (InvalidOperationException)
            {
                ClearOwnedServerSession();
                return false;
            }
        }

        private static bool IsRunning(Process process)
        {
            if (process == null)
            {
                return false;
            }

            try
            {
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static bool IsPortInUse(int port)
        {
            IPEndPoint[] listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            for (int index = 0; index < listeners.Length; index++)
            {
                if (listeners[index].Port == port)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryValidatePackageRoot(string root, out string packageRoot, out string message)
        {
            packageRoot = ResolvePackageRoot(root);
            if (string.IsNullOrWhiteSpace(packageRoot) || !Directory.Exists(packageRoot))
            {
                message = "请选择存在的 CDN 资源包目录。";
                return false;
            }

            string versionFilePath = Path.Combine(packageRoot, PackageVersionFileName);
            if (!File.Exists(versionFilePath))
            {
                message = $"目录缺少 {PackageVersionFileName}：{packageRoot}";
                return false;
            }

            string[] manifestFiles = Directory.GetFiles(packageRoot, PackageManifestPattern, SearchOption.TopDirectoryOnly);
            if (manifestFiles.Length == 0)
            {
                message = $"目录缺少资源清单 {PackageManifestPattern}：{packageRoot}";
                return false;
            }

            string version = File.ReadAllText(versionFilePath).Trim();
            string[] bundleFiles = Directory.GetFiles(packageRoot, "*.bundle", SearchOption.TopDirectoryOnly);
            message = $"资源包有效：版本 {version}，包含 {bundleFiles.Length} 个 bundle。";
            return true;
        }

        private static string FindLatestPackageRoot()
        {
            string packageVersionRoot = Path.Combine(GetProjectRoot(), "Bundles", "WebGL", "DefaultPackage");
            return ResolvePackageRoot(packageVersionRoot);
        }

        private static string ResolvePackageRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return string.Empty;
            }

            string fullRoot = Path.GetFullPath(root);
            if (HasPackageManifest(fullRoot))
            {
                return fullRoot;
            }

            if (!Directory.Exists(fullRoot))
            {
                return fullRoot;
            }

            DirectoryInfo[] childDirectories = new DirectoryInfo(fullRoot).GetDirectories();
            Array.Sort(childDirectories, CompareLastWriteTimeDescending);
            for (int index = 0; index < childDirectories.Length; index++)
            {
                string childPath = childDirectories[index].FullName;
                if (HasPackageManifest(childPath))
                {
                    return childPath;
                }
            }

            return fullRoot;
        }

        private static bool HasPackageManifest(string directory)
        {
            return Directory.Exists(directory) &&
                   File.Exists(Path.Combine(directory, PackageVersionFileName)) &&
                   Directory.GetFiles(directory, PackageManifestPattern, SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static int CompareLastWriteTimeDescending(DirectoryInfo left, DirectoryInfo right)
        {
            return right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string GetRepositoryRoot()
        {
            return Directory.GetParent(GetProjectRoot()).FullName;
        }

        private static string GetServerScriptPath()
        {
            return Path.Combine(GetRepositoryRoot(), "Tools", ServerScriptFileName);
        }

        private static string GetEndpoint()
        {
            return $"http://{DefaultHost}:{DefaultPort}";
        }

        private static string QuoteArgument(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        private static string GetProjectScopedPreferenceKey(string suffix)
        {
            string projectHash = Hash128.Compute(GetProjectRoot()).ToString();
            return $"EF.WechatLocalCdn.{projectHash}.{suffix}";
        }

        private static string CdnRootPreferenceKey => GetProjectScopedPreferenceKey("CdnRoot");
        private static string NodeExecutablePreferenceKey => GetProjectScopedPreferenceKey("NodeExecutable");
        private static string ServerProcessIdSessionKey => GetProjectScopedPreferenceKey("ServerProcessId");
        private static string ServerProcessStartTicksSessionKey => GetProjectScopedPreferenceKey("ServerProcessStartTicks");

        private void ClearOwnedServerSession()
        {
            SessionState.SetInt(ServerProcessIdSessionKey, 0);
            SessionState.SetString(ServerProcessStartTicksSessionKey, string.Empty);
        }

        private void SetStatus(string message, MessageType messageType)
        {
            _statusMessage = message;
            _statusType = messageType;
            Repaint();
        }
    }
}

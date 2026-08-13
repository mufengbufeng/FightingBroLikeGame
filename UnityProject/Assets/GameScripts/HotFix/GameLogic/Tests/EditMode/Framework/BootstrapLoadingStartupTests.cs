using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证启动遮罩在资源系统初始化前可用，并在框架骨架初始化后关闭。
    /// </summary>
    [TestFixture]
    public sealed class BootstrapLoadingStartupTests
    {
        private const string LoadingPrefabPath = "Assets/Resources/Bootstrap/BootstrapLoading.prefab";

        /// <summary>
        /// 启动 Prefab 必须位于 Resources 下，并保持为无需游戏资源系统的独立覆盖层。
        /// </summary>
        [Test]
        public void LoadingPrefab_位于Resources下并包含覆盖层组件()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingPrefabPath);

            Assert.IsNotNull(prefab, "缺少 Resources 启动 Prefab");
            Canvas canvas = prefab.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "启动 Prefab 必须包含 Canvas");
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.IsNotNull(prefab.GetComponent<CanvasScaler>(), "启动 Prefab 必须包含 CanvasScaler");
            Assert.IsNotNull(prefab.GetComponent<GraphicRaycaster>(), "启动 Prefab 必须包含 GraphicRaycaster");
            Assert.IsNotNull(prefab.transform.Find("Panel/StatusText"), "启动 Prefab 必须包含状态文案");
            Assert.IsNotNull(prefab.transform.Find("Panel/ProgressTrack/ProgressFill"), "启动 Prefab 必须包含进度条");
        }

        /// <summary>
        /// 启动入口必须先显示遮罩并让出一帧，再进入资源初始化。
        /// </summary>
        [Test]
        public void GameEntry_源码契约_资源初始化前显示启动遮罩()
        {
            string source = File.ReadAllText(GetProjectPath("Assets", "GameScripts", "Runtime", "GameEntry.cs"));
            int showIndex = source.IndexOf("BootstrapLoadingService.Show", System.StringComparison.Ordinal);
            int nextFrameIndex = source.IndexOf("await UniTask.NextFrame()", System.StringComparison.Ordinal);
            int initializeIndex = source.IndexOf("await _resourceManager.InitializeAsync", System.StringComparison.Ordinal);

            Assert.That(showIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(nextFrameIndex, Is.GreaterThan(showIndex));
            Assert.That(initializeIndex, Is.GreaterThan(nextFrameIndex));
        }

        /// <summary>
        /// 热更新框架骨架完成初始化后关闭遮罩，具体项目可改为首个窗口显示后关闭。
        /// </summary>
        [Test]
        public void GameLogicEntry_源码契约_框架初始化完成后关闭启动遮罩()
        {
            string source = File.ReadAllText(GetProjectPath(
                "Assets",
                "GameScripts",
                "HotFix",
                "GameLogic",
                "GameLogicEntry.cs"));
            int procedureIndex = source.IndexOf("InitializeProcedures();", System.StringComparison.Ordinal);
            int hideIndex = source.IndexOf("BootstrapLoadingService.Hide();", System.StringComparison.Ordinal);

            Assert.That(procedureIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(hideIndex, Is.GreaterThan(procedureIndex));
            StringAssert.Contains("using EF.Bootstrap;", source);
        }

        /// <summary>
        /// 将项目相对路径转换为测试可读的绝对路径。
        /// </summary>
        private static string GetProjectPath(params string[] parts)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..");
            foreach (string part in parts)
            {
                path = Path.Combine(path, part);
            }

            return Path.GetFullPath(path);
        }
    }
}

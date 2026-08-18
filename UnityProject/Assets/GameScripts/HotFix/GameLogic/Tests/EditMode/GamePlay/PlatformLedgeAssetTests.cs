using GameLogic.GamePlay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameLogic.Tests
{
    [TestFixture]
    public sealed class PlatformLedgeAssetTests
    {
        private static readonly Bounds PlatformBounds = new Bounds(Vector3.zero, new Vector3(4f, 1f, 0f));
        private static readonly Bounds PlayerBoundsFromLeft = new Bounds(
            new Vector3(-1.56f, 1.44f, 0f),
            new Vector3(1f, 2f, 0f));
        private static readonly Bounds PlayerBoundsFromRight = new Bounds(
            new Vector3(1.56f, 1.44f, 0f),
            new Vector3(1f, 2f, 0f));
        private static readonly PlatformFootCatchConfig FootCatchConfig = new PlatformFootCatchConfig(
            0.10f,
            0.12f,
            0.02f,
            new Vector2(0.24f, 0.24f));

        private static bool TryResolveLanding(
            Bounds playerBounds,
            Bounds platformBounds,
            float moveX,
            out Vector2 targetBoundsCenter)
        {
            return PlatformLedgeResolver.TryResolveLanding(
                playerBounds,
                platformBounds,
                moveX,
                FootCatchConfig,
                out targetBoundsCenter);
        }


        [Test]
        public void 左侧脚部卡边_小幅校正到顶面()
        {
            bool resolved = TryResolveLanding(
                PlayerBoundsFromLeft,
                PlatformBounds,
                1f,
                out Vector2 target);

            Assert.That(resolved, Is.True);
            Assert.That(target, Is.EqualTo(new Vector2(-1.48f, 1.52f)));
            Assert.That(Mathf.Abs(target.x - PlayerBoundsFromLeft.center.x), Is.LessThanOrEqualTo(0.12f));
            Assert.That(Mathf.Abs(target.y - PlayerBoundsFromLeft.center.y), Is.LessThanOrEqualTo(0.12f));

        }

        [Test]
        public void 右侧脚部卡边_小幅校正到顶面()
        {
            bool resolved = TryResolveLanding(
                PlayerBoundsFromRight,
                PlatformBounds,
                -1f,
                out Vector2 target);

            Assert.That(resolved, Is.True);
            Assert.That(target, Is.EqualTo(new Vector2(1.48f, 1.52f)));
            Assert.That(Mathf.Abs(target.x - PlayerBoundsFromRight.center.x), Is.LessThanOrEqualTo(0.12f));
            Assert.That(Mathf.Abs(target.y - PlayerBoundsFromRight.center.y), Is.LessThanOrEqualTo(0.12f));

        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(0.1f)]
        public void 非向内输入_不能登台(float moveX)
        {
            Assert.That(
                TryResolveLanding(
                    PlayerBoundsFromLeft,
                    PlatformBounds,
                    moveX,
                    out _),
                Is.False);
        }

        [Test]
        public void 需要大幅水平校正_不能登台()
        {
            Bounds playerBounds = new Bounds(new Vector3(-1.65f, 1.44f, 0f), new Vector3(1f, 2f, 0f));

            Assert.That(
                TryResolveLanding(playerBounds, PlatformBounds, 1f, out _),
                Is.False);
        }

        [Test]
        public void 脚部陷入平台过深_不能登台()
        {
            Bounds playerBounds = new Bounds(new Vector3(-1.56f, 1.25f, 0f), new Vector3(1f, 2f, 0f));

            Assert.That(
                TryResolveLanding(playerBounds, PlatformBounds, 1f, out _),
                Is.False);
        }

        [Test]
        public void 脚部高于平台顶缘_不能登台()
        {
            Bounds playerBounds = new Bounds(new Vector3(-1.56f, 1.55f, 0f), new Vector3(1f, 2f, 0f));

            Assert.That(
                TryResolveLanding(playerBounds, PlatformBounds, 1f, out _),
                Is.False);
        }


        [Test]
        public void 平台过窄_不能登台()
        {
            Bounds narrowPlatform = new Bounds(Vector3.zero, new Vector3(1.03f, 1f, 0f));
            Bounds playerBounds = new Bounds(new Vector3(-1.015f, -0.5f, 0f), new Vector3(1f, 2f, 0f));

            Assert.That(
                TryResolveLanding(playerBounds, narrowPlatform, 1f, out _),
                Is.False);
        }
        [Test]
        public void 关卡预制体_仅指定平台可登台且梯顶可承托()
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/AssetRaw/GamePlay/Level_01.prefab");
            try
            {
                Transform platformLow = root.transform.Find("PlatformLow");
                Transform platformHigh = root.transform.Find("PlatformHigh");
                Assert.That(platformLow, Is.Not.Null);
                Assert.That(platformHigh, Is.Not.Null);
                Assert.That(platformLow.GetComponent<PlatformLedge>(), Is.Not.Null);
                Assert.That(platformHigh.GetComponent<PlatformLedge>(), Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<PlatformLedge>(true), Has.Length.EqualTo(2));
                GameObject playerPrefab = PrefabUtility.LoadPrefabContents(
                    "Assets/AssetRaw/GamePlay/GamePlayPlayer_01.prefab");
                try
                {
                    PlayerFootCatchSettings footCatchSettings = playerPrefab.GetComponent<PlayerFootCatchSettings>();
                    Assert.That(footCatchSettings, Is.Not.Null);
                    Assert.That(footCatchSettings.FootCatchDepth, Is.EqualTo(0.32f));
                    Assert.That(footCatchSettings.MaxSnapCorrection, Is.EqualTo(0.12f));
                    Assert.That(footCatchSettings.LandingInset, Is.EqualTo(0.02f));
                    Assert.That(footCatchSettings.QueryPadding, Is.EqualTo(new Vector2(0.24f, 0.24f)));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(playerPrefab);
                }


                Transform ladderTop = root.transform.Find("Ladder/LadderTopPlatform");
                Assert.That(ladderTop, Is.Not.Null);
                BoxCollider2D ladderTopCollider = ladderTop.GetComponent<BoxCollider2D>();
                PlatformEffector2D effector = ladderTop.GetComponent<PlatformEffector2D>();
                Assert.That(ladderTopCollider, Is.Not.Null);
                Assert.That(ladderTopCollider.isTrigger, Is.False);
                Assert.That(ladderTopCollider.usedByEffector, Is.True);
                Assert.That(effector, Is.Not.Null);
                Assert.That(effector.useOneWay, Is.True);
                Assert.That(effector.useSideFriction, Is.False);
                Assert.That(effector.useSideBounce, Is.False);
                Assert.That(effector.surfaceArc, Is.EqualTo(180f));

                BoxCollider2D platformHighCollider = platformHigh.GetComponent<BoxCollider2D>();
                Assert.That(platformHighCollider, Is.Not.Null);
                Assert.That(ladderTopCollider.bounds.max.x, Is.GreaterThanOrEqualTo(platformHighCollider.bounds.min.x));
                Assert.That(
                    Mathf.Abs(ladderTopCollider.bounds.max.y - platformHighCollider.bounds.max.y),
                    Is.LessThanOrEqualTo(0.02f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

    }
}

using EF.Debugger;
using UnityEngine;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 玩家实体的体验优化逻辑。
    /// </summary>
    public sealed partial class PlayerEntity
    {
        private PlayerFootCatchSettings _footCatchSettings;
        private PlatformFootCatchConfig _footCatchConfig;

        /// <summary>
        /// 初始化玩家预制体上的体验优化配置。
        /// </summary>
        private void InitializeExperienceOptimizations()
        {
            _footCatchSettings = Handle.GetComponent<PlayerFootCatchSettings>();
            _footCatchConfig = _footCatchSettings != null
                ? _footCatchSettings.CreateConfig()
                : default;
            if (_footCatchSettings == null)
            {
                Log.Error("[PlayerEntity] 玩家预制体缺少 PlayerFootCatchSettings。");
            }
        }

        /// <summary>
        /// 在移动电机计算前应用玩家体验优化。
        /// </summary>
        /// <param name="input">本帧电机输入。</param>
        /// <param name="contacts">待修正的本帧电机接触结果。</param>
        private void ApplyExperienceOptimizations(in PlayerMotorInput input, ref PlayerMotorContacts contacts)
        {
            if (MovementState == PlayerMoveState.Airborne)
            {
                TrySnapToPlatformLedge(in input, ref contacts);
            }
        }

        /// <summary>
        /// 将脚部卡在标记平台顶缘的空中玩家小幅校正到站立位置。
        /// </summary>
        /// <param name="input">本帧电机输入。</param>
        /// <param name="contacts">成功时会更新为接地状态的电机接触结果。</param>
        /// <returns>发生脚部卡边校正时为 true。</returns>
        private bool TrySnapToPlatformLedge(in PlayerMotorInput input, ref PlayerMotorContacts contacts)
        {
            if (_footCatchSettings == null)
            {
                return false;
            }

            Bounds playerBounds = _capsule.bounds;
            int hitCount = Physics2D.OverlapBox(
                playerBounds.center,
                new Vector2(playerBounds.size.x, playerBounds.size.y) + _footCatchConfig.QueryPadding,
                0f,
                GroundFilter,
                _overlapHits);
            if (hitCount >= _overlapHits.Length)
            {
                return false;
            }

            BoxCollider2D candidate = null;
            Vector2 targetBoundsCenter = default;
            float nearestHorizontalCorrection = float.PositiveInfinity;
            float highestPlatformTop = float.NegativeInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                BoxCollider2D collider = _overlapHits[i] as BoxCollider2D;
                if (collider == null || collider.isTrigger || collider.GetComponent<PlatformLedge>() == null)
                {
                    continue;
                }

                Bounds platformBounds = collider.bounds;
                if (!PlatformLedgeResolver.TryResolveLanding(
                        playerBounds,
                        platformBounds,
                        input.Move.x,
                        in _footCatchConfig,
                        out Vector2 resolvedTarget))
                {
                    continue;
                }

                float horizontalCorrection = Mathf.Abs(resolvedTarget.x - playerBounds.center.x);
                if (horizontalCorrection < nearestHorizontalCorrection
                    || (Mathf.Approximately(horizontalCorrection, nearestHorizontalCorrection)
                        && platformBounds.max.y > highestPlatformTop))
                {
                    candidate = collider;
                    targetBoundsCenter = resolvedTarget;
                    nearestHorizontalCorrection = horizontalCorrection;
                    highestPlatformTop = platformBounds.max.y;
                }
            }

            if (candidate == null)
            {
                return false;
            }

            int blockingHitCount = Physics2D.OverlapCapsule(
                targetBoundsCenter,
                new Vector2(playerBounds.size.x, playerBounds.size.y),
                _capsule.direction,
                0f,
                GroundFilter,
                _overlapHits);
            if (blockingHitCount >= _overlapHits.Length)
            {
                return false;
            }

            Transform playerTransform = Handle.transform;
            for (int i = 0; i < blockingHitCount; i++)
            {
                Collider2D hit = _overlapHits[i];
                if (hit != null
                    && hit.transform != candidate.transform
                    && hit.transform != playerTransform
                    && !hit.transform.IsChildOf(playerTransform))
                {
                    return false;
                }
            }

            Vector2 currentBoundsCenter = _capsule.bounds.center;
            _body.position += targetBoundsCenter - currentBoundsCenter;
            contacts.Grounded = true;
            contacts.CeilingHit = false;
            contacts.TouchingWallLeft = false;
            contacts.TouchingWallRight = false;
            return true;
        }
    }
}

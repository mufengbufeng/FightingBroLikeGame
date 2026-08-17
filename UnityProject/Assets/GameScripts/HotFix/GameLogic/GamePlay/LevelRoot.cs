using UnityEngine;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 关卡根组件，暴露出生点、玩法相机和玩家。
    /// </summary>
    public sealed class LevelRoot : MonoBehaviour
    {
        [SerializeField] private Transform _playerSpawn;
        [SerializeField] private Camera _gamePlayCamera;
        [SerializeField] private PlayerController _player;

        /// <summary>
        /// 玩家出生点。
        /// </summary>
        public Transform PlayerSpawn => _playerSpawn;

        /// <summary>
        /// 关卡正交相机。
        /// </summary>
        public Camera GamePlayCamera => _gamePlayCamera;

        /// <summary>
        /// 关卡内玩家。
        /// </summary>
        public PlayerController Player => _player;

        /// <summary>
        /// 绑定工厂生成的关卡引用。
        /// </summary>
        public void Bind(Transform playerSpawn, Camera gamePlayCamera, PlayerController player)
        {
            _playerSpawn = playerSpawn;
            _gamePlayCamera = gamePlayCamera;
            _player = player;
        }
    }

}

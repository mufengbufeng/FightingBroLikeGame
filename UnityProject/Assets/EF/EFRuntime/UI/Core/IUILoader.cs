using Cysharp.Threading.Tasks;
using UnityEngine;

namespace EF.UI.WFramework {

	/// <summary>
	/// <see cref="UIManager">UI管理器</see>的<see cref="UIManager.Init(IUILoader, IUILoadingOverlay, bool)">初始化方法</see>中需要的<b>加载器</b>对象应实现的接口。
	/// </summary>
	public interface IUILoader {

		/// <summary>
		/// 根据界面的id获取打开界面时所需要的参数。
		/// </summary>
		/// <param name="id">通过<see cref="UIManager">UI管理器</see>中以界面字符串id为参数的开启方法传入的界面id。</param>
		/// <returns>开界面时所需要的参数</returns>
		ParametersForUI GetParameterForUI(string id);

		/// <summary>
		/// 加载UI界面<see cref="GameObject"/>实例。
		/// </summary>
		/// <param name="path">UI界面资源的加载路径</param>
		/// <returns>加载的结果</returns>
		UniTask<GameObject> LoadUIObject(string path);

		/// <summary>
		/// 卸载UI界面<see cref="GameObject"/>实例。
		/// </summary>
		/// <param name="go">需要被卸载的<see cref="GameObject"/>实例</param>
		void UnloadUIObject(GameObject go);

	}

}

using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// <see cref="UIContentBind"/>��Ҫ����Դ��������
/// </summary>
namespace EF.UI.WFramework
{

public interface IUIContentBindLoader {

	/// <summary>
	/// ������Դ�ļ���·������<see cref="Sprite"/>����
	/// </summary>
	/// <param name="path"><see cref="Sprite"/>��Դ�ļ���·��</param>
	/// <returns>�첽���ؽ��</returns>
	UniTask<Sprite> LoadSprite(string path);

	/// <summary>
	/// ����<see cref="UnityEngine.U2D.SpriteAtlas"/>��Դ�ļ���·����<see cref="Sprite"/>���Ƽ���<see cref="Sprite"/>����
	/// </summary>
	/// <param name="atlasPath"><see cref="UnityEngine.U2D.SpriteAtlas"/>��Դ�ļ���·��</param>
	/// <param name="spriteName">��Ҫ���ص�<see cref="Sprite"/>����</param>
	/// <returns>�첽���ؽ��</returns>
	UniTask<Sprite> LoadSprite(string atlasPath, string spriteName);

	/// <summary>
	/// ж��<see cref="Sprite"/>����
	/// </summary>
	/// <param name="sprite">��Ҫ��ж�ص�<see cref="Sprite"/>����</param>
	void UnloadSprite(Sprite sprite);

	/// <summary>
	/// ������Դ�ļ���·������<see cref="Texture"/>����
	/// </summary>
	/// <param name="path"><see cref="Texture"/>��Դ�ļ���·��</param>
	/// <returns>�첽���ؽ��</returns>
	UniTask<Texture> LoadTexture(string path);

	/// <summary>
	/// ж��<see cref="Texture"/>����
	/// </summary>
	/// <param name="texture">��Ҫ��ж�ص�ж��<see cref="Texture"/>����</param>
	void UnloadTexture(Texture texture);

	/// <summary>
	/// ������Դ�ļ���·������<see cref="GameObject"/>����
	/// </summary>
	/// <param name="path"><see cref="GameObject"/>��Դ�ļ���·��</param>
	/// <returns>�첽���ؽ��</returns>
	UniTask<GameObject> LoadGameObject(string path);

	/// <summary>
	/// ж��<see cref="GameObject"/>����
	/// </summary>
	/// <param name="gameObject">��Ҫ��ж�ص�ж��<see cref="GameObject"/>����</param>
	void UnloadGameObject(GameObject gameObject);

}
}

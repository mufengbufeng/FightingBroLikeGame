using UnityEditor;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	[CustomPropertyDrawer(typeof(AnimationClipAlias.CrossFadeData))]
	public class AnimationClipCrossFadeDrawer : AnimCrossFadeDrawer {

		protected override void OnItemCreated(SerializedProperty property) {
			string path = property.propertyPath;
			SerializedProperty p = property.serializedObject.FindProperty(path.Substring(0, path.LastIndexOf('.')) + ".m_PlaybackSpeed");
			if (p != null) { p.floatValue = 1f; }
		}

		protected override string GetAnimName(SerializedProperty property) {
			string path = property.propertyPath;
			SerializedProperty p = property.serializedObject.FindProperty(path.Substring(0, path.LastIndexOf('.')) + ".m_ClipName");
			if (p == null) { return null; }
			return p.stringValue;
		}

		protected override float GetClipLength(GameObject go, string animName) {
			Animation animation = go.GetComponent<Animation>();
			if (animation == null) { return -1f; }
			AnimationState state = animation[animName];
			return state == null ? -1f : state.length;
		}

	}

}

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	[CustomPropertyDrawer(typeof(AnimatorStateAlias.CrossFadeData))]
	public class AnimatorStateCrossFadeDrawer : AnimCrossFadeDrawer {

		protected override void OnItemCreated(SerializedProperty property) {
			string path = property.propertyPath;
			SerializedProperty p = property.serializedObject.FindProperty(path.Substring(0, path.LastIndexOf('.')) + ".m_PlaybackSpeed");
			if (p != null) { p.floatValue = 1f; }
		}

		protected override string GetAnimName(SerializedProperty property) {
			string path = property.propertyPath;
			SerializedProperty p = property.serializedObject.FindProperty(path.Substring(0, path.LastIndexOf('.')) + ".m_StateName");
			if (p == null) { return null; }
			return p.stringValue;
		}

		protected override float GetClipLength(GameObject go, string animName) {
			Animator animator = go.GetComponent<Animator>();
			if (animator == null) { return -1f; }
			AnimatorController ctrl = animator.runtimeAnimatorController as AnimatorController;
			if (ctrl == null) { return -1f; }
			foreach (AnimatorControllerLayer layer in ctrl.layers) {
				foreach (ChildAnimatorState state in layer.stateMachine.states) {
					if (!(state.state.motion is AnimationClip clip) || clip == null) { continue; }
					if (state.state.name != animName) { continue; }
					return clip.length;
				}
			}
			return -1f;
		}

	}

}

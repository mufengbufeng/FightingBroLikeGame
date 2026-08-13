using UnityEditor;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	public abstract class AnimCrossFadeDrawer : PropertyDrawer {

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			const int lines = 3;
			return EditorGUIUtility.singleLineHeight * (lines + 1) + EditorGUIUtility.standardVerticalSpacing * lines;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			if (!s_inited) {
				s_inited = true;
				s_label_mode = new GUIContent("Mode Time");
				s_label_time_normalized = new GUIContent("Time Normalized");
				s_label_time_in_seconds = new GUIContent("Time in Seconds");
			}
			Rect rect = position;
			rect.height = EditorGUIUtility.singleLineHeight;
			EditorGUI.PrefixLabel(rect, label);
			rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
			string animName = GetAnimName(property);
			if (mPrevName != animName) {
				mPrevName = animName;
				mAnimLength = -1f;
				if (!string.IsNullOrEmpty(mPrevName)) {
					Component comp = property.serializedObject.targetObject as Component;
					if (comp != null) {
						mAnimLength = GetClipLength(comp.gameObject, mPrevName);
					}
				}
			}
			SerializedProperty pInited = property.FindPropertyRelative("m_Inited");
			if (!pInited.boolValue) {
				pInited.boolValue = true;
				OnItemCreated(property);
			}
			SerializedProperty pLength = property.FindPropertyRelative("m_Length");
			SerializedProperty pCrossFadeModeTime = property.FindPropertyRelative("m_CrossFadeModeTime");
			SerializedProperty pCrossFadeNormalized = property.FindPropertyRelative("m_CrossFadeNormalized");
			SerializedProperty pCrossFadeInSeconds = property.FindPropertyRelative("m_CrossFadeInSeconds");
			if (pLength.floatValue != mAnimLength) {
				pLength.floatValue = mAnimLength;
				if (mAnimLength > 0f) {
					if (pCrossFadeModeTime.boolValue) {
						if (pCrossFadeInSeconds.floatValue > mAnimLength) {
							pCrossFadeInSeconds.floatValue = mAnimLength;
						}
						pCrossFadeNormalized.floatValue = pCrossFadeInSeconds.floatValue / mAnimLength;
					} else {
						pCrossFadeInSeconds.floatValue = pCrossFadeNormalized.floatValue * mAnimLength;
					}
				}
			}
			EditorGUI.indentLevel++;
			EditorGUI.PropertyField(rect, pCrossFadeModeTime, s_label_mode);
			rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
			EditorGUI.BeginDisabledGroup(pCrossFadeModeTime.boolValue || mAnimLength < 0f);
			EditorGUI.BeginChangeCheck();
			EditorGUI.Slider(rect, pCrossFadeNormalized, 0f, 1f, s_label_time_normalized);
			if (EditorGUI.EndChangeCheck()) {
				pCrossFadeInSeconds.floatValue = pCrossFadeNormalized.floatValue * mAnimLength;
			}
			EditorGUI.EndDisabledGroup();
			rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
			EditorGUI.BeginDisabledGroup(!pCrossFadeModeTime.boolValue || mAnimLength < 0f);
			EditorGUI.BeginChangeCheck();
			EditorGUI.Slider(rect, pCrossFadeInSeconds, 0f, Mathf.Max(0f, mAnimLength), s_label_time_in_seconds);
			if (EditorGUI.EndChangeCheck()) {
				pCrossFadeNormalized.floatValue = pCrossFadeInSeconds.floatValue / mAnimLength;
			}
			EditorGUI.EndDisabledGroup();
			EditorGUI.indentLevel--;
		}

		protected virtual void OnItemCreated(SerializedProperty property) { }
		protected abstract string GetAnimName(SerializedProperty property);
		protected abstract float GetClipLength(GameObject go, string animName);

		private string mPrevName;
		private float mAnimLength;

		private static bool s_inited = false;
		private static GUIContent s_label_mode;
		private static GUIContent s_label_time_normalized;
		private static GUIContent s_label_time_in_seconds;

	}

}

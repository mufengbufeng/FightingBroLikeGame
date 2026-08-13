using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	public abstract class StringSelectorPropertyDrawer : PropertyDrawer {

		private Object mPrevTarget;
		private GUIContent[] mValueList;

		protected abstract IEnumerable<string> GetValueList(Object target);

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			return EditorGUIUtility.singleLineHeight;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			Object target = property.serializedObject.targetObject;
			if (mPrevTarget != target) {
				mPrevTarget = target;
				mValueList = GetValueList(target).Select(x => new GUIContent(x)).ToArray();
			}
			int index = -1;
			string val = property.stringValue;
			for (int i = mValueList.Length - 1; i >= 0; i--) {
				if (mValueList[i].text == val) {
					index = i;
					break;
				}
			}
			EditorGUI.BeginChangeCheck();
			Color cachedColor = GUI.color;
			if (index < 0) { GUI.color = Color.red; }
			index = EditorGUI.Popup(position, label, index, mValueList);
			GUI.color = cachedColor;
			if (EditorGUI.EndChangeCheck()) {
				property.stringValue = mValueList[index].text;
			}
		}

	}

}

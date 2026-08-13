using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	[CustomPropertyDrawer(typeof(AnimationClipNameAttribute))]
	public class AnimationClipNamePropertyDrawer : StringSelectorPropertyDrawer {

		protected override IEnumerable<string> GetValueList(Object target) {
			Component comp = target as Component;
			Animation animation = comp.GetComponent<Animation>();
			return animation.Cast<AnimationState>().Select(x => x.clip.name).Distinct();
		}

	}

}

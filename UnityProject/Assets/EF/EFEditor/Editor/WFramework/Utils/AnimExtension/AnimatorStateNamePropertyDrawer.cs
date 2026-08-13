using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	[CustomPropertyDrawer(typeof(AnimatorStateNameAttribute))]
	public class AnimatorStateNamePropertyDrawer : StringSelectorPropertyDrawer {

		protected override IEnumerable<string> GetValueList(UnityEngine.Object target) {
			Component comp = target as Component;
			Animator animator = comp.GetComponent<Animator>();
			AnimatorController ctrl = animator.runtimeAnimatorController as AnimatorController;
			if (ctrl == null) { return Array.Empty<string>(); }
			return ctrl.layers.SelectMany(x => x.stateMachine.states)
				.Select(x => x.state.name)
				.Where(x => !string.IsNullOrEmpty(x))
				.Distinct();
		}

	}

}

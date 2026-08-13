using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	public static partial class AnimExtension {

		public static bool PlayAnim(this Animator anim, string clip) {
			return PlayAnimator(anim, clip, AnimParam.Default, null);
		}
		public static bool PlayAnim(this Animator anim, string clip, AnimParam param) {
			return PlayAnimator(anim, clip, param, null);
		}
		public static bool PlayAnim(this Animator anim, string clip, IEnumerable<AnimEvent> events) {
			return PlayAnimator(anim, clip, AnimParam.Default, events);
		}
		public static bool PlayAnim(this Animator anim, string clip, AnimParam param, IEnumerable<AnimEvent> events) {
			return PlayAnimator(anim, clip, param, events);
		}
		public static bool PlayAnim(this Animator anim, string clip, AnimEvent evt) {
			s_temp_events[0] = evt;
			bool ret = PlayAnimator(anim, clip, AnimParam.Default, s_temp_events);
			s_temp_events[0] = default(AnimEvent);
			return ret;
		}
		public static bool PlayAnim(this Animator anim, string clip, AnimParam param, AnimEvent evt) {
			s_temp_events[0] = evt;
			bool ret = PlayAnimator(anim, clip, param, s_temp_events);
			s_temp_events[0] = default(AnimEvent);
			return ret;
		}
		public static bool PlayAnim(this Animator anim, string clip, Action onfinish) {
			if (onfinish == null) {
				return PlayAnimator(anim, clip, AnimParam.Default, null);
			}
			s_temp_events[0] = AnimEvent.Progress(1f, 1u, (t, l) => { onfinish.Invoke(); });
			bool ret = PlayAnimator(anim, clip, AnimParam.Default, s_temp_events);
			s_temp_events[0] = default(AnimEvent);
			return ret;
		}
		public static bool PlayAnim(this Animator anim, string clip, AnimParam param, Action onfinish) {
			if (onfinish == null) {
				return PlayAnimator(anim, clip, param, null);
			}
			s_temp_events[0] = AnimEvent.Progress(1f, 1u, (t, l) => { onfinish.Invoke(); });
			bool ret = PlayAnimator(anim, clip, param, s_temp_events);
			s_temp_events[0] = default(AnimEvent);
			return ret;
		}

		private static bool PlayAnimator(Animator anim, string clip, AnimParam param, IEnumerable<AnimEvent> events) {
			if (anim == null || anim.Equals(null)) { return false; }
			int layer = param._layer;
			s_playing.Remove(anim, layer);
			float speed = 1f;
			float crossfade = 0f;
			AnimatorStateAlias alias = anim.GetComponent<AnimatorStateAlias>();
			if (alias != null) {
				string cn = alias.GetStateName(clip, true, out crossfade, out speed, out _);
				if (string.IsNullOrEmpty(cn)) {
					speed = 1f;
					crossfade = 0f;
				} else {
					clip = cn;
				}
			}
			AnimatorItem item = AnimatorItem.Get(anim, clip, layer, events);
			s_playing.AddPlaying(item);
			if (param._speed > 0f) { speed = param._speed; }
			if (param._crossfade >= 0f) { crossfade = param._crossfade; }
			return item.Play(speed, crossfade);
		}

		private sealed class AnimatorItem : AnimItem<Animator> {

			private int mNameHash;

			private AnimatorItem() { }

			public override bool Play(float speed, float crossfade) {
				Component.speed = speed;
				if (crossfade > 0f) {
					Component.CrossFade(mNameHash, crossfade, Layer);
				} else {
					Component.Play(mNameHash, Layer);
				}
				return true;
			}

			public override void Recycle() {
				ClearEvents(this);
				ClearComponent(this);
				s_cache.Enqueue(this);
			}

			protected override bool CheckValid() {
				return Component != null && !Component.Equals(null);
			}

			protected override void TryGetClipLength(out float length, out bool unscaledTime) {
				unscaledTime = Component.updateMode == AnimatorUpdateMode.UnscaledTime;
				if (GetStateInfo(out AnimatorStateInfo info)) {
					length = info.length;
				} else {
					length = 0f;
				}
			}

			protected override float GetNormalizedTime(out float speed) {
				if (GetStateInfo(out AnimatorStateInfo info)) {
					speed = info.speed * Component.speed;
					return info.normalizedTime;
				}
				speed = 0f;
				return -1f;
			}

			private bool GetStateInfo(out AnimatorStateInfo info) {
				AnimatorStateInfo ci = Component.GetCurrentAnimatorStateInfo(Layer);
				if (ci.shortNameHash == mNameHash) {
					info = ci;
					return true;
				}
				AnimatorStateInfo ni = Component.GetCurrentAnimatorStateInfo(Layer);
				if (ni.shortNameHash == mNameHash) {
					info = ni;
					return true;
				}
				info = default(AnimatorStateInfo);
				return false;
			}

			private static Queue<AnimatorItem> s_cache = new Queue<AnimatorItem>();

			public static AnimatorItem Get(Animator anim, string clip, int layer, IEnumerable<AnimEvent> events) {
				AnimatorItem item = null;
				if (s_cache.Count > 0) { item = s_cache.Dequeue(); }
				if (item == null) { item = new AnimatorItem(); }
				InitAnimNameAndEvents(item, clip, events);
				InitComponentAndLayer(item, anim, layer);
				item.mNameHash = Animator.StringToHash(clip);
				return item;
			}

		}

	}

}
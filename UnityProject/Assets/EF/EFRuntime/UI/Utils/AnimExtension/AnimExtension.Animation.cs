using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	public static partial class AnimExtension {

		public static bool PlayAnim(this Animation anim, string clip) {
			return PlayAnimation(anim, clip, AnimParam.Default, null);
		}
		public static bool PlayAnim(this Animation anim, AnimParam param, string clip) {
			return PlayAnimation(anim, clip, param, null);
		}
		public static bool PlayAnim(this Animation anim, string clip, IEnumerable<AnimEvent> events) {
			return PlayAnimation(anim, clip, AnimParam.Default, events);
		}
		public static bool PlayAnim(this Animation anim, string clip, AnimParam param, IEnumerable<AnimEvent> events) {
			return PlayAnimation(anim, clip, param, events);
		}
		public static bool PlayAnim(this Animation anim, string clip, AnimEvent evt) {
			s_temp_events[0] = evt;
			bool ret = PlayAnimation(anim, clip, AnimParam.Default, s_temp_events);
			s_temp_events[0] = default(AnimEvent);
			return ret;
		}
		public static bool PlayAnim(this Animation anim, string clip, AnimParam param, AnimEvent evt) {
			s_temp_events[0] = evt;
			bool ret = PlayAnimation(anim, clip, param, s_temp_events);
			s_temp_events[0] = default(AnimEvent);
			return ret;
		}
		public static bool PlayAnim(this Animation anim, string clip, Action onfinish) {
			if (onfinish == null) {
				return PlayAnimation(anim, clip, AnimParam.Default, null);
			}
			s_temp_events[0] = AnimEvent.Progress(1f, 1u, (t, l) => { onfinish.Invoke(); });
			bool ret = PlayAnimation(anim, clip, AnimParam.Default, s_temp_events);
			s_temp_events[0] = default(AnimEvent);
			return ret;
		}
		public static bool PlayAnim(this Animation anim, string clip, AnimParam param, Action onfinish) {
			if (onfinish == null) {
				return PlayAnimation(anim, clip, param, null);
			}
			s_temp_events[0] = AnimEvent.Progress(1f, 1u, (t, l) => { onfinish.Invoke(); });
			bool ret = PlayAnimation(anim, clip, param, s_temp_events);
			s_temp_events[0] = default(AnimEvent);
			return ret;
		}

		private static bool PlayAnimation(Animation anim, string clip, AnimParam param, IEnumerable<AnimEvent> events) {
			if (anim == null || anim.Equals(null)) { return false; }
			int layer = param._layer;
			s_playing.Remove(anim, layer);
			float crossfade = 1f;
			float speed = 0f;
			AnimationClipAlias alias = anim.GetComponent<AnimationClipAlias>();
			if (alias != null) {
				string cn = alias.GetClipName(clip, true, out crossfade, out speed, out _);
				if (string.IsNullOrEmpty(cn)) {
					speed = 1f;
					crossfade = 0f;
				} else {
					clip = cn;
				}
			}
			AnimationItem item = AnimationItem.Get(anim, clip, layer, events);
			if (param._speed > 0f) { speed = param._speed; }
			if (param._crossfade >= 0f) { crossfade = param._crossfade; }
			if (!item.Play(speed, crossfade)) { return false; }
			s_playing.AddPlaying(item);
			return true;
		}

		private sealed class AnimationItem : AnimItem<Animation> {

			private AnimationState mState;

			private AnimationItem() { }

			public override bool Play(float speed, float crossfade) {
				mState = Component[AnimName];
				if (mState == null) { return false; }
				mState.layer = Layer;
				mState.speed = speed;
				if (crossfade <= 0f) {
					return Component.Play(AnimName, PlayMode.StopSameLayer);
				}
				Component.CrossFade(AnimName, crossfade, PlayMode.StopSameLayer);
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
				unscaledTime = false;
				if (!Component.IsPlaying(AnimName)) {
					length = 0f;
					return;
				}
				length = mState.length;
			}

			protected override float GetNormalizedTime(out float speed) {
				speed = mState.speed;
				return mState.normalizedTime;
			}

			private static Queue<AnimationItem> s_cache = new Queue<AnimationItem>();

			public static AnimationItem Get(Animation anim, string clip, int layer, IEnumerable<AnimEvent> events) {
				AnimationItem item = null;
				if (s_cache.Count > 0) { item = s_cache.Dequeue(); }
				if (item == null) { item = new AnimationItem(); }
				InitAnimNameAndEvents(item, clip, events);
				InitComponentAndLayer(item, anim, layer);
				item.mState = null;
				return item;
			}

		}

	}

}
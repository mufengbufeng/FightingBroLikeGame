using System;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	[RequireComponent(typeof(Animation))]
	public class AnimationClipAlias : MonoBehaviour {

		[SerializeField]
		private AliasItem[] m_Clips;

		public string GetClipName(string alias) {
			AliasItem item = GetAliasItem(alias);
			return item == null ? null : item.ClipName;
		}

		public string GetClipName(string alias, out float speed, out float length) {
			AliasItem item = GetAliasItem(alias);
			if (item == null) { speed = 1f; length = -1f; return null; }
			speed = item.PlaybackSpeed;
			length = item.Length;
			return item.ClipName;
		}

		public string GetClipName(string alias, bool crossfadeModeTime, out float crossfade) {
			AliasItem item = GetAliasItem(alias);
			if (item == null) { crossfade = 0f; return null; }
			crossfade = item.GetCrossFade(crossfadeModeTime);
			return item.ClipName;
		}

		public string GetClipName(string alias, bool crossfadeModeTime, out float crossfade, out float speed, out float length) {
			AliasItem item = GetAliasItem(alias);
			if (item == null) { crossfade = 0f; speed = 1f; length = -1f; return null; }
			crossfade = item.GetCrossFade(crossfadeModeTime);
			speed = item.PlaybackSpeed;
			length = item.Length;
			return item.ClipName;
		}

		private AliasItem GetAliasItem(string alias) {
			if (string.IsNullOrEmpty(alias)) { return null; }
			for (int i = m_Clips.Length - 1; i >= 0; i--) {
				AliasItem item = m_Clips[i];
				if (item.Alias == alias) { return item; }
			}
			return null;
		}

		[Serializable]
		public class AliasItem {

			[SerializeField, AnimationClipName]
			private string m_ClipName;

			[SerializeField]
			private string m_Alias;

			[SerializeField]
			private float m_PlaybackSpeed = 1f;

			[SerializeField]
			private CrossFadeData m_CrossFade;

			public string ClipName { get { return m_ClipName; } }

			public string Alias { get { return m_Alias; } }

			public float PlaybackSpeed { get { return m_PlaybackSpeed; } }

			public float Length { get { return m_CrossFade.Length; } }

			public float GetCrossFade(bool modeTime) {
				return m_CrossFade.GetCrossFade(modeTime);
			}

		}

		[Serializable]
		public class CrossFadeData {

			[SerializeField]
			private bool m_Inited;

			[SerializeField]
			private bool m_CrossFadeModeTime;

			[SerializeField]
			private float m_CrossFadeNormalized;

			[SerializeField]
			private float m_CrossFadeInSeconds;

			[SerializeField]
			private float m_Length;

			public float Length { get { return m_Length; } }

			public float GetCrossFade(bool modeTime) {
				return modeTime ? m_CrossFadeInSeconds : m_CrossFadeNormalized;
			}

		}

	}

}

using System;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	[RequireComponent(typeof(Animator))]
	public class AnimatorStateAlias : MonoBehaviour {

		[SerializeField]
		private AliasItem[] m_States;

		public string GetStateName(string alias) {
			AliasItem item = GetAliasItem(alias);
			return item == null ? null : item.StateName;
		}

		public string GetStateName(string alias, out float speed, out float length) {
			AliasItem item = GetAliasItem(alias);
			if (item == null) { speed = 1f; length = -1f; return null; }
			speed = item.PlaybackSpeed;
			length = item.Length;
			return item.StateName;
		}

		public string GetStateName(string alias, bool crossfadeModeTime, out float crossfade) {
			AliasItem item = GetAliasItem(alias);
			if (item == null) { crossfade = 0f; return null; }
			crossfade = item.GetCrossFade(crossfadeModeTime);
			return item.StateName;
		}

		public string GetStateName(string alias, bool crossfadeModeTime, out float crossfade, out float speed, out float length) {
			AliasItem item = GetAliasItem(alias);
			if (item == null) { crossfade = 0f; speed = 1f; length = -1f; return null; }
			crossfade = item.GetCrossFade(crossfadeModeTime);
			speed = item.PlaybackSpeed;
			length = item.Length;
			return item.StateName;
		}

		private AliasItem GetAliasItem(string alias) {
			if (string.IsNullOrEmpty(alias)) { return null; }
			for (int i = m_States.Length - 1; i >= 0; i--) {
				AliasItem item = m_States[i];
				if (item.Alias == alias) { return item; }
			}
			return null;
		}

		[Serializable]
		public class AliasItem {

			[SerializeField, AnimatorStateName]
			private string m_StateName;

			[SerializeField]
			private string m_Alias;

			[SerializeField]
			private float m_PlaybackSpeed = 1f;

			[SerializeField]
			private CrossFadeData m_CrossFade;

			public string StateName { get { return m_StateName; } }

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

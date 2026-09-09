using System;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Shared
{
	// Plays AudioClips using Unity's internal editor preview so only one clip plays at a time.
	public static class AudioClipUtility
	{
		private static readonly Type s_AudioUtilType = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
		private static readonly MethodInfo s_PlayPreviewClip = GetMethod(new[] { "PlayPreviewClip", "PlayClip" }, new[] { typeof(AudioClip), typeof(int), typeof(bool) });
		private static readonly MethodInfo s_StopAllPreviewClips = GetMethod(new[] { "StopAllPreviewClips", "StopAllClips" }, Type.EmptyTypes);
		private static readonly MethodInfo s_IsPreviewClipPlaying = GetMethod(new[] { "IsPreviewClipPlaying" }, Type.EmptyTypes);

		private static AudioClip s_CurrentClip;

		private static MethodInfo GetMethod(string[] names, Type[] parameterTypes)
		{
			if (s_AudioUtilType == null)
			{
				return null;
			}
			foreach (var name in names)
			{
				var method = s_AudioUtilType.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
				if (method != null)
				{
					return method;
				}
			}
			return null;
		}

		// Returns true while the given AudioClip is the active preview.
		public static bool IsPlaying(AudioClip audioClip)
		{
			return audioClip != null && audioClip == s_CurrentClip;
		}

		public static void Stop()
		{
			s_StopAllPreviewClips?.Invoke(null, null);
			SetCurrentClip(null);
		}

		public static void Play(AudioClip audioClip)
		{
			if (audioClip == null)
			{
				return;
			}
			if (s_PlayPreviewClip == null)
			{
				Debug.LogWarning("Unable to preview the AudioClip. The internal UnityEditor.AudioUtil play method was not found.");
				return;
			}
			// Stop any active preview so only one AudioClip plays at a time.
			s_StopAllPreviewClips?.Invoke(null, null);
			s_PlayPreviewClip.Invoke(null, new object[] { audioClip, 0, false });
			SetCurrentClip(audioClip);
		}

		// Stops the clip if it is already playing, otherwise plays it.
		public static void Toggle(AudioClip audioClip)
		{
			if (IsPlaying(audioClip))
			{
				Stop();
			}
			else
			{
				Play(audioClip);
			}
		}

		private static void SetCurrentClip(AudioClip audioClip)
		{
			s_CurrentClip = audioClip;
			EditorApplication.update -= MonitorPlayback;
			if (audioClip != null)
			{
				EditorApplication.update += MonitorPlayback;
			}
			// Repaint so play buttons reflect the new state, including any previously playing clip.
			InternalEditorUtility.RepaintAllViews();
		}

		// Clears the playing state once the preview finishes so play buttons unhighlight on their own.
		private static void MonitorPlayback()
		{
			if (s_CurrentClip == null || (s_IsPreviewClipPlaying != null && !(bool) s_IsPreviewClipPlaying.Invoke(null, null)))
			{
				SetCurrentClip(null);
			}
		}
	}
}

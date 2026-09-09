using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunaWolfStudiosEditor.ScriptableSheets.Scanning
{
	[System.Flags]
	public enum SheetAsset
	{
		Default = 0,
		ScriptableObject = 1 << 0,
		AnimationClip = 1 << 1,
		AnimatorController = 1 << 2,
		AudioClip = 1 << 3,
		AudioMixer = 1 << 4,
		AvatarMask = 1 << 5,
		Flare = 1 << 6,
		Font = 1 << 7,
		Material = 1 << 8,
		Mesh = 1 << 9,
#if UNITY_6000_0_OR_NEWER
		PhysicsMaterial = 1 << 10,
#else
		PhysicMaterial = 1 << 10,
#endif
		PhysicsMaterial2D = 1 << 11,
		Prefab = 1 << 12,
		Scene = 1 << 13,
		Shader = 1 << 14,
		Sprite = 1 << 15,
		TerrainData = 1 << 16,
		TextAsset = 1 << 17,
		Texture = 1 << 18,
		VideoClip = 1 << 19,
	}

	public static class SheetAssetExtensions
	{
		private static readonly Dictionary<SheetAsset, string> s_SheetAssetToType = new Dictionary<SheetAsset, string>()
		{
			{ SheetAsset.AnimationClip, "UnityEngine.AnimationClip" },
			{ SheetAsset.AnimatorController, "UnityEditor.Animations.AnimatorController" },
			{ SheetAsset.AudioClip, "UnityEngine.AudioClip" },
			{ SheetAsset.AudioMixer, "UnityEditor.Audio.AudioMixerController" },
			{ SheetAsset.AvatarMask, "UnityEngine.AvatarMask" },
			{ SheetAsset.Flare, "UnityEngine.Flare" },
			{ SheetAsset.Font, "UnityEngine.Font" },
			{ SheetAsset.Material, "UnityEngine.Material" },
			{ SheetAsset.Mesh, "UnityEngine.Mesh" },
#if UNITY_6000_0_OR_NEWER
			{ SheetAsset.PhysicsMaterial, "UnityEngine.PhysicsMaterial" },
#else
			{ SheetAsset.PhysicMaterial, "UnityEngine.PhysicMaterial" },
#endif
			{ SheetAsset.PhysicsMaterial2D, "UnityEngine.PhysicsMaterial2D" },
			{ SheetAsset.Prefab, "UnityEngine.GameObject" },
			{ SheetAsset.Scene, "UnityEditor.SceneAsset" },
			{ SheetAsset.ScriptableObject, "UnityEngine.ScriptableObject" },
			{ SheetAsset.Shader, "UnityEngine.Shader" },
			{ SheetAsset.Sprite, "UnityEngine.Sprite" },
			{ SheetAsset.TerrainData, "UnityEngine.TerrainData" },
			{ SheetAsset.TextAsset, "UnityEngine.TextAsset" },
			{ SheetAsset.Texture, "UnityEngine.Texture2D" },
			{ SheetAsset.VideoClip, "UnityEngine.Video.VideoClip" }
		};

		public static string GetDefaultType(this SheetAsset sheetAsset)
		{
			if (s_SheetAssetToType.TryGetValue(sheetAsset, out string type))
			{
				return type;
			}
			else
			{
				Debug.LogWarning($"{nameof(SheetAsset)} '{sheetAsset}' not found in dictionary {nameof(s_SheetAssetToType)}.");
				return string.Empty;
			}
		}

		private static Dictionary<SheetAsset, Type> s_ResolvedTypes;

		private static Dictionary<SheetAsset, Type> GetResolvedTypes()
		{
			if (s_ResolvedTypes == null)
			{
				s_ResolvedTypes = new Dictionary<SheetAsset, Type>();
				foreach (var pair in s_SheetAssetToType)
				{
					var type = FindType(pair.Value);
					if (type != null)
					{
						s_ResolvedTypes[pair.Key] = type;
					}
				}
			}
			return s_ResolvedTypes;
		}

		private static Type FindType(string fullName)
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var type = assembly.GetType(fullName);
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}

		public static bool TryGetSheetAsset(Object obj, out SheetAsset sheetAsset)
		{
			sheetAsset = SheetAsset.Default;
			if (obj == null)
			{
				return false;
			}
			if (obj is ScriptableObject)
			{
				sheetAsset = SheetAsset.ScriptableObject;
				return true;
			}
			// The Texture entry maps to Texture2D for a sensible default type, so route Cubemaps and other variants here too.
			if (obj is Texture)
			{
				sheetAsset = SheetAsset.Texture;
				return true;
			}
			Type bestType = null;
			foreach (var pair in GetResolvedTypes())
			{
				// ScriptableObject and Texture are already handled above, so skip their entries.
				if (pair.Key == SheetAsset.ScriptableObject || pair.Key == SheetAsset.Texture)
				{
					continue;
				}
				var candidateType = pair.Value;
				if (candidateType.IsInstanceOfType(obj) && (bestType == null || candidateType.IsSubclassOf(bestType)))
				{
					bestType = candidateType;
					sheetAsset = pair.Key;
				}
			}
			return bestType != null;
		}
	}
}

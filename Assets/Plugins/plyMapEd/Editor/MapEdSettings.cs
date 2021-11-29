using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PLY.MapEd
{
	public static class MapEdSettings
	{
		public static bool KeepPrefabLink
		{
			set { keepPrefabLink = value; EditorPrefs.SetBool(prefs_KeepPrefabLink, value); }
			get => keepPrefabLink;
		}

		public static float MoveSnap
		{
			set { moveSnap = value; EditorPrefs.SetFloat(prefs_MoveSnap, value); }
			get => moveSnap;
		}

		public static float RotateSnap
		{
			set { rotateSnap = value; EditorPrefs.SetFloat(prefs_RotateSnap, value); }
			get => rotateSnap;
		}

		public static string PrefabsRoot
		{
			set { prefabsRoot = value; EditorPrefs.SetString(prefs_PrefabsRoot, value); }
			get => prefabsRoot;
		}

		public static int ThumbsSize
		{
			set { thumbsSize = value; EditorPrefs.SetInt(prefs_ThumbsSize, value); }
			get => thumbsSize;
		}

		public static int GridStyle
		{
			set { gridStyle = value; EditorPrefs.SetInt(prefs_GridStyle, value); GridStyleChanged?.Invoke(value); }
			get => gridStyle;
		}

		public static Color GridFrontColor
		{
			set { gridFrontColor = value; SetColor(prefs_GridFrontColor, value); GridFrontColorChanged?.Invoke(value); }
			get => gridFrontColor;
		}

		public static Color GridBackColor
		{
			set { gridBackColor = value; SetColor(prefs_GridBackColor, value); GridBackColorChanged?.Invoke(value); }
			get => gridBackColor;
		}

		public static Color GridFillColor
		{
			set { gridFillColor = value; SetColor(prefs_GridFillColor, value); GridFillColorChanged?.Invoke(value); }
			get => gridFillColor;
		}

		public static bool IcoGenUseLight
		{
			set { icoGenUseLight = value; EditorPrefs.SetBool(prefs_IcoGenUseLight, value); }
			get => icoGenUseLight;
		}

		public static float LightIntensity
		{
			set { lightIntensity = value; EditorPrefs.SetFloat(prefs_LightIntensity, value); }
			get => lightIntensity;
		}

		public static event System.Action<int> GridStyleChanged;
		public static event System.Action<Color> GridFrontColorChanged;
		public static event System.Action<Color> GridBackColorChanged;
		public static event System.Action<Color> GridFillColorChanged;

		private static bool inited;

		private static bool keepPrefabLink;
		private static float moveSnap;
		private static float rotateSnap;
		private static string prefabsRoot;
		private static int thumbsSize;
		private static int gridStyle;
		private static Color gridFrontColor;
		private static Color gridBackColor;
		private static Color gridFillColor;
		private static bool icoGenUseLight;
		private static float lightIntensity;

		private static string prefs_KeepPrefabLink;
		private static string prefs_PrefabsRoot;
		private static string prefs_ThumbsSize;
		private static string prefs_MoveSnap;
		private static string prefs_RotateSnap;
		private static string prefs_GridStyle;
		private static string prefs_GridFrontColor;
		private static string prefs_GridBackColor;
		private static string prefs_GridFillColor;
		private static string prefs_IcoGenUseLight;
		private static string prefs_LightIntensity;

		public static readonly Color DefaultGridFrontColor = new Color(0f, 0f, 0f, 1f);
		public static readonly Color DefaultGridBackColor = new Color(0f, 0f, 0f, 0.5f);
		public static readonly Color DefaultGridFillColor = new Color(0.5f, 0.65f, 0.8f, 0.2f);

		// ------------------------------------------------------------------------------------------------------------

		public static void Init()
		{
			if (inited) return;
			inited = true;

			var s = Application.dataPath.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
			var projectName = s[^2]; // s[s.Length - 2];

			prefs_KeepPrefabLink = $"plyMapEd.KeepPrefabLink.{projectName}";
			prefs_PrefabsRoot = $"plyMapEd.PrefabsRoot.{projectName}";
			prefs_ThumbsSize = $"plyMapEd.ThumbsSize.{projectName}";
			prefs_MoveSnap = $"plyMapEd.MoveSnap.{projectName}";
			prefs_RotateSnap = $"plyMapEd.RotateSnap.{projectName}";
			prefs_GridStyle = $"plyMapEd.GridStyle.{projectName}";
			prefs_GridFrontColor = $"plyMapEd.GridFrontColor.{projectName}";
			prefs_GridBackColor = $"plyMapEd.GridBackColor.{projectName}";
			prefs_GridFillColor = $"plyMapEd.GridFillColor.{projectName}";
			prefs_IcoGenUseLight = $"plyMapEd.IcoGenUseLight.{projectName}";
			prefs_LightIntensity = $"plyMapEd.LightIntensity.{projectName}";

			keepPrefabLink = EditorPrefs.GetBool(prefs_KeepPrefabLink, true);
			moveSnap = EditorPrefs.GetFloat(prefs_MoveSnap, 1f);
			RotateSnap = EditorPrefs.GetFloat(prefs_RotateSnap, 90f);
			prefabsRoot = EditorPrefs.GetString(prefs_PrefabsRoot, null);
			thumbsSize = EditorPrefs.GetInt(prefs_ThumbsSize, 120);
			gridStyle = EditorPrefs.GetInt(prefs_GridStyle, 0);
			gridFrontColor = GetColor(prefs_GridFrontColor, DefaultGridFrontColor);
			gridBackColor = GetColor(prefs_GridBackColor, DefaultGridBackColor);
			gridFillColor = GetColor(prefs_GridFillColor, DefaultGridFillColor);
			icoGenUseLight = EditorPrefs.GetBool(prefs_IcoGenUseLight, true);
			lightIntensity = EditorPrefs.GetFloat(prefs_LightIntensity, 1f);
		}

		public static void ResetGridColors()
		{
			GridFrontColor = DefaultGridFrontColor;
			GridBackColor = DefaultGridBackColor;
			GridFillColor = DefaultGridFillColor;
		}

		// ------------------------------------------------------------------------------------------------------------

		private static void SetColor(string key, Color value)
		{
			EditorPrefs.SetString(key, ColorToString(value));
		}

		private static Color GetColor(string key, Color defaultValue)
		{
			var s = EditorPrefs.GetString(key, null);
			return StringToColor(s, defaultValue);
		}

		private static string ColorToString(Color c)
		{
			return $"{c.r};{c.g};{c.b};{c.a}";
		}

		private static Color StringToColor(string s, Color defaultValue)
		{
			if (s != null)
			{
				var vals = s.Split(';');
				if (vals.Length == 4 &&
					float.TryParse(vals[0], out float r) &&
					float.TryParse(vals[1], out float g) &&
					float.TryParse(vals[2], out float b) &&
					float.TryParse(vals[3], out float a))
				{
					return new Color(r, g, b, a);
				}
			}
			return defaultValue;
		}

		// ============================================================================================================
	}
}
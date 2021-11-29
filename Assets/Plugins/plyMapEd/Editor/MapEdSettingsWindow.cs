using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace PLY.MapEd
{
	public class MapEdSettingsWindow : EditorWindow
	{
		private static readonly List<string> GridStyleChoises = new List<string> { "Dashed 1", "Dashed 2", "Solid 1", "Solid 2" };

		public static void Open()
		{
			GetWindow<MapEdSettingsWindow>(true, "MapEd Settings").ShowUtility();
		}

		protected void OnEnable()
		{
			var prefabLinkEle = new Toggle("Keep Prefab Link") { value = MapEdSettings.KeepPrefabLink };
			prefabLinkEle.RegisterValueChangedCallback(ev => MapEdSettings.KeepPrefabLink = ev.newValue);

			var moveSnapEle = new FloatField("Movement Snap") { value = MapEdSettings.MoveSnap };
			moveSnapEle.RegisterValueChangedCallback(ev => MapEdSettings.MoveSnap = ev.newValue);

			var rotateSnapEle = new FloatField("Rotation Snap") { value = MapEdSettings.RotateSnap };
			rotateSnapEle.RegisterValueChangedCallback(ev => MapEdSettings.RotateSnap = ev.newValue);

			var gridStyleEle = new DropdownField("Grid Style", GridStyleChoises, MapEdSettings.GridStyle);
			gridStyleEle.RegisterValueChangedCallback(_ => MapEdSettings.GridStyle = gridStyleEle.index);
			rootVisualElement.Add(gridStyleEle);

			var gridFrontColorEle = new ColorField("Grid Front Color") { value = MapEdSettings.GridFrontColor };
			gridFrontColorEle.RegisterValueChangedCallback(ev => MapEdSettings.GridFrontColor = ev.newValue);

			var gridBackColorEle = new ColorField("Grid Back Color") { value = MapEdSettings.GridBackColor };
			gridBackColorEle.RegisterValueChangedCallback(ev => MapEdSettings.GridBackColor = ev.newValue);

			var gridFillColorEle = new ColorField("Grid Fill Color") { value = MapEdSettings.GridFillColor };
			gridFillColorEle.RegisterValueChangedCallback(ev => MapEdSettings.GridFillColor = ev.newValue);

			var resetColorsButton = new Button { text = "Reset Grid Colors" };
			resetColorsButton.style.width = 120;
			resetColorsButton.style.alignSelf = Align.FlexEnd;
			resetColorsButton.clicked += () => MapEdSettings.ResetGridColors();

			var useLightEle = new Toggle("IcoGen uses Light") { value = MapEdSettings.IcoGenUseLight };
			useLightEle.RegisterValueChangedCallback(ev => MapEdSettings.IcoGenUseLight = ev.newValue);

			var lightIntensityEle = new FloatField("Light Intensity") { value = MapEdSettings.LightIntensity };
			lightIntensityEle.RegisterValueChangedCallback(ev => MapEdSettings.LightIntensity = ev.newValue);

			InsertSpacer();
			rootVisualElement.Add(prefabLinkEle);
			InsertSpacer();
			rootVisualElement.Add(moveSnapEle);
			rootVisualElement.Add(rotateSnapEle);
			InsertSpacer();
			rootVisualElement.Add(gridStyleEle);
			rootVisualElement.Add(gridFrontColorEle);
			rootVisualElement.Add(gridBackColorEle);
			rootVisualElement.Add(gridFillColorEle);
			rootVisualElement.Add(resetColorsButton);
			InsertSpacer();
			rootVisualElement.Add(useLightEle);
			rootVisualElement.Add(lightIntensityEle);

		}

		private void InsertSpacer()
		{
			var spacer = new VisualElement();
			spacer.style.height = 10;
			rootVisualElement.Add(spacer);
		}

		// ============================================================================================================
	}
}
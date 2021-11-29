using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PLY.MapEd
{
	public class ThumbItem : VisualElement
	{
		public new class UxmlFactory : UxmlFactory<ThumbItem, UxmlTraits> { }

		public new class UxmlTraits : VisualElement.UxmlTraits
		{
			public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription { get { yield break; } }
		}

		public bool AutoDestroyUnusedThumbImage { get; set; }
		public bool ThumbIsShown => thumbIcon?.image != null;

		private const string ussClass = "ply-thumb-item";
		private const string ussLabel = ussClass + "__label";
		private const string ussStatus = ussClass + "__status";
		private const string ussImage = ussClass + "__image";

		private string loadIcon = null;
		private string errorIcon = null;

		private int size;
		private TextElement statusIcon;
		private Image thumbIcon;
		private Label label;

		// ------------------------------------------------------------------------------------------------------------

		public ThumbItem()
		{
			AddToClassList(ussClass);

			label = new Label();
			label.AddToClassList(ussLabel);
			Add(label);

			SetSize(50);
		}

		public ThumbItem(string loadIcon, string errorIcon)
			: this(null, loadIcon, errorIcon)
		{ }

		public ThumbItem(string labeltext, string loadIcon, string errorIcon)
			: this()
		{
			if (labeltext == null)
			{
				label.style.visibility = Visibility.Hidden;
			}
			else
			{
				label.text = labeltext;
				label.style.visibility = Visibility.Visible;
			}

			this.loadIcon = loadIcon;
			this.errorIcon = errorIcon;

			ShowStatus(this.loadIcon);
		}

		public void Dispose()
		{
			ShowThumb(false);
			HideStatus();
		}

		public void SetSize(int size)
		{
			this.size = size;
			style.width = size;
			style.height = size;

			UpdateContentSize();
		}

		public void ShowThumb(bool show, Texture texture = null)
		{
			// destroy old texture if any
			if (AutoDestroyUnusedThumbImage && thumbIcon?.image != null)
			{
				var oldTexture = thumbIcon.image;
				thumbIcon.image = null;
				Object.DestroyImmediate(oldTexture);
			}

			if (show)
			{
				if (texture != null)
				{
					HideStatus();

					if (thumbIcon == null)
					{
						thumbIcon = new Image { focusable = false, pickingMode = PickingMode.Ignore };
						thumbIcon.AddToClassList(ussImage);
						Add(thumbIcon);
					}

					thumbIcon.image = texture;
				}
				else
				{
					ShowStatus(errorIcon);
				}
			}
			else if (thumbIcon != null)
			{
				Remove(thumbIcon);
				thumbIcon.image = null;
				thumbIcon = null;
			}

			// label should be drawn at top
			label.BringToFront();
		}

		public void HideStatus()
		{
			if (statusIcon != null)
			{
				Remove(statusIcon);
				statusIcon = null;
			}
		}

		public void ShowStatus(string ico)
		{
			ShowThumb(false);
			if (statusIcon == null)
			{
				statusIcon = new TextElement { text = ico, focusable = false, pickingMode = PickingMode.Ignore };
				statusIcon.AddToClassList(ussStatus);
				Add(statusIcon);
				UpdateContentSize();
			}
			else
			{
				statusIcon.text = ico;
			}
		}

		private void UpdateContentSize()
		{
			if (statusIcon != null)
			{
				statusIcon.style.fontSize = size / 3;
				statusIcon.MarkDirtyRepaint();
			}
		}

		// ============================================================================================================
	}
}
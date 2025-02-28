using UnityEngine;

[CreateAssetMenu(fileName="NewColorSet", menuName="Util/ColorSet", order=1)]
public class ColorSet : ScriptableObject {
	[System.Serializable]
	public struct WeightedColor {
		public Color color;
		public float weight;
	}
	public WeightedColor[] colors;
}

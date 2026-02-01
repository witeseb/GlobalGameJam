using UnityEngine;

public class PlayerMask : MonoBehaviour
{
	public bool IsMasked { get; private set; }

	[Header("Visuals")]
	public GameObject unmaskedModel;
	public GameObject maskedModel;

	public void ToggleMask()
	{
		SetMask(!IsMasked);
	}

	private void SetMask(bool value)
	{
		IsMasked = value;
		Debug.Log($"Mask state: {value}");
		Debug.Log($"ToggleMask called by {gameObject.name}");

		unmaskedModel.SetActive(!IsMasked);
		maskedModel.SetActive(IsMasked);
	}
}

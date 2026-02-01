using UnityEngine;

public class SmallCheeseIcon : MonoBehaviour
{
	[SerializeField]
	private GameObject offState;

	[SerializeField]
	private GameObject onState;

	private void Awake()
	{
		offState = transform.Find("offSmallCheese")?.gameObject;
		onState = transform.Find("onSmallCheese")?.gameObject;

		if (offState == null || onState == null)
			Debug.LogError($"[SmallCheeseIcon] Missing 'off' or 'on' on {name}");
	}

	public void SetCollected(bool collected)
	{
		if (offState != null)
			offState.SetActive(!collected);

		if (onState != null)
			onState.SetActive(collected);
	}
}

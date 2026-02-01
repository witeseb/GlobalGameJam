using UnityEngine;

public class ExtractionPoint : MonoBehaviour
{
	private void OnTriggerEnter(Collider other)
	{
		var inventory = other.GetComponent<PlayerInventory>();
		if (!inventory)
			return;

		if (inventory.HasBigCheese)
		{
			Debug.Log("YOU ESCAPED WITH THE BIG CHEESE!");
			// TODO: Win Screen
		}
		else
		{
			Debug.Log("You need the BIG CHEESE first!");
		}
	}
}

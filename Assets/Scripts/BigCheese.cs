using UnityEngine;

public class BigCheese : MonoBehaviour, IInteractable
{
	public void Interact(PlayerInventory player)
	{
		player.CollectBigCheese();
		Destroy(gameObject);
	}
}

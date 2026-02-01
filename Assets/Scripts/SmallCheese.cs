using UnityEngine;

public class SmallCheese : MonoBehaviour, IInteractable
{
	public void Interact(PlayerInventory player)
	{
		player.AddSmallCheese();
		Destroy(gameObject);
	}
}

using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
	public int smallCheeseCount { get; private set; }
	public bool HasBigCheese { get; private set; }

	public void AddSmallCheese()
	{
		smallCheeseCount++;
		Debug.Log($"Small Cheese Collected: {smallCheeseCount}");
	}

	public void CollectBigCheese()
	{
		HasBigCheese = true;
		Debug.Log("BIG CHEESE COLLECTED!");
	}
}

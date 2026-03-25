using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
	public int smallCheeseCount { get; private set; }
	public bool HasBigCheese { get; private set; }
    public event Action<int, bool> OnInventoryChanged;

    public void AddSmallCheese()
	{
		smallCheeseCount++;
		Debug.Log($"Small Cheese Collected: {smallCheeseCount}");
        OnInventoryChanged?.Invoke(smallCheeseCount, HasBigCheese);
    }

	public void CollectBigCheese()
	{
		HasBigCheese = true;
		Debug.Log("BIG CHEESE COLLECTED!");
        OnInventoryChanged?.Invoke(smallCheeseCount, HasBigCheese);
    }
}

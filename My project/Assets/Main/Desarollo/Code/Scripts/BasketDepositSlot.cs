using UnityEngine;

public sealed class BasketDepositSlot : MonoBehaviour
{
    public string ingredient;
    public CollectionProgress progress;
    public CollectibleIngredient StoredItem { get; private set; }

    public bool Accepts(CollectibleIngredient item)
    {
        return item && !StoredItem && (ingredient == "Ingredient" || ingredient == item.ingredient);
    }

    public void Store(CollectibleIngredient item)
    {
        if (!Accepts(item)) return;
        StoredItem = item;
        progress.Deposited(item.ingredient);
    }
}

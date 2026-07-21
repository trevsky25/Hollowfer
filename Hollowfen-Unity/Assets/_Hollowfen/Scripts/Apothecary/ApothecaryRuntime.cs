using System;
using System.Collections.Generic;
using Hollowfen.Foraging;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    /// <summary>
    /// Save-backed stock of preparations made at Tobin's workbench. Recipes consume the entire
    /// mushroom order and publish the resulting bottle/jar as one atomic journal revision.
    /// </summary>
    public static class ApothecaryRuntime
    {
        public enum CraftResult
        {
            Prepared,
            InvalidRecipe,
            RecipeLocked,
            SpeciesUnidentified,
            MissingIngredients,
            SaveUnavailable,
        }

        private static readonly Dictionary<string, int> Products =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> CraftedRecipes =
            new HashSet<string>(StringComparer.Ordinal);
        private static bool _interiorLightsOn;
        private static bool _hydrated;

        public static event Action OnChanged;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Products.Clear();
            CraftedRecipes.Clear();
            _interiorLightsOn = false;
            _hydrated = false;
            OnChanged = null;
        }

        public static bool InteriorLightsOn
        {
            get
            {
                EnsureHydrated();
                return _interiorLightsOn;
            }
        }

        public static int ProductCount(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId)) return 0;
            EnsureHydrated();
            return Products.TryGetValue(productId, out int count) ? count : 0;
        }

        public static bool HasCrafted(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId)) return false;
            EnsureHydrated();
            return CraftedRecipes.Contains(recipeId);
        }

        public static bool CanConsumeProducts(string[] productIds, int[] counts)
        {
            EnsureHydrated();
            int count = Math.Min(productIds?.Length ?? 0, counts?.Length ?? 0);
            if (count == 0 || productIds.Length != counts.Length) return false;
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string id = productIds[i];
                if (string.IsNullOrWhiteSpace(id) || counts[i] <= 0) return false;
                totals.TryGetValue(id, out int current);
                totals[id] = current + counts[i];
            }
            foreach (var row in totals)
                if (ProductCount(row.Key) < row.Value) return false;
            return true;
        }

        /// <summary>
        /// Removes prepared stock inside an already-open cross-system save transaction. The
        /// caller owns the final full save and rollback; outward shelf/UI refresh waits for commit.
        /// </summary>
        public static bool TryConsumeProductsForTransaction(string[] productIds, int[] counts)
        {
            if (!SaveManager.IsAtomicTransactionActive || !CanConsumeProducts(productIds, counts))
                return false;
            int count = Math.Min(productIds.Length, counts.Length);
            for (int i = 0; i < count; i++)
                Products[productIds[i]] -= counts[i];
            SaveManager.PublishAfterAtomicCommit(InvokeChangedSafely);
            return true;
        }

        public static CraftResult Availability(PreparationRecipeData recipe)
        {
            EnsureHydrated();
            if (recipe == null || !recipe.HasValidIngredients) return CraftResult.InvalidRecipe;
            if (!string.IsNullOrWhiteSpace(recipe.RequiredFlagId) &&
                !GameScores.HasFlag(recipe.RequiredFlagId)) return CraftResult.RecipeLocked;

            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                var species = recipe.Ingredients[i];
                if (species == null || !MushroomDiscovery.IsDiscovered(species.Id))
                    return CraftResult.SpeciesUnidentified;
            }

            for (int i = 0; i < recipe.Ingredients.Length; i++)
                if (InventoryRuntime.GetCount(recipe.Ingredients[i]) < recipe.Amounts[i])
                    return CraftResult.MissingIngredients;
            return CraftResult.Prepared;
        }

        public static CraftResult TryPrepare(PreparationRecipeData recipe)
        {
            CraftResult availability = Availability(recipe);
            if (availability != CraftResult.Prepared) return availability;

            InventorySnapshot inventoryBefore = InventoryRuntime.ToSnapshot();
            ApothecarySnapshot apothecaryBefore = ToSnapshot();
            var scoresBefore = new SaveSlotMeta();
            GameScores.WriteTo(scoresBefore);
            if (!SaveManager.TryBeginAtomicTransaction(out _)) return CraftResult.SaveUnavailable;

            try
            {
                if (!InventoryRuntime.TryRemoveBatch(recipe.Ingredients, recipe.Amounts, out _))
                {
                    SaveManager.CancelAtomicTransaction();
                    return CraftResult.MissingIngredients;
                }

                Products.TryGetValue(recipe.ResultId, out int current);
                Products[recipe.ResultId] = current + 1;
                bool firstCraft = CraftedRecipes.Add(recipe.Id);
                if (firstCraft)
                {
                    if (!string.IsNullOrWhiteSpace(recipe.CompletionFlagId))
                        GameScores.SetFlag(recipe.CompletionFlagId);
                    if (recipe.FirstCraftKnowledge > 0)
                        GameScores.AddKnowledge(recipe.FirstCraftKnowledge);
                }

                SaveCoordinator.SaveAllWithPlayer();
                if (SaveManager.TryCommitAtomicTransaction(out _))
                {
                    InvokeChangedSafely();
                    return CraftResult.Prepared;
                }

                Restore(inventoryBefore, apothecaryBefore, scoresBefore);
                return CraftResult.SaveUnavailable;
            }
            catch (Exception exception)
            {
                if (SaveManager.IsAtomicTransactionActive)
                    Restore(inventoryBefore, apothecaryBefore, scoresBefore);
                Debug.LogWarning("[Apothecary] Preparation failed: " + exception.Message);
                return CraftResult.SaveUnavailable;
            }
        }

        /// <summary>
        /// Persists the room-wide candlelight state as one recoverable journal revision so every
        /// switch in the building stays in agreement and the room returns as Wren left it.
        /// </summary>
        public static bool TrySetInteriorLights(bool on)
        {
            EnsureHydrated();
            if (_interiorLightsOn == on) return true;
            bool before = _interiorLightsOn;
            if (!SaveManager.TryBeginAtomicTransaction(out _)) return false;

            try
            {
                _interiorLightsOn = on;
                SaveCoordinator.SaveAllWithPlayer();
                if (SaveManager.TryCommitAtomicTransaction(out _))
                {
                    InvokeChangedSafely();
                    return true;
                }

                _interiorLightsOn = before;
                SaveManager.CancelAtomicTransaction();
                return false;
            }
            catch (Exception exception)
            {
                _interiorLightsOn = before;
                if (SaveManager.IsAtomicTransactionActive)
                    SaveManager.CancelAtomicTransaction();
                Debug.LogWarning("[Apothecary] Candlelight change failed: " + exception.Message);
                return false;
            }
        }

        public static void HydrateFrom(ApothecarySnapshot snapshot)
        {
            Products.Clear();
            CraftedRecipes.Clear();
            _interiorLightsOn = snapshot != null && snapshot.InteriorLightsOn;
            if (snapshot != null)
            {
                int count = Mathf.Min(snapshot.ProductIds?.Length ?? 0,
                    snapshot.ProductCounts?.Length ?? 0);
                for (int i = 0; i < count; i++)
                {
                    string id = snapshot.ProductIds[i];
                    if (string.IsNullOrWhiteSpace(id) || snapshot.ProductCounts[i] <= 0) continue;
                    Products[id] = snapshot.ProductCounts[i];
                }
                if (snapshot.CraftedRecipeIds != null)
                    foreach (string id in snapshot.CraftedRecipeIds)
                        if (!string.IsNullOrWhiteSpace(id)) CraftedRecipes.Add(id);
            }
            _hydrated = true;
        }

        public static ApothecarySnapshot ToSnapshot()
        {
            EnsureHydrated();
            var snapshot = new ApothecarySnapshot
            {
                ProductIds = new string[Products.Count],
                ProductCounts = new int[Products.Count],
                CraftedRecipeIds = new string[CraftedRecipes.Count],
                InteriorLightsOn = _interiorLightsOn,
            };
            int i = 0;
            foreach (var row in Products)
            {
                snapshot.ProductIds[i] = row.Key;
                snapshot.ProductCounts[i] = row.Value;
                i++;
            }
            CraftedRecipes.CopyTo(snapshot.CraftedRecipeIds);
            return snapshot;
        }

        private static void Restore(InventorySnapshot inventory, ApothecarySnapshot apothecary,
            SaveSlotMeta scores)
        {
            InventoryRuntime.HydrateFrom(inventory);
            HydrateFrom(apothecary);
            GameScores.HydrateFrom(scores);
            SaveManager.CancelAtomicTransaction();
        }

        private static void InvokeChangedSafely()
        {
            var handlers = OnChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Apothecary] Change listener failed: " + exception.Message);
                }
            }
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                HydrateFrom(SaveManager.GetSlotMeta(SaveManager.ActiveSlot)?.Apothecary);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Apothecary] Hydration failed: " + exception.Message);
            }
        }
    }
}

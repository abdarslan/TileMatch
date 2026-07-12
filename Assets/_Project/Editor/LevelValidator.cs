using System.Collections.Generic;
using System.Linq;
using TileMatch.Model;

namespace TileMatch.Editor
{
    /// <summary>
    /// Post-bake solvability analysis for a LevelData asset.
    /// Simulates an ideal player to detect deadlocks and depth traps.
    /// </summary>
    public static class LevelValidator
    {
        private const int RackCapacity   = 6;
        private const int MaxActiveOrders = 2;

        public class ValidationResult
        {
            public bool          IsSolvable    { get; set; }
            public int           TileCount     { get; set; }
            public int           ClickableCount { get; set; }
            public int           BlockedCount  { get; set; }
            public List<string>  Issues        { get; set; } = new List<string>();
            public List<string>  Hints         { get; set; } = new List<string>();
        }

        public static ValidationResult Validate(LevelData level)
        {
            var result = new ValidationResult
            {
                TileCount      = level.activeTiles.Count,
                ClickableCount = level.activeTiles.Count(t => t.blockingTileIDs.Count == 0),
            };
            result.BlockedCount = result.TileCount - result.ClickableCount;

            if (result.TileCount == 0)
            {
                result.Issues.Add("No tiles placed. Bake a level with at least one tile.");
                result.IsSolvable = false;
                return result;
            }

            if (result.ClickableCount == 0)
            {
                result.Issues.Add("No tiles are immediately clickable — the level is unbeatable from the start.");
                result.IsSolvable = false;
                return result;
            }

            // Simulate play: maintain a runtime tile dict and rack
            var runtimeTiles = level.activeTiles
                .ToDictionary(t => t.tileID, t => new SimTile(t));

            // Build reverse-blocker map: when a tile is removed, which tiles does it unblock?
            var unblocks = BuildUnblockMap(runtimeTiles);

            // Simulate with a greedy strategy (always prefer tiles that match an active order)
            var simulation = RunSimulation(runtimeTiles, unblocks, level.pendingOrders, result);

            result.IsSolvable = simulation.Completed;
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        private static Dictionary<int, List<int>> BuildUnblockMap(
            Dictionary<int, SimTile> tiles)
        {
            var map = new Dictionary<int, List<int>>();
            foreach (var kvp in tiles)
            {
                foreach (int blockerID in kvp.Value.BlockingIDs)
                {
                    if (!map.ContainsKey(blockerID))
                        map[blockerID] = new List<int>();
                    map[blockerID].Add(kvp.Key);
                }
            }
            return map;
        }

        private static SimResult RunSimulation(
            Dictionary<int, SimTile> originalTiles,
            Dictionary<int, List<int>> unblocks,
            List<OrderData> orders,
            ValidationResult result)
        {
            // Deep-copy mutable state
            var tiles     = originalTiles.ToDictionary(k => k.Key, k => k.Value.Clone());
            var rack      = new List<int>();
            var pending   = orders.Select(o => new SimOrder(o)).ToList();
            var active    = new List<SimOrder>();

            PromoteOrders(active, pending);

            int maxSteps = originalTiles.Count * 2 + 1;

            for (int step = 0; step < maxSteps; step++)
            {
                // Find all clickable tiles
                var clickable = tiles.Values.Where(t => t.RemainingBlockers == 0).ToList();
                if (clickable.Count == 0)
                    break;

                // Prefer a tile that directly satisfies any active order next slot
                SimTile best = FindOrderMatch(clickable, active)
                            ?? FindOrderMatch(clickable, pending.Take(MaxActiveOrders).ToList())
                            ?? clickable[0];

                PickTile(best, tiles, unblocks, rack, active, pending, result);

                if (tiles.Count == 0)
                    return new SimResult { Completed = true };

                if (rack.Count >= RackCapacity)
                {
                    // Check if any rack tile matches an active order
                    if (!RackHasOrderMatch(rack, tiles, active))
                    {
                        result.Issues.Add($"Rack fills to {RackCapacity} slots with no matching order — game over.");
                        DiagnoseRackFull(tiles, rack, active, result);
                        return new SimResult { Completed = false };
                    }
                }
            }

            bool allTilesDone = tiles.Count == 0;
            if (!allTilesDone)
                result.Issues.Add($"{tiles.Count} tiles remain and cannot be reached — possible deadlock.");

            return new SimResult { Completed = allTilesDone };
        }

        private static void PickTile(
            SimTile tile,
            Dictionary<int, SimTile> tiles,
            Dictionary<int, List<int>> unblocks,
            List<int> rack,
            List<SimOrder> active,
            List<SimOrder> pending,
            ValidationResult result)
        {
            tiles.Remove(tile.ID);

            // Unblock tiles that depended on this one
            if (unblocks.TryGetValue(tile.ID, out var nowFree))
            {
                foreach (int freedID in nowFree)
                    if (tiles.TryGetValue(freedID, out var freed))
                        freed.RemainingBlockers--;
            }

            // Try to route to an active order first
            bool routedToOrder = false;
            foreach (var order in active)
            {
                if (order.CurrentIndex < order.Types.Count &&
                    order.Types[order.CurrentIndex] == tile.TypeID)
                {
                    order.CurrentIndex++;
                    routedToOrder = true;

                    if (order.CurrentIndex >= order.Types.Count)
                    {
                        active.Remove(order);
                        PromoteOrders(active, pending);
                    }
                    break;
                }
            }

            if (!routedToOrder)
                rack.Add(tile.TypeID);

            // Try to drain rack tiles against active orders
            bool draining = true;
            while (draining)
            {
                draining = false;
                foreach (var order in active)
                {
                    if (order.CurrentIndex >= order.Types.Count) continue;
                    int needed = order.Types[order.CurrentIndex];

                    int rackIdx = rack.IndexOf(needed);
                    if (rackIdx < 0) continue;

                    rack.RemoveAt(rackIdx);
                    order.CurrentIndex++;
                    draining = true;

                    if (order.CurrentIndex >= order.Types.Count)
                    {
                        active.Remove(order);
                        PromoteOrders(active, pending);
                        break;
                    }
                }
            }
        }

        private static SimTile FindOrderMatch(List<SimTile> clickable, List<SimOrder> orders)
        {
            foreach (var order in orders)
            {
                if (order.CurrentIndex >= order.Types.Count) continue;
                int needed = order.Types[order.CurrentIndex];
                var match  = clickable.FirstOrDefault(t => t.TypeID == needed);
                if (match != null) return match;
            }
            return null;
        }

        private static bool RackHasOrderMatch(List<int> rack, Dictionary<int, SimTile> tiles, List<SimOrder> active)
        {
            foreach (var order in active)
            {
                if (order.CurrentIndex >= order.Types.Count) continue;
                if (rack.Contains(order.Types[order.CurrentIndex])) return true;
            }
            return false;
        }

        private static void PromoteOrders(List<SimOrder> active, List<SimOrder> pending)
        {
            while (active.Count < MaxActiveOrders && pending.Count > 0)
            {
                active.Add(pending[0]);
                pending.RemoveAt(0);
            }
        }

        private static void DiagnoseRackFull(
            Dictionary<int, SimTile> tiles,
            List<int> rack,
            List<SimOrder> active,
            ValidationResult result)
        {
            // Find which type IDs are needed but completely buried under blocking tiles
            var neededTypes = active
                .Where(o => o.CurrentIndex < o.Types.Count)
                .Select(o => o.Types[o.CurrentIndex])
                .Distinct()
                .ToList();

            foreach (int needed in neededTypes)
            {
                var tilesOfType = tiles.Values.Where(t => t.TypeID == needed).ToList();
                if (tilesOfType.Count == 0)
                {
                    result.Issues.Add($"Type {needed} is required by an active order but has no remaining tiles on the board.");
                    continue;
                }

                var reachable = tilesOfType.Where(t => t.RemainingBlockers == 0).ToList();
                if (reachable.Count == 0)
                {
                    int deepest = tilesOfType.Max(t => t.RemainingBlockers);
                    result.Hints.Add(
                        $"Type {needed} (needed next) is fully buried — deepest tile has {deepest} blocker(s). " +
                        "Consider exposing at least one tile of this type earlier in the layer stack.");
                }
            }

            if (neededTypes.Count == 0)
                result.Hints.Add("Rack filled but no active orders remain — level may have more tiles than orders can consume.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private class SimTile
        {
            public int        ID                { get; private set; }
            public int        TypeID            { get; private set; }
            public int        RemainingBlockers { get; set; }
            private List<int> _blockingIDs;
            public List<int>  BlockingIDs => _blockingIDs;

            public SimTile(TileSaveData data)
            {
                ID                = data.tileID;
                TypeID            = data.typeID;
                _blockingIDs      = new List<int>(data.blockingTileIDs);
                RemainingBlockers = data.blockingTileIDs.Count;
            }

            private SimTile() { }

            public SimTile Clone()
            {
                return new SimTile
                {
                    ID                = this.ID,
                    TypeID            = this.TypeID,
                    _blockingIDs      = new List<int>(this._blockingIDs),
                    RemainingBlockers = this.RemainingBlockers
                };
            }
        }

        private class SimOrder
        {
            public List<int> Types        { get; }
            public int       CurrentIndex { get; set; }

            public SimOrder(OrderData data)
            {
                Types        = new List<int>(data.requiredTypeIDs);
                CurrentIndex = 0;
            }
        }

        private class SimResult
        {
            public bool Completed { get; set; }
        }
    }
}

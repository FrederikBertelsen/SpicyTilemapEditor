using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpicyTilemapEditor.PathFindingLib;

namespace SpicyTilemapEditor
{
    public class MapTileNode : IPathNode
    {
        public int GridX;
        public int GridY;
        public override Vector3 Position { get; set; }

        private readonly TilemapGroup _tilemapGroup;

        // the index is a concat of the grid position (two int32) into a single int32
        private readonly int[] _neighborIdxArray = new int[8];

        private readonly MapPathFinding _owner;
        private readonly float _costFactor;

        public override string ToString() =>
            "MapTileNode: " + " GridX: " + GridX + " GridY: " + GridY + " Position: " + Position;

        public static int UnsafeJointTwoInts(System.Int32 a, System.Int32 b) =>
            ((System.UInt16)a << 16) | (System.UInt16)b;

        /// <summary>
        /// Creates a new MapTileNode
        /// </summary>
        /// <param name="idx">The index is a concatenation of the two grid positions</param>
        /// <param name="tilemapGroup"></param>
        /// <param name="owner"></param>
        public MapTileNode(TilemapGroup tilemapGroup, MapPathFinding owner, int idx)
        {
            _owner = owner;
            _tilemapGroup = tilemapGroup;
            SetGridPos(idx >> 16, (int)(short)idx, _owner.CellSize);

            //NOTE: calculate m_costFactor here using Tile parameters
            _costFactor = 1f;
        }

        public void SetGridPos(int gridX, int gridY, Vector2 cellSize)
        {
            GridX = gridX;
            GridY = gridY;
            Position = TilemapUtils.GetGridWorldPos(gridX, gridY, cellSize);

            for (int y = -1, neighborIdx = 0; y <= 1; ++y)
            for (int x = -1; x <= 1; ++x)
                if ((x | y) != 0) // skip this node
                    _neighborIdxArray[neighborIdx++] = UnsafeJointTwoInts(GridX + x, GridY + y);
        }

        #region IPathNode

        public override bool IsPassable()
        {
            if (_owner.IsComputing && _owner.AllowBlockedDestination && _owner.EndNode == this)
                return true;

            bool isBlocked = false;
            bool isEmptyCell = true; // true is not tile is found in the node position
            for (int i = 0; !isBlocked && i < _tilemapGroup.Tilemaps.Count; ++i)
            {
                STETilemap tilemap = _tilemapGroup[i];
                if (!tilemap ||
                    tilemap.ColliderType == EColliderType.None ||
                    !tilemap.IsGridPositionInsideTilemap(GridX, GridY))
                    continue;

                // Use Position instead to allow tilemaps with offset different than 0, tilemap.GetTile(GridX, GridY);
                Tile tile = tilemap.GetTile(tilemap.transform.InverseTransformPoint(Position));
                isEmptyCell = false;
                isBlocked = tile != null && tile.collData.type != eTileCollider.None;
            }

            return !isBlocked && !isEmptyCell;
        }

        public override float GetHeuristic()
        {
            //NOTE: 10f in Manhattan and 14f in Diagonal should really be 1f and 1.41421356237f, but I discovered by mistake these values improve the performance

            float h = 0f;

            switch (_owner.HeuristicType)
            {
                case MapPathFinding.eHeuristicType.None:
                    h = 0f;
                    break;
                case MapPathFinding.eHeuristicType.Manhattan: {
                    h = 10f * (Mathf.Abs(GridX - _owner.EndNode.GridX) + Mathf.Abs(GridY - _owner.EndNode.GridY));
                    break;
                }
                case MapPathFinding.eHeuristicType.Diagonal: {
                    float xf = Mathf.Abs(GridX - _owner.EndNode.GridX);
                    float yf = Mathf.Abs(GridY - _owner.EndNode.GridY);
                    if (xf > yf)
                        h = 14f * yf + 10f * (xf - yf);
                    else
                        h = 14f * xf + 10f * (yf - xf);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return h;
        }


        public override float GetNeighborMovingCost(int neighborIdx)
        {
            MapTileNode neighborNode = GetNeighbor(neighborIdx) as MapTileNode;
            if (!_owner.AllowBlockedDestination || _owner.EndNode != neighborNode)
            {
                if ((_owner.PassableDetectionMode & MapPathFinding.ePassableDetectionMode.Raycast2D) != 0)
                {
                    if (RaycastCheck2D(neighborNode.Position))
                        return PathFinding.k_InfiniteCostValue;
                }
                else if ((_owner.PassableDetectionMode & MapPathFinding.ePassableDetectionMode.Raycast3D) != 0)
                {
                    if (RaycastCheck3D(neighborNode.Position))
                        return PathFinding.k_InfiniteCostValue;
                }
            }

            float fCost = 1f;
            //567 // 
            //3X4 // neighbor index positions, X is the position of this node
            //012
            if (neighborIdx is 0 or 2 or 5 or 7)
            {
                //check if can reach diagonals as it could be not possible if flank tiles are not passable      
                MapTileNode nodeN = GetNeighbor(1) as MapTileNode;
                MapTileNode nodeW = GetNeighbor(3) as MapTileNode;
                MapTileNode nodeE = GetNeighbor(4) as MapTileNode;
                MapTileNode nodeS = GetNeighbor(6) as MapTileNode;
                bool usingColliders = (_owner.PassableDetectionMode &
                                       (MapPathFinding.ePassableDetectionMode.Raycast2D |
                                        MapPathFinding.ePassableDetectionMode.Raycast2D)) != 0;
                bool nodeNisPassable = nodeN.IsPassable() &&
                                       (!usingColliders || GetNeighborMovingCost(1) != PathFinding.k_InfiniteCostValue);
                bool nodeWisPassable = nodeW.IsPassable() &&
                                       (!usingColliders || GetNeighborMovingCost(3) != PathFinding.k_InfiniteCostValue);
                bool nodeEisPassable = nodeE.IsPassable() &&
                                       (!usingColliders || GetNeighborMovingCost(4) != PathFinding.k_InfiniteCostValue);
                bool nodeSisPassable = nodeS.IsPassable() &&
                                       (!usingColliders || GetNeighborMovingCost(6) != PathFinding.k_InfiniteCostValue);

                if (
                    !_owner.AllowDiagonals ||
                    (neighborIdx == 0 && (!nodeNisPassable || !nodeWisPassable)) || // check North West
                    (neighborIdx == 2 && (!nodeNisPassable || !nodeEisPassable)) || // check North East
                    (neighborIdx == 5 && (!nodeSisPassable || !nodeWisPassable)) || // check South West
                    (neighborIdx == 7 && (!nodeSisPassable || !nodeEisPassable)) // check South East
                )
                    return PathFinding.k_InfiniteCostValue;

                fCost = 1.41421356237f;
            }
            else
                fCost = 1f;

            fCost *= _costFactor;
            return fCost;
        }

        public override int GetNeighborCount() => 8;

        public override IPathNode GetNeighbor(int neighbourIdx) =>
            _owner.GetMapTileNode(_neighborIdxArray[neighbourIdx]);

        static RaycastHit2D[] s_raycastHit2DCache = new RaycastHit2D[10];

        private bool RaycastCheck2D(Vector3 targetPosition)
        {
            bool savedValue = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = false;
            Vector3 dir = targetPosition - Position;
            int hitCount = Physics2D.RaycastNonAlloc(Position, dir, s_raycastHit2DCache, dir.magnitude,
                _owner.RaycastDetectionLayerMask);
            bool raycastCheck = false;
            for (int i = 0; i < hitCount; i++)
            {
                // skip collisions from starting position (avoid collision with self)
                if (s_raycastHit2DCache[i].distance == 0)
                    continue;
                raycastCheck = true;
                break;
            }

            Physics2D.queriesHitTriggers = savedValue;
            return raycastCheck;
        }

        static RaycastHit[] s_raycastHit3DCache = new RaycastHit[10];

        private bool RaycastCheck3D(Vector3 targetPosition)
        {
            bool savedValue = Physics.queriesHitTriggers;
            Physics.queriesHitTriggers = false;
            Vector3 dir = targetPosition - Position;
            int hitCount = Physics.RaycastNonAlloc(Position, dir, s_raycastHit3DCache, dir.magnitude,
                _owner.RaycastDetectionLayerMask);
            bool raycastCheck = false;
            for (int i = 0; i < hitCount; i++)
            {
                // skip collisions from starting position (avoid collision with self)
                if (s_raycastHit3DCache[i].distance == 0)
                    continue;

                raycastCheck = true;
                break;
            }

            Physics.queriesHitTriggers = savedValue;
            return raycastCheck;
        }

        #endregion
    }

    [System.Serializable]
    public class MapPathFinding
    {
        public enum eHeuristicType
        {
            /// <summary>
            /// Very slow but guarantees the shortest path
            /// </summary>
            None,

            /// <summary>
            /// Faster than None, but does not guarantees the shortest path
            /// </summary>
            Manhattan,

            /// <summary>
            /// Faster than Manhattan but less accurate
            /// </summary>
            Diagonal
        }

        [System.Flags]
        public enum ePassableDetectionMode
        {
            /// <summary>
            /// Check if the tile has colliders to consider it non-passable
            /// </summary>
            TileColliderCheck = 1,

            /// <summary>
            /// Use 2D raycasting
            /// </summary>
            Raycast2D = 1 << 1,

            /// <summary>
            /// Use 3D raycasting
            /// </summary>
            Raycast3D = 1 << 2,
        }

        public eHeuristicType HeuristicType = eHeuristicType.Manhattan;

        [Tooltip(
            "Maximum distance or number of nodes a path can have when calculating the route. If the value is 0 or below, it won't be taking into account.")]
        public int MaxDistance = 0;

        public ePassableDetectionMode PassableDetectionMode = ePassableDetectionMode.TileColliderCheck;
        public LayerMask RaycastDetectionLayerMask = -1;
        public TilemapGroup TilemapGroup;

        public Vector2 CellSize
        {
            get
            {
                if (m_cellSize == default(Vector2) && TilemapGroup.Tilemaps.Count > 0)
                    m_cellSize = TilemapGroup[0].CellSize;

                return m_cellSize;
            }
            set => m_cellSize = value;
        }

        /// <summary>
        /// Set if diagonal movement is allowed
        /// </summary>
        [Tooltip("Set if diagonal movement is allowed")]
        public bool AllowDiagonals = true;

        /// <summary>
        /// If this is true, the final destination tile can be a blocked tile. 
        /// Used to reach the closest tile to a blocked tile, like a door, chest, etc
        /// </summary>
        [Tooltip("If this is true, the final destination tile can be a blocked tile")]
        public bool AllowBlockedDestination;

        [SerializeField] private Vector2 m_cellSize = default(Vector2);

        /// <summary>
        /// Max iterations to find a path. Use a value <= 0 for infinite iterations.
        /// Remember max iterations will be reached when trying to find a path with no solutions.
        /// </summary>
        public int MaxIterations
        {
            get => _pathFinding.MaxIterations;
            set => _pathFinding.MaxIterations = value;
        }

        public bool IsComputing => _pathFinding.IsComputing;

        private PathFinding _pathFinding = new PathFinding();

        Dictionary<int, MapTileNode> _dicTileNodes = new();

        // EndNode is used for the heuristic
        internal MapTileNode EndNode { get; private set; }

        public void ClearNodeDictionary() => _dicTileNodes.Clear();

        /// <summary>
        /// Get a map tile node based on its index
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        public MapTileNode GetMapTileNode(int idx)
        {
            bool wasFound = _dicTileNodes.TryGetValue(idx, out var mapTileNode);
            if (!wasFound)
            {
                mapTileNode = new MapTileNode(TilemapGroup, this, idx);
                _dicTileNodes[idx] = mapTileNode;
            }

            return mapTileNode;
        }

        public MapTileNode GetMapTileNode(int gridX, int gridY) =>
            GetMapTileNode(MapTileNode.UnsafeJointTwoInts(gridX, gridY));

        public MapTileNode GetMapTileNode(Vector2 position) =>
            GetMapTileNode(BrushUtil.GetGridX(position, CellSize), BrushUtil.GetGridY(position, CellSize));

        /// <summary>
        /// Return a list of path nodes from the start tile to the end tile
        /// </summary>
        public LinkedList<IPathNode> GetRouteFromTo(Vector2 startPosition, Vector2 endPosition)
        {
            LinkedList<IPathNode> nodeList = new LinkedList<IPathNode>();
            if (_pathFinding.IsComputing)
            {
                Debug.LogWarning("PathFinding is already computing. GetRouteFromTo will not be executed!");
            }
            else
            {
                IPathNode start = GetMapTileNode(startPosition);
                EndNode = GetMapTileNode(endPosition);
                nodeList = _pathFinding.ComputePath(start, EndNode,
                    MaxDistance > 0
                        ? MaxDistance
                        : int.MaxValue); //NOTE: the path is given from end to start ( change the order? )
            }

            return nodeList;
        }

        /// <summary>
        /// Return a list of path nodes from the start tile to the end tile
        /// </summary>
        public IEnumerator GetRouteFromToAsync(Vector2 startPosition, Vector2 endPosition)
        {
            IPathNode start = GetMapTileNode(startPosition);
            EndNode = GetMapTileNode(endPosition);
            return _pathFinding.ComputePathAsync(start, EndNode, MaxDistance > 0 ? MaxDistance : int.MaxValue);
        }
    }
}
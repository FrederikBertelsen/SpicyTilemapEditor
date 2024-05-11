using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpicyTilemapEditor.PathFindingLib;

namespace SpicyTilemapEditor
{
    public class PathfindingBehaviour : MonoBehaviour
    {
        public MapPathFinding PathFinding = new MapPathFinding();

        public enum EComputeMode
        {
            /// <summary>
            /// Stops execution until path is computed
            /// </summary>
            Synchronous, 
            /// <summary>
            /// Using a coroutine
            /// </summary>
            Asynchronous,
        }
        public EComputeMode computeMode = EComputeMode.Asynchronous;
        public int asyncCoroutineIterations = 20;
        public float movingSpeed = 1f;
        [Tooltip("Distance to the next node to be considered reached and move to the next one")]
        public float reachNodeDistance = 0.1f;

        public delegate void OnComputedPathDelegate(PathfindingBehaviour source);
        public OnComputedPathDelegate OnComputedPath;


        private bool _isComputingPath;
        private LinkedList<IPathNode> _pathNodes;
        private LinkedListNode<IPathNode> _curNode;
        private Vector2 _targetPosition;

        private void Start()
        {
            if (!PathFinding.TilemapGroup)
                PathFinding.TilemapGroup = FindObjectOfType<TilemapGroup>();
            if (PathFinding.CellSize == default(Vector2))
                PathFinding.CellSize = PathFinding.TilemapGroup[0].CellSize;
        }

        public void Update()
        {
            // compute path to destination
            if(Input.GetMouseButtonDown(0))
            {
                _targetPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                switch(computeMode)
                { 
                    case EComputeMode.Asynchronous:
                        StopAllCoroutines();
                        StartCoroutine(UpdatePathAsync(transform.position, _targetPosition, asyncCoroutineIterations));
                        break;
                    case EComputeMode.Synchronous:
                        UpdatePathSync(transform.position, _targetPosition);
                        break;
                }
            }

            //Move to destination
            if (_curNode != null)
            {
                Vector2 position = transform.position;
                Vector2 dest = _curNode.Next == null ? _targetPosition : (Vector2)_curNode.Value.Position;

                Vector3 dir = dest - position;
                if (dir.magnitude <= reachNodeDistance)
                    _curNode = _curNode.Next;
                transform.position += dir.normalized * (movingSpeed * Time.deltaTime);
            }

#if UNITY_EDITOR
            DebugDrawPath();
#endif
        }

        private void DebugDrawPath()
        {
            if (_isComputingPath) 
                return;
            if (_pathNodes?.First == null) 
                return;
            
            Color color = Color.red;
            LinkedListNode<IPathNode> node = _pathNodes.First;
            while (node.Next != null)
            {
                Debug.DrawLine(((MapTileNode)node.Value).Position, ((MapTileNode)node.Next.Value).Position, color);
                node = node.Next;
                color = node.Next?.Next != null ? Color.white : Color.green;
            }
        }

        private void UpdatePathSync(Vector2 startPos, Vector2 endPos)
        {
            _pathNodes = PathFinding.GetRouteFromTo(startPos, endPos);
            ProcessComputedPath();
        }

        private IEnumerator UpdatePathAsync(Vector2 startPos, Vector2 endPos, int stepIterations)
        {
            _isComputingPath = true;
            IEnumerator coroutine = PathFinding.GetRouteFromToAsync(startPos, endPos);
            bool isFinished = false;
            do
            {
                for (int i = 0; i < stepIterations && !isFinished; ++i) 
                    isFinished = !coroutine.MoveNext();
                
                yield return null;
            }
            while (!isFinished);
            
            //Debug.Log("GetRouteFromToAsync execution time(ms): " + (Time.realtimeSinceStartup - now) * 1000);
            _pathNodes = coroutine.Current as LinkedList<IPathNode>;
            ProcessComputedPath();
            _isComputingPath = false;
            
            OnComputedPath?.Invoke(this);

            yield return null;
        }

        private void ProcessComputedPath()
        {
            //+++find the closest node and take next one if possible
            _curNode = _pathNodes.Count >= 2? _pathNodes.First : null;
            if (_curNode == null) 
                return;
            
            Vector2 vPos = transform.position;
            while (_curNode.Next != null)
            {
                MapTileNode n0 = _curNode.Value as MapTileNode;
                MapTileNode n1 = _curNode.Next.Value as MapTileNode;
                float distSqr = (vPos - (Vector2)n0.Position).sqrMagnitude;
                float distSqr2 = (vPos - (Vector2)n1.Position).sqrMagnitude;
                
                if (distSqr2 < distSqr)
                    _curNode = _curNode.Next;
                else
                    break;
            }
            // take next one, avoid moving backward in the path
            if (_curNode.Next != null)
                _curNode = _curNode.Next;
            //---
        }
    }
}

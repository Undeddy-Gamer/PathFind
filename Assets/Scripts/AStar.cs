using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;
using TMPro;

public enum TileType { START, GOAL, BLOCKED, STANDARD, PATH }

public class AStar : MonoBehaviour
{
    [SerializeField]
    private Tilemap tilemap;

    private TileType tileType;

    private bool start;

    private bool goal;

    private Vector3Int startPos;

    private Vector3Int goalPos;

    [SerializeField]
    private Tile[] tiles;

    [SerializeField]
    private RuleTile water;

    [SerializeField]
    private LayerMask mask;

    private HashSet<Vector3Int> blocked = new HashSet<Vector3Int>();

    bool first = true;

    private Node current;

    private Stack<Vector3Int> path;

    private HashSet<Node> openList;

    private HashSet<Node> closedList;

    private HashSet<Vector3Int> changedTiles = new HashSet<Vector3Int>();

    private Dictionary<Vector3Int, Node> allNodes = new Dictionary<Vector3Int, Node>();

    public static Dictionary<Vector3Int, TextMeshProUGUI> DEBUGINFO = new Dictionary<Vector3Int, TextMeshProUGUI>();

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0) && current == null)
        {
            //Thorws a raycast in the direction of the target
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero, Mathf.Infinity, mask);

            //If we didn't hit the block, then we can cast a spell
            if (hit.collider != null)
            {
                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector3Int clickPos = tilemap.WorldToCell(mouseWorldPos);

                ChangeTile(clickPos);
            }
        }
        if (Input.GetKeyDown(KeyCode.Space) && start && goal)
        {
            Algorithm(false);
        }
        if (Input.GetKeyDown(KeyCode.LeftControl) && start && goal)
        {
            Algorithm(true);
        }
    }

    public void PickTile(int type)
    {
        this.tileType = (TileType)type;
    }

    private void ChangeTile(Vector3Int clickPos)
    {
        if (clickPos == startPos)
        {
            start = false;
            startPos = Vector3Int.zero;
        }
        else if (clickPos == goalPos)
        {
            goal = false;
            goalPos = Vector3Int.zero;
        }
        if (tileType == TileType.START)
        {
            if (start)
            {
                tilemap.SetTile(startPos, tiles[3]);
            }

            startPos = clickPos;
            start = true;

        }
        else if (tileType == TileType.GOAL)
        {
            if (goal)
            {
                tilemap.SetTile(goalPos, tiles[3]);
            }

            goalPos = clickPos;
            goal = true;
        }
        else if (tileType == TileType.BLOCKED)
        {
            blocked.Add(clickPos);
            tilemap.SetTile(clickPos, water);
        }
        else if (tileType == TileType.STANDARD)
        {
            blocked.Remove(clickPos);
        }

        if (tileType != TileType.BLOCKED)
        {
            tilemap.SetTile(clickPos, tiles[(int)tileType]);
        }

        changedTiles.Add(clickPos);

    }

    private void Initialize()
    {
        current = GetNode(startPos);

        //Creates an open list for nodes that we might want to look at later
        openList = new HashSet<Node>();

        //Creates a closed list for nodes that we have examined
        closedList = new HashSet<Node>();

        changedTiles = new HashSet<Vector3Int>();

        //Adds the current node to the open list (we have examined it)
        openList.Add(current);

        path = null;

        first = false;
    }

    public void Algorithm(bool step)
    {
        if (current == null)
        {
            Initialize();
        }

        while (openList.Count > 0 && path == null)
        {
            List<Node> neighbours = FindNeighbours(current.Position);

            ExamineNeighbours(neighbours, current);

            UpdateCurrentTile(ref current);

            path = GeneratePath(current);

            if (step)
            {
                break;
            }
        }

        if (path != null)
        {
            foreach (Vector3Int position in path)
            {
                if (position != goalPos)
                {
                    tilemap.SetTile(position, tiles[4]);
                }
              
            }
        }

        AstarDebugger.Instance.CreateTiles(openList, closedList,allNodes,current.Position ,startPos, goalPos, path);

    }

    private List<Node> FindNeighbours(Vector3Int parentPosition)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++) //These two forloops makes sure that we all nodes around our current node
        {
            for (int y = -1; y <= 1; y++)
            {
                if (y != 0 || x != 0)
                {
                    Vector3Int neighbourPosition = new Vector3Int(parentPosition.x - x, parentPosition.y - y, parentPosition.z);

                    if (neighbourPosition != startPos && !blocked.Contains(neighbourPosition) && tilemap.GetTile(neighbourPosition))
                    {
                        Node neighbour = GetNode(neighbourPosition);
                        neighbours.Add(neighbour);
                    }

                }
            }
        }

        return neighbours;
    }

    private void ExamineNeighbours(List<Node> neighbours, Node current)
    {
        for (int i = 0; i < neighbours.Count; i++)
        {
            Node neighbour = neighbours[i];

            if (!ConnectedDiagonally(current, neighbour))
            {
                continue;
            }

            int gScore = DetermineGScore(neighbour.Position, current.Position);

            if (openList.Contains(neighbour))
            {
                if (current.G + gScore < neighbour.G)
                {
                    CalcValues(current, neighbour, gScore);
                }
            }
            else if (!closedList.Contains(neighbour))
            {
                CalcValues(current, neighbour, gScore);

                if (!openList.Contains(neighbour)) //An extra check for openlist containing the neigbour
                {
                    openList.Add(neighbour); //Then we need to add the node to the openlist
                }
            }
        }
    }

    private bool ConnectedDiagonally(Node currentNode, Node neighbour)
    {
        //Get's the direction
        Vector3Int direction = currentNode.Position - neighbour.Position;

        //Gets the positions of the nodes
        Vector3Int first = new Vector3Int(currentNode.Position.x + (direction.x * -1), currentNode.Position.y, currentNode.Position.z);
        Vector3Int second = new Vector3Int(currentNode.Position.x, currentNode.Position.y + (direction.y * -1), currentNode.Position.z);

        //Checks if both nodes are empty
        if (blocked.Contains(first) || blocked.Contains(second))
        {
            return false;
        }

        //The ndoes are empty
        return true;
    }

    private int DetermineGScore(Vector3Int neighbour, Vector3Int current)
    {
        int gScore = 0;

        int x = current.x - neighbour.x;
        int y = current.y - neighbour.y;

        if (Math.Abs(x - y) % 2 == 1)
        {
            gScore = 10; //The gscore for a vertical or horizontal node is 10
        }
        else
        {
            gScore = 14;
        }

        return gScore;
    }

    private void UpdateCurrentTile(ref Node current)
    {
        //The current node is removed fromt he open list
        openList.Remove(current);

        //The current node is added to the closed list
        closedList.Add(current);

        if (openList.Count > 0) //If the openlist has nodes on it, then we need to sort them by it's F value
        {
            current = openList.OrderBy(x => x.F).First();//Orders the list by the f value, to make it easier to pick the node with the lowest F val
        }
    }

    private Stack<Vector3Int> GeneratePath(Node current)
    {
        if (current.Position == goalPos) //If our current node is the goal, then we found a path
        {
            //Creates a stack to contain the final path
            Stack<Vector3Int> finalPath = new Stack<Vector3Int>();

            //Adds the nodes to the final path
            while (current.Position != startPos)
            {
                //Adds the current node to the final path
                finalPath.Push(current.Position);
                //Find the parent of the node, this is actually retracing the whole path back to start
                //By doing so, we will end up with a complete path.
                current = current.Parent;
            }

            //Returns the complete path
            return finalPath;
        }

        return null;

    }

    private void CalcValues(Node parent, Node neighbour, int cost)
    {
        //Sets the parent node
        neighbour.Parent = parent;

        //Calculates this nodes g cost, The parents g cost + what it costs to move tot his node
        neighbour.G = parent.G + cost;

        //H is calucalted, it's the distance from this node to the goal * 10
        neighbour.H = ((Math.Abs((neighbour.Position.x - goalPos.x)) + Math.Abs((neighbour.Position.y - goalPos.y))) * 10);

        //F is calcualted 
        neighbour.F = neighbour.G + neighbour.H;
    }



    private Node GetNode(Vector3Int position)
    {
        if (allNodes.ContainsKey(position))
        {
            return allNodes[position];
        }
        else
        {
            Node node = new Node(position);
            allNodes.Add(position, node);
            return node;
        }
    }

    public void Erase()
    {
        AstarDebugger.Instance.Erase(allNodes);

        foreach (Vector3Int position in changedTiles)
        {
            tilemap.SetTile(position, tiles[3]);
        }

        foreach (Vector3Int path in path)
        {
            tilemap.SetTile(path, tiles[3]);
        }

        tilemap.SetTile(startPos, tiles[3]);

        tilemap.SetTile(goalPos, tiles[3]);

        allNodes.Clear();

        current = null;
    }
}

public class Node
{
    public int G { get; set; }
    public int H { get; set; }
    public int F { get; set; }
    public Node Parent { get; set; }
    public Vector3Int Position { get; set; }
    
    private TextMeshProUGUI MyText { get; set; }

    public Node(Vector3Int position)
    {
        this.Position = position;
    }
}


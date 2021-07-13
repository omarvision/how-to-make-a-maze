using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeGeneratorAnimated : MonoBehaviour
{
    #region --- helper ---    
    private enum enumNeighborCase
    {
        UNDEFINED,
        t1, //top row
        t2,
        t3,
        m4, //middle rows
        m5,
        m6,
        b7, //bottom row
        b8,
        b9,
    }
    private enum enumNeighborToThe
    {
        UNDEFINED,
        Left,
        Right,
        Forward,
        Back,
    }
    private class Coord
    {
        public int X = -1;
        public int Z = -1;
        public enumNeighborToThe neighborToThe = enumNeighborToThe.UNDEFINED;
        public MazeCell.enumWall wallToRemoveInCurrent = MazeCell.enumWall.UNDEFINED;
        public MazeCell.enumWall wallToRemoveInParent = MazeCell.enumWall.UNDEFINED;
        public bool isSet
        {
            get
            {
                if (X == -1 || Z == -1)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        public Coord()
        {

        }
        public Coord(int x, int z)
        {
            X = x;
            Z = z;
        }
        public Coord(int x, int z, enumNeighborToThe kindofneighbor)
        {
            X = x;
            Z = z;
            neighborToThe = kindofneighbor;
            switch (neighborToThe)
            {
                case enumNeighborToThe.Left:
                    wallToRemoveInCurrent = MazeCell.enumWall.Right;
                    wallToRemoveInParent = MazeCell.enumWall.Left;
                    break;
                case enumNeighborToThe.Right:
                    wallToRemoveInCurrent = MazeCell.enumWall.Left;
                    wallToRemoveInParent = MazeCell.enumWall.Right;
                    break;
                case enumNeighborToThe.Forward:
                    wallToRemoveInCurrent = MazeCell.enumWall.Back;
                    wallToRemoveInParent = MazeCell.enumWall.Forward;
                    break;
                case enumNeighborToThe.Back:
                    wallToRemoveInCurrent = MazeCell.enumWall.Forward;
                    wallToRemoveInParent = MazeCell.enumWall.Back;
                    break;
            }
        }
        public Coord(Coord other)
        {
            X = other.X;
            Z = other.Z;
            neighborToThe = other.neighborToThe;
            wallToRemoveInCurrent = other.wallToRemoveInCurrent;
            wallToRemoveInParent = other.wallToRemoveInParent;
        }
    }
    private class LinkedCoord
    {
        public Coord coord = null;
        public LinkedCoord linkprev = null;
        public LinkedCoord linknext = null;

        public LinkedCoord(ref Coord current)
        {
            this.coord = current;
        }
    }
    #endregion

    public GameObject MazeCellPrefab = null;
    public Camera overheadCam = null;
    public int sizeX = 10;
    public int sizeZ = 10;
    public float generateSpeed = 0.1f;
    private AudioSource green = null;
    private AudioSource red = null;
    private AudioSource complete = null;
    private GameObject[,] mzCells = null;
    private LinkedCoord link;
    private IEnumerator coroutine;

    private void Start()
    {
        LoadSounds();

        MakeGridOfCells();
        CenterHighCamera();

        int rx = Random.Range(0, sizeX);
        int rz = Random.Range(0, sizeZ);
        Coord randomStartCell = new Coord(rx, rz);
        link = new LinkedCoord(ref randomStartCell);

        GenerateMaze(ref link);

        //open up the maze in two places 
        Coord start = new Coord(0, 0);
        Coord win = new Coord(sizeX - 1, sizeZ - 1);
        scriptOf(start).RemoveWall(MazeCell.enumWall.Back);
        scriptOf(win).RemoveWall(MazeCell.enumWall.Forward);
    }
    private void LoadSounds()
    {
        green = this.gameObject.AddComponent<AudioSource>();
        green.clip = Resources.Load<AudioClip>("green");

        red = this.gameObject.AddComponent<AudioSource>();
        red.clip = Resources.Load<AudioClip>("red");

        complete = this.gameObject.AddComponent<AudioSource>();
        complete.clip = Resources.Load<AudioClip>("complete");
    }
    private void MakeGridOfCells()
    {
        //allocate array
        mzCells = new GameObject[sizeX, sizeZ];

        //bottom, left corner
        float startX = (-sizeX / 2.0f) + (MazeCellPrefab.transform.localScale.x / 2.0f);
        float startZ = (-sizeZ / 2.0f) + (MazeCellPrefab.transform.localScale.z / 2.0f);

        //create grid of mazecells
        for (int z = 0; z < sizeZ; z++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                float xpos = startX + (x * MazeCellPrefab.transform.localScale.x);
                float zpos = startZ + (z * MazeCellPrefab.transform.localScale.z);
                Vector3 position = new Vector3(xpos, 0.0f, zpos);
                mzCells[x, z] = Instantiate(MazeCellPrefab, position, Quaternion.identity, this.transform);
                mzCells[x, z].name = string.Format("MazeCell[{0},{1}]", x.ToString("0"), z.ToString("0"));
            }
        }
    }
    private void GenerateMaze(ref LinkedCoord LINKCURR)
    {
        scriptOf(LINKCURR.coord).isVisited = true;

        //Note:
        //  PREV CELL
        //  moves to
        //  CURRENT CELL
        //  moves to
        //  (NEXT) NEIGHBOR CELL
        //
        //  which cell wall do I remove in current ?
        //  which cell wall do I remove in neighbor ?
        //
        //  (remove the touching walls)
        //      ie. if neighbor is LEFT then remove neighbor rightwall and current leftwall
        //
        //  (looking for neighbor in previous when run into deadend)
        //      LinkedCoord item will identify a current, previous, and next cell. Why? so that 
        //      we can travel backwards to look for unvisited neighbor when we run into deadends
        //

        Coord NeighborNEXT = RandomNeighbor(ref LINKCURR.coord);

        if (NeighborNEXT != null && NeighborNEXT.isSet == true)
        {
            //move to unvisted neighbor
            //  neighbor is not visited
            scriptOf(NeighborNEXT).isVisited = true;

            //  instantiate linknext
            LinkedCoord LINKNEXT = new LinkedCoord(ref NeighborNEXT);
            LINKNEXT.linkprev = LINKCURR;
            LINKNEXT.linknext = null;

            //  update the current link
            LINKCURR.linknext = LINKNEXT;

            Debug.Log(decriptionPrevToCurr(ref LINKNEXT));

            if (generateSpeed == 0.0f)
            {
                GenerateMaze(ref LINKNEXT);
            }
            else
            {
                coroutine = delayGenMaze(LINKNEXT, UnityEngine.Color.green);
                green.Play();
                StartCoroutine(coroutine);
            }
        }
        else
        {
            //backup, look for unvisited neighbor in previous cells
            if (LINKCURR.coord != null && anyUnvisitedCells() == true)
            {
                if (generateSpeed == 0.0f)
                {
                    GenerateMaze(ref LINKCURR.linkprev);
                }
                else
                {
                    coroutine = delayGenMaze(LINKCURR.linkprev, UnityEngine.Color.red);
                    red.Play();
                    StartCoroutine(coroutine);
                }
            }
        }
    }
    private MazeCell scriptOf(Coord COORD)
    {
        return mzCells[COORD.X, COORD.Z].gameObject.GetComponent<MazeCell>();
    }
    private Coord RandomNeighbor(ref Coord CURR)
    {
        //Note: 
        //  must find a random neighbor, fo rthe current cell, that is unvisited ONLY!
        //  set the neighbor up with it's wall to remove (which you will know because of the side it's a neighbor on)

        Coord NEXT = new Coord();

        /*
        *      t1 +    - t2 +    - t3
        *       -         -        -
        *      
        *      +          +         +     
        *      m4 +    - m5 +    - m6
        *      -          -         -     
        *      
        *      +          +         +
        *      b7 +    - b8 +    - b9
        *      
        *      
        *           Forward(+)
        *          
        *      Left(-)   +   Right(+)
        *      
        *             Back(-)
        *      
        */

        // 1. get random neighbor case
        enumNeighborCase NeighborCASE = enumNeighborCase.UNDEFINED;
        int maxX = sizeX - 1;
        int maxZ = sizeZ - 1;
        if (CURR.Z == maxZ)
        {
            if (CURR.X == 0)
            {
                NeighborCASE = enumNeighborCase.t1;
            }
            else if (CURR.X > 0 && CURR.X < maxX)
            {
                NeighborCASE = enumNeighborCase.t2;
            }
            else if (CURR.X == maxX)
            {
                NeighborCASE = enumNeighborCase.t3;
            }
        }
        else if (CURR.Z > 0 && CURR.Z < maxZ)
        {
            if (CURR.X == 0)
            {
                NeighborCASE = enumNeighborCase.m4;
            }
            else if (CURR.X > 0 && CURR.X < maxX)
            {
                NeighborCASE = enumNeighborCase.m5;
            }
            else if (CURR.X == maxX)
            {
                NeighborCASE = enumNeighborCase.m6;
            }
        }
        else if (CURR.Z == 0)
        {
            if (CURR.X == 0)
            {
                NeighborCASE = enumNeighborCase.b7;
            }
            else if (CURR.X > 0 && CURR.X < maxX)
            {
                NeighborCASE = enumNeighborCase.b8;
            }
            else if (CURR.X == maxX)
            {
                NeighborCASE = enumNeighborCase.b9;
            }
        }

        // 2. pick a random unvisited neighbor (based on the case)
        switch (NeighborCASE)
        {
            case enumNeighborCase.t1:
                NEXT = getnextfor_t1(CURR);
                break;
            case enumNeighborCase.t2:
                NEXT = getnextfor_t2(CURR);
                break;
            case enumNeighborCase.t3:
                NEXT = getnextfor_t3(CURR);
                break;

            case enumNeighborCase.m4:
                NEXT = getnextfor_m4(CURR);
                break;
            case enumNeighborCase.m5:
                NEXT = getnextfor_m5(CURR);
                break;
            case enumNeighborCase.m6:
                NEXT = getnextfor_m6(CURR);
                break;

            case enumNeighborCase.b7:
                NEXT = getnextfor_b7(CURR);
                break;
            case enumNeighborCase.b8:
                NEXT = getnextfor_b8(CURR);
                break;
            case enumNeighborCase.b9:
                NEXT = getnextfor_b9(CURR);
                break;
        }
        return NEXT;
    }
    private Coord getnextfor_t1(Coord CURR)
    {
        //possible next cells
        Coord NeighborNEXT = null;
        Coord rightNeighbor = new Coord(CURR.X + 1, CURR.Z, enumNeighborToThe.Right);
        Coord backNeighbor = new Coord(CURR.X, CURR.Z - 1, enumNeighborToThe.Back);

        //just use unvisited ones
        List<Coord> unvisited = new List<Coord>();
        if (scriptOf(rightNeighbor).isVisited == false) unvisited.Add(rightNeighbor);
        if (scriptOf(backNeighbor).isVisited == false) unvisited.Add(backNeighbor);
        if (unvisited.Count == 0) return NeighborNEXT;

        //random from unvisited ones
        int rnd = Random.Range(0, unvisited.Count);
        NeighborNEXT = new Coord(unvisited[rnd]);
        //remove the appropriate walls
        scriptOf(NeighborNEXT).RemoveWall(NeighborNEXT.wallToRemoveInCurrent);
        scriptOf(CURR).RemoveWall(NeighborNEXT.wallToRemoveInParent);

        return NeighborNEXT;
    }
    private Coord getnextfor_t2(Coord CURR)
    {
        Coord NeighborNEXT = null;
        Coord leftNeighbor = new Coord(CURR.X - 1, CURR.Z, enumNeighborToThe.Left);
        Coord rightNeighbor = new Coord(CURR.X + 1, CURR.Z, enumNeighborToThe.Right);
        Coord backNeighbor = new Coord(CURR.X, CURR.Z - 1, enumNeighborToThe.Back);

        List<Coord> unvisited = new List<Coord>();
        if (scriptOf(leftNeighbor).isVisited == false) unvisited.Add(leftNeighbor);
        if (scriptOf(rightNeighbor).isVisited == false) unvisited.Add(rightNeighbor);
        if (scriptOf(backNeighbor).isVisited == false) unvisited.Add(backNeighbor);
        if (unvisited.Count == 0) return NeighborNEXT;

        int rnd = Random.Range(0, unvisited.Count);
        NeighborNEXT = new Coord(unvisited[rnd]);
        scriptOf(NeighborNEXT).RemoveWall(NeighborNEXT.wallToRemoveInCurrent);
        scriptOf(CURR).RemoveWall(NeighborNEXT.wallToRemoveInParent);

        return NeighborNEXT;
    }
    private Coord getnextfor_t3(Coord CURR)
    {
        Coord NeighborNEXT = null;
        Coord leftNeighbor = new Coord(CURR.X - 1, CURR.Z, enumNeighborToThe.Left);
        Coord backNeighbor = new Coord(CURR.X, CURR.Z - 1, enumNeighborToThe.Back);

        List<Coord> availableNeighbors = new List<Coord>();
        if (scriptOf(leftNeighbor).isVisited == false) availableNeighbors.Add(leftNeighbor);
        if (scriptOf(backNeighbor).isVisited == false) availableNeighbors.Add(backNeighbor);
        if (availableNeighbors.Count == 0) return NeighborNEXT;

        int rnd = Random.Range(0, availableNeighbors.Count);
        NeighborNEXT = new Coord(availableNeighbors[rnd]);
        scriptOf(NeighborNEXT).RemoveWall(NeighborNEXT.wallToRemoveInCurrent);
        scriptOf(CURR).RemoveWall(NeighborNEXT.wallToRemoveInParent);

        return NeighborNEXT;
    }
    private Coord getnextfor_m4(Coord CURR)
    {
        Coord NeighborNEXT = null;
        Coord forwardNeighbor = new Coord(CURR.X, CURR.Z + 1, enumNeighborToThe.Forward);
        Coord backNeighbor = new Coord(CURR.X, CURR.Z - 1, enumNeighborToThe.Back);
        Coord rightNeighbor = new Coord(CURR.X + 1, CURR.Z, enumNeighborToThe.Right);

        List<Coord> availableNeighbors = new List<Coord>();
        if (scriptOf(forwardNeighbor).isVisited == false) availableNeighbors.Add(forwardNeighbor);
        if (scriptOf(backNeighbor).isVisited == false) availableNeighbors.Add(backNeighbor);
        if (scriptOf(rightNeighbor).isVisited == false) availableNeighbors.Add(rightNeighbor);
        if (availableNeighbors.Count == 0) return NeighborNEXT;

        int rnd = Random.Range(0, availableNeighbors.Count);
        NeighborNEXT = new Coord(availableNeighbors[rnd]);
        scriptOf(NeighborNEXT).RemoveWall(NeighborNEXT.wallToRemoveInCurrent);
        scriptOf(CURR).RemoveWall(NeighborNEXT.wallToRemoveInParent);

        return NeighborNEXT;
    }
    private Coord getnextfor_m5(Coord CURR)
    {
        Coord NeighborNEXT = null;
        Coord forwardNeighbor = new Coord(CURR.X, CURR.Z + 1, enumNeighborToThe.Forward);
        Coord backNeighbor = new Coord(CURR.X, CURR.Z - 1, enumNeighborToThe.Back);
        Coord leftNeighbor = new Coord(CURR.X - 1, CURR.Z, enumNeighborToThe.Left);
        Coord rightNeighbor = new Coord(CURR.X + 1, CURR.Z, enumNeighborToThe.Right);

        List<Coord> unvisited = new List<Coord>();
        if (scriptOf(forwardNeighbor).isVisited == false) unvisited.Add(forwardNeighbor);
        if (scriptOf(backNeighbor).isVisited == false) unvisited.Add(backNeighbor);
        if (scriptOf(leftNeighbor).isVisited == false) unvisited.Add(leftNeighbor);
        if (scriptOf(rightNeighbor).isVisited == false) unvisited.Add(rightNeighbor);
        if (unvisited.Count == 0) return NeighborNEXT;

        int rnd = Random.Range(0, unvisited.Count);
        NeighborNEXT = new Coord(unvisited[rnd]);
        scriptOf(NeighborNEXT).RemoveWall(NeighborNEXT.wallToRemoveInCurrent);
        scriptOf(CURR).RemoveWall(NeighborNEXT.wallToRemoveInParent);
        return NeighborNEXT;
    }
    private Coord getnextfor_m6(Coord CURR)
    {
        Coord NeighborNEXT = null;
        Coord forwardNeighbor = new Coord(CURR.X, CURR.Z + 1, enumNeighborToThe.Forward);
        Coord backNeighbor = new Coord(CURR.X, CURR.Z - 1, enumNeighborToThe.Back);
        Coord leftNeighbor = new Coord(CURR.X - 1, CURR.Z, enumNeighborToThe.Left);

        List<Coord> unvisited = new List<Coord>();
        if (scriptOf(forwardNeighbor).isVisited == false) unvisited.Add(forwardNeighbor);
        if (scriptOf(backNeighbor).isVisited == false) unvisited.Add(backNeighbor);
        if (scriptOf(leftNeighbor).isVisited == false) unvisited.Add(leftNeighbor);
        if (unvisited.Count == 0) return NeighborNEXT;

        int rnd = Random.Range(0, unvisited.Count);
        NeighborNEXT = new Coord(unvisited[rnd]);
        scriptOf(NeighborNEXT).RemoveWall(NeighborNEXT.wallToRemoveInCurrent);
        scriptOf(CURR).RemoveWall(NeighborNEXT.wallToRemoveInParent);
        return NeighborNEXT;
    }
    private Coord getnextfor_b7(Coord CURR)
    {
        Coord NeighborNEXT = null;
        Coord forwardNeighbor = new Coord(CURR.X, CURR.Z + 1, enumNeighborToThe.Forward);
        Coord rightNeighbor = new Coord(CURR.X + 1, CURR.Z, enumNeighborToThe.Right);

        List<Coord> unvisited = new List<Coord>();
        if (scriptOf(forwardNeighbor).isVisited == false) unvisited.Add(forwardNeighbor);
        if (scriptOf(rightNeighbor).isVisited == false) unvisited.Add(rightNeighbor);
        if (unvisited.Count == 0) return NeighborNEXT;

        int rnd = Random.Range(0, unvisited.Count);
        NeighborNEXT = new Coord(unvisited[rnd]);
        scriptOf(NeighborNEXT).RemoveWall(NeighborNEXT.wallToRemoveInCurrent);
        scriptOf(CURR).RemoveWall(NeighborNEXT.wallToRemoveInParent);
        return NeighborNEXT;
    }
    private Coord getnextfor_b8(Coord CURR)
    {
        Coord NeighborNEXT = null;
        Coord forwardNeighbor = new Coord(CURR.X, CURR.Z + 1, enumNeighborToThe.Forward);
        Coord leftNeighbor = new Coord(CURR.X - 1, CURR.Z, enumNeighborToThe.Left);
        Coord rightNeighbor = new Coord(CURR.X + 1, CURR.Z, enumNeighborToThe.Right);

        List<Coord> unvisited = new List<Coord>();
        if (scriptOf(forwardNeighbor).isVisited == false) unvisited.Add(forwardNeighbor);
        if (scriptOf(leftNeighbor).isVisited == false) unvisited.Add(leftNeighbor);
        if (scriptOf(rightNeighbor).isVisited == false) unvisited.Add(rightNeighbor);
        if (unvisited.Count == 0) return NeighborNEXT;

        int rnd = Random.Range(0, unvisited.Count);
        NeighborNEXT = new Coord(unvisited[rnd]);
        scriptOf(NeighborNEXT).RemoveWall(NeighborNEXT.wallToRemoveInCurrent);
        scriptOf(CURR).RemoveWall(NeighborNEXT.wallToRemoveInParent);
        return NeighborNEXT;
    }
    private Coord getnextfor_b9(Coord CURR)
    {
        Coord NeighborNEXT = null;
        Coord forwardNeighbor = new Coord(CURR.X, CURR.Z + 1, enumNeighborToThe.Forward);
        Coord leftNeighbor = new Coord(CURR.X - 1, CURR.Z, enumNeighborToThe.Left);

        List<Coord> unvisited = new List<Coord>();
        if (scriptOf(forwardNeighbor).isVisited == false) unvisited.Add(forwardNeighbor);
        if (scriptOf(leftNeighbor).isVisited == false) unvisited.Add(leftNeighbor);
        if (unvisited.Count == 0) return NeighborNEXT;

        int rnd = Random.Range(0, unvisited.Count);
        NeighborNEXT = new Coord(unvisited[rnd]);
        scriptOf(NeighborNEXT).RemoveWall(NeighborNEXT.wallToRemoveInCurrent);
        scriptOf(CURR).RemoveWall(NeighborNEXT.wallToRemoveInParent);
        return NeighborNEXT;
    }
    IEnumerator delayGenMaze(LinkedCoord LC, UnityEngine.Color color)
    {
        //the waitforseconds delays the visual maze generation process, for user to see it in action
        scriptOf(LC.coord).TravelIndicator(true, color);
        yield return new WaitForSeconds(generateSpeed);
        scriptOf(LC.coord).TravelIndicator(false, color);
        GenerateMaze(ref LC);
    }
    private bool anyUnvisitedCells()
    {
        for (int z = 0; z < sizeZ; z++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                if (mzCells[x, z].gameObject.GetComponent<MazeCell>().isVisited == false)
                    return true;
            }
        }
        complete.Play();
        return false;
    }
    private string decriptionPrevToCurr(ref LinkedCoord linkcurr)
    {
        string s1 = "";
        string prevName = mzCells[linkcurr.linkprev.coord.X, linkcurr.linkprev.coord.Z].name;
        string currName = mzCells[linkcurr.coord.X, linkcurr.coord.Z].name;

        s1 += "MOVE ";
        switch (linkcurr.linkprev.coord.neighborToThe)
        {
            case enumNeighborToThe.Left:
                s1 += " L";
                break;
            case enumNeighborToThe.Right:
                s1 += " R";
                break;
            case enumNeighborToThe.Forward:
                s1 += " F";
                break;
            case enumNeighborToThe.Back:
                s1 += " B";
                break;
        }
        s1 += "   :   " + prevName + "  =>  " + currName;

        return s1;
    }
    private void CenterHighCamera()
    {
        //Note: 
        //  plan is to raise the first center the camera by X,Z in maze
        //  and then raise camera on Y until corner of maze is seen

        Transform BL = mzCells[0, 0].transform;
        Transform XX = mzCells[((sizeX) / 2), ((sizeZ) / 2)].transform;
        Transform TR = mzCells[sizeX - 1, sizeZ - 1].transform;

        /*  
        *  Maze grid of cells
        *  
        *  [] [] [] [] TR
        *  [] [] [] [] []
        *  [] [] XX [] []
        *  [] [] [] [] []
        *  BL [] [] [] []
        *  
        *  */

        //center camera over maze
        overheadCam.transform.position = new Vector3(XX.transform.position.x, 1.0f, XX.transform.position.z);
        overheadCam.transform.LookAt(XX.transform);

        //raise camera up (until corner visible)
        for (int i = 0; i < 20; i++)
        {
            //move up
            overheadCam.transform.Translate(new Vector3(0.0f, 1.0f, 0.0f), Space.World);

            //check visibility
            Vector3 sp = overheadCam.WorldToViewportPoint(BL.transform.position);
            bool onScreen = (sp.z > 0 && sp.x > 0) && (sp.x < 1) && (sp.y > 0 && sp.y < 1);
            if (onScreen == true)
            {
                //if the corner is now visible, just move up one more notch
                overheadCam.transform.Translate(new Vector3(0, 2.0f, 0), Space.World);
                break;
            }
        }
    }
}
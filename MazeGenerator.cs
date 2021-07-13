using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    #region --- helper ---    
    private enum enumNeighborCase
    {
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
        */
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
        /*
        *  ARRAY:
        *  [] [] [] [] [] [] [] 
        *  
        *  LINKED LIST:
        *  null <-[]-> <-[]-> <-[]-> <-[]-> <-[]-> <-[]-> <-[]-> null
        *  
        * */
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
    public GameObject PortalStartPrefab = null;
    public GameObject PortalWinPrefab = null;
    public GameObject Player = null;
    public GameObject TheMinatour = null;
    public Camera overheadCam = null;
    public int sizeX = 10;
    public int sizeZ = 10;
    public float cellDelaySeconds = 0.0f;

    public int numDrop = 25;
    public GameObject DropPrefab = null;
    public GameObject DropParent = null;
    private List<GameObject> DropObjects = new List<GameObject>();

    private GameObject[,] mzCells = null;
    private GameObject portalstart = null;
    private GameObject portalwin = null;
    private LinkedCoord link;
    private AudioSource green = null;
    private AudioSource red = null;
    private AudioSource complete = null;
    private IEnumerator coroutine = null;

    private void Start()
    {
        LoadAudio();
        MakeGridOfCells();

        // portals
        float mzCellSize = mzCells[0, 0].transform.localScale.z;
        Vector3 ppStart = new Vector3(mzCells[0, 0].transform.position.x, mzCells[0, 0].transform.position.y, mzCells[0, 0].transform.position.z - mzCellSize);
        Vector3 ppWin = new Vector3(mzCells[sizeX - 1, sizeZ - 1].transform.position.x, mzCells[sizeX - 1, sizeZ - 1].transform.position.y, mzCells[sizeX - 1, sizeZ - 1].transform.position.z + mzCellSize);
        portalstart = Instantiate(PortalStartPrefab, ppStart, Quaternion.identity, this.transform);
        portalwin = Instantiate(PortalWinPrefab, ppWin, Quaternion.identity, this.transform);

        CenterHighCamera();

        // pick random cell
        int rx = Random.Range(0, sizeX);
        int rz = Random.Range(0, sizeZ);
        Coord randomStartCell = new Coord(rx, rz);

        // first cell in linkedlist
        link = new LinkedCoord(ref randomStartCell);

        // start maze algorithm
        GenerateMaze(ref link);

        // open maze in 2 places
        Coord start = new Coord(0, 0);
        Coord win = new Coord(sizeX - 1, sizeZ - 1);
        scriptOf(start).RemoveWall(MazeCell.enumWall.Back);
        scriptOf(win).RemoveWall(MazeCell.enumWall.Forward);

        // position minatour
        TheMinatour.GetComponent<Minatour>().Init(mzCells);

        // drop a number of objects in the maze, let them fall where they do
        RandomDropObjects();

        // first person controller
        Vector3 BLpos = mzCells[0, 0].transform.position;
        Player.transform.position = portalstart.transform.position;
    }
    private void MakeGridOfCells()
    {
        // allocate 2D array
        mzCells = new GameObject[sizeX, sizeZ];

        // grid of cells (coords stored in 2D array)
        float mazeStartLeftX = (-sizeX / 2.0f) + (MazeCellPrefab.transform.localScale.x / 2.0f);
        float mazeStartBottomZ = (-sizeZ / 2.0f) + (MazeCellPrefab.transform.localScale.z / 2.0f);
        for (int z = 0; z < sizeZ; z++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                float currX = mazeStartLeftX + (x * MazeCellPrefab.transform.localScale.x);
                float currZ = mazeStartBottomZ + (z * MazeCellPrefab.transform.localScale.z);
                mzCells[x, z] = Instantiate(MazeCellPrefab, new Vector3(currX, 0.0f, currZ), Quaternion.identity, this.transform);
                mzCells[x, z].name = string.Format("MazeCell[{0},{1}]", x.ToString("0"), z.ToString("0"));
                MazeCell script = mzCells[x, z].GetComponent<MazeCell>();
                script.isVisited = false;
                script.AllWallsOn();
                script.CeilingMode = MazeCell.enumCeiling.Down;
                script.TorchMode = MazeCell.enumTorch.Off;
            }
        }
    }
    private void GenerateMaze(ref LinkedCoord LINKCURR)
    {
        scriptOf(LINKCURR.coord).isVisited = true;

        Coord NeighborNEXT = RandomNeighbor(ref LINKCURR.coord);
        if (NeighborNEXT != null && NeighborNEXT.isSet == true)
        {
            scriptOf(NeighborNEXT).isVisited = true;

            // new link for NeighborNEXT
            LinkedCoord LINKNEXT = new LinkedCoord(ref NeighborNEXT);
            LINKNEXT.linkprev = LINKCURR;
            LINKNEXT.linknext = null;

            // update CURRENT
            LINKCURR.linknext = LINKNEXT;

            // repeat...
            if (cellDelaySeconds == 0.0f)
            {
                GenerateMaze(ref LINKNEXT);
            }
            else
            {
                coroutine = coroutineMazeDelay(LINKNEXT, UnityEngine.Color.green);
                green.Play();
                StartCoroutine(coroutine);
            }
        }
        else
        {
            if (LINKCURR.coord != null && anyUnvisitedCells() == true)
            {
                // backup...
                if (cellDelaySeconds == 0.0f)
                {
                    GenerateMaze(ref LINKCURR.linkprev);
                }
                else
                {
                    coroutine = coroutineMazeDelay(LINKCURR.linkprev, UnityEngine.Color.red);
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
        //  current cell can be categorized into 9 cases (t1-b9)
        //  the case determines the directions we can look in for possible neighbors

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

        // neighbor case of current cell?
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

        // random possible neighbor
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
        Coord NeighborNEXT = null;

        // possible neighbors
        Coord rightNeighbor = new Coord(CURR.X + 1, CURR.Z, enumNeighborToThe.Right);
        Coord backNeighbor = new Coord(CURR.X, CURR.Z - 1, enumNeighborToThe.Back);

        // filter for unvisited
        List<Coord> unvisited = new List<Coord>();
        if (scriptOf(rightNeighbor).isVisited == false) unvisited.Add(rightNeighbor);
        if (scriptOf(backNeighbor).isVisited == false) unvisited.Add(backNeighbor);
        if (unvisited.Count == 0) return NeighborNEXT;

        // pick neighbor from unvisited
        int rnd = Random.Range(0, unvisited.Count);
        NeighborNEXT = new Coord(unvisited[rnd]);

        // remove adjoining walls
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
    private bool anyUnvisitedCells()
    {
        // scan 2D array for any/first unvisited cell
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
    private void CenterHighCamera()
    {
        //Note: 
        //  position camera over a center mazecell (XX)
        //  rotate camera to look at XX
        //  raise camera until bottom, left mazecell (BL) is visible

        Transform BL = mzCells[0, 0].transform;
        Transform XX = mzCells[((sizeX) / 2), ((sizeZ) / 2)].transform;
        Transform TR = mzCells[sizeX - 1, sizeZ - 1].transform;

        /*  
        *  Maze grid of cells
        *  
        *              PW - portalwin
        *  [] [] [] [] TR
        *  [] [] [] [] []
        *  [] [] XX [] []
        *  [] [] [] [] []
        *  BL [] [] [] []
        *  PS - portalstart
        *  
        *  */

        // center camera, lookat XX
        float centerX = Mathf.Abs(portalwin.transform.position.x - portalstart.transform.position.x) / 2.0f;
        float centerZ = Mathf.Abs(portalwin.transform.position.z - portalstart.transform.position.z) / 2.0f;
        float shortest = centerX;
        if (centerZ < centerX)
        {
            shortest = centerZ;
        }
        overheadCam.transform.position = new Vector3(shortest, sizeX + sizeZ, shortest);
        overheadCam.transform.LookAt(new Vector3(shortest, 0.0f, shortest));

        // raise camera until BL visible (forloop is stop, incase of error and deadlock)
        for (int i = 0; i < (sizeX * sizeZ); i++)
        {
            // up one step
            overheadCam.transform.Translate(new Vector3(0.0f, 1.0f, 0.0f), Space.World);

            // check if point is visible in viewport
            Vector3 pt = overheadCam.WorldToViewportPoint(BL.transform.position);    //note: transforms point from world space to viewport space. bottom-left (0,0) to top-right (1,1)           
            bool onScreen = (pt.x > 0.1f && pt.y > 0.1f);
            if (onScreen == true)
            {
                break;
            }
        }
    }
    private void LoadAudio()
    {
        green = this.gameObject.AddComponent<AudioSource>();
        green.clip = Resources.Load<AudioClip>("green");

        red = this.gameObject.AddComponent<AudioSource>();
        red.clip = Resources.Load<AudioClip>("red");

        complete = this.gameObject.AddComponent<AudioSource>();
        complete.clip = Resources.Load<AudioClip>("complete");
    }
    IEnumerator coroutineMazeDelay(LinkedCoord LINKCURR, UnityEngine.Color color)
    {
        scriptOf(LINKCURR.coord).TravelIndicator(true, color);
        yield return new WaitForSeconds(cellDelaySeconds);
        scriptOf(LINKCURR.coord).TravelIndicator(false, color);
        GenerateMaze(ref LINKCURR);
    }
    private void RandomDropObjects()
    {
        Transform BL = mzCells[0, 0].transform;
        Transform TR = mzCells[sizeX - 1, sizeZ - 1].transform;
        for (int i = 0; i < numDrop; i++)
        {
            float posX = Random.Range(BL.position.x, TR.position.x);
            float posZ = Random.Range(BL.position.z, TR.position.z);
            GameObject obj = Instantiate(DropPrefab, new Vector3(posX, 8.0f, posZ), Quaternion.identity, DropParent.transform);
            obj.name = "barrel " + i.ToString();
            DropObjects.Add(obj);
        }
    }
}

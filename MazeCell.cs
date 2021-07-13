using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MazeCell : MonoBehaviour
{
    #region --- helper ---
    public enum enumWall
    {
        UNDEFINED,
        Left,
        Right,
        Forward,
        Back,
    }
    public enum enumCeiling
    {
        Off,
        MovingDown,
        Down,
        MovingUp,
        Up,
    }
    public enum enumTorch
    {
        Off,
        On,
    }
    #endregion

    public GameObject wallLeft = null;
    public GameObject wallRight = null;
    public GameObject wallForward = null;
    public GameObject wallBack = null;
    public GameObject Floor = null;
    public GameObject Ceiling = null;
    public float ceilingDelta = 0.5f;
    public AudioSource CeilingMove = null;
    public AudioSource CeilingStop = null;
    public Material defaultWall = null;
    public float RandomMaterialChance = 0.5f;
    private int randomSize = 100;
    private bool _isVisited = false;
    private enumCeiling _ceilingMode = enumCeiling.Down;
    private enumTorch _torchMode = enumTorch.Off;
    private Vector3 downScaleTarget = new Vector3(1.0f, 0.1f, 1.0f);
    private Vector3 upScaleTarget = new Vector3(0.8f, 0.1f, 0.8f);

    public bool isVisited
    {
        //mark a cell visited so it is taken out of the maze algorithm (and you can walk thru it)
        get
        {
            return _isVisited;
        }
        set
        {
            _isVisited = value;
            this.GetComponent<Collider>().enabled = false;
            this.GetComponent<Renderer>().enabled = false;
        }
    }
    public enumCeiling CeilingMode
    {
        get
        {
            return _ceilingMode;
        }
        set
        {
            _ceilingMode = value;
            switch (_ceilingMode)
            {
                case enumCeiling.Off:
                    Ceiling.SetActive(false);
                    break;
                case enumCeiling.MovingDown:
                    Ceiling.SetActive(true);
                    CeilingMove.Play();
                    break;
                case enumCeiling.Down:
                    Ceiling.SetActive(true);
                    CeilingMove.Stop();
                    CeilingStop.Play();
                    Ceiling.transform.position = new Vector3(Ceiling.transform.position.x, 2.0f, Ceiling.transform.position.z);
                    Ceiling.transform.localScale = downScaleTarget;
                    break;
                case enumCeiling.MovingUp:
                    Ceiling.SetActive(true);
                    CeilingMove.Play();
                    break;
                case enumCeiling.Up:
                    Ceiling.SetActive(true);
                    CeilingMove.Stop();
                    CeilingStop.Play();
                    Ceiling.transform.position = new Vector3(Ceiling.transform.position.x, 3.0f, Ceiling.transform.position.z);
                    Ceiling.transform.localScale = upScaleTarget;
                    break;
            }
        }
    }
    public enumTorch TorchMode
    {
        get
        {
            return _torchMode;
        }
        set
        {
            _torchMode = value;
            try
            {
                switch (TorchMode)
                {
                    case enumTorch.Off:
                        wallLeft.transform.Find("Torch/Fire").gameObject.SetActive(false);
                        wallRight.transform.Find("Torch/Fire").gameObject.SetActive(false);
                        wallForward.transform.Find("Torch/Fire").gameObject.SetActive(false);
                        wallBack.transform.Find("Torch/Fire").gameObject.SetActive(false);
                        break;
                    case enumTorch.On:
                        wallLeft.transform.Find("Torch/Fire").gameObject.SetActive(true);
                        wallRight.transform.Find("Torch/Fire").gameObject.SetActive(true);
                        wallForward.transform.Find("Torch/Fire").gameObject.SetActive(true);
                        wallBack.transform.Find("Torch/Fire").gameObject.SetActive(true);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log(this.name + " [EXCEPTION]=" + ex.Message);
            }
        }
    }

    private void Start()
    {
        //default wall for all 4 sides
        wallLeft.GetComponent<Renderer>().material = defaultWall;
        wallRight.GetComponent<Renderer>().material = defaultWall;
        wallForward.GetComponent<Renderer>().material = defaultWall;
        wallBack.GetComponent<Renderer>().material = defaultWall;

        wallLeft.transform.Find("Torch/Fire").gameObject.SetActive(false);
        wallRight.transform.Find("Torch/Fire").gameObject.SetActive(false);
        wallForward.transform.Find("Torch/Fire").gameObject.SetActive(false);
        wallBack.transform.Find("Torch/Fire").gameObject.SetActive(false);

        //randomize the wall materials
        RandomWallMaterial();
    }
    private void Update()
    {
        switch (_ceilingMode)
        {
            case enumCeiling.MovingDown:
                Ceiling.transform.Translate(0.0f, -ceilingDelta * Time.deltaTime, 0.0f);
                if (Ceiling.transform.position.y <= 2.0f)
                {
                    CeilingMode = enumCeiling.Down;
                }
                break;
            case enumCeiling.MovingUp:
                Ceiling.transform.Translate(0.0f, ceilingDelta * Time.deltaTime, 0.0f);
                if (Ceiling.transform.position.y >= 3.0f)
                {
                    CeilingMode = enumCeiling.Up;
                }
                break;
        }
    }
    public void AllWallsOn()
    {
        wallLeft.SetActive(true);
        wallRight.SetActive(true);
        wallForward.SetActive(true);
        wallBack.SetActive(true);
    }
    public void RemoveWall(enumWall code)
    {
        switch (code)
        {
            case enumWall.Left:
                wallLeft.SetActive(false);
                break;
            case enumWall.Right:
                wallRight.SetActive(false);
                break;
            case enumWall.Forward:
                wallForward.SetActive(false);
                break;
            case enumWall.Back:
                wallBack.SetActive(false);
                break;
        }
    }
    public void TravelIndicator(bool bOn, UnityEngine.Color color)
    {
        this.GetComponent<Renderer>().enabled = bOn;
        this.GetComponent<Renderer>().material.color = new Color(color.r, color.g, color.b, 0.2f);
    }
    public List<enumWall> MissingWalls()
    {
        List<enumWall> missing = new List<enumWall>();

        if (wallLeft.activeSelf == false)
        {
            missing.Add(enumWall.Left);
        }
        if (wallRight.activeSelf == false)
        {
            missing.Add(enumWall.Right);
        }
        if (wallForward.activeSelf == false)
        {
            missing.Add(enumWall.Forward);
        }
        if (wallBack.activeSelf == false)
        {
            missing.Add(enumWall.Back);
        }

        return missing;
    }
    private void RandomWallMaterial()
    {
        //all materials
        FileInfo[] material_files = GetResourceFiles("\\brickwalls", "opt*.mat");

        //four walls
        if (Random.Range(0, randomSize) < (randomSize * RandomMaterialChance))
        {
            int R = Random.Range(1, material_files.Length);
            Material mat = Resources.Load("brickwalls/" + Path.GetFileNameWithoutExtension(material_files[R].Name), typeof(Material)) as Material;
            wallLeft.GetComponent<Renderer>().material = mat;
        }
        if (Random.Range(0, randomSize) < (randomSize * RandomMaterialChance))
        {
            int R = Random.Range(1, material_files.Length);
            Material mat = Resources.Load("brickwalls/" + Path.GetFileNameWithoutExtension(material_files[R].Name), typeof(Material)) as Material;
            wallRight.GetComponent<Renderer>().material = mat;
        }
        if (Random.Range(0, randomSize) < (randomSize * RandomMaterialChance))
        {
            int R = Random.Range(1, material_files.Length);
            Material mat = Resources.Load("brickwalls/" + Path.GetFileNameWithoutExtension(material_files[R].Name), typeof(Material)) as Material;
            wallForward.GetComponent<Renderer>().material = mat;
        }
        if (Random.Range(0, randomSize) < (randomSize * RandomMaterialChance))
        {
            int R = Random.Range(1, material_files.Length);
            Material mat = Resources.Load("brickwalls/" + Path.GetFileNameWithoutExtension(material_files[R].Name), typeof(Material)) as Material;
            wallBack.GetComponent<Renderer>().material = mat;
        }
    }
    private FileInfo[] GetResourceFiles(string subFolder, string searchPattern)
    {
        DirectoryInfo dirInfo = new DirectoryInfo(Application.dataPath + "\\Resources" + subFolder);
        FileInfo[] files = dirInfo.GetFiles(searchPattern);
        return files;
    }
}

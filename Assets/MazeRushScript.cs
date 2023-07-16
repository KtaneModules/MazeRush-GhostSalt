using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KModkit;
using Rnd = UnityEngine.Random;

public class MazeRushScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMNeedyModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public Image[] Images;

    private Coroutine[] ButtonAnimCoroutines = new Coroutine[4];
    private int[] WallPosToRow = new[] { 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6 };
    private int[] WallPosToCol = new[] { 0, 1, 2, 0, 1, 2, 3, 0, 1, 2, 0, 1, 2, 3, 0, 1, 2, 0, 1, 2, 3, 0, 1, 2 };
    private int GoalPos, MousePos;
    private int[,] Matrix = new int[9, 9];
    private bool[][] Walls = { new bool[3], new bool[4], new bool[3], new bool[4], new bool[3], new bool[4], new bool[3] };
    private bool[,] VisitedSquares = new bool[3, 3];
    private bool CannotMove = true, Focused, TPActive, Warning;

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].OnInteract += delegate { ButtonPress(x); return false; };
        }
        foreach (var image in Images)
            image.color = new Color();
        Module.OnNeedyActivation += delegate { Activate(); };
        Module.GetComponent<KMSelectable>().OnFocus += delegate { Focused = true; };
        Module.GetComponent<KMSelectable>().OnDefocus += delegate { Focused = false; };
        Module.OnTimerExpired += delegate { Strike("The timer ran out"); };
        StartCoroutine(GoalAnim());
    }

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown(new[] { KeyCode.W, KeyCode.D, KeyCode.S, KeyCode.A }[i]) && Focused)
                Buttons[i].OnInteract();
    }

    void ButtonPress(int pos)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, Buttons[pos].transform);
        Buttons[pos].AddInteractionPunch();
        if (ButtonAnimCoroutines[pos] != null)
            StopCoroutine(ButtonAnimCoroutines[pos]);
        ButtonAnimCoroutines[pos] = StartCoroutine(ButtonAnim(pos));
        if (!CannotMove)
        {
            bool valid = false;
            var offender = new int[2];
            switch (pos)
            {
                case 0:
                    if (!Walls[(MousePos / 3) * 2][MousePos % 3])
                    {
                        valid = true;
                        MousePos -= 3;
                    }
                    else
                        offender = new[] { (MousePos / 3) * 2, MousePos % 3 };
                    break;
                case 1:
                    if (!Walls[((MousePos / 3) * 2) + 1][(MousePos % 3) + 1])
                    {
                        valid = true;
                        MousePos += 1;
                    }
                    else
                        offender = new[] { ((MousePos / 3) * 2) + 1, (MousePos % 3) + 1 };
                    break;
                case 2:
                    if (!Walls[((MousePos / 3) * 2) + 2][MousePos % 3])
                    {
                        valid = true;
                        MousePos += 3;
                    }
                    else
                        offender = new[] { ((MousePos / 3) * 2) + 2, MousePos % 3 };
                    break;
                default:
                    if (!Walls[((MousePos / 3) * 2) + 1][MousePos % 3])
                    {
                        valid = true;
                        MousePos -= 1;
                    }
                    else
                        offender = new[] { ((MousePos / 3) * 2) + 1, MousePos % 3 };
                    break;
            }
            Images[Images.Length - 2].transform.localPosition = new Vector3(((MousePos % 3) - 1f) * 0.3f, ((MousePos / 3) - 1f) * -0.3f, 0);
            if (MousePos == GoalPos)
                StartCoroutine(CompleteStage());
            if (!valid)
            {
                Warning = !Warning;
                if (Warning && Module.GetNeedyTimeRemaining() > 20f)
                {
                    if (!TPActive)
                        Module.SetNeedyTimeRemaining(Module.GetNeedyTimeRemaining() - 20f);
                    Images[Enumerable.Range(0, Images.Length - 2).Where(x => WallPosToRow[x] == offender[0] && WallPosToCol[x] == offender[1]).First()].color = new Color(1, 0, 0, 1);
                    Audio.PlaySoundAtTransform("warning", Images[Images.Length - 2].transform);
                }
                else
                    Strike("You ran into two walls");
            }
        }
    }

    void Activate()
    {
        Debug.LogFormat("[Maze Rush #{0}] Module activated.", _moduleID);
        for (int i = 0; i < Walls.Length; i++)
            for (int j = 0; j < Walls[i].Length; j++)
                Walls[i][j] = true;
        Matrix = new int[9, 9];
        VisitedSquares = new bool[3, 3];
        GenerateMaze(Rnd.Range(0, 3), Rnd.Range(0, 3));
        CannotMove = false;
        for (int i = 0; i < Images.Length - 2; i++)
            Images[i].color = new Color(1, 1, 1, 1) * (Walls[WallPosToRow[i]][WallPosToCol[i]] ? 1 : 0);
        Images[Images.Length - 2].color = new Color(1, 1, 1, 1);
        Images[Images.Length - 1].color = new Color(1, 1, 1, 0.5f);
        MousePos = GoalPos = Rnd.Range(0, 9);
        while (GoalPos == MousePos)
            GoalPos = Rnd.Range(0, 9);
        Images[Images.Length - 2].transform.localPosition = new Vector3(((MousePos % 3) - 1f) * 0.3f, ((MousePos / 3) - 1f) * -0.3f, 0);
        Images[Images.Length - 1].transform.localPosition = new Vector3(((GoalPos % 3) - 1f) * 0.3f, ((GoalPos / 3) - 1f) * -0.3f, 0);
    }

    void Strike(string message)
    {
        Module.HandleStrike();
        Module.HandlePass();
        Warning = false;
        CannotMove = true;
        foreach (var image in Images)
            image.color = new Color();
        Debug.LogFormat("[Maze Rush #{0}] Module struck: {1}!", _moduleID, message);
    }

    void GenerateMaze(int x, int y)
    {
        VisitedSquares[x, y] = true;
        List<int> directions = new List<int> { 0, 1, 2, 3 };
        directions.Shuffle();
        for (int i = 0; i < 4; i++)
        {
            switch (directions[i])
            {
                case 0:
                    if (y != 0 && VisitedSquares[x, y - 1] != true)
                    {
                        Walls[y * 2][x] = false;
                        GenerateMaze(x, y - 1);
                    }
                    break;
                case 1:
                    if (x != 2 && VisitedSquares[x + 1, y] != true)
                    {
                        Walls[(y * 2) + 1][x + 1] = false;
                        GenerateMaze(x + 1, y);
                    }
                    break;
                case 2:
                    if (y != 2 && VisitedSquares[x, y + 1] != true)
                    {
                        Walls[(y * 2) + 2][x] = false;
                        GenerateMaze(x, y + 1);
                    }
                    break;
                default:
                    if (x != 0 && VisitedSquares[x - 1, y] != true)
                    {
                        Walls[(y * 2) + 1][x] = false;
                        GenerateMaze(x - 1, y);
                    }
                    break;
            }
        }
        for (int i = 0; i < 9; i++)
        {
            if (!Walls[(i / 3) * 2][i % 3])
                Matrix[i, i - 3] = 1;
            if (!Walls[((i / 3) * 2) + 1][(i % 3) + 1])
                Matrix[i, i + 1] = 2;
            if (!Walls[((i / 3) * 2) + 2][i % 3])
                Matrix[i, i + 3] = 3;
            if (!Walls[((i / 3) * 2) + 1][i % 3])
                Matrix[i, i - 1] = 4;
        }
        for (int i = 0; i < 9; i++)
        {
            int[,] Matrix2 = new int[9, 9];
            for (int j = 0; j < 9; j++)
                for (int k = 0; k < 9; k++)
                    for (int l = 0; l < 9; l++)
                        if (Matrix2[j, l] == 0 && Matrix[k, l] != 0)
                            Matrix2[j, l] = Matrix[j, k];
            for (int j = 0; j < 9; j++)
                for (int k = 0; k < 9; k++)
                    if (Matrix[j, k] == 0)
                        Matrix[j, k] = Matrix2[j, k];
        }
    }

    private IEnumerator CompleteStage(float duration = 0.25f)
    {
        Module.SetNeedyTimeRemaining(Mathf.Min(Module.GetNeedyTimeRemaining() + (TPActive ? 20 : 5), 99));
        Warning = false;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.TitleMenuPressed, Images[Images.Length - 2].transform);
        CannotMove = true;
        var startColours = new List<Color>();
        foreach (var colour in Images.Select(x => x.color))
            startColours.Add(colour);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Images[Images.Length - 1].transform.parent.localScale = Vector3.Lerp(new Vector3(1, 1, 1), new Vector3(2, 2, 2), timer / duration);
            for (int i = 0; i < Images.Length - 2; i++)
                if (Walls[WallPosToRow[i]][WallPosToCol[i]])
                    Images[i].color = Color.Lerp(startColours[i], startColours[i] * new Color(1, 1, 1, 0), timer / duration);
            Images[Images.Length - 1].color = Color.Lerp(new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0), timer / duration);
        }
        for (int i = 0; i < Walls.Length; i++)
            for (int j = 0; j < Walls[i].Length; j++)
                Walls[i][j] = true;
        Matrix = new int[9, 9];
        VisitedSquares = new bool[3, 3];
        GenerateMaze(Rnd.Range(0, 3), Rnd.Range(0, 3));
        while (GoalPos == MousePos)
            GoalPos = Rnd.Range(0, 9);
        Images[Images.Length - 1].transform.localPosition = new Vector3(((GoalPos % 3) - 1f) * 0.3f, ((GoalPos / 3) - 1f) * -0.3f, 0);
        for (int i = 0; i < Images.Length - 2; i++)
            if (!Walls[WallPosToRow[i]][WallPosToCol[i]])
                Images[i].color = new Color();
        timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Images[Images.Length - 1].transform.parent.localScale = Vector3.Lerp(new Vector3(), new Vector3(1, 1, 1), timer / duration);
            for (int i = 0; i < Images.Length - 2; i++)
                if (Walls[WallPosToRow[i]][WallPosToCol[i]])
                    Images[i].color = Color.Lerp(new Color(1, 1, 1, 0), new Color(1, 1, 1, 1), timer / duration);
            Images[Images.Length - 1].color = Color.Lerp(new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.5f), timer / duration);
        }
        Images[Images.Length - 1].transform.parent.localScale = new Vector3(1, 1, 1);
        for (int i = 0; i < Images.Length - 2; i++)
            if (Walls[WallPosToRow[i]][WallPosToCol[i]])
                Images[i].color = new Color(1, 1, 1, 1);
        Images[Images.Length - 1].color = new Color(1, 1, 1, 0.5f);
        CannotMove = false;
    }

    private IEnumerator GoalAnim(float duration = 4f)
    {
        while (true)
        {
            float timer = 0;
            while (timer < duration)
            {
                yield return null;
                timer += Time.deltaTime;
                Images[Images.Length - 1].transform.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(0, -360, timer / duration));
            }
        }
    }

    private IEnumerator ButtonAnim(int pos, float duration = 0.075f)
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Buttons[pos].transform.localPosition = Vector3.Lerp(new Vector3(Buttons[pos].transform.localPosition.x, 0.0161f, Buttons[pos].transform.localPosition.z), new Vector3(Buttons[pos].transform.localPosition.x, 0.013f, Buttons[pos].transform.localPosition.z), timer / duration);
        }
        timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Buttons[pos].transform.localPosition = Vector3.Lerp(new Vector3(Buttons[pos].transform.localPosition.x, 0.013f, Buttons[pos].transform.localPosition.z), new Vector3(Buttons[pos].transform.localPosition.x, 0.0161f, Buttons[pos].transform.localPosition.z), timer / duration);
        }
        Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, 0.0161f, Buttons[pos].transform.localPosition.z);
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} urdl' to move up, right, down and left.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        TPActive = true;
        foreach (var character in command)
            if (!"urdl".Contains(character))
            {
                yield return "sendtochaterror Invalid command.";
                yield break;
            }
        yield return null;
        foreach (var character in command)
        {
            bool wasWarning = Warning;
            Buttons["urdl".IndexOf(character)].OnInteract();
            if (!wasWarning && Warning)
            {
                yield return "Warning! You have hit a wall — command stopped!";
                break;
            }
            float timer = 0;
            while (timer < 0.05f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }
}

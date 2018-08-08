// ↓ デスクトップへのウィンドウの設置方法の参考にさせていただきました。
// https://github.com/HatsuneMiku3939/3939LiveWallpaer

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;

public class WindowController : MonoBehaviour
{
    // Singleton
    static WindowController instance = null;

    /// <summary> このプロセスのウィンドウハンドル </summary>
    public static IntPtr HWnd { get; } = CurrentHandle();

    /// <summary> プログラム開始時のこのウィンドウの親 </summary>
    public static IntPtr HWndDefaultParent { get; private set; } = IntPtr.Zero;

    /// <summary> 現在デスクトップに配置されているか </summary>
    public static bool IsSetToDesktop { get; private set; } = false;


    /// <summary>
    /// ウィンドウをデスクトップに設置した後、他のウィンドウを触ると操作を受け付けなくなる。
    /// よって、この時間(秒)経過後にウィンドウは自動的に初期状態に戻る。
    /// アプリとして成立させるためには、タスクトレイに常駐するアプリが別途必要になりそう。
    /// </summary>
    [SerializeField,Range(1,30)]
    public int timeResumeIn = 5;

    private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;

            if (HWnd != IntPtr.Zero)
            {
                HWndDefaultParent = W32.GetParent(HWnd);

                var str = new System.Text.StringBuilder(0x1000);
                W32.GetWindowText(HWnd, str,str.Capacity);
                Debug.Log($"Found window handle. (title = \"{str}\")");
            }
            else
                Debug.LogError("Window handle couldn't found!!!");
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    // Use this for initialization
    void Start () {

        stopwatch.Start();

        SetToDesktop(true);
    }
	
	// Update is called once per frame
	void Update ()
    {
        GameObject.Find("DebugText").GetComponent<UnityEngine.UI.Text>().text
            = $"Wallpaper state is \"{IsSetToDesktop}\"\n"
             + $"(return in {Mathf.Max(timeResumeIn - stopwatch.ElapsedMilliseconds * 0.001f, 0):f1} second or by press B)";

        if (Input.GetKeyDown(KeyCode.B) || stopwatch.ElapsedMilliseconds > 1000 * timeResumeIn)
        {
            SetToDesktop(false);
        }
    }

    private void OnDestroy()
    {
        SetToDesktop(false);
    }

    /// <summary>
    /// このウィンドウのParentを、デスクトップ、もしくは初期値に設定する。
    /// </summary>
    /// <param name="bDesktop">trueならデスクトップに配置</param>
    private void SetToDesktop(bool bDesktop)
    {

        if (HWnd == IntPtr.Zero)
            return;

        if(bDesktop && !IsSetToDesktop)
        {
            IntPtr workerw = FindWorkerW();
            if (workerw != IntPtr.Zero)
            {
                IsSetToDesktop = true;
                Debug.Log("Set parent to WorkerW");
                W32.SetParent(HWnd, workerw);
            }
        }
        else if(!bDesktop && IsSetToDesktop)
        {
            IsSetToDesktop = false;
            Debug.Log("Set parent to default");
            W32.SetParent(HWnd, HWndDefaultParent);
        }
    }

    /// <summary>
    /// デスクトップに設置するための親ウィンドウ「WorkerW」を探し、ハンドルを返す。
    /// 参考サイト様は上記
    /// </summary>
    private static IntPtr FindWorkerW()
    {

        // Fetch the Progman window
        IntPtr progman = W32.FindWindow("Progman", null);

        IntPtr result = IntPtr.Zero;

        // Send 0x052C to Progman. This message directs Progman to spawn a
        // WorkerW behind the desktop icons. If it is already there, nothing
        // happens.
        W32.SendMessageTimeout(progman,
                               0x052C,
                               new IntPtr(0),
                               IntPtr.Zero,
                               W32.SendMessageTimeoutFlags.SMTO_NORMAL,
                               1000,
                               out result);

        IntPtr workerw = IntPtr.Zero;
        W32.EnumWindows(new W32.EnumWindowsProc((tophandle, topparamhandle) =>
        {
            IntPtr p = W32.FindWindowEx(tophandle,
                                        IntPtr.Zero,
                                        "SHELLDLL_DefView",
                                        IntPtr.Zero);

            if (p != IntPtr.Zero)
            {
                // Gets the WorkerW Window after the current one.
                workerw = W32.FindWindowEx(IntPtr.Zero,
                                           tophandle,
                                           "WorkerW",
                                           IntPtr.Zero);
            }

            return true;
        }), IntPtr.Zero);

        return workerw;
    }


    /// <summary>
    /// プロセスIDをもとに、このプロセスのウインドウハンドルを取得する。
    /// GetCurrentProcess().MainWindow は Unity では null になる模様。
    /// </summary>
    private static IntPtr CurrentHandle()
    {
        int idCurrent = System.Diagnostics.Process.GetCurrentProcess().Id;
        IntPtr hWndCurrent = IntPtr.Zero;


        // 可視状態のウィンドウを列挙し、プロセスIDが一致するもののハンドルを返す。
        W32.EnumWindows((IntPtr hWnd, IntPtr lParam) =>
        {
            if (W32.IsWindowVisible(hWnd))
            {
                long id = 0;
                W32.GetWindowThreadProcessId(hWnd, out id);
                if (id == idCurrent)
                    hWndCurrent = hWnd;
            }
            return true;
        }, IntPtr.Zero);

        return hWndCurrent;
    }
}

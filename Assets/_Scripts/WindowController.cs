/*
MIT License

Copyright (c) 2017 Ben Otter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. 
*/

using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using UnityEngine;

public class WindowController : MonoBehaviour
{
    public bool hasTrayIcon = true;
    public Texture2D trayIconTex;

    private const int GWL_EXSTYLE = -0x14;
    private const int WS_EX_TOOLWINDOW = 0x0080;
    private const int SWP_HIDEWINDOW = 0x0080;

    private IntPtr winHandle;

    private TrayForm trayForm;


#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

    [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)] static extern IntPtr FindWindowByCaption(IntPtr zeroOnly, string lpWindowName);
    [DllImport("user32.dll", EntryPoint = "SetWindowPos")] private static extern bool SetWindowPos(IntPtr hwnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", SetLastError = true)] static extern int GetWindowLong(IntPtr hWnd, int nIndex);

#else

    static IntPtr FindWindowByCaption(IntPtr zeroOnly, string lpWindowName) { return IntPtr.Zero; }
    static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong) { return 0; }
    static int GetWindowLong(IntPtr hWnd, int nIndex) { return 0; }
    private static bool SetWindowPos(IntPtr hwnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags) { return true; }
    private static bool ShowWindow(IntPtr hWnd, uint nCmdShow) { return true; }

#endif
    void Start()
    {
        winHandle = FindWindowByCaption(IntPtr.Zero, UnityEngine.Application.productName);

        if (hasTrayIcon)
            CreateTray();

        HideUnityWindow();
        ShowTrayIcon();
    }

    void OnEnable()
    {
        if (hasTrayIcon)
            CreateTray();
    }

    void OnDestroy()
    {
        DestroyTray();
    }

    void OnApplicationQuit()
    {
        DestroyTray();
    }

    void OnDisable()
    {
        DestroyTray();
    }

    public void CreateTray()
    {
#if !UNITY_EDITOR
        if(trayForm == null)
        {
            trayForm = new TrayForm(trayIconTex); //CreateIcon(trayIconTex));

            trayForm.onExitCallback += OnExit;
        }
#endif
    }

    public void DestroyTray()
    {
        if (trayForm != null)
        {
            trayForm.Dispose();
            trayForm = null;
        }
    }

    public void OnExit()
    {
        UnityEngine.Application.Quit();
    }

    public void ShowTrayIcon()
    {
        if (trayForm != null)
            trayForm.ShowTray();
    }

    public bool HideTaskbarIcon()
    {
        bool res = false;

        res = ShowWindow(winHandle, (uint)0);
        SetWindowLong(winHandle, GWL_EXSTYLE, GetWindowLong(winHandle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
        res = ShowWindow(winHandle, (uint)5);

        return res;
    }

    public bool MinimizeUnityWindow()
    {
        bool res = ShowWindow(winHandle, (uint)6);
        return res;
    }

    public bool HideUnityWindow()
    {
        bool res = false;

        res = MinimizeUnityWindow();
        res = HideTaskbarIcon();

        return res;
    }
}

public class TrayForm : System.Windows.Forms.Form
{
    public delegate void OnExitDel();
    public delegate void OnShowWindowDel();

    public OnExitDel onExitCallback;
    public OnShowWindowDel onShowWindow;

    private System.Windows.Forms.ContextMenuStrip trayMenu;
    private NotifyIcon trayIcon;

    public TrayForm(Texture2D tex = null)
    {
        trayMenu = new System.Windows.Forms.ContextMenuStrip();

        trayMenu.Items.Add("Exit", CreateBitmap(Texture2D.whiteTexture), OnExit);

        trayIcon = new NotifyIcon();
        trayIcon.Text = "Steameeter";

        trayIcon.ContextMenuStrip = trayMenu;

        if (tex == null)
            trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);
        else
            trayIcon.Icon = Icon.FromHandle(CreateBitmap(tex).GetHicon());
    }

    public Bitmap CreateBitmap(Texture2D tex)
    {
        MemoryStream memS = new MemoryStream(tex.EncodeToPNG());
        memS.Seek(0, System.IO.SeekOrigin.Begin);

        Bitmap bitmap = new Bitmap(memS);

        return bitmap;
    }

    protected override void OnLoad(EventArgs e)
    {
        Visible = false;
        ShowInTaskbar = false;

        base.OnLoad(e);
    }

    ~TrayForm()
    {
        Dispose(true);
    }

    public void ShowTray()
    {
        trayIcon.Visible = true;
    }

    public void HideTray()
    {
        trayIcon.Visible = false;
    }

    protected void OnExit(object sender, EventArgs e)
    {
        onExitCallback.Invoke();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            trayIcon.Dispose();

        base.Dispose(disposing);
    }
}
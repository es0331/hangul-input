using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

static class Program
{
    const string Eng = "rRseEfaqQtTdwWczxvgkoiOjpuPhynbml";
    const string Kor = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎㅏㅐㅑㅒㅓㅔㅕㅖㅗㅛㅜㅠㅡㅣ";
    const string Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    const string Jung = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
    const string Jong = "ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";
    const int MaxKeys = 40;

    delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    static HookProc keyboardProc = KeyboardHook;
    static HookProc mouseProc = MouseHook;
    static IntPtr keyboardHandle = IntPtr.Zero;
    static IntPtr mouseHandle = IntPtr.Zero;
    static NotifyIcon tray;
    static Icon onIcon;
    static Icon offIcon;
    static bool hangulOn = false;
    static bool toggleHeld = false;
    static StringBuilder keys = new StringBuilder();
    static string emitted = "";

    [STAThread]
    static void Main()
    {
        onIcon = MakeIcon(Color.FromArgb(30, 100, 220), "KO");
        offIcon = MakeIcon(Color.FromArgb(90, 90, 90), "EN");

        tray = new NotifyIcon();
        ContextMenu menu = new ContextMenu();
        menu.MenuItems.Add("Exit", delegate(object s, EventArgs e)
        {
            tray.Visible = false;
            Application.Exit();
        });
        tray.ContextMenu = menu;
        tray.MouseUp += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Toggle();
            }
        };
        tray.Visible = true;
        Refresh();

        using (Process p = Process.GetCurrentProcess())
        using (ProcessModule m = p.MainModule)
        {
            IntPtr module = GetModuleHandle(m.ModuleName);
            keyboardHandle = SetWindowsHookEx(13, keyboardProc, module, 0);
            mouseHandle = SetWindowsHookEx(14, mouseProc, module, 0);
        }

        if (keyboardHandle == IntPtr.Zero)
        {
            tray.Visible = false;
            MessageBox.Show("Keyboard hook failed.");
            return;
        }

        Application.Run();
        tray.Visible = false;

        UnhookWindowsHookEx(keyboardHandle);
        if (mouseHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(mouseHandle);
        }
    }

    public static void Toggle()
    {
        hangulOn = !hangulOn;
        ResetState();
        Refresh();
    }

    static void Refresh()
    {
        tray.Icon = hangulOn ? onIcon : offIcon;
        tray.Text = hangulOn ? "Hangul input: ON" : "Hangul input: OFF";
    }

    static Icon MakeIcon(Color back, string text)
    {
        using (Bitmap bmp = new Bitmap(16, 16))
        using (Graphics g = Graphics.FromImage(bmp))
        using (Font f = new Font("Segoe UI", 7f, FontStyle.Bold))
        using (Brush b = new SolidBrush(back))
        {
            g.FillRectangle(b, 0, 0, 16, 16);
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            g.DrawString(text, f, Brushes.White, -1f, 2f);
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    static void ResetState()
    {
        keys.Length = 0;
        emitted = "";
    }

    static IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == 0x201 || msg == 0x204 || msg == 0x207)
            {
                ResetState();
            }
        }
        return CallNextHookEx(mouseHandle, code, wParam, lParam);
    }

    static IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
            if ((k.flags & 0x10) == 0)
            {
                int msg = wParam.ToInt32();
                bool down = msg == 0x100 || msg == 0x104;
                bool up = msg == 0x101 || msg == 0x105;

                if (k.vkCode == 0xA5 || k.vkCode == 0x15)
                {
                    if (down && !toggleHeld)
                    {
                        toggleHeld = true;
                        Toggle();
                    }
                    if (up)
                    {
                        toggleHeld = false;
                    }
                    return (IntPtr)1;
                }

                if (hangulOn && down)
                {
                    if (HandleKey((int)k.vkCode))
                    {
                        return (IntPtr)1;
                    }
                }
            }
        }
        return CallNextHookEx(keyboardHandle, code, wParam, lParam);
    }

    static bool HandleKey(int vk)
    {
        if (vk == 0x10 || vk == 0xA0 || vk == 0xA1 || vk == 0x11 || vk == 0xA2 || vk == 0xA3 ||
            vk == 0x12 || vk == 0xA4 || vk == 0x14)
        {
            return false;
        }

        bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool win = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
        if (ctrl || alt || win)
        {
            ResetState();
            return false;
        }

        if (vk >= 0x41 && vk <= 0x5A)
        {
            char c = (char)('a' + (vk - 0x41));
            bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
            if (shift && "qwertop".IndexOf(c) >= 0)
            {
                c = char.ToUpper(c);
            }
            if (keys.Length >= MaxKeys)
            {
                ResetState();
            }
            keys.Append(c);
            Update();
            return true;
        }

        if (vk == 0x08)
        {
            if (keys.Length > 0)
            {
                keys.Length = keys.Length - 1;
                Update();
                return true;
            }
            return false;
        }

        ResetState();
        return false;
    }

    static void Update()
    {
        string next = ToHangul(keys.ToString());
        int common = 0;
        while (common < emitted.Length && common < next.Length && emitted[common] == next[common])
        {
            common++;
        }
        int backs = emitted.Length - common;
        if (backs > 0)
        {
            SendBackspaces(backs);
        }
        if (common < next.Length)
        {
            SendText(next.Substring(common));
        }
        emitted = next;
    }

    static void SendBackspaces(int count)
    {
        INPUT[] inputs = new INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] = MakeKey(0x08, 0, 0);
            inputs[i * 2 + 1] = MakeKey(0x08, 0, 0x2);
        }
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    static void SendText(string text)
    {
        INPUT[] inputs = new INPUT[text.Length * 2];
        for (int i = 0; i < text.Length; i++)
        {
            inputs[i * 2] = MakeKey(0, text[i], 0x4);
            inputs[i * 2 + 1] = MakeKey(0, text[i], 0x4 | 0x2);
        }
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    static INPUT MakeKey(ushort vk, ushort scan, uint flags)
    {
        INPUT input = new INPUT();
        input.type = 1;
        input.u.ki.wVk = vk;
        input.u.ki.wScan = scan;
        input.u.ki.dwFlags = flags;
        input.u.ki.time = 0;
        input.u.ki.dwExtraInfo = IntPtr.Zero;
        return input;
    }

    static char MakeHangul(int cho, int jung, int jong)
    {
        return (char)(0xAC00 + cho * 21 * 28 + jung * 28 + jong + 1);
    }

    static int JongCombine(int jong, int p)
    {
        if (jong == 0 && p == 9) return 2;
        if (jong == 3 && p == 12) return 4;
        if (jong == 3 && p == 18) return 5;
        if (jong == 7 && p == 0) return 8;
        if (jong == 7 && p == 6) return 9;
        if (jong == 7 && p == 7) return 10;
        if (jong == 7 && p == 9) return 11;
        if (jong == 7 && p == 16) return 12;
        if (jong == 7 && p == 17) return 13;
        if (jong == 7 && p == 18) return 14;
        if (jong == 16 && p == 9) return 17;
        return -1;
    }

    static int ChoCombine(int cho, int p)
    {
        if (cho == 0 && p == 9) return 2;
        if (cho == 2 && p == 12) return 4;
        if (cho == 2 && p == 18) return 5;
        if (cho == 5 && p == 0) return 8;
        if (cho == 5 && p == 6) return 9;
        if (cho == 5 && p == 7) return 10;
        if (cho == 5 && p == 9) return 11;
        if (cho == 5 && p == 16) return 12;
        if (cho == 5 && p == 17) return 13;
        if (cho == 5 && p == 18) return 14;
        if (cho == 7 && p == 9) return 17;
        return -1;
    }

    static int JungCombine(int jung, int p)
    {
        if (jung == 8 && p == 19) return 9;
        if (jung == 8 && p == 20) return 10;
        if (jung == 8 && p == 32) return 11;
        if (jung == 13 && p == 23) return 14;
        if (jung == 13 && p == 24) return 15;
        if (jung == 13 && p == 32) return 16;
        if (jung == 18 && p == 32) return 19;
        return -1;
    }

    static string ToHangul(string src)
    {
        StringBuilder res = new StringBuilder();
        int nCho = -1, nJung = -1, nJong = -1;

        for (int i = 0; i < src.Length; i++)
        {
            int p = Eng.IndexOf(src[i]);
            if (p == -1)
            {
                continue;
            }

            if (p < 19)
            {
                if (nJung != -1)
                {
                    if (nCho == -1)
                    {
                        res.Append(Jung[nJung]);
                        nJung = -1;
                        nCho = Cho.IndexOf(Kor[p]);
                    }
                    else if (nJong == -1)
                    {
                        nJong = Jong.IndexOf(Kor[p]);
                        if (nJong == -1)
                        {
                            res.Append(MakeHangul(nCho, nJung, nJong));
                            nCho = Cho.IndexOf(Kor[p]);
                            nJung = -1;
                        }
                    }
                    else
                    {
                        int combined = JongCombine(nJong, p);
                        if (combined != -1)
                        {
                            nJong = combined;
                        }
                        else
                        {
                            res.Append(MakeHangul(nCho, nJung, nJong));
                            nCho = Cho.IndexOf(Kor[p]);
                            nJung = -1;
                            nJong = -1;
                        }
                    }
                }
                else
                {
                    if (nCho == -1)
                    {
                        if (nJong != -1)
                        {
                            res.Append(Jong[nJong]);
                            nJong = -1;
                        }
                        nCho = Cho.IndexOf(Kor[p]);
                    }
                    else
                    {
                        int combined = ChoCombine(nCho, p);
                        if (combined != -1)
                        {
                            nCho = -1;
                            nJong = combined;
                        }
                        else
                        {
                            res.Append(Cho[nCho]);
                            nCho = Cho.IndexOf(Kor[p]);
                        }
                    }
                }
            }
            else
            {
                if (nJong != -1)
                {
                    int newCho;
                    if (nJong == 2) { nJong = 0; newCho = 9; }
                    else if (nJong == 4) { nJong = 3; newCho = 12; }
                    else if (nJong == 5) { nJong = 3; newCho = 18; }
                    else if (nJong == 8) { nJong = 7; newCho = 0; }
                    else if (nJong == 9) { nJong = 7; newCho = 6; }
                    else if (nJong == 10) { nJong = 7; newCho = 7; }
                    else if (nJong == 11) { nJong = 7; newCho = 9; }
                    else if (nJong == 12) { nJong = 7; newCho = 16; }
                    else if (nJong == 13) { nJong = 7; newCho = 17; }
                    else if (nJong == 14) { nJong = 7; newCho = 18; }
                    else if (nJong == 17) { nJong = 16; newCho = 9; }
                    else
                    {
                        newCho = Cho.IndexOf(Jong[nJong]);
                        nJong = -1;
                    }

                    if (nCho != -1)
                    {
                        res.Append(MakeHangul(nCho, nJung, nJong));
                    }
                    else if (nJong != -1)
                    {
                        res.Append(Jong[nJong]);
                    }

                    nCho = newCho;
                    nJung = -1;
                    nJong = -1;
                }

                if (nJung == -1)
                {
                    nJung = Jung.IndexOf(Kor[p]);
                }
                else
                {
                    int combined = JungCombine(nJung, p);
                    if (combined != -1)
                    {
                        nJung = combined;
                    }
                    else
                    {
                        if (nCho != -1)
                        {
                            res.Append(MakeHangul(nCho, nJung, nJong));
                            nCho = -1;
                        }
                        else
                        {
                            res.Append(Jung[nJung]);
                        }
                        nJung = -1;
                        res.Append(Kor[p]);
                    }
                }
            }
        }

        if (nCho != -1)
        {
            if (nJung != -1)
            {
                res.Append(MakeHangul(nCho, nJung, nJong));
            }
            else
            {
                res.Append(Cho[nCho]);
            }
        }
        else
        {
            if (nJung != -1)
            {
                res.Append(Jung[nJung]);
            }
            else if (nJong != -1)
            {
                res.Append(Jong[nJong]);
            }
        }

        return res.ToString();
    }
}

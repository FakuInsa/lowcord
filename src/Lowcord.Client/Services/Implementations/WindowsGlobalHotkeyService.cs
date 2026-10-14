using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Lowcord.Client.Services.Contracts;

namespace Lowcord.Client.Services.Implementations;

public class WindowsGlobalHotkeyService : IHotkeyService
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelKeyboardProc? _keyboardProc;
    private IntPtr _hookId = IntPtr.Zero;

    private bool _isTargetKeyDown = false;

    public uint TargetVk { get; private set; } = (uint)Keys.M;
    public string HotkeyCode { get; private set; } = "KeyM";

    public event Action<bool>? OnKeyToggled;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;
        _keyboardProc = HookCallback;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule?.ModuleName), 0);
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _keyboardProc = null;
        }
    }

    public void SetHotkey(string code, uint vk = 0)
    {
        HotkeyCode = string.IsNullOrWhiteSpace(code) ? "KeyM" : code;
        if (vk > 0)
        {
            TargetVk = vk;
            return;
        }

        var normalized = HotkeyCode.ToLowerInvariant();
        TargetVk = normalized switch
        {
            "space" => (uint)Keys.Space,
            "capslock" => (uint)Keys.CapsLock,
            "tab" => (uint)Keys.Tab,
            "backquote" => 0xDC, // Tecla al lado del 1 en layout español/latinoamericano (| / \)
            "oem5" or "oem_5" => 0xDC,
            "oem3" or "oem_3" => 0xC0,
            "minus" => 0xBD,
            "equal" => 0xBB,
            "bracketleft" => 0xDB,
            "bracketright" => 0xDD,
            "backslash" => 0xDC,
            "semicolon" => 0xBA,
            "quote" => 0xDE,
            "comma" => 0xBC,
            "period" => 0xBE,
            "slash" => 0xBF,
            _ => ParseStandardKey(normalized)
        };
    }

    private uint ParseStandardKey(string code)
    {
        if (code.StartsWith("key") && code.Length == 4)
        {
            char letter = char.ToUpperInvariant(code[3]);
            if (letter >= 'A' && letter <= 'Z') return (uint)letter;
        }
        if (code.StartsWith("digit") && code.Length == 6)
        {
            char num = code[5];
            if (num >= '0' && num <= '9') return (uint)num;
        }
        if (code.StartsWith("numpad") && code.Length == 7)
        {
            char num = code[6];
            if (num >= '0' && num <= '9') return (uint)(Keys.NumPad0 + (num - '0'));
        }
        return (uint)Keys.M;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            int vkCode = Marshal.ReadInt32(lParam);

            // Coincidencia exacta o equivalencia para la tecla al lado del 1 (0xDC / 0xC0 / 0xDF)
            bool isMatch = ((uint)vkCode == TargetVk);
            if (!isMatch && (TargetVk == 0xDC || TargetVk == 0xC0 || TargetVk == 0xDF))
            {
                isMatch = (vkCode == 0xDC || vkCode == 0xC0 || vkCode == 0xDF);
            }

            if (isMatch)
            {
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (!_isTargetKeyDown)
                    {
                        _isTargetKeyDown = true;
                        OnKeyToggled?.Invoke(true);
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    _isTargetKeyDown = false;
                    OnKeyToggled?.Invoke(false);
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }
}

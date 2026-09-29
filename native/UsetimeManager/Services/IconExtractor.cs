using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UsetimeManager.Services;

/// <summary>从 exe 提取图标并缓存为 WPF ImageSource</summary>
public static class IconExtractor
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static ImageSource? GetIcon(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return null;

        lock (Gate)
        {
            if (Cache.TryGetValue(exePath, out var cached))
                return cached;
        }

        ImageSource? result = null;
        try
        {
            // SHGetFileInfo 更稳；失败再用 ExtractAssociatedIcon
            result = FromShellIcon(exePath) ?? FromExtractAssociated(exePath);
        }
        catch
        {
            result = null;
        }

        lock (Gate)
        {
            Cache[exePath] = result;
        }
        return result;
    }

    private static ImageSource? FromShellIcon(string exePath)
    {
        var shinfo = new SHFILEINFO();
        var flags = SHGFI_ICON | SHGFI_LARGEICON;
        var cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf<SHFILEINFO>();
        var h = SHGetFileInfo(exePath, 0, ref shinfo, cb, flags);
        if (h == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
            return null;
        try
        {
            var src = Imaging.CreateBitmapSourceFromHIcon(
                shinfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            src.Freeze();
            return src;
        }
        finally
        {
            DestroyIcon(shinfo.hIcon);
        }
    }

    private static ImageSource? FromExtractAssociated(string exePath)
    {
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
        if (icon == null) return null;
        var src = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(32, 32));
        src.Freeze();
        return src;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi,
        uint cbFileInfo, uint uFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}

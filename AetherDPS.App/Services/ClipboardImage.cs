using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace AetherDPS.App.Services;

/// <summary>
/// Bild in die Windows-Zwischenablage (CF_DIB + „PNG“), damit es z. B. in Discord mit
/// STRG+V eingefügt werden kann. Avalonia selbst kann nur Text in die Zwischenablage legen.
/// </summary>
public static partial class ClipboardImage
{
    private const uint CfDib = 8, GmemMoveable = 0x0002;

    /// <summary>Control (z. B. die Lauf-Details) als Bild rendern und kopieren.</summary>
    public static bool CopyControl(Control control, double scale = 1.5)
    {
        var size = new PixelSize(Math.Max(1, (int)(control.Bounds.Width * scale)), Math.Max(1, (int)(control.Bounds.Height * scale)));
        using var rtb = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        rtb.Render(control);
        using var png = new MemoryStream();
        rtb.Save(png);
        return SetPng(png.ToArray());
    }

    public static bool SetPng(byte[] png)
    {
        using var image = new System.Drawing.Bitmap(new MemoryStream(png));
        byte[] dib = ToDib(image);

        if (!OpenClipboard(IntPtr.Zero)) return false;
        try
        {
            EmptyClipboard();
            SetGlobal(CfDib, dib);
            SetGlobal(RegisterClipboardFormatW("PNG"), png);
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>BITMAPINFOHEADER + 32-bit-Pixel, unten beginnend (klassisches DIB-Format).</summary>
    private static byte[] ToDib(System.Drawing.Bitmap image)
    {
        int w = image.Width, h = image.Height, stride = w * 4;
        var data = image.LockBits(new System.Drawing.Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var result = new byte[40 + stride * h];
            using var header = new BinaryWriter(new MemoryStream(result));
            header.Write(40); header.Write(w); header.Write(h); header.Write((short)1); header.Write((short)32);
            header.Write(0); header.Write(stride * h); header.Write(2835); header.Write(2835); header.Write(0); header.Write(0);
            for (int y = 0; y < h; y++)
                Marshal.Copy(data.Scan0 + (h - 1 - y) * data.Stride, result, 40 + y * stride, stride);
            return result;
        }
        finally
        {
            image.UnlockBits(data);
        }
    }

    private static void SetGlobal(uint format, byte[] bytes)
    {
        var handle = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
        var target = GlobalLock(handle);
        Marshal.Copy(bytes, 0, target, bytes.Length);
        GlobalUnlock(handle);
        SetClipboardData(format, handle);   // Windows übernimmt den Speicher
    }

    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool OpenClipboard(IntPtr owner);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool CloseClipboard();
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool EmptyClipboard();
    [LibraryImport("user32.dll")] private static partial IntPtr SetClipboardData(uint format, IntPtr data);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)] private static partial uint RegisterClipboardFormatW(string name);
    [LibraryImport("kernel32.dll")] private static partial IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [LibraryImport("kernel32.dll")] private static partial IntPtr GlobalLock(IntPtr mem);
    [LibraryImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GlobalUnlock(IntPtr mem);
}

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;

namespace DropGoLine {
    public static class DragHelper {
        
        [ComImport]
        [Guid("DE5BF786-477A-11D2-839D-00C04FD918D0")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDragSourceHelper {
            void InitializeFromBitmap(
                [In, MarshalAs(UnmanagedType.Struct)] ref SHDRAGIMAGE pshdi,
                [In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject pDataObject);
            
            void InitializeFromWindow(
                [In] IntPtr hwnd,
                [In] ref Point ppt,
                [In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject pDataObject);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SHDRAGIMAGE {
            public SIZE sizeDragImage;
            public Point ptOffset;
            public IntPtr hbmpDragImage;
            public int crColorKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE {
            public int cx;
            public int cy;
            public SIZE(int w, int h) { cx = w; cy = h; }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const string CLSID_DragDropHelper = "4657278A-411B-11d2-839A-00C04FD918D0";

        public static void SetDragImage(System.Windows.Forms.IDataObject data, Bitmap image, Point cursorOffset) {
            try {
                // Get the COM IDragSourceHelper
                Type? type = Type.GetTypeFromCLSID(new Guid(CLSID_DragDropHelper));
                if (type == null) return;

                object? helperObj = Activator.CreateInstance(type);
                if (helperObj is IDragSourceHelper helper) {
                    
                    SHDRAGIMAGE shdi = new SHDRAGIMAGE();
                    shdi.sizeDragImage = new SIZE(image.Width, image.Height);
                    shdi.ptOffset = cursorOffset;
                    shdi.hbmpDragImage = image.GetHbitmap();
                    shdi.crColorKey = ColorTranslator.ToWin32(Color.Magenta); // Transparent Key?

                    // We wrap the System.Windows.Forms.IDataObject into a ComTypes.IDataObject
                    // Fortunately, Windows Forms DataObject implements ComTypes.IDataObject
                    if (data is System.Runtime.InteropServices.ComTypes.IDataObject comData) {
                        helper.InitializeFromBitmap(ref shdi, comData);
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to set drag image: {ex.Message}");
            }
        }
    }
}

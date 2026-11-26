using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using StickyNote.Models;
using Forms = System.Windows.Forms;

namespace StickyNote.Services
{
    public static class ScreenService
    {
        // RECT: choose nearest visible work area.
        public static Rect Clamp(Rect note, Rect[] workAreas)
        {
            if (workAreas == null || workAreas.Length == 0) return note;
            var target = workAreas.OrderByDescending(r => { var i = Rect.Intersect(note, r); return i.IsEmpty ? 0 : i.Width * i.Height; })
                .ThenBy(r => Math.Pow(r.Left - note.Left, 2) + Math.Pow(r.Top - note.Top, 2)).First();
            double width = Math.Min(note.Width, target.Width), height = Math.Min(note.Height, target.Height);
            return new Rect(Math.Max(target.Left, Math.Min(note.Left, target.Right - width)), Math.Max(target.Top, Math.Min(note.Top, target.Bottom - height)), width, height);
        }
        // DPI: clamp to current work area.
        public static void Reposition(Window window, Note note)
        {
            var source = PresentationSource.FromVisual(window);
            var matrix = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            var areas = Forms.Screen.AllScreens.Select(s =>
            {
                var area = s.WorkingArea;
                return new Rect(matrix.Transform(new Point(area.Left, area.Top)), matrix.Transform(new Point(area.Right, area.Bottom)));
            }).ToArray();
            var rect = Clamp(new Rect(note.x, note.y, note.width, note.height), areas);
            window.Left = rect.Left; window.Top = rect.Top; window.Width = rect.Width; window.Height = rect.Height;
            var center = new System.Drawing.Point((int)(rect.X / matrix.M11 + rect.Width / matrix.M11 / 2), (int)(rect.Y / matrix.M22 + rect.Height / matrix.M22 / 2));
            note.monitorDeviceName = Forms.Screen.FromPoint(center).DeviceName;
        }
        // MON: record current display.
        public static void UpdateMonitor(Window window, Note note)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero) note.monitorDeviceName = Forms.Screen.FromHandle(handle).DeviceName;
        }
    }
}

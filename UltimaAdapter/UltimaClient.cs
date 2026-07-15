using System;

namespace Ultima
{
    public static class Client
    {
        public static bool Running => false;

        public static bool IsIris2 { get; set; }

        public static bool IsAlternativeClient { get; set; }

        public static void Calibrate()
        {
        }

        public static void Calibrate(int x, int y, int z)
        {
        }

        public static void Calibrate(CalibrationInfo[] info)
        {
        }

        public static bool FindLocation(ref int x, ref int y, ref int z, ref int facet)
        {
            return false;
        }

        public static bool BringToTop()
        {
            return false;
        }

        public static bool SendText(string text)
        {
            return false;
        }

        public static bool SendText(string format, params object[] args)
        {
            return false;
        }

        public static string GetWindowText(IntPtr hWnd)
        {
            return string.Empty;
        }
    }
}
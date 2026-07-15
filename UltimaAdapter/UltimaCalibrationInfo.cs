namespace Ultima
{
    public class CalibrationInfo
    {
        public byte[] Mask { get; }
        public byte[] Vals { get; }
        public byte[] DetX { get; }
        public byte[] DetY { get; }
        public byte[] DetZ { get; }
        public byte[] DetF { get; }

        public CalibrationInfo(byte[] mask, byte[] vals, byte[] detx, byte[] dety, byte[] detz, byte[] detf)
        {
            Mask = mask;
            Vals = vals;
            DetX = detx;
            DetY = dety;
            DetZ = detz;
            DetF = detf;
        }

        public static CalibrationInfo[] Get()
        {
            return new CalibrationInfo[0];
        }
    }
}
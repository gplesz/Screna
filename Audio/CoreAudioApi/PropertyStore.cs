using System;
using System.Runtime.InteropServices;

namespace Screna.Audio
{
    [StructLayout(LayoutKind.Sequential)]
    struct PropString
    {
        short vt, wReserved1, wReserved2, wReserved3;
        IntPtr pointerValue;

        public string Value => Marshal.PtrToStringUni(pointerValue);
    }

    struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;

        public bool EqualTo(PropertyKey PKey) => PKey.FormatId == FormatId && PKey.PropertyId == PropertyId;
    }
}


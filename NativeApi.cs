using System;
using System.Runtime.InteropServices;

namespace DisplayManager;

#region Enums and Flags

[Flags]
public enum QueryDisplayConfigFlags : uint
{
    QDC_ALL_PATHS = 0x00000001,
    QDC_ONLY_ACTIVE_PATHS = 0x00000002,
    QDC_DATABASE_CURRENT = 0x00000004,
    QDC_VIRTUAL_MODE_AWARE = 0x00000010,
    QDC_INCLUDE_HMD = 0x00000020,
}

[Flags]
public enum SetDisplayConfigFlags : uint
{
    SDC_TOPOLOGY_INTERNAL = 0x00000001,
    SDC_TOPOLOGY_CLONE = 0x00000002,
    SDC_TOPOLOGY_EXTEND = 0x00000004,
    SDC_TOPOLOGY_EXTERNAL = 0x00000008,
    SDC_APPLY = 0x00000080,
    SDC_NO_OPTIMIZATION = 0x00000100,
    SDC_SAVE_TO_DATABASE = 0x00000200,
    SDC_ALLOW_CHANGES = 0x00000400,
    SDC_PATH_PERSIST_IF_REQUIRED = 0x00000800,
    SDC_FORCE_MODE_ENUMERATION = 0x00001000,
    SDC_ALLOW_PATH_ORDER_CHANGES = 0x00002000,
    SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020,
    SDC_VALIDATE = 0x00000040,
    SDC_USE_DATABASE_CURRENT = SDC_TOPOLOGY_INTERNAL | SDC_TOPOLOGY_CLONE | SDC_TOPOLOGY_EXTEND | SDC_TOPOLOGY_EXTERNAL,
}

public enum DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY : uint
{
    OTHER = 0xFFFFFFFF,
    HD15 = 0,
    SVIDEO = 1,
    COMPOSITE_VIDEO = 2,
    COMPONENT_VIDEO = 3,
    DVI = 4,
    HDMI = 5,
    LVDS = 6,
    D_JPN = 8,
    SDI = 9,
    DISPLAYPORT_EXTERNAL = 10,
    DISPLAYPORT_EMBEDDED = 11,
    UDI_EXTERNAL = 12,
    UDI_EMBEDDED = 13,
    SDTVDONGLE = 14,
    MIRACAST = 15,
    INDIRECT_WIRED = 16,
    INDIRECT_VIRTUAL = 17,
    INTERNAL = 0x80000000,
}

public enum DISPLAYCONFIG_SCANLINE_ORDERING : uint
{
    UNSPECIFIED = 0,
    PROGRESSIVE = 1,
    INTERLACED = 2,
    INTERLACED_UPPERFIELDFIRST = INTERLACED,
    INTERLACED_LOWERFIELDFIRST = 3,
}

public enum DISPLAYCONFIG_ROTATION : uint
{
    IDENTITY = 1,
    ROTATE90 = 2,
    ROTATE180 = 3,
    ROTATE270 = 4,
}

public enum DISPLAYCONFIG_SCALING : uint
{
    IDENTITY = 1,
    CENTERED = 2,
    STRETCHED = 3,
    ASPECTRATIOCENTEREDMAX = 4,
    CUSTOM = 5,
    PREFERRED = 128,
}

public enum DISPLAYCONFIG_PIXELFORMAT : uint
{
    PIXELFORMAT_8BPP = 1,
    PIXELFORMAT_16BPP = 2,
    PIXELFORMAT_24BPP = 3,
    PIXELFORMAT_32BPP = 4,
    PIXELFORMAT_NONGDI = 5,
}

public enum DISPLAYCONFIG_MODE_INFO_TYPE : uint
{
    SOURCE = 1,
    TARGET = 2,
    DESKTOP_IMAGE = 3,
}

public enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint
{
    GET_SOURCE_NAME = 1,
    GET_TARGET_NAME = 2,
    GET_TARGET_PREFERRED_MODE = 3,
    GET_ADAPTER_NAME = 4,
    SET_TARGET_PERSISTENCE = 5,
    GET_TARGET_BASE_TYPE = 6,
    GET_SUPPORT_VIRTUAL_RESOLUTION = 7,
    SET_SUPPORT_VIRTUAL_RESOLUTION = 8,
    GET_ADVANCED_COLOR_INFO = 9,
    SET_ADVANCED_COLOR_STATE = 10,
    GET_SDR_WHITE_LEVEL = 11,
}

public enum DISPLAYCONFIG_TOPOLOGY_ID : uint
{
    INTERNAL = 0x00000001,
    CLONE = 0x00000002,
    EXTEND = 0x00000004,
    EXTERNAL = 0x00000008,
}

#endregion

#region Structs

[StructLayout(LayoutKind.Sequential)]
public struct LUID
{
    public uint LowPart;
    public int HighPart;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_SOURCE_INFO
{
    public LUID adapterId;
    public uint id;
    public uint modeInfoIdx;
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_TARGET_INFO
{
    public LUID adapterId;
    public uint id;
    public uint modeInfoIdx;
    public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
    public DISPLAYCONFIG_ROTATION rotation;
    public DISPLAYCONFIG_SCALING scaling;
    public DISPLAYCONFIG_RATIONAL refreshRate;
    public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
    public bool targetAvailable;
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_RATIONAL
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_INFO
{
    public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
    public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
    public uint flags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_2DREGION
{
    public uint cx;
    public uint cy;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
{
    public ulong pixelRate;
    public DISPLAYCONFIG_RATIONAL hSyncFreq;
    public DISPLAYCONFIG_RATIONAL vSyncFreq;
    public DISPLAYCONFIG_2DREGION activeSize;
    public DISPLAYCONFIG_2DREGION totalSize;
    public uint videoStandard;
    public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_TARGET_MODE
{
    public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct POINTL
{
    public int x;
    public int y;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_SOURCE_MODE
{
    public uint width;
    public uint height;
    public DISPLAYCONFIG_PIXELFORMAT pixelFormat;
    public POINTL position;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
{
    public POINTL PathSourceSize;
    public RECTL DesktopImageRegion;
    public RECTL DesktopImageClip;
}

[StructLayout(LayoutKind.Sequential)]
public struct RECTL
{
    public int left;
    public int top;
    public int right;
    public int bottom;
}

[StructLayout(LayoutKind.Explicit)]
public struct DISPLAYCONFIG_MODE_INFO_UNION
{
    [FieldOffset(0)]
    public DISPLAYCONFIG_TARGET_MODE targetMode;

    [FieldOffset(0)]
    public DISPLAYCONFIG_SOURCE_MODE sourceMode;

    [FieldOffset(0)]
    public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_MODE_INFO
{
    public DISPLAYCONFIG_MODE_INFO_TYPE infoType;
    public uint id;
    public LUID adapterId;
    public DISPLAYCONFIG_MODE_INFO_UNION info;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
{
    public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
    public uint size;
    public LUID adapterId;
    public uint id;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS flags;
    public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
    public ushort edidManufactureId;
    public ushort edidProductCodeId;
    public uint connectorInstance;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string monitorFriendlyDeviceName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string monitorDevicePath;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS
{
    public uint value;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string viewGdiDeviceName;
}

#endregion

#region Native API

public static class NativeApi
{
    public const uint ERROR_SUCCESS = 0;
    public const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;
    public const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(
        QueryDisplayConfigFlags flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(
        QueryDisplayConfigFlags flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        out DISPLAYCONFIG_TOPOLOGY_ID currentTopologyId);

    // Overload without topology for QDC_ALL_PATHS
    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(
        QueryDisplayConfigFlags flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(
        uint numPathArrayElements,
        DISPLAYCONFIG_PATH_INFO[] pathArray,
        uint numModeInfoArrayElements,
        DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        SetDisplayConfigFlags flags);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);
}

#endregion

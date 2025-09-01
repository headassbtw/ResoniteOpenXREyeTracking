using System;
using System.Runtime.InteropServices;
using System.Text;
using Elements.Core;
using OpenXREyeTracking.Extensions;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.OpenXR;
using Result = Silk.NET.OpenXR.Result;

namespace OpenXREyeTracking;

public static class Helpers {
    public static unsafe void CopyStringToBytePtr(byte* arr, string str) {
        Span<byte> bytes = Encoding.UTF8.GetBytes(str);
        Span<byte> tgt = new(arr, str.Length);
        bytes.CopyTo(tgt);
    }

    public static Posef PosefIdentity() {
        return new Posef {
            Orientation = new Quaternionf(0, 0, 0, 1),
            Position = new Vector3f(0, 0, 0)
        };
    }
}

public static class Extenders {
    public static void EnsureSuccess(this Result result) {
        if (result == Result.Success) return;
        throw new ApplicationException($"[Err] OpenXR: {result}");
    }
    
    public static unsafe bool PollEvent(this XR oxr, Instance instance, out EventDataBuffer eventData) {
        EventDataBuffer buf = new() {
            Type = StructureType.EventDataBuffer,
        };
        Result res = oxr.PollEvent(instance, ref buf);
        eventData = buf;
        return res == Result.Success;
    }
}

public static class XRPfnHelpers
{
    public static unsafe PtrFuncTyped<T> GetXRFunction<T>(XR xr, Instance inst, string funcName) where T : Delegate
    {
        Span<byte> funcNameBytes = stackalloc byte[funcName.Length];
        Encoding.UTF8.GetBytes(funcName, funcNameBytes);
        PfnVoidFunction pfn = new();
        xr.GetInstanceProcAddr(inst, in MemoryMarshal.GetReference(funcNameBytes), ref pfn).EnsureSuccess();
        return new(pfn);
    }
}


public static class Clock {
    private static PtrFuncTyped<XrConvertTimespecTimeToTimeKHR> timespecToTime;
    public enum ClockType : int {
        Realtime,
        Monotonic,
        ProcessCpuTimeId,
        ThreadCpuTimeId
    }

    public static void Init(XR oxr, Instance instance) {
        timespecToTime = XRPfnHelpers.GetXRFunction<XrConvertTimespecTimeToTimeKHR>(oxr, instance, "xrConvertTimespecTimeToTimeKHR");
    }
    
    public static long Time(Instance instance) {
        [DllImport("libc")]
        [SuppressGCTransition]
        static extern Timespec clock_gettime(ClockType clockId, out Timespec time);

        clock_gettime(ClockType.Monotonic, out Timespec time);
        timespecToTime.Call(instance, in time, out long xrTime);
        return xrTime;
    }
}


public readonly unsafe struct PtrFuncTyped<T>(PfnVoidFunction pfn) : IDisposable
    where T : Delegate
{
    public T Call { get; } = Marshal.GetDelegateForFunctionPointer<T>((nint)pfn.Handle);

    public void Dispose()
    {
        pfn.Dispose();
    }
}
using Elements.Core;
using FrooxEngine;
using Silk.NET.OpenXR;
using Session = Silk.NET.OpenXR.Session;

namespace OpenXREyeTracking.Extensions;

public interface IExtensionConsumer {
    public struct EyeInfo {
        public float3 Direction;
        public float3 Origin;
        public float Openness;
        public float PupilDiameter;
        public bool Valid;
    }
    public unsafe void Initialize(XR oxr, Instance instance, Session session);
    public unsafe void Update(double delta, out EyeInfo leftEye, out EyeInfo rightEye);
    public unsafe void Destroy();
}
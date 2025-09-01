using System;
using System.Runtime.InteropServices;
using Elements.Core;
using FrooxEngine;
using Silk.NET.Core.Native;
using Silk.NET.OpenXR;
using Action = Silk.NET.OpenXR.Action;
using Result = Silk.NET.OpenXR.Result;
using Session = Silk.NET.OpenXR.Session;

namespace OpenXREyeTracking.Extensions;

public class EyeGazeInteraction : IExtensionConsumer {
    private XR oxr;
    private Instance _instance;
    private Session _session;
    private Action _gazeAction;
    private ActionSet _gazeActionSet;
    private Space _stageSpace;
    private Space _eyeSpace;
    private Space _viewSpace;
    
    private PtrFuncTyped<XrConvertTimespecTimeToTimeKHR> xrConvertTimespecTimeToTimeKHR;
    
    public unsafe void Initialize(XR oxr, Instance instance, Session session) {
        this.oxr = oxr;
        _session = session;
        _instance = instance;
        
        xrConvertTimespecTimeToTimeKHR = XRPfnHelpers.GetXRFunction<XrConvertTimespecTimeToTimeKHR>(oxr, instance, nameof(xrConvertTimespecTimeToTimeKHR));
        
        byte* binding = stackalloc byte[256];
        Helpers.CopyStringToBytePtr(binding, "/user/eyes_ext/input/gaze_ext/pose");
        
        ulong bindingPath = 0;
        oxr.StringToPath(instance, binding, ref bindingPath).EnsureSuccess();

        ActionSetCreateInfo actionSetCreateInfo = new() {
            Type = StructureType.ActionSetCreateInfo,
            Priority = 0
        };
        Helpers.CopyStringToBytePtr(actionSetCreateInfo.LocalizedActionSetName, Engine.Current.AppName);
        Helpers.CopyStringToBytePtr(actionSetCreateInfo.ActionSetName, Engine.Current.AppName.ToLower() + "_openxreyetracking");
        
        _gazeActionSet = new();
        oxr.CreateActionSet(instance, actionSetCreateInfo, ref _gazeActionSet).EnsureSuccess();
        
        ActionCreateInfo eyeGazeActionCreateInfo = new() {
            Type = StructureType.ActionCreateInfo,
            CountSubactionPaths = 0,
        };
        Helpers.CopyStringToBytePtr(eyeGazeActionCreateInfo.ActionName, "eye_gaze");
        Helpers.CopyStringToBytePtr(eyeGazeActionCreateInfo.LocalizedActionName, "Eye Gaze");

        _gazeAction = new();
        oxr.CreateAction(_gazeActionSet, &eyeGazeActionCreateInfo, ref _gazeAction).EnsureSuccess();

        ActionSuggestedBinding asb = new() {
            Binding = bindingPath,
            Action = _gazeAction,
        };
            
        byte* intProfile = stackalloc byte[256];
        Helpers.CopyStringToBytePtr(intProfile, "/interaction_profiles/ext/eye_gaze_interaction");

        ulong profilePath = 0;
        oxr.StringToPath(instance, intProfile, ref profilePath).EnsureSuccess();
        
        InteractionProfileSuggestedBinding sb = new() {
            Type = StructureType.InteractionProfileSuggestedBinding,
            InteractionProfile = profilePath,
            SuggestedBindings = &asb,
            CountSuggestedBindings = 1
        };
        oxr.SuggestInteractionProfileBinding(instance, sb).EnsureSuccess();

        ActionSet[] array = [_gazeActionSet];
        fixed (ActionSet* ptr = array)
        {
            SessionActionSetsAttachInfo info = new() {
                Type = StructureType.SessionActionSetsAttachInfo,
                CountActionSets = (uint)array.Length,
                ActionSets = ptr
            };
            oxr.AttachSessionActionSets(session, &info).EnsureSuccess();
        }
        
        _stageSpace = new();
        ReferenceSpaceCreateInfo stageSpaceCreateInfo = new() {
            Type = StructureType.ReferenceSpaceCreateInfo,
            ReferenceSpaceType = ReferenceSpaceType.Stage,
            PoseInReferenceSpace = Helpers.PosefIdentity(),
        };
        oxr.CreateReferenceSpace(session, in stageSpaceCreateInfo, ref _stageSpace).EnsureSuccess();

        _viewSpace = new();
        ReferenceSpaceCreateInfo viewSpaceCreateInfo = new() {
            Type = StructureType.ReferenceSpaceCreateInfo,
            ReferenceSpaceType = ReferenceSpaceType.View,
            PoseInReferenceSpace = Helpers.PosefIdentity(),
        };
        oxr.CreateReferenceSpace(session, in viewSpaceCreateInfo, ref _viewSpace).EnsureSuccess();
        
        _eyeSpace = new();
        ActionSpaceCreateInfo eyeSpaceCreateInfo = new() {
            Type = StructureType.ActionSpaceCreateInfo,
            Action = _gazeAction,
            PoseInActionSpace = Helpers.PosefIdentity(),
        };
        oxr.CreateActionSpace(session, in eyeSpaceCreateInfo, ref _eyeSpace).EnsureSuccess();
    }

    private long _lastTime;
    public unsafe void Update(double delta, out IExtensionConsumer.EyeInfo leftEye, out IExtensionConsumer.EyeInfo rightEye) {
       
        ActiveActionSet activeActionSet = new ActiveActionSet() {
            ActionSet = _gazeActionSet,
        };
        ActionsSyncInfo syncInfo = new() {
            Type = StructureType.ActionsSyncInfo,
            ActiveActionSets = &activeActionSet,
            CountActiveActionSets = 1
        };

        var time = Clock.Time(_instance);
        oxr.SyncAction(_session, syncInfo).EnsureSuccess();

        SpaceLocation viewLoc = new() {
            Type = StructureType.SpaceLocation,
        };
        oxr.LocateSpace(_viewSpace, _stageSpace, time, ref viewLoc).EnsureSuccess();

        SpaceLocation eyeLoc = new() {
            Type = StructureType.SpaceLocation,
        };
        oxr.LocateSpace(_eyeSpace, _viewSpace, time, ref eyeLoc).EnsureSuccess();
        
        var quat0 = eyeLoc.Pose.Orientation;
        var quat = new floatQ(quat0.X, quat0.Y, quat0.Z, quat0.W);
        var pos0 = eyeLoc.Pose.Position;
        float3 pos = new(pos0.X, pos0.Y, pos0.Z);

        leftEye = new IExtensionConsumer.EyeInfo {
            Origin = pos,
            Direction = quat.EulerAngles,
            Openness = 1.0f,
            Valid = eyeLoc.LocationFlags.HasFlag(SpaceLocationFlags.PositionValidBit) && eyeLoc.LocationFlags.HasFlag(SpaceLocationFlags.OrientationValidBit),
        };
        
        rightEye = new IExtensionConsumer.EyeInfo {
            Origin = pos,
            Direction = quat.EulerAngles,
            Openness = 1.0f,
            Valid = eyeLoc.LocationFlags.HasFlag(SpaceLocationFlags.PositionValidBit) && eyeLoc.LocationFlags.HasFlag(SpaceLocationFlags.OrientationValidBit),
        };
    }

    public unsafe void Destroy() {
        throw new System.NotImplementedException();
    }
}
using System;
using Elements.Core;
using Silk.NET.Core.Native;
using Silk.NET.OpenXR;
using Silk.NET.OpenXR.Extensions.KHR;
using Result = Silk.NET.OpenXR.Result;

namespace OpenXREyeTracking.Extensions;

public delegate Result XrCreateFaceTracker2FB(Session session, ref FaceTrackerCreateInfo2FB info, ref FaceTracker2FB faceTracker);
public delegate Result XrGetFaceExpressionWeights2FB(FaceTracker2FB faceTracker, ref FaceExpressionInfo2FB expressionInfo, ref FaceExpressionWeights2FB expressionWeights);
public delegate Result XrDestroyFaceTracker2FB(FaceTracker2FB faceTracker);
public delegate Result XrConvertTimespecTimeToTimeKHR(Instance instance, in Timespec timespec, out long time);

public class FacebookFaceTracking2: IExtensionConsumer {
    private PtrFuncTyped<XrCreateFaceTracker2FB> xrCreateFaceTracker2FB;
    private PtrFuncTyped<XrDestroyFaceTracker2FB> xrDestroyFaceTracker2FB;
    private PtrFuncTyped<XrGetFaceExpressionWeights2FB> xrGetFaceExpressionWeights2FB;
    private PtrFuncTyped<XrConvertTimespecTimeToTimeKHR> xrConvertTimespecTimeToTimeKHR;

    private XR _oxr;
    private Instance _instance;
    private Session _session;
    private FaceTracker2FB _faceTracker;
    
    public unsafe void Initialize(XR oxr, Instance instance, Session session) {
        _oxr = oxr;
        _instance = instance;
        _session = session;
        xrCreateFaceTracker2FB = XRPfnHelpers.GetXRFunction<XrCreateFaceTracker2FB>(oxr, instance, nameof(xrCreateFaceTracker2FB));
        xrDestroyFaceTracker2FB = XRPfnHelpers.GetXRFunction<XrDestroyFaceTracker2FB>(oxr, instance, nameof(xrDestroyFaceTracker2FB));
        xrGetFaceExpressionWeights2FB = XRPfnHelpers.GetXRFunction<XrGetFaceExpressionWeights2FB>(oxr, instance, nameof(xrGetFaceExpressionWeights2FB));
        xrConvertTimespecTimeToTimeKHR = XRPfnHelpers.GetXRFunction<XrConvertTimespecTimeToTimeKHR>(oxr, instance, nameof(xrConvertTimespecTimeToTimeKHR));

        var dataSource = FaceTrackingDataSource2FB.VisualFB;
        
        FaceTrackerCreateInfo2FB createInfo = new() {
            Type = StructureType.FaceTrackerCreateInfo2FB,
            Next = null,
            FaceExpressionSet = FaceExpressionSet2FB.DefaultFB,
            RequestedDataSources = &dataSource,
            RequestedDataSourceCount = 1,
        };

        _faceTracker = new();
        
        xrCreateFaceTracker2FB.Call(session, ref createInfo, ref _faceTracker).EnsureSuccess();
    }

    private const float Mult = 1.0f;
    
    public unsafe void Update(double delta, out IExtensionConsumer.EyeInfo leftEye, out IExtensionConsumer.EyeInfo rightEye) {
        FaceExpressionInfo2FB expressionInfo = new() {
            Type = StructureType.FaceExpressionInfo2FB,
            Next = null,
            Time = Clock.Time(_instance)
        };

        float* weights = stackalloc float[(int)FaceExpression2FB.CountFB]; 
        float* confidences = stackalloc float[(int)FaceExpression2FB.CountFB]; 
        
        FaceExpressionWeights2FB expressionWeights = new() {
            Type = StructureType.FaceExpressionWeights2FB,
            DataSource = FaceTrackingDataSource2FB.VisualFB,
            WeightCount = (uint)FaceExpression2FB.CountFB,
            Weights = weights,
            ConfidenceCount = (uint)FaceExpression2FB.CountFB,
            Confidences = confidences,
        };
        xrGetFaceExpressionWeights2FB.Call(_faceTracker, ref expressionInfo, ref expressionWeights).EnsureSuccess();
        
        leftEye = new IExtensionConsumer.EyeInfo {
            Valid = expressionWeights.IsEyeFollowingBlendshapesValid == 1,
            Origin = float3.Zero,
            Direction = new float3(
                MathX.Tan(expressionWeights.Weights[(int)FaceExpression2FB.EyesLookLeftLFB]*-Mult+
                          expressionWeights.Weights[(int)FaceExpression2FB.EyesLookRightLFB]*Mult),
                MathX.Tan(expressionWeights.Weights[(int)FaceExpression2FB.EyesLookDownLFB]*-Mult+
                          expressionWeights.Weights[(int)FaceExpression2FB.EyesLookUpLFB]*Mult),
                Mult
                ).Normalized,
            Openness = 1.0f - expressionWeights.Weights[(int)FaceExpression2FB.EyesClosedLFB],
        };
        rightEye = new IExtensionConsumer.EyeInfo {
            Valid = expressionWeights.IsEyeFollowingBlendshapesValid == 1,
            Origin = float3.Zero,
            Direction = new float3(
                MathX.Tan(expressionWeights.Weights[(int)FaceExpression2FB.EyesLookLeftRFB]*-Mult+
                expressionWeights.Weights[(int)FaceExpression2FB.EyesLookRightRFB]*Mult),
                MathX.Tan(expressionWeights.Weights[(int)FaceExpression2FB.EyesLookDownRFB]*-Mult+
                expressionWeights.Weights[(int)FaceExpression2FB.EyesLookUpRFB]*Mult),
                Mult
            ).Normalized,
            Openness = 1.0f - expressionWeights.Weights[(int)FaceExpression2FB.EyesClosedRFB],
        };
    }

    public unsafe void Destroy() {
        throw new System.NotImplementedException();
    }
}
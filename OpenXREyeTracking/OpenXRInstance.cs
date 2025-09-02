using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FrooxEngine;
using OpenXREyeTracking.Extensions;
using ResoniteModLoader;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.OpenXR;
using Session = Silk.NET.OpenXR.Session;

namespace OpenXREyeTracking;

internal enum UsingExtension {
    Gaze,
    Facebook
}

public class OpenXRInstance {
    
    public readonly bool GazeSupported;
    public readonly string SystemName;
    public bool SessionRunning { get; private set; }
    private UsingExtension _extension;
    private Instance _instance;
    private Session _session;
    private XR oxr;
    
    private IExtensionConsumer _consumer;

    internal IExtensionConsumer.EyeInfo Left { get; private set; }
    internal IExtensionConsumer.EyeInfo Right { get; private set; }
    
    public unsafe OpenXRInstance() {
        oxr = XR.GetApi();

        uint propCount = 0;
        oxr.EnumerateInstanceExtensionProperties((byte*)null, 0, &propCount, null);

        var extProps = new ExtensionProperties[propCount];
        for (var i = 0; i < extProps.Length; i++) {
            extProps[i].Type = StructureType.ExtensionProperties;
            extProps[i].Next = null;
        }

        oxr.EnumerateInstanceExtensionProperties((byte*)null, propCount, &propCount, extProps);

        var availableExtensions = new List<string>();
        for (var i = 0; i < extProps.Length; i++) {
            fixed (byte* ptr = extProps[i].ExtensionName) {
                var extName = Marshal.PtrToStringAnsi(new IntPtr(ptr));
                if (extName == null)
                    continue;
                availableExtensions.Add(extName);
            }
        }

        if (availableExtensions.Contains("XR_EXT_eye_gaze_interaction")) {
            _extension = UsingExtension.Gaze;
        }
        
        if (availableExtensions.Contains("XR_FB_face_tracking2")) {
            _extension = UsingExtension.Facebook;
        }
        
        #region Instance Creation

        string[] extensions = [
            "XR_MND_headless",
            _extension == UsingExtension.Facebook ? "XR_FB_face_tracking2" : "XR_EXT_eye_gaze_interaction",
            "XR_EXT_debug_utils",
            "XR_KHR_convert_timespec_time"
        ];
        ResoniteMod.Msg($"Using {extensions[1]} for OpenXR eye tracking");
        string[] layers = [
            "XR_APILAYER_LUNARG_core_validation"
        ];
        
        ApplicationInfo appInf = new() {
            ApplicationVersion = (Version32)1,
            EngineVersion = 0,
            ApiVersion = (Version64)new Version(1, 0, 0),
        };
        
        Helpers.CopyStringToBytePtr(appInf.ApplicationName, Engine.Current.AppName);
        Helpers.CopyStringToBytePtr(appInf.EngineName, "FrooxEngine");
        
        InstanceCreateInfo inf = new() {
            Type = StructureType.InstanceCreateInfo,
            ApplicationInfo = appInf,
            EnabledExtensionCount = (uint)extensions.Length,
            EnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions),
            // EnabledApiLayerCount = (uint)layers.Length,
            // EnabledApiLayerNames = (byte**)SilkMarshal.StringArrayToPtr(layers),
        };

        _instance = new();
        oxr.CreateInstance(in inf, ref _instance).EnsureSuccess();
        Clock.Init(oxr, _instance);
        
        InstanceProperties props = new();
        oxr.GetInstanceProperties(_instance, ref props);

        SessionCreateInfo sessionInf = new() {
            Type = StructureType.SessionCreateInfo,
            CreateFlags = SessionCreateFlags.None,
            SystemId = 1,
        };
        
        _session = new Session();
        oxr.CreateSession(_instance, in sessionInf, ref _session).EnsureSuccess();

        switch (_extension) {
            case UsingExtension.Gaze: {
                SystemEyeGazeInteractionPropertiesEXT gazeProps = new() {
                    Type = StructureType.SystemEyeGazeInteractionPropertiesExt,
                };

                SystemProperties sysProps = new() {
                    Type = StructureType.SystemProperties,
                    Next = &gazeProps,
                };

                oxr.GetSystemProperties(_instance, 1, ref sysProps).EnsureSuccess();
                GazeSupported = gazeProps.SupportsEyeGazeInteraction != 0;
                SystemName = Marshal.PtrToStringAnsi(new IntPtr(sysProps.SystemName));
                break;
            }
            case UsingExtension.Facebook: {
                SystemFaceTrackingProperties2FB faceProps = new() {
                    Type = StructureType.SystemFaceTrackingProperties2FB,
                };
                
                SystemProperties sysProps = new() {
                    Type = StructureType.SystemProperties,
                    Next = &faceProps,
                };

                oxr.GetSystemProperties(_instance, 1, ref sysProps).EnsureSuccess();
                GazeSupported = faceProps.SupportsVisualFaceTracking == 1;
                
                SystemName = Marshal.PtrToStringAnsi(new IntPtr(sysProps.SystemName));
                break;
            }
        }
        
        SystemName = SystemName == null ? "Unknown" : SystemName;
        
        #endregion

        switch (_extension) {
            case UsingExtension.Gaze:
                _consumer = new EyeGazeInteraction();
                break;
            case UsingExtension.Facebook:
                _consumer = new FacebookFaceTracking2();
                break;
        }
        
        _consumer.Initialize(oxr, _instance, _session);
        
    }

    public unsafe void Update(double delta) {
        while (oxr.PollEvent(_instance, out var eventData)) {
            if (eventData.Type == StructureType.EventDataSessionStateChanged) {
                var sessionEvent = Unsafe.As<EventDataBuffer, EventDataSessionStateChanged>(ref eventData);
                switch (sessionEvent.State) {
                    case SessionState.Unknown:
                    case SessionState.Idle: {
                        break;
                    }
                    case SessionState.Ready: {
                        SessionBeginInfo beginInf = new() {
                            Type = StructureType.SessionBeginInfo,
                            PrimaryViewConfigurationType = ViewConfigurationType.PrimaryStereo
                        };
                        oxr.BeginSession(_session, beginInf).EnsureSuccess();
                        SessionRunning = true;
                        break;
                    }
                    case SessionState.Focused: {
                        SessionRunning = true;
                        break;
                    }
                    case SessionState.Synchronized:
                    case SessionState.Visible: {
                        break;
                    }
                    case SessionState.Stopping:
                    case SessionState.LossPending: 
                    case SessionState.Exiting: {
                        SessionRunning = false;
                        break;
                    }
                }
                break;
            }
        }   

        if (!SessionRunning) {
            return;
        }

        var left = Left;
        var right = Right;
        _consumer.Update(delta, out left, out right);
        Left = left;
        Right = right;
    }
}
using System;
using Elements.Core;
using FrooxEngine;
using Renderite.Shared;
using HarmonyLib;
using OpenXREyeTracking.Extensions;
using ResoniteModLoader;
using Silk.NET.OpenXR;

namespace OpenXREyeTracking;
public class OpenXREyeTracking : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.2";
	public override string Name => "OpenXREyeTracking";
	public override string Author => "headassbtw";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/headassbtw/ResoniteOpenXREyeTracking/";

	public override void OnEngineInit() {
		Harmony harmony = new Harmony("net.headassbtw.OpenXREyeTracking");
		harmony.PatchAll();
		Engine engine = Engine.Current;
		engine.RunPostInit(() => {
			try {
				engine.InputInterface.RegisterInputDriver(new OpenXRInterface());
			}
			catch (Exception e) {
				Msg("Failed to initialize OpenXREyeTracking");
				Msg(e);
			}
		});
	}

	private class OpenXRInterface : IInputDriver {
		private Eyes _eyes;
		private OpenXRInstance _instance;
		
		public int UpdateOrder => 100;

		public OpenXRInterface() {
			_instance = new();
		}
		
		public void CollectDeviceInfos(DataTreeList list) {
			DataTreeDictionary dict = new();
			dict.Add("Name", "OpenXR Eye Tracking");
			dict.Add("Type", "Eye Tracking");
			// TODO: Init OpenXR first and use the HMD's model name
			dict.Add("Model", _instance.SystemName); // Is this how this works??
			list.Add(dict);
		}
		
		public void RegisterInputs(InputInterface inputInterface) {
			_eyes = new Eyes(inputInterface, "OpenXR Eye Tracking", false);
			UniLog.Log($"OpenXR eye tracking registered (using {_instance.SystemName})");
		}

		public void UpdateInputs(float deltaTime) {
			_eyes.Timestamp += deltaTime;
			_instance.Update(_eyes.Timestamp);
			
			UpdateEye(_eyes.LeftEye, _instance.Left);
			UpdateEye(_eyes.RightEye, _instance.Right);
			
			_eyes.IsEyeTrackingActive = _eyes.LeftEye.IsTracking && _eyes.RightEye.IsTracking;
			
			_eyes.ComputeCombinedEyeParameters();
			_eyes.ConvergenceDistance = 0f;
			
			_eyes.FinishUpdate();
		}

		private void UpdateEye(Eye eye, IExtensionConsumer.EyeInfo eyeInfo) {
			eye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
			eye.IsTracking = eyeInfo.Valid;

			if (eyeInfo.Valid) {
				eye.UpdateWithDirection(eyeInfo.Direction);
				eye.RawPosition = eyeInfo.Origin;
				eye.Openness = eyeInfo.Openness;
			}
		}
		
	}
}

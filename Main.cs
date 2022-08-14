using System;
using System.Text;
using Assets.Scripts.Engine.Network;
using Assets.Scripts.Game;
using Assets.Scripts.Game.Client;
using MelonLoader;
using UnityEngine;
using UnhollowerBaseLib;
using System.Runtime.InteropServices;
using Assets.Scripts.Engine;
using System.Runtime.CompilerServices;

[assembly: MelonInfo(typeof(HopsHack.Main), "HopsHack", "1.0.0")]
[assembly: MelonGame]
namespace HopsHack
{
    public class Main : MelonMod
    {
        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

        [DllImport("kernel32")]
        static extern bool VirtualProtect(IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        private static string OriginalHWID;
		private static char[] CurrentHWID;
        private Rect MenuRect = new Rect(128f, 128f, 300f, 500f);
        private GUI.WindowFunction MenuFunction;
        private GUIContent MenuTitle = new GUIContent("Title");
		private GUIContent TempText = new GUIContent(string.Empty);
		private StringBuilder stringBuilder = new StringBuilder(256);
		private Il2CppReferenceArray<GUILayoutOption> EmptyLayoutOptions = new Il2CppReferenceArray<GUILayoutOption>(0);
        private GUIStyle textStyle;

        public bool InMenu;
        public bool EnableESP = true;
        public bool EnableAim = true;
		public float AimFOV = 20f;
		public static bool NoRecoil;

		public override void OnApplicationStart()
		{
			OriginalHWID = StaticInfoHelper.GetUserDeviceIdMd5();
			CurrentHWID = OriginalHWID.ToCharArray();

			MenuFunction = new Action<int>(MenuWindow);
			textStyle = new GUIStyle();
			textStyle.fontSize = 12;
			textStyle.alignment = TextAnchor.UpperCenter;
			textStyle.normal.textColor = Color.red;

			unsafe
			{
                IntPtr lib = LoadLibrary("GameAssembly.dll");
				IntPtr origMethod;
				uint oldProtect;
				IntPtr ovmethod;

                var stub = new byte[]
                {
                    0x90, 0x90, 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, // jmp qword ptr [$+6]
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00  // ptr
                };



                origMethod = lib + 0x8077D0; // needs to be updated
                MelonLogger.Warning(origMethod.ToString("X"));
				ovmethod = typeof(Main).GetMethod("OV_GetUserDeviceIdMD5").MethodHandle.GetFunctionPointer();

                Array.Copy(BitConverter.GetBytes((long)ovmethod), 0, stub, 8, 8);
				VirtualProtect(origMethod, new IntPtr(16), 0x04, out oldProtect);
				Marshal.Copy(stub, 0, origMethod, 16);
                VirtualProtect(origMethod, new IntPtr(16), oldProtect, out oldProtect);
            }
        }

		public static unsafe IntPtr OV_GetUserDeviceIdMD5()
		{
			fixed (char* c = &CurrentHWID[0])
				return IL2CPP.il2cpp_string_new_utf16(c, CurrentHWID.Length);
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Home))
                InMenu = !InMenu;

			var game = UnityNetworkConnection.prop_ClientNetworkGame_0;

			if (game == null)
				return;

			ClientNetPlayer localPlayer = game.prop_ClientNetPlayer_0;
			if (localPlayer == null)
				return;

			CVars.AccuracyAimMultiplier = 0f;
			CVars.WeaponMaxAccuracy = 0f;
			CVars.WeaponBaseAccuracyInAim = 0f;
			CVars.WeaponSitAccuracyMultiplier = 0f;
			CVars.WeaponOpticAccuracyMultNotAim = 0f;

			var ammo = localPlayer.prop_BaseAmmunitions_0;
			if (ammo != null)
			{
				ammo.ShouldShakeCamera = false;
				var primary = ammo.prop_BaseWeapon_0;
				if (primary != null)
				{
					var recoil = primary.recoilSettings;
					recoil.AimingFactor = 0f;
					recoil.NoAimingFactor = 0f;
					recoil.AimingOpticFactor = 0f;
				}
			}

			var MainCamera = Camera.main;
			if (MainCamera == null)
				return;

			var camPos = MainCamera.transform.position;
			var forward = MainCamera.transform.forward;

			bool isTeamGame = game.prop_Boolean_1;
			var myFraction = localPlayer.prop_EnumPublicSealedvaBeUsSpNePl6vUnique_0;

			if (localPlayer.field_Protected_BaseMoveController_0 != null && EnableAim && Input.GetKey(KeyCode.Mouse1))
			{
				EntityNetPlayer target = null;
				float minAngle = 0f;
				foreach (EntityNetPlayer entityNetPlayer in game.prop_List_1_EntityNetPlayer_0)
				{
					bool isTeammate = isTeamGame && entityNetPlayer.prop_EnumPublicSealedvaBeUsSpNePl6vUnique_0 == myFraction;
					if (isTeammate)
						continue;

					float angle = Angle(entityNetPlayer.prop_Vector3_0 - camPos, forward);
					if (angle > AimFOV)
						continue;

					if (target == null || angle < minAngle)
					{
						minAngle = angle;
						target = entityNetPlayer;
					}
				}

				if (target != null)
				{
					var direction = target.playerBoneFinder.NPC_Head.position + new Vector3(0f, 0.07f) - camPos;
					if (SqrMagnitude(direction) > 0.0001f)
					{
						var euler = Quaternion.LookRotation(direction).eulerAngles;
						float pitch = euler.x;
						if (pitch > 270f)
							pitch -= 90f;
						else
							pitch += 270f;

						var state = localPlayer.field_Protected_BaseMoveController_0.state;
						var stateEuler = state.euler;

						stateEuler.x = pitch;
						stateEuler.y = euler.y;

						state.euler = stateEuler;
					}
				}
			}

			if (localPlayer.field_Protected_BaseMoveController_0 != null && Input.GetKey(KeyCode.LeftAlt))
            {
				foreach (EntityNetPlayer entityNetPlayer in game.prop_List_1_EntityNetPlayer_0)
				{
					bool isTeammate = isTeamGame && entityNetPlayer.prop_EnumPublicSealedvaBeUsSpNePl6vUnique_0 == myFraction;
					if (isTeammate)
						continue;

					entityNetPlayer.PlayerTransform.position = localPlayer.PlayerTransform.position;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Angle(Vector3 from, Vector3 to)
        {
			float num = (float)Math.Sqrt((double)(SqrMagnitude(from) * SqrMagnitude(to)));
			if (num < 1E-15f)
				return 0f;

			float num2 = Clamp(Dot(from, to) / num, -1f, 1f);
			return (float)Math.Acos((double)num2) * 57.29578f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float SqrMagnitude(Vector3 vector)
		{
			return vector.x * vector.x + vector.y * vector.y + vector.z * vector.z;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Clamp(float value, float min, float max)
		{
			if (value < min)
				value = min;
			else if (value > max)
				value = max;

			return value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Dot(Vector3 lhs, Vector3 rhs)
		{
			return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
		}

		public override void OnGUI()
		{
			try
			{
				if (InMenu)
					MenuRect = GUI.Window(666, MenuRect, MenuFunction, MenuTitle);

				var MainCamera = Camera.main;
				if (MainCamera == null)
					return;

				var game = UnityNetworkConnection.prop_ClientNetworkGame_0;

				if (game == null)
					return;

				ClientNetPlayer localPlayer = game.prop_ClientNetPlayer_0;
				if (localPlayer == null)
					return;

				bool isTeamGame = game.prop_Boolean_1;
				var myFraction = localPlayer.prop_EnumPublicSealedvaBeUsSpNePl6vUnique_0;
				Vector3 camPos = MainCamera.transform.position;

				var sb = stringBuilder;
				float screenHeight = Screen.height;
				float screenWidth = Screen.width;
				if (EnableESP)
				{
					foreach (EntityNetPlayer entityNetPlayer in game.prop_List_1_EntityNetPlayer_0)
					{
						bool isTeammate = isTeamGame && entityNetPlayer.prop_EnumPublicSealedvaBeUsSpNePl6vUnique_0 == myFraction;
						Vector3 v = MainCamera.WorldToViewportPoint(entityNetPlayer.prop_Vector3_0 + new Vector3(0f, -1f));
						if (v.z > 0.01f && v.x > 0f && v.y > 0f && v.x < 1f && v.y < 1f)
						{
							v.x *= screenWidth;
							v.y *= screenHeight;

							float dist = Vector3.Distance(camPos, entityNetPlayer.prop_Vector3_0);

							sb.Clear();
							sb.Append('[').Append((int)dist).Append("] ").Append(entityNetPlayer.prop_String_1);

							TempText.text = sb.ToString();

							Vector2 size = textStyle.CalcSize(TempText);
							textStyle.normal.textColor = isTeammate ? Color.blue : Color.red;
							textStyle.Draw(new Rect(v.x - size.x * 0.5f, screenHeight - v.y, size.x, size.y), TempText, -1);
						}
					}
				}
			}
			catch (Exception e)
			{
				MelonLogger.Error("HopsGUI", e);
			}
		}

        private void MenuWindow(int windowID)
        {
			Il2CppReferenceArray<GUILayoutOption> options = EmptyLayoutOptions;
            //GUILayout.Label("User: " + StaticInfoHelper.GetUserID(), options);
            GUILayout.Label("HWID: " + StaticInfoHelper.GetUserDeviceIdMd5(), options);
            if (GUILayout.Button("Change HWID", options))
            {
				var random = new System.Random();
				for (int i = 0; i < CurrentHWID.Length; i++)
				{
                    CurrentHWID[i] = "0123456789abcdef"[random.Next(16)];
				}
            }
            GUILayout.Space(5f);

            EnableESP = GUILayout.Toggle(EnableESP, "ESP Names", options);
			EnableAim = GUILayout.Toggle(EnableAim, "Aimbot", options);

			GUILayout.Label("FOV: " + AimFOV, options);
			AimFOV = GUILayout.HorizontalSlider(AimFOV, 1f, 90f, options);
			GUILayout.Label("Jump: " + CVars.g_jumpHeight, options);
			CVars.g_jumpHeight = GUILayout.HorizontalSlider(CVars.g_jumpHeight, 0.65f, 3f, options);
			GUILayout.Label("Sprint: " + CVars.g_runSpeed, options);
			CVars.g_runSpeed = GUILayout.HorizontalSlider(CVars.g_runSpeed, 3.4f, 9f, options);

			var game = UnityNetworkConnection.prop_ClientNetworkGame_0;

			if (game != null)
			{
				ClientNetPlayer localPlayer = game.prop_ClientNetPlayer_0;
				if (localPlayer != null)
				{
					var ammo = localPlayer.prop_BaseAmmunitions_0;
					if (ammo != null)
					{
						var primary = ammo.prop_BaseWeapon_0;
						if (primary != null)
						{
							GUILayout.Label(primary.prop_Single_31.ToString(), options);
							GUILayout.Label(primary.prop_Single_33.ToString(), options);
							GUILayout.Label(primary.prop_Single_40.ToString(), options);
						}
					}
				}
			}
			GUI.DragWindow();
        }
	}
}

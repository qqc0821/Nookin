using System.Collections;
using Kirurobo;
using UnityEngine;

namespace Nookin
{
    /// <summary>The small desktop interaction loop used by the feasibility build.</summary>
    public sealed class NookinDesktop : MonoBehaviour
    {
        public UniWindowController Window;
        public Camera CharacterCamera;
        public Animator CharacterAnimator;

        private const float DragThresholdPoints = 6f;
        private static readonly Vector2 IntendedWindowSize = new Vector2(460f, 540f);
        private bool wasLeftDown;
        private bool pressOnCharacter;
        private bool dragging;
        private Vector2 pressCursor;
        private Vector2 pressWindowPosition;

        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution((int)IntendedWindowSize.x, (int)IntendedWindowSize.y, false);

            Window.currentCamera = CharacterCamera;
            Window.hitTestType = UniWindowController.HitTestType.Raycast;
            Window.isHitTestEnabled = true;
            Window.isTransparent = true;
            Window.isTopmost = true;
            StartCoroutine(CentreWindowAfterNativeSetup());
        }

        private IEnumerator CentreWindowAfterNativeSetup()
        {
            // UniWindowController acquires the player window after the first frame.
            for (var attempt = 0; attempt < 120 && Window.windowSize.x <= 0; attempt++)
                yield return null;

            if (Window.windowSize.x <= 0)
            {
                Debug.LogError("Could not acquire the desktop window.");
                yield break;
            }

            Window.windowSize = IntendedWindowSize;
            yield return null;

            var screen = UniWindowController.GetMonitorRect(0);
            var size = Window.windowSize;
            Window.windowPosition = new Vector2(
                screen.xMin + (screen.width - size.x) * 0.5f,
                screen.yMin + (screen.height - size.y) * 0.5f);
            Debug.Log($"Nookin desktop window acquired and centred: native={Window.windowSize}, rendered={Screen.width}x{Screen.height}.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Application.Quit();

            if (Input.GetKeyDown(KeyCode.Space) && CharacterAnimator != null)
            {
                CharacterAnimator.SetTrigger("Wave");
                Debug.Log("Nookin wave triggered by keyboard.");
            }

            if (Window == null || CharacterCamera == null || CharacterAnimator == null || Window.windowSize.x <= 0)
                return;

            var cursor = UniWindowController.GetCursorPosition();
            var leftDown = Input.GetMouseButton(0) ||
                (UniWindowController.GetMouseButtons() & UniWindowController.MouseButton.Left) != 0;

            if (leftDown && !wasLeftDown)
            {
                pressOnCharacter = PointerHitsCharacter(cursor);
                if (pressOnCharacter)
                {
                    pressCursor = cursor;
                    pressWindowPosition = Window.windowPosition;
                }
            }

            if (leftDown && pressOnCharacter)
            {
                if (!dragging && Vector2.Distance(cursor, pressCursor) >= DragThresholdPoints)
                {
                    dragging = true;
                    CharacterAnimator.enabled = false;
                    Window.isHitTestEnabled = false;
                    Window.isClickThrough = false;
                }

                if (dragging)
                    MoveWindowKeepingItVisible(pressWindowPosition + cursor - pressCursor);
            }

            if (!leftDown && wasLeftDown && pressOnCharacter)
            {
                if (dragging)
                {
                    CharacterAnimator.enabled = true;
                    CharacterAnimator.Play("Idle", 0, 0f);
                    Window.isHitTestEnabled = true;
                    Debug.Log("Nookin drag ended.");
                }
                else
                {
                    CharacterAnimator.SetTrigger("Wave");
                    Debug.Log("Nookin wave triggered.");
                }

                dragging = false;
                pressOnCharacter = false;
            }

            wasLeftDown = leftDown;
        }

        private bool PointerHitsCharacter(Vector2 globalCursor)
        {
            var size = Window.clientSize;
            if (size.x <= 0 || size.y <= 0)
                return false;

            var unityMouse = Input.mousePosition;
            if (unityMouse.x >= 0 && unityMouse.y >= 0 &&
                unityMouse.x < Screen.width && unityMouse.y < Screen.height &&
                Physics.Raycast(CharacterCamera.ScreenPointToRay(unityMouse), out _, 100f))
                return true;

            var local = globalCursor - Window.windowPosition;
            var pixel = new Vector2(local.x * Screen.width / size.x, local.y * Screen.height / size.y);
            if (pixel.x < 0 || pixel.y < 0 || pixel.x >= Screen.width || pixel.y >= Screen.height)
                return false;

            return Physics.Raycast(CharacterCamera.ScreenPointToRay(pixel), out _, 100f);
        }

        private void MoveWindowKeepingItVisible(Vector2 proposed)
        {
            var monitor = UniWindowController.GetMonitorRect(0);
            var size = Window.windowSize;
            Window.windowPosition = new Vector2(
                Mathf.Clamp(proposed.x, monitor.xMin, monitor.xMax - size.x),
                Mathf.Clamp(proposed.y, monitor.yMin, monitor.yMax - size.y));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Losing focus during a drag must never leave click-through disabled.
            if (hasFocus || !dragging || Window == null)
                return;

            dragging = false;
            pressOnCharacter = false;
            wasLeftDown = false;
            Window.isHitTestEnabled = true;
            if (CharacterAnimator != null)
            {
                CharacterAnimator.enabled = true;
                CharacterAnimator.Play("Idle", 0, 0f);
            }
        }
    }
}

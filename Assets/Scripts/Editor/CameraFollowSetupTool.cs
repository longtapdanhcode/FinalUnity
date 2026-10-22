#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.Juice;

namespace Vanguard.EditorTools
{
    /// <summary>Tạo CameraRig (pivot) bám theo Player và đưa Main Camera vào làm con, giữ nguyên góc nhìn hiện tại.</summary>
    public static class CameraFollowSetupTool
    {
        [MenuItem("Vanguard/Graphics/Setup Camera Follow")]
        public static void Setup()
        {
            Camera cam = Camera.main;
            GameObject player = GameObject.Find("Player");
            if (cam == null || player == null)
            {
                Debug.LogError("[CameraFollow] Cần Main Camera và GameObject 'Player' trong scene.");
                return;
            }

            // Điểm camera đang nhìn tới (giao tia forward với mặt phẳng ngang qua Player).
            // Pivot đặt ở đó để khi pivot trùng XZ Player thì Player nằm đúng giữa màn hình.
            Plane plane = new Plane(Vector3.up, new Vector3(0f, player.transform.position.y, 0f));
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!plane.Raycast(ray, out float dist))
            {
                Debug.LogError("[CameraFollow] Camera không nhìn xuống mặt phẳng sàn.");
                return;
            }
            Vector3 lookPoint = ray.GetPoint(dist);
            lookPoint.y = 0f;

            Transform rig = cam.transform.parent != null && cam.transform.parent.name == "CameraRig"
                ? cam.transform.parent
                : null;

            if (rig == null)
            {
                GameObject go = new GameObject("CameraRig");
                Undo.RegisterCreatedObjectUndo(go, "Setup Camera Follow");
                go.transform.position = lookPoint;
                Undo.SetTransformParent(cam.transform, go.transform, "Setup Camera Follow");
                rig = go.transform; // worldPositionStays: góc nhìn không đổi
            }
            else
            {
                // Đặt lại pivot về điểm nhìn mà không làm camera dịch chuyển
                Vector3 camPos = cam.transform.position;
                Undo.RecordObject(rig, "Setup Camera Follow");
                Undo.RecordObject(cam.transform, "Setup Camera Follow");
                rig.position = lookPoint;
                cam.transform.position = camPos;
            }

            CameraFollow follow = rig.GetComponent<CameraFollow>();
            if (follow == null) follow = Undo.AddComponent<CameraFollow>(rig.gameObject);

            SerializedObject so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = rig.gameObject;
            Debug.Log("[CameraFollow] Đã tạo CameraRig bám theo Player. Bấm Play để kiểm tra.");
        }
    }
}
#endif

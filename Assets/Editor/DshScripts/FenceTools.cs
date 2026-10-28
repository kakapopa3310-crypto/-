using System.Collections.Generic;
using System.Text;
using PaperHouse.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshScripts
{
    /// <summary>
    /// Сборка забора: находит все разрозненные секции (fence, fbxFence…),
    /// удаляет их и собирает ровный периметр одним мешем с коллайдерами.
    /// </summary>
    public static class FenceTools
    {
        public static string Run(string args)
        {
            string command = (args ?? string.Empty).Trim().ToLowerInvariant();
            if (command == "build") return Build();
            if (command == "check") return Check();
            if (command == "view") return View();
            if (command == "paint") return Paint();
            if (command == "single") return Single();
            return Info();
        }

        /// <summary>Все меши-секции забора, которые пользователь расставил вручную.</summary>
        private static List<GameObject> FindLooseSections()
        {
            var result = new List<GameObject>();
            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
            {
                GameObject go = filter.gameObject;

                // Наш собранный забор (House/Fence) не трогаем.
                if (go.name == "Fence") continue;

                string n = go.name.ToLowerInvariant();
                if (n.StartsWith("fence") || n.StartsWith("fbxfence"))
                    result.Add(go);
            }
            return result;
        }

        private static string Build()
        {
            Scene scene = SceneManager.GetActiveScene();

            // 1. Убираем всё, что пользователь расставил вручную.
            var loose = FindLooseSections();
            int removed = 0;
            foreach (GameObject go in loose)
            {
                if (go == null) continue;
                Object.DestroyImmediate(go);
                removed++;
            }

            // 2. Старый собранный забор и опустевшие группы fbxFence тоже убираем.
            GameObject house = GameObject.Find("House");
            if (house == null) return "Нет объекта House";

            var emptyGroups = new List<GameObject>();
            foreach (Transform child in house.transform)
            {
                if (child.name.ToLowerInvariant().StartsWith("fbxfence") && child.childCount == 0)
                    emptyGroups.Add(child.gameObject);
            }
            foreach (GameObject go in emptyGroups) Object.DestroyImmediate(go);
            removed += emptyGroups.Count;

            Transform old = house.transform.Find("Fence");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject sample = GameObject.Find("Fence Sample");
            if (sample != null) Object.DestroyImmediate(sample);

            // 3. Собираем заново.
            HouseBuilder.BuildFence(house.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Transform fence = house.transform.Find("Fence");
            int colliders = fence != null ? fence.GetComponentsInChildren<Collider>(true).Length : 0;
            MeshRenderer renderer = fence != null ? fence.GetComponent<MeshRenderer>() : null;
            string size = renderer != null ? renderer.bounds.size.ToString("F1") : "-";

            return "секций удалено: " + removed +
                   "; новый забор: коллайдеров " + colliders + ", размер " + size;
        }

        /// <summary>Проверка в Play Mode: стена держит, калитка пропускает.</summary>
        private static string Check()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            GameObject player = GameObject.Find("Player");
            if (player == null) return "Нет Player";

            var controller = player.GetComponent<CharacterController>();
            if (controller == null) return "Нет CharacterController";

            Application.runInBackground = true;

            float wall = StepIntoFence(player, controller, 5f);
            float gate = StepIntoFence(player, controller, 0f);

            string wallVerdict = wall > -9.8f ? "стена держит" : "стена ПРОПУСТИЛА";
            string gateVerdict = gate < -10.2f ? "калитка открыта" : "калитка ЗАКРЫТА";

            return "у стены z=" + wall.ToString("F2") + " (" + wallVerdict + "); " +
                   "в калитке z=" + gate.ToString("F2") + " (" + gateVerdict + ")";
        }

        private static float StepIntoFence(GameObject player, CharacterController controller, float x)
        {
            controller.enabled = false;
            player.transform.position = new Vector3(x, 0.3f, -8.5f);
            controller.enabled = true;
            Physics.SyncTransforms();

            controller.Move(new Vector3(0f, 0f, -4f));
            Physics.SyncTransforms();
            return player.transform.position.z;
        }

        /// <summary>Поставить игрока так, чтобы в кадр попал передний забор.</summary>
        private static string View()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            GameObject player = GameObject.Find("Player");
            if (player == null) return "Нет Player";

            Application.runInBackground = true;

            player.transform.position = new Vector3(3f, 0.3f, -7.3f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
                camera.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);

            Physics.SyncTransforms();
            return "игрок смотрит на передний забор";
        }

        /// <summary>Временная яркая краска — проверить геометрию.</summary>
        private static string Paint()
        {
            GameObject house = GameObject.Find("House");
            Transform fence = house != null ? house.transform.Find("Fence") : null;
            if (fence == null) return "Забора нет";

            var renderer = fence.GetComponent<MeshRenderer>();
            if (renderer == null) return "Нет рендерера";

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var material = new Material(shader);
            material.color = new Color(0.95f, 0.55f, 0.25f);
            renderer.sharedMaterial = material;

            var meshFilter = fence.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            string stats = mesh != null
                ? " verts=" + mesh.vertexCount + " submeshes=" + mesh.subMeshCount
                : " нет меша";

            return "забор покрашен" + stats;
        }

        /// <summary>Показать одну секцию модели отдельно.</summary>
        private static string Single()
        {
            Mesh source = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Wood_Fence/fbxFence.fbx"))
            {
                Mesh mesh = asset as Mesh;
                if (mesh != null) { source = mesh; break; }
            }

            if (source == null) return "Нет меша";

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var material = new Material(shader);
            material.color = new Color(0.95f, 0.55f, 0.25f);

            GameObject old = GameObject.Find("Fence Sample");
            if (old != null) Object.DestroyImmediate(old);

            var sample = new GameObject("Fence Sample");
            sample.transform.position = new Vector3(0f, 0.6f, -6f);
            sample.AddComponent<MeshFilter>().sharedMesh = source;
            sample.AddComponent<MeshRenderer>().sharedMaterial = material;

            Vector3 size = source.bounds.size;
            sample.transform.localScale = new Vector3(3.94f / Mathf.Max(0.0001f, size.x),
                                                     1.2f / Mathf.Max(0.0001f, size.y), 1f);

            return "секция: verts=" + source.vertexCount + " bounds=" + size.ToString("F3");
        }

        private static string Info()
        {
            var sb = new StringBuilder();

            var loose = FindLooseSections();
            sb.AppendLine("разрозненных секций: " + loose.Count);
            foreach (GameObject go in loose)
            {
                var filter = go.GetComponent<MeshFilter>();
                string meshInfo = filter != null && filter.sharedMesh != null
                    ? filter.sharedMesh.name + " v=" + filter.sharedMesh.vertexCount
                    : "-";
                sb.AppendLine("  " + go.name +
                              " pos=" + go.transform.position.ToString("F2") +
                              " rot=" + go.transform.eulerAngles.ToString("F0") +
                              " scale=" + go.transform.localScale.ToString("F2") +
                              " mesh=" + meshInfo);
            }

            GameObject house = GameObject.Find("House");
            Transform fence = house != null ? house.transform.Find("Fence") : null;
            if (fence != null)
            {
                var renderer = fence.GetComponent<MeshRenderer>();
                var material = renderer != null ? renderer.sharedMaterial : null;
                sb.AppendLine("собранный забор: материал=" +
                              (material != null ? material.name + " / " + material.shader.name : "нет"));
                foreach (Collider collider in fence.GetComponentsInChildren<Collider>(true))
                {
                    sb.AppendLine("  " + collider.name +
                                  " center=" + collider.bounds.center.ToString("F2") +
                                  " size=" + collider.bounds.size.ToString("F2"));
                }
            }

            System.IO.File.WriteAllText("Library/DshBridge/fence-info.txt", sb.ToString(),
                new UTF8Encoding(false));
            return sb.ToString();
        }
    }
}

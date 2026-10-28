using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshScripts
{
    /// <summary>
    /// Одноразовая настройка: вешает направленный свет на модель луны и
    /// приглушает старый «фасадный» направленный свет. Сохраняет сцену.
    /// Вызов: exec DshScripts.MoonLightSetup.Run
    /// </summary>
    public static class MoonLightSetup
    {
        public static string Run(string args)
        {
            GameObject moon = GameObject.Find("Moon");
            if (moon == null) return "Не найден объект Moon";

            Transform existing = moon.transform.Find("Moon Light");
            GameObject lightObject = existing != null ? existing.gameObject : new GameObject("Moon Light");

            lightObject.transform.SetParent(moon.transform, false);
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;
            lightObject.transform.localScale = Vector3.one;

            var light = lightObject.GetComponent<Light>();
            if (light == null) light = lightObject.AddComponent<Light>();

            light.type = LightType.Directional;
            light.color = new Color(0.62f, 0.72f, 1f);
            light.intensity = 0.9f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;
            light.shadowBias = 0.05f;

            var target = new Vector3(-6f, 0f, -6f);
            Vector3 direction = (target - moon.transform.position).normalized;
            lightObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            var dimmed = new List<string>();
            foreach (Light other in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (other == light) continue;
                if (other.type != LightType.Directional) continue;
                if (other.transform.IsChildOf(moon.transform)) continue;

                other.intensity = 0.22f;
                dimmed.Add(other.name);
            }

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();

            Vector3 euler = lightObject.transform.eulerAngles;
            return "Moon Light: pitch=" + euler.x.ToString("F0") + "° yaw=" + euler.y.ToString("F0") +
                   "° intensity=" + light.intensity.ToString("F2") +
                   " moonPos=" + moon.transform.position +
                   "; приглушены: " + (dimmed.Count > 0 ? string.Join(", ", dimmed.ToArray()) : "нет");
        }
    }
}

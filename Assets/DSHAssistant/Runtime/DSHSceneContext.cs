using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DSH.Assistant
{
    /// <summary>
    /// Собирает короткое текстовое описание проекта и активной сцены,
    /// которое добавляется к системному сообщению.
    /// </summary>
    public static class DSHSceneContext
    {
        private const int MaxRoots = 12;
        private const int MaxChildrenListed = 5;
        private const int MaxSelection = 6;
        private const int MaxComponents = 6;

        public static string Build()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("Контекст Unity-проекта (актуально на момент запроса):");
            sb.AppendLine("- Продукт: " + Safe(Application.productName));
            sb.AppendLine("- Версия Unity: " + Application.unityVersion);
            sb.AppendLine("- Платформа: " + Application.platform);

#if UNITY_EDITOR
            sb.AppendLine("- Режим: редактор");
#else
            sb.AppendLine("- Режим: собранный плеер");
#endif

            Scene scene = SceneManager.GetActiveScene();
            sb.AppendLine("- Активная сцена: " + (string.IsNullOrEmpty(scene.name) ? "(без имени)" : scene.name));

            GameObject[] roots = scene.GetRootGameObjects();
            sb.AppendLine("- Корневых объектов: " + roots.Length);

            int shown = 0;
            foreach (GameObject root in roots)
            {
                if (shown >= MaxRoots)
                {
                    sb.AppendLine("  … остальные корневые объекты не перечислены");
                    break;
                }

                AppendObjectLine(sb, "  • ", root);
                shown++;

                int children = root.transform.childCount;
                for (int i = 0; i < children && i < MaxChildrenListed; i++)
                {
                    Transform child = root.transform.GetChild(i);
                    sb.AppendLine("      - " + Safe(child.name) + " (" + child.childCount + " дочерних)");
                }
                if (children > MaxChildrenListed)
                    sb.AppendLine("      - … ещё " + (children - MaxChildrenListed) + " объектов");
            }

#if UNITY_EDITOR
            AppendSelection(sb);
#endif
            return sb.ToString();
        }

        private static void AppendObjectLine(StringBuilder sb, string prefix, GameObject go)
        {
            if (go == null) return;

            Vector3 p = go.transform.position;
            sb.Append(prefix)
              .Append(Safe(go.name))
              .Append("  pos=(")
              .Append(p.x.ToString("0.##")).Append(", ")
              .Append(p.y.ToString("0.##")).Append(", ")
              .Append(p.z.ToString("0.##")).Append(')');

            if (go.isStatic) sb.Append(" [static]");
            if (!go.activeSelf) sb.Append(" [inactive]");

            Component[] components = go.GetComponents<Component>();
            if (components != null && components.Length > 1)
            {
                sb.Append("  компоненты: ");
                int listed = 0;
                foreach (Component c in components)
                {
                    if (c == null || c is Transform) continue;
                    if (listed >= MaxComponents)
                    {
                        sb.Append(", …");
                        break;
                    }
                    if (listed > 0) sb.Append(", ");
                    sb.Append(c.GetType().Name);
                    listed++;
                }
            }

            sb.AppendLine();
        }

#if UNITY_EDITOR
        private static void AppendSelection(StringBuilder sb)
        {
            GameObject[] selection = UnityEditor.Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                sb.AppendLine("- Выделение: пусто");
                return;
            }

            sb.AppendLine("- Выделенные объекты (" + selection.Length + "):");
            int shown = 0;
            foreach (GameObject go in selection)
            {
                if (go == null) continue;
                if (shown >= MaxSelection)
                {
                    sb.AppendLine("  … остальные не перечислены");
                    break;
                }
                AppendObjectLine(sb, "  • ", go);
                sb.AppendLine("      путь: " + Safe(UnityEditor.AssetDatabase.GetAssetPath(go)));
                shown++;
            }
        }
#endif

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "(без имени)" : value;
        }
    }
}

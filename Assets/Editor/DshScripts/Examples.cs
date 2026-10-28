// Пример сценария для DSH Bridge.
// Положи сюда свой класс: Assets/Editor/DshScripts/Имя.cs, namespace DshScripts,
// публичный статический метод Run(string args) — и вызывай его через мост:
//
//   Tools/dsh-unity.ps1 exec -Arg DshScripts.Examples.Ping -Params "привет"
//
// Файлы в Assets/Editor компилируются только в редакторе и не попадают в билд.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DshScripts
{
    public static class Examples
    {
        /// <summary>Проверка связи: возвращает строку обратно в мост.</summary>
        public static string Ping(string args)
        {
            return "pong: " + (args ?? "(пусто)");
        }

        /// <summary>Создаёт куб в текущей сцене и выделяет его.</summary>
        public static string CreateCube(string args)
        {
            string name = string.IsNullOrEmpty(args) ? "Cube" : args.Trim();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Undo.RegisterCreatedObjectUndo(cube, "DSH: создать " + name);
            Selection.activeGameObject = cube;
            EditorSceneManager.MarkSceneDirty(cube.scene);
            return cube.name + " @ " + cube.transform.position;
        }

        /// <summary>Краткая сводка по сцене: объекты, камеры, свет.</summary>
        public static string SceneSummary(string args)
        {
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            var cameras = roots.SelectMany(r => r.GetComponentsInChildren<Camera>()).Count();
            var lights = roots.SelectMany(r => r.GetComponentsInChildren<Light>()).Count();
            var renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>()).Count();

            var lines = new List<string>();
            lines.Add("сцена: " + EditorSceneManager.GetActiveScene().name);
            lines.Add("корневых объектов: " + roots.Length);
            lines.Add("камер: " + cameras + ", источников света: " + lights + ", рендереров: " + renderers);
            return string.Join("; ", lines.ToArray());
        }
    }
}

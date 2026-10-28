# Проект «бумага» — заметки для агента (DeepSeek Harness)

Игра **Paper House**: first-person прототип про дом, бумагу, сдачу макулатуры и прокачку.
Исходники импортированы из репозитория `https://github.com/kakapopa3310-crypto/-`.

- Unity **6000.6.7f1**, рендер URP 17, ввод — Input System (New).
- Редактор: `D:\6000.6.7f1\Editor\Unity.exe`.
- Корень проекта: `C:\Users\пользователь\bugaga game\бумага`.
- Главная сцена: `Assets/Scenes/SampleScene.unity` (в Build Settings уже прописана).

## Запуск

```powershell
Tools\dsh-unity.ps1 open          # откроет редактор с -force-d3d11
```

`-force-d3d11` желателен на этой машине: при первом запуске с DX12 редактор Unity 6
упал на импорте, после падения остаются зависшие процессы, которые держат
`Library` и не дают открыть проект. Если такое случилось — снять процессы
`Unity*` и `UnityCrashHandler64`, затем удалить `Library`.

## Что внутри

| Путь | Назначение |
|---|---|
| `Assets/House/Editor/HouseBuilder.cs` | генератор дома, интерьера, городка, ночи, игрока, забора |
| `Assets/House/Runtime/` | FPS-контроллер, взаимодействия, HUD, настройки, прокачка, звук |
| `Assets/House/House.prefab` | собранный дом (пересобирается через меню) |
| `Assets/House/Materials/`, `Meshes/`, `Audio/` | URP-материалы, меши, звуки |
| `Assets/DSHAssistant/` | ИИ-ассистент проекта: окно редактора и записка в игре (F1) |
| `Tools/DSHBridge/` | Python-мост ассистента к OpenAI-совместимому API (порт 8787) |
| `Assets/Editor/DshBridge/` | **наш** мост DSH Bridge (HTTP в редакторе) |
| `Assets/Editor/DshScripts/` | сценарии агента (namespace `DshScripts`) |
| `Assets/House/Runtime/FoldablePaper.cs` | лист-оригами: сгибы пополам и вчетверо |
| `Assets/House/Runtime/PaperPileInteractable.cs` | стол-стопка: листы ложатся друг на друга, стопка растёт |
| `Assets/House/Meshes/Fence.asset` | слитый меш забора (генерируется `HouseBuilder.BuildFence`) |
| `Tools/dsh-unity.ps1` | CLI для нашего моста |

## DSH Bridge (наш инструмент)

```powershell
Tools\dsh-unity.ps1 ping
Tools\dsh-unity.ps1 state
Tools\dsh-unity.ps1 console -Count 30 -Type error
Tools\dsh-unity.ps1 scene -Arg Assets/Scenes/SampleScene.unity
Tools\dsh-unity.ps1 hierarchy -Arg Assets/Scenes/SampleScene.unity
Tools\dsh-unity.ps1 play | stop
Tools\dsh-unity.ps1 screenshot
Tools\dsh-unity.ps1 exec -Arg DshScripts.МойКласс.Метод -Params "аргумент"
Tools\dsh-unity.ps1 build -Arg win64
```

Headless (редактор закрыт):

```powershell
Tools\dsh-unity.ps1 batch -Arg Compile
Tools\dsh-unity.ps1 batch -Arg State
```

Порт: по умолчанию 8787. У ассистента игры свой мост на том же порту — если он
запущен, наш мост возьмёт следующий свободный и запишет его в
`Library/DshBridge/bridge.json`; CLI читает этот файл сам.

## Правила работы

1. Сначала `compile`, потом `console -Type error`, только потом `play`.
2. Проверять результат визуально: `screenshot` в Play Mode.
3. Новые сценарии — `Assets/Editor/DshScripts/Имя.cs`, `namespace DshScripts`,
   `public static` метод без параметров или с одним `string`.
4. Дом не править руками в префабе — он генерируется
   `Tools → Paper House → Пересобрать дом`. Правки должны попадать в `HouseBuilder.cs`.
5. `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` — генерируемое, не трогать.
6. `ProjectSettings/*.asset` — только осознанно, проект на Unity 6 и URP 17.

## Управление в игре (для проверки)

`WASD` — ходьба, мышь — обзор, `Shift` — бег, `Space` — прыжок, `E` — взаимодействие,
`Q` — бросить бумажный самолётик вперёд, `Esc` — настройки, `F1` — записка DSH.

Игровой цикл: `E` у стопки бумаги → `E` у коврика (сложить дважды) → `E` забрать лист
в руки → `E` у стола-стопки у правой стены (положить лист, стопка растёт в высоту) →
`E` у зелёного бака за домом (продажа **только из рук**) → стул + ноутбук = древо прокачки.

## Стиль ответов

Пользователь говорит по-русски — отвечай по-русски, живо и по делу.

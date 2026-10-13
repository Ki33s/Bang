# Подготовка после клонирования

## Исходники и зависимости

Откройте корень проекта в Unity **6000.6.4f1**. Unity восстановит зависимости из `Packages/manifest.json` и `Packages/packages-lock.json`.

Для полной 3D-демонстрации нужны собственные лицензированные копии пакетов, используемых в локальном проекте:

- POLYGON Western → `Assets/PolygonWestern`.
- POLYGON Western Frontier → `Assets/PolygonWesternFrontier`.
- Pistol Animset Pro → `Assets/PistolAnimsetPro`.
- Rifle Animset Pro → `Assets/RifleAnimsetPro`.
- Synty Particle FX → исходная структура `Assets/Synty`, включая используемые в `Assets/Editor/PrepareTurnAssets.cs` префабы.

Импортируйте исходные пакеты вместе с их `.meta`, сохраняя имена и пути. Затем в Unity выберите **Пыль и порох → Подготовить модели и анимации**. Команда создаёт локальные ресурсы `Assets/Resources/Turn` и `Assets/Resources/TurnFX`. Исходные пакеты и подготовленные копии исключены из Git. Без этих пакетов полная 3D-сцена не воспроизводится.

Откройте `Assets/Scenes/OneTurn.unity` и нажмите Play.

Шрифт Inter включён с лицензией `Docs/Inter-LICENSE.txt`. Лицензия шрифта не распространяется на код игры или сторонние пакеты.

## Проверка правил

Установите .NET 8 SDK и выполните из корня:

```powershell
dotnet run --project Tests/Bang.Tests.csproj
```

Проверки ядра не требуют Unity и сторонних ассетов. На 13 октября 2026 проходят 129 проверок одного хода и 51 429 проверок старого ядра, включая 400 матчей ботов.

## Сборка

После импорта ассетов выполните:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/build-one-turn.ps1
```

Или используйте команды сборки Windows и Web в меню **Пыль и порох** открытого редактора Unity.

Windows: `Play.cmd` / `PlayOneTurn.cmd`, результат в `Builds/OneTurn`.

Web: `node Tools/serve-web.mjs`, затем `http://127.0.0.1:8080`; результат сборки в `Builds/Web`.

Кэш Unity, готовые сборки, журналы, настройки пользователя и локальные секреты исключены из Git. Игровой прототип демонстрирует один ход; онлайн в новой сцене пока не подключён. `Docs/TZ_content_v0.3.md` описывает предлагаемый контент, а не список уже реализованных возможностей.

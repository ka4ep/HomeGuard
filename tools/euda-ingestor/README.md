# EUDA Odometer Ingestor

Сайдкар-контейнер: забирает пробег из официального портала VW Group **EU Data Act**
(`eu-data-act.drivesomethinggreater.com`) и постит его в HomeGuard
(`POST /api/meter-readings`, `Source: "Auto"`). HomeGuard хранит не больше одного
Auto-показания на технику в день (upsert), поэтому повторные запуски безопасны.

Протокол портала (OIDC-логин, proxy_api, формат датасетов) адаптирован из
[TommiG1/HA_VAG-EU-Data-Act](https://github.com/TommiG1/HA_VAG-EU-Data-Act) (MIT).

## Обязательная разовая настройка на портале

1. Зайди на `eu-data-act.drivesomethinggreater.com` под своим Cupra ID.
2. Создай **continuous** запрос данных: набор **All Data**, интервал **15 минут**.
3. Первые ZIP приходят через 15–60 минут (иногда часы). Подписка действует **1 год**
   — потом её нужно продлить на портале.

Без активной подписки инжестору нечего скачивать (`No datasets delivered yet`).

## Переменные окружения

| Переменная | Обязательна | По умолчанию | Описание |
|---|---|---|---|
| `EUDA_EMAIL` | да | — | Логин Cupra ID / VW ID |
| `EUDA_PASSWORD` | да | — | Пароль |
| `EUDA_BRAND` | нет | `cupra` | `cupra` / `seat` / `volkswagen` / `audi` / `skoda` |
| `EUDA_VIN` | нет | автоопределение | Нужен, только если на аккаунте несколько машин |
| `HOMEGUARD_URL` | нет | `http://homeguard:8080` | Адрес HomeGuard API |
| `HOMEGUARD_API_KEY` | да | — | Значение `Auth__ApiKey` сервера (заголовок `X-Api-Key`) |
| `HOMEGUARD_EQUIPMENT_ID` | да | — | GUID техники в HomeGuard (машины) |
| `HOMEGUARD_METER_UNIT` | нет | `km` | Единица `MeterUnit` техники; мили конвертируются |
| `POLL_INTERVAL_MINUTES` | нет | `360` | Период опроса портала |
| `STATE_FILE` | нет | `/data/state.json` | Файл состояния (последний обработанный ZIP) |
| `LOG_LEVEL` | нет | `INFO` | Уровень логов |

GUID техники виден в URL страницы техники: `/equipment/{guid}`.

## Запуск

Через podman-compose (см. `infra/podman-compose.yml`, сервис `odometer-ingestor`)
— переменные берутся из `infra/.env`.

Локальная проверка без записи в HomeGuard:

```bash
EUDA_EMAIL=... EUDA_PASSWORD=... HOMEGUARD_API_KEY=x HOMEGUARD_EQUIPMENT_ID=x \
  python3 euda_ingestor.py --dry-run
```

`--once` — один цикл и выход (для cron вместо встроенного цикла).

## Как это работает

1. OIDC-логин на `identity.vwgroup.io` (скрейпинг форм email/пароля — официального
   API у портала нет; при смене флоу VW логин может сломаться, чинить по аналогии
   с upstream-интеграцией).
2. `metadata/partial` → идентификатор подписки; `list` → перечень ZIP.
3. Скачивается новейший ZIP, из `Data[]` берутся поля `mileage.value`/`mileage`
   (по имени или по UUID словаря), отфильтровываются sentinel-значения
   (65535, 2^31−1, 2^32−1), берётся максимум — одометр монотонен, лагающие
   снапшоты занижают значение.
4. Дата показания — `car_captured_time` датасета (fallback: сегодня, UTC).
5. POST в HomeGuard; имя обработанного ZIP запоминается в `STATE_FILE`.

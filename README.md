﻿
## HcsClientApi: пример реализации на С# клиента для API ГИС ЖКХ

Демонстрационная программа размещена в проекте Hcs.ClientDemo.
Публикую в надежде помочь таким-же страждущим как я оставленным
искать ответы на вопросы об API ГИС ЖКХ только собственным 
`экспериментальным` путем. Код рабочий и применяется в таком 
виде на момент `февраль 2025 года`. 

Реализованы функции: 
* debt-request: сервис получения запросов о наличии задолженности
  и направление ответов на эти запросы. 
* house-mangement: сведения о жилом фонде и договорах ресурсоснабжения;
* device-metering: сведения о показаниях приборов учета;
* organizations-registry: сведения из реестра организаций ГИС ЖКХ;
* file-store-service: размещение в ГИС ЖКХ файлов и получение содержимого
  размещенных файлов;

в промышленной эксплуатации. Версия форматов интеграции: hsc-wsdl-v.14.5.0.1. 
Работает самостоятельно без внешних туннелей. 

Требуется предварительно зарегистрировать информационную систему в ЛК организации 
ГИС ЖКХ чтобы получить в ЛК код поставщика информации, и пройти процедуру описанную
в документации к пакету интеграции с ГИС ЖКХ: отправить заявку на тестирование в СТП 
и далее уведомление об окончании тестирования для допуска к промышленному стенду.

Проверено с ключем ЭЦП RuToken выпущенным ТЕНЗОР (ГИС ЖКХ обязательно требует 
ключ типа КС2).

## HCS
Построено на основе проекта HCS: https://github.com/gizmo75rus/HCS 

Большая часть кода прямо заимствована у gizmo75rus и только перекомпанована
чтобы иметь одну готовую библиотеку для включения в промышленное 
решение.

Выполнена адаптация к актуальной (2022/04) версии GostCryptography.
Алгоритмы подписи заменены с устаревшего ГОСТ (ProviderType=75) на 
ГОСТ 2012 (ProviderType=80)

## Использованные библиотеки
* GostCryptography: https://github.com/AlexMAS/GostCryptography
* BouncyCastle.Crypto: https://github.com/bcgit/bc-csharp
* Microsoft.Xades: https://github.com/Caliper/Xades

Пакет NuGet для GostCryptography существует, но не содержит сборку 
подписанную ключем StrongName. Чтобы вызывать Hcs.ClientApi.dll из
подписанных сборок, надо иметь подписанную GostCryptography.dll. 
Чтобы не собирать отдельно подписанную сборку код GostCryptography 
прямо добавлен в проект. Это потребовало сделать сборку проекта в 
параметрах проекта UnSafe (GostCryptography использует unsafe
вызовы CryptoApi). 

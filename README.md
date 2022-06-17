﻿
## HcsClientApi Пример реализации клиента для API ГИС ЖКХ на C#

Реализованы функции сервиса получения запросов о наличии задолженности
и направление ответов на эти запросы. 

Код рабочий и применяется в таком виде на момент июнь 2022 в промышленной 
эксплуатации (и для отладки на тестовом стенде SIT1). Работает самостоятельно 
без внешних туннелей. 

Требуется предварительно зарегистрировать информационную систему в ЛК организации 
ГИС ЖКХ чтобы получить в ЛК код поставщика информации, и пройти процедуру описанную
в документации к пакету интеграции с ГИС ЖКХ: отправить заявку на тестирование в СТП 
и далее уведомление об окончании тестирования для допуска к промышленному стенду.

Проверено с ключем ЭЦП RuToken выпущенным ТЕНЗОР (ГИС ЖКХ обязательно требует ключ типа КС2).

Демонстрационная программа размещена в проекте Hcs.ClientDemo.
Минимальный пример запроса информации по жилому дому:
```
var client = new HcsClient();
var cert = client.FindCertificate(x => x.Subject.Contains("Иванов"));
client.SetSigningCertificate(cert); 
client.IsPPAK = true; // использовать промышленный стенд
client.OrgPPAGUID = "488d95f6-4f6a-4e4e-b78a-ea259ef0ded2"; // код поставщика информации
var guid = Guid.Parse("60d080fc-f711-470f-bd21-eab217de2230"); // Петрозаводск, Андропова, 10
var number = client.ExportHouseByFiasGuid(guid).Result;
Console.WriteLine("house number=" + number);
```

## HCS
Построено на основе проекта HCS: https://github.com/gizmo75rus/HCS
Большая часть кода прямо заимствована у gizmo75rus и только перекомпанована
для того чтобы иметь одну готовую библиотеку для включение в промышленное 
решение.

Выполнена адаптация к актуальной (2022/04) версии GostCryptography
Алгоритмы подписи заменены с устаревшего ГОСТ (ProviderType=75) на 
ГОСТ 2012 (ProviderType=80)

## Использованные библиотеки
GostCryptography: https://github.com/AlexMAS/GostCryptography
BouncyCastle.Crypto: https://github.com/bcgit/bc-csharp
Microsoft.Xades: https://github.com/Caliper/Xades

Пакет NuGet для GostCryptography существует, но не содержит сборку 
подписанную ключем StrongName. Чтобы вызывать Hcs.ClientApi.dll из
подписанных сборок, надо иметь подписанную GostCryptography.dll. 
Чтобы не собирать отдельно подписанную сборку ее код добавлен
в проект. Это потребовало сделать сборку проекта в параметрах 
проекта UnSafe. 

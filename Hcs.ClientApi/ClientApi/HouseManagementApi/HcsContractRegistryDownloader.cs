﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.DataTypes;

namespace Hcs.ClientApi.HouseManagementApi
{
    /// <summary>
    /// Метод получения из ГИС ЖКХ полного реестра договоров ресурсоснабжения 
    /// и всех связанных с ними лицевых счетов и приборов учета.
    /// </summary>
    internal class HcsContractRegistryDownloader
    {
        private HcsHouseManagementApi api;

        internal HcsContractRegistryDownloader(HcsHouseManagementApi api)
        {
            this.api = api;
        }

        private void ThrowOperationCancelled()
        {
            throw new HcsException("Операция прервана пользователем");
        }

        /// <summary>
        /// Получить все договоры РСО удовлетворяющие фильтру @фильтрДоговоров
        /// с подчиненными объектами (ЛС и ПУ).
        /// </summary>
        internal async Task<ГисДоговорыИПриборы> ПолучитьВсеДоговорыИПриборы(
            Func<ГисДоговор, bool> фильтрДоговоров, CancellationToken token)
        {
            // В процессе будет много запросов, возвращающих мало данных,
            // но требующих стандартного ожидания в несколько секунд, что
            // суммарно складывается в целые часы. Экспериментально установлено
            // что ГИС ЖКХ способна пережить некоторую параллельность запросов
            // к ней. Так, с параллельностью в 5 потоков получение данных
            // по 3000 договорам РСО (70000 ПУ) длится 2,5 часа.
            int числоПотоковПараллельности = 5;

            var все = new ГисДоговорыИПриборы();
            все.ДатаНачалаСборки = DateTime.Now;

            // сохраняем все договоры проходящие фильтр пользователя
            Action<ГисДоговор> обработчикДоговора = (ГисДоговор договор) => {
                if (фильтрДоговоров(договор)) все.ДоговорыРСО.Add(договор);
            };
            await api.ПолучитьДоговорыРСО(обработчикДоговора, token);

            // получаем адреса связанные с договорами
            int сделаноДоговоров = 0;
            Action<ГисАдресныйОбъект> обработчикАдреса = все.АдресаОбъектов.Add;
            Func<ГисДоговор, Task> обработчикДоговораАдреса = async (договор) => {
                if (token.IsCancellationRequested) ThrowOperationCancelled();
                api.Config.Log($"Получаем адреса договора #{++сделаноДоговоров}/{все.ДоговорыРСО.Count()}...");
                await api.ПолучитьАдресаДоговораРСО(договор, обработчикАдреса, token);
            };
            await HcsParallel.ForEachAsync(все.ДоговорыРСО, обработчикДоговораАдреса, числоПотоковПараллельности);

            // все уникальные здания в договорах
            var гуидыЗданий = все.АдресаОбъектов.Select(x => x.ГуидЗданияФиас).Distinct();

            // получаем сведения о зданиях, включая полные списки их помещений
            // которые нужны чтобы связать лицевые счета с адресами договора
            int сделаноЗданий = 0;
            Func<Guid, Task> обработчикЗдания = async (гуидЗдания) => {
                if (token.IsCancellationRequested) ThrowOperationCancelled();
                api.Config.Log($"Получаем помещения здания #{++сделаноЗданий}/{гуидыЗданий.Count()}...");
                try {
                    var здание = await api.ПолучитьЗданиеПоГуидФиас(гуидЗдания, token);
                    все.Здания.Add(здание);
                }
                catch (Exception e) { // не все здания ГИС ЖКХ может выдать даже по кодам из его собственных адресных объектов 
                    if (HcsRemoteException.ContainsErrorCode(e, HcsRemoteException.KnownCodes.ОтсутствуетВРеестре)) {
                        api.Config.Log($"Не удалось получить здание по ФИАС ГУИД {гуидЗдания}: здание отсутствует в реестре");
                        var здание = new ГисЗдание() { ГуидЗданияФиас = гуидЗдания };
                        все.Здания.Add(здание);
                    }
                    else if (HcsRemoteException.ContainsErrorCode(e, HcsRemoteException.KnownCodes.ДоступЗапрещен)) {
                        api.Config.Log($"Не удалось получить здание по ФИАС ГУИД {гуидЗдания}: доступ запрещен");
                        var здание = new ГисЗдание() { ГуидЗданияФиас = гуидЗдания };
                        все.Здания.Add(здание);
                    }
                    else {
                        throw new HcsException($"Вложенная ошибка получения здания {гуидЗдания}", e);
                    }
                }
            };
            await HcsParallel.ForEachAsync(гуидыЗданий, обработчикЗдания, числоПотоковПараллельности);

            // получаем лицевые счета по всем зданиям
            сделаноЗданий = 0;
            Action<ГисЛицевойСчет> обработчикЛС = (ГисЛицевойСчет лс) => {
                if (лс.ДействуетСейчас && все.ЭтотЛицевойСчетСвязанСДоговорами(лс)) {
                    все.ЛицевыеСчета.Add(лс);
                }
            };
            Func<Guid, Task> обработчикЗданияЛС = async (гуидЗдания) => {
                if (token.IsCancellationRequested) ThrowOperationCancelled();
                api.Config.Log($"Получаем ЛС по зданию #{++сделаноЗданий}/{гуидыЗданий.Count()}...");
                await api.ПолучитьЛицевыеСчетаПоЗданию(гуидЗдания, обработчикЛС, token);
            };
            await HcsParallel.ForEachAsync(гуидыЗданий, обработчикЗданияЛС, числоПотоковПараллельности);

            // получаем приборы учета
            сделаноЗданий = 0;
            Action<ГисПриборУчета> обработчикПУ = (ГисПриборУчета прибор) => {
                if (прибор.ЭтоАктивный && (прибор.ЭтоОДПУ || все.ЭтотПриборСвязанСЛицевымиСчетами(прибор))) {
                    все.ПриборыУчета.Add(прибор);
                }
            };
            Func<Guid, Task> обработчикЗданияПУ = async (гуидЗдания) => {
                if (token.IsCancellationRequested) ThrowOperationCancelled();
                api.Config.Log($"Получаем ПУ по зданию #{++сделаноЗданий}/{гуидыЗданий.Count()}...");
                await api.ПолучитьПриборыУчетаПоЗданию(гуидЗдания, обработчикПУ, token);
            };
            await HcsParallel.ForEachAsync(гуидыЗданий, обработчикЗданияПУ, числоПотоковПараллельности);

            все.ДатаКонцаСборки = DateTime.Now;
            return все;
        }
    }
}

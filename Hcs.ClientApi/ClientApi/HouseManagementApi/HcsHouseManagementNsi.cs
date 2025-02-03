using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//using HouseManagement = Hcs.Service.Async.HouseManagement.v13_2_3_3;
// переключение на версию 14.1.0.0 (12.03.2024)
//using HouseManagement = Hcs.Service.Async.HouseManagement.v14_1_0_0;
// переключение на версию 14.5.0.1 (11.09.2024)
using HouseManagement = Hcs.Service.Async.HouseManagement.v14_5_0_1;

namespace Hcs.ClientApi.HouseManagementApi
{
    /// <summary>
    /// Методы и константы для работы с номенклатурно-справочной информацией (НСИ)
    /// применяемой в сервисе hcs-house-management.
    /// https://my.dom.gosuslugi.ru/#!/open-data
    /// </summary>
    public class HcsHouseManagementNsi
    {
        // Ссылка на НСИ "54 Причина расторжения договора" (реестровый номер 54)
        // https://my.dom.gosuslugi.ru/#!/open-data-passport?passportName=7710474375-nsi-54
        public class ПричинаРасторженияДоговора 
        {
            public static HouseManagement.nsiRef ПоВзаимномуСогласиюСторон => new HouseManagement.nsiRef() {
                Name = "По взаимному согласию сторон",
                Code = "4",
                GUID = "4a481322-05c9-47cb-9d05-30387dff1f93"
            };
        }

        // Ссылка на НСИ "22 Причина закрытия лицевого счета" (реестровый номер 22)
        // https://dom.gosuslugi.ru/opendataapi/nsi-22/v1
        public class ПричинаЗакрытияЛицевогоСчета
        {
            public static HouseManagement.nsiRef РасторжениеДоговора => new HouseManagement.nsiRef() {
                Name = "Расторжение договора",
                Code = "11",
                GUID = "7ee8b4db-dabc-40eb-9009-f4f80b36bfe5"
            };
        }

        // Ссылка на НСИ "Причина архивации прибора учета" (реестровый номер 21)
        // https://my.dom.gosuslugi.ru/#!/open-data-passport?passportName=7710474375-nsi-21
        public class ПричинаАрхивацииПрибораУчета
        {
            public static HouseManagement.nsiRef ИстекСрокЭксплуатации => new HouseManagement.nsiRef() {
                Code = "12",
                GUID = "2b8f44f9-7ca1-44f5-803a-af80d6912f36",
                Name = "Истек срок эксплуатации прибора учета"
            };

            public static HouseManagement.nsiRef Ошибка => new HouseManagement.nsiRef() {
                Code = "4",
                GUID = "d723696f-5ed7-4923-ad6a-9c2c5bce5032",
                Name = "Ошибка"
            };
        }

        // Ссылка на НСИ "Основание заключения договора" (реестровый номер 58)
        // https://my.dom.gosuslugi.ru/#!/open-data-passport?passportName=7710474375-nsi-58
        public class ОснованиеЗаключенияДоговора
        {
            public static HouseManagement.nsiRef ЗаявлениеПотребителя => new HouseManagement.nsiRef() {
                Code = "7",
                GUID = "93cd9d85-91b8-4bf9-ae48-c5f1e691949f",
                Name = "Заявление потребителя"
            };

            public static HouseManagement.nsiRef ДоговорУправления => new HouseManagement.nsiRef() {
                Code = "3",
                GUID = "11efe618-79f8-4f53-bfd6-11620e8e9e1e",
                Name = "Договор управления"
            };
        }

        public static HouseManagement.ContractSubjectTypeServiceType ElectricSupplyServiceType
            => new HouseManagement.ContractSubjectTypeServiceType() {
                Code = "4",
                GUID = "903c7763-73f8-4af2-9ec2-94ee08c7beaa",
                Name = "Электроснабжение"
            };

        public static HouseManagement.ContractSubjectTypeMunicipalResource ElectricSupplyMunicipalResource
            => new HouseManagement.ContractSubjectTypeMunicipalResource() {
                Code = "8",
                GUID = "7379be86-6c95-4e41-b000-3bc703d35969",
                Name = "Электрическая энергия"
            };
    }
}
